using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using Nnrp.Runtime;
using Nnrp.Transport.Ipc;
using Nnrp.Transport.Quic;
using Nnrp.Transport.Tcp;
using Nnrp.Transport.WebSocket;

namespace Nnrp.WireConformance;

public sealed record WireTargetTransportSecurity(
    [property: JsonPropertyName("server_name")] string ServerName,
    [property: JsonPropertyName("trusted_certificate_der_path")] string TrustedCertificateDerPath,
    [property: JsonPropertyName("certificate_der_path")] string CertificateDerPath,
    [property: JsonPropertyName("private_key_pkcs8_der_path")] string PrivateKeyPkcs8DerPath);

public sealed record WireTargetTransport(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("endpoint")] string Endpoint,
    [property: JsonPropertyName("tls")] bool Tls = false,
    [property: JsonPropertyName("security")] WireTargetTransportSecurity? Security = null);

public sealed record WireHostRouteProvider(
    [property: JsonPropertyName("transport")] string Transport,
    [property: JsonPropertyName("provider_id")] string ProviderId,
    [property: JsonPropertyName("installed")] bool Installed,
    [property: JsonPropertyName("platforms")] IReadOnlyList<string> Platforms,
    [property: JsonPropertyName("security_modes")] IReadOnlyList<string> SecurityModes);

public sealed class WireTargetSupport
{
    public WireTargetSupport(
        IEnumerable<string> modes,
        IEnumerable<string> transports,
        IEnumerable<string> capabilities)
    {
        Modes = ToFrozenSet(modes, nameof(modes));
        Transports = ToFrozenSet(transports, nameof(transports));
        Capabilities = ToFrozenSet(capabilities, nameof(capabilities));
    }

    public IReadOnlySet<string> Modes { get; }

    public IReadOnlySet<string> Transports { get; }

    public IReadOnlySet<string> Capabilities { get; }

    public static WireTargetSupport Compiled { get; } = new(
        [WireTargetModes.SuiteAsClient, WireTargetModes.SuiteAsServer, WireTargetModes.SuiteAsProxy],
        [
            ToSchemaTransportName(NnrpNativeTcpTransportProvider.Instance.Descriptor.TransportId),
            ToSchemaTransportName(NnrpNativeQuicTransportProvider.Instance.Descriptor.TransportId),
            ToSchemaTransportName(NnrpNativeIpcTransportProvider.Instance.Descriptor.TransportId),
            ToSchemaTransportName(NnrpNativeWebSocketTransportProvider.Instance.Descriptor.TransportId),
        ],
        NnrpPreview4CapabilityTokens.AllCapabilities.Append(WireTargetCapabilities.HostRoutes));

    private static IReadOnlySet<string> ToFrozenSet(IEnumerable<string> values, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(values);
        HashSet<string> result = new(StringComparer.Ordinal);
        foreach (string value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Support entries must be non-empty.", parameterName);
            }

            result.Add(value);
        }

        if (result.Count == 0)
        {
            throw new ArgumentException("Support sets must not be empty.", parameterName);
        }

        return result;
    }

    private static string ToSchemaTransportName(Nnrp.Core.TransportId transportId) => transportId switch
    {
        Nnrp.Core.TransportId.Tcp => NnrpPreview4CapabilityTokens.TransportTcp,
        Nnrp.Core.TransportId.Quic => NnrpPreview4CapabilityTokens.TransportQuic,
        Nnrp.Core.TransportId.Ipc => NnrpPreview4CapabilityTokens.TransportIpc,
        Nnrp.Core.TransportId.WebSocket => NnrpPreview4CapabilityTokens.TransportWebSocket,
        _ => throw new InvalidOperationException($"Transport id {transportId} has no preview4 schema name."),
    };
}

public static class WireTargetCapabilities
{
    public const string HostRoutes = "host.routes";
}

public static class WireTargetModes
{
    public const string SuiteAsClient = "suite_as_client";
    public const string SuiteAsServer = "suite_as_server";
    public const string SuiteAsProxy = "suite_as_proxy";
}

public sealed class WireTargetManifestBuilder
{
    public const string ProtocolVersion = "nnrp-1-preview4";
    public const string SchemaUrl =
        "https://github.com/NagareWorks/nnrp-conformance/schemas/wire-conformance-target.schema.json";
    public const int DefaultMaxFrameBytes = 16 * 1024 * 1024;
    public const int DefaultMaxInFlight = 256;

    private static readonly HashSet<string> ValidModes =
    [
        WireTargetModes.SuiteAsClient,
        WireTargetModes.SuiteAsServer,
        WireTargetModes.SuiteAsProxy,
    ];

    private static readonly HashSet<string> ValidTransports =
        new(NnrpPreview4CapabilityTokens.Transports, StringComparer.Ordinal);

    private static readonly HashSet<string> ValidHostPlatforms = new(StringComparer.Ordinal)
    {
        "native",
        "browser",
    };

    private static readonly HashSet<string> ValidHostSecurityModes = new(StringComparer.Ordinal)
    {
        "plain",
        "tls_server_auth",
        "mutual_tls",
        "wss",
        "browser_host",
    };

    private readonly WireTargetSupport support;

    private static readonly IReadOnlySet<string> RequiredReleaseModes = new HashSet<string>(
        [WireTargetModes.SuiteAsClient, WireTargetModes.SuiteAsServer, WireTargetModes.SuiteAsProxy],
        StringComparer.Ordinal);

    private static readonly IReadOnlySet<string> RequiredReleaseTransports = new HashSet<string>(
        NnrpPreview4CapabilityTokens.Transports,
        StringComparer.Ordinal);

    public WireTargetManifestBuilder(WireTargetSupport support)
    {
        this.support = support ?? throw new ArgumentNullException(nameof(support));
    }

    public WireTargetManifest Build(
        string targetName,
        string suiteVersion,
        IEnumerable<string> modes,
        IEnumerable<WireTargetTransport> transports,
        IEnumerable<string> capabilities,
        int maxFrameBytes = DefaultMaxFrameBytes,
        int maxInFlight = DefaultMaxInFlight,
        IEnumerable<WireHostRouteProvider>? hostRouteProviders = null)
    {
        RequireNonEmpty(targetName, nameof(targetName));
        RequireNonEmpty(suiteVersion, nameof(suiteVersion));
        if (maxFrameBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxFrameBytes), "Maximum frame bytes must be positive.");
        }

        if (maxInFlight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxInFlight), "Maximum in-flight count must be positive.");
        }

        IReadOnlyList<string> normalizedModes = NormalizeStrings(modes, "mode", ValidateMode);
        IReadOnlyList<WireTargetTransport> normalizedTransports = NormalizeTransports(transports);
        IReadOnlyList<string> normalizedCapabilities = NormalizeStrings(
            capabilities,
            "capability",
            ValidateCapability);
        IReadOnlyList<WireHostRouteProvider> normalizedHostRouteProviders =
            NormalizeHostRouteProviders(hostRouteProviders ?? []);
        bool claimsHostRoutes = normalizedCapabilities.Contains(WireTargetCapabilities.HostRoutes);
        if (claimsHostRoutes != (normalizedHostRouteProviders.Count != 0))
        {
            throw new ArgumentException(
                "host.routes capability and host_route_providers must be declared together.",
                nameof(hostRouteProviders));
        }

        if (normalizedTransports.Count == 0 && normalizedHostRouteProviders.Count == 0)
        {
            throw new ArgumentException(
                "Wire target must declare transports or host-route providers.",
                nameof(transports));
        }

        return new WireTargetManifest(
            SchemaUrl,
            targetName,
            ProtocolVersion,
            suiteVersion,
            new WireTargetDeclaration(
                normalizedModes,
                normalizedTransports,
                normalizedHostRouteProviders.Count == 0 ? null : normalizedHostRouteProviders,
                normalizedCapabilities,
                new WireTargetLimits(maxFrameBytes, maxInFlight)));
    }

    public static void ValidateReleaseTarget(WireTargetManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        RequireCompleteReleaseSet(
            manifest.WireConformance.Modes,
            RequiredReleaseModes,
            "modes");
        RequireCompleteReleaseSet(
            manifest.WireConformance.Transports.Select(transport => transport.Name),
            RequiredReleaseTransports,
            "transports");
    }

    public static void Write(string outputPath, WireTargetManifest manifest)
    {
        RequireNonEmpty(outputPath, nameof(outputPath));
        ArgumentNullException.ThrowIfNull(manifest);
        string fullPath = Path.GetFullPath(outputPath);
        string? directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string json = JsonSerializer.Serialize(manifest, WireTargetJsonContext.Default.WireTargetManifest);
        File.WriteAllText(fullPath, $"{json}{Environment.NewLine}");
    }

    private void ValidateMode(string mode)
    {
        if (!ValidModes.Contains(mode))
        {
            throw new ArgumentException($"Unsupported wire conformance mode: {mode}", nameof(mode));
        }

        if (!support.Modes.Contains(mode))
        {
            throw new InvalidOperationException($"Wire harness mode is not compiled into this command: {mode}");
        }
    }

    private void ValidateCapability(string capability)
    {
        if (!support.Capabilities.Contains(capability))
        {
            throw new InvalidOperationException($"Capability is not implemented by this target: {capability}");
        }
    }

    private IReadOnlyList<WireTargetTransport> NormalizeTransports(IEnumerable<WireTargetTransport> transports)
    {
        ArgumentNullException.ThrowIfNull(transports);
        List<WireTargetTransport> result = [];
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (WireTargetTransport transport in transports)
        {
            ArgumentNullException.ThrowIfNull(transport);
            string name = transport.Name.Trim().ToLowerInvariant();
            string endpoint = transport.Endpoint.Trim();
            if (!ValidTransports.Contains(name))
            {
                throw new ArgumentException($"Unsupported wire conformance transport: {transport.Name}", nameof(transports));
            }

            if (!support.Transports.Contains(name))
            {
                throw new InvalidOperationException($"Transport provider is not compiled into this command: {name}");
            }

            RequireNonEmpty(endpoint, nameof(transports));
            if (!seen.Add(name))
            {
                throw new ArgumentException($"Duplicate wire conformance transport: {name}", nameof(transports));
            }

            ValidateTransportSecurity(name, endpoint, transport.Tls, transport.Security);
            result.Add(transport with { Name = name, Endpoint = endpoint });
        }

        return new ReadOnlyCollection<WireTargetTransport>(result);
    }

    private IReadOnlyList<WireHostRouteProvider> NormalizeHostRouteProviders(
        IEnumerable<WireHostRouteProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);
        List<WireHostRouteProvider> result = [];
        HashSet<(string Transport, string ProviderId)> seen = [];
        foreach (WireHostRouteProvider provider in providers)
        {
            ArgumentNullException.ThrowIfNull(provider);
            string transport = provider.Transport.Trim().ToLowerInvariant();
            string providerId = provider.ProviderId.Trim();
            if (!ValidTransports.Contains(transport) || !support.Transports.Contains(transport))
            {
                throw new InvalidOperationException(
                    $"Host-route transport provider is not compiled into this command: {transport}");
            }

            RequireNonEmpty(providerId, nameof(providers));
            if (!seen.Add((transport, providerId)))
            {
                throw new ArgumentException(
                    $"Duplicate host-route provider identity: {transport}/{providerId}",
                    nameof(providers));
            }

            IReadOnlyList<string> platforms = NormalizeEnumStrings(
                provider.Platforms,
                ValidHostPlatforms,
                "host-route platform");
            IReadOnlyList<string> securityModes = NormalizeEnumStrings(
                provider.SecurityModes,
                ValidHostSecurityModes,
                "host-route security mode");
            result.Add(provider with
            {
                Transport = transport,
                ProviderId = providerId,
                Platforms = platforms,
                SecurityModes = securityModes,
            });
        }

        return new ReadOnlyCollection<WireHostRouteProvider>(result);
    }

    private static IReadOnlyList<string> NormalizeEnumStrings(
        IEnumerable<string> values,
        IReadOnlySet<string> validValues,
        string fieldName)
    {
        return NormalizeStrings(values, fieldName, value =>
        {
            if (!validValues.Contains(value))
            {
                throw new ArgumentException($"Unsupported {fieldName}: {value}", fieldName);
            }
        });
    }

    private static void ValidateTransportSecurity(
        string name,
        string endpoint,
        bool tls,
        WireTargetTransportSecurity? security)
    {
        bool endpointUsesTls = name switch
        {
            NnrpPreview4CapabilityTokens.TransportQuic => true,
            NnrpPreview4CapabilityTokens.TransportWebSocket => endpoint.StartsWith("wss://", StringComparison.OrdinalIgnoreCase),
            _ => false,
        };

        if (name == NnrpPreview4CapabilityTokens.TransportQuic && !tls)
        {
            throw new ArgumentException("QUIC wire conformance transport requires TLS.");
        }

        if ((name == NnrpPreview4CapabilityTokens.TransportTcp ||
             name == NnrpPreview4CapabilityTokens.TransportIpc) && tls)
        {
            throw new ArgumentException($"{name} wire conformance transport does not use TLS.");
        }

        if (name == NnrpPreview4CapabilityTokens.TransportWebSocket)
        {
            if (!endpoint.StartsWith("ws://", StringComparison.OrdinalIgnoreCase) &&
                !endpoint.StartsWith("wss://", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("WebSocket endpoint must use ws:// or wss://.");
            }

            if (tls != endpointUsesTls)
            {
                throw new ArgumentException("WebSocket TLS flag must match its endpoint scheme.");
            }
        }

        if (tls != endpointUsesTls)
        {
            throw new ArgumentException($"TLS flag does not match the {name} transport contract.");
        }

        if (tls && security is null)
        {
            throw new ArgumentException($"{name} TLS transport requires security material.");
        }

        if (!tls && security is not null)
        {
            throw new ArgumentException($"{name} non-TLS transport must not declare security material.");
        }

        if (security is not null)
        {
            RequireNonEmpty(security.ServerName, nameof(security.ServerName));
            RequireNonEmpty(security.TrustedCertificateDerPath, nameof(security.TrustedCertificateDerPath));
            RequireNonEmpty(security.CertificateDerPath, nameof(security.CertificateDerPath));
            RequireNonEmpty(security.PrivateKeyPkcs8DerPath, nameof(security.PrivateKeyPkcs8DerPath));
        }
    }

    private static IReadOnlyList<string> NormalizeStrings(
        IEnumerable<string> values,
        string fieldName,
        Action<string> validate)
    {
        ArgumentNullException.ThrowIfNull(values);
        List<string> result = [];
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (string rawValue in values)
        {
            string value = rawValue?.Trim() ?? string.Empty;
            RequireNonEmpty(value, fieldName);
            validate(value);
            if (seen.Add(value))
            {
                result.Add(value);
            }
        }

        if (result.Count == 0)
        {
            throw new ArgumentException($"{fieldName} values must not be empty.", fieldName);
        }

        return new ReadOnlyCollection<string>(result);
    }

    private static void RequireCompleteReleaseSet(
        IEnumerable<string> actualValues,
        IReadOnlySet<string> requiredValues,
        string fieldName)
    {
        HashSet<string> actual = new(actualValues, StringComparer.Ordinal);
        string[] missing = requiredValues
            .Where(value => !actual.Contains(value))
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (missing.Length != 0)
        {
            throw new InvalidOperationException(
                $"Release wire target must declare all preview4 {fieldName}; missing: {string.Join(", ", missing)}");
        }
    }

    private static void RequireNonEmpty(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value must be non-empty.", parameterName);
        }
    }
}

public sealed record WireTargetManifest(
    [property: JsonPropertyName("$schema")] string Schema,
    [property: JsonPropertyName("target_name")] string TargetName,
    [property: JsonPropertyName("protocol_version")] string ProtocolVersion,
    [property: JsonPropertyName("suite_version")] string SuiteVersion,
    [property: JsonPropertyName("wire_conformance")] WireTargetDeclaration WireConformance);

public sealed record WireTargetDeclaration(
    [property: JsonPropertyName("modes")] IReadOnlyList<string> Modes,
    [property: JsonPropertyName("transports")] IReadOnlyList<WireTargetTransport> Transports,
    [property: JsonPropertyName("host_route_providers")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<WireHostRouteProvider>? HostRouteProviders,
    [property: JsonPropertyName("capabilities")] IReadOnlyList<string> Capabilities,
    [property: JsonPropertyName("limits")] WireTargetLimits Limits);

public sealed record WireTargetLimits(
    [property: JsonPropertyName("max_frame_bytes")] int MaxFrameBytes,
    [property: JsonPropertyName("max_in_flight")] int MaxInFlight);

[JsonSerializable(typeof(WireTargetManifest))]
[JsonSourceGenerationOptions(WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
public partial class WireTargetJsonContext : JsonSerializerContext;
