using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using Nnrp.Runtime;
using Nnrp.WireConformance;
using Xunit;

namespace Nnrp.Core.Tests;

public sealed class WireTargetManifestTests
{
    private static readonly WireTargetTransportSecurity Security = new(
        "localhost",
        "certs/trusted.der",
        "certs/server.der",
        "certs/server-key.der");

    [Fact]
    public void BuildProducesFrozenPreview4ShapeAndStableDistinctValues()
    {
        WireTargetManifestBuilder builder = new(FullSupport());

        WireTargetManifest manifest = builder.Build(
            "nnrp-cs-dev",
            "0.1.0",
            [WireTargetModes.SuiteAsClient, WireTargetModes.SuiteAsServer, WireTargetModes.SuiteAsClient],
            [
                new WireTargetTransport("tcp", "127.0.0.1:19091"),
                new WireTargetTransport("quic", "127.0.0.1:19092", true, Security),
                new WireTargetTransport("ipc", "npipe://nnrp-preview4"),
                new WireTargetTransport("websocket", "wss://localhost/nnrp", true, Security),
            ],
            [
                NnrpPreview4CapabilityTokens.ControlCancelAbort,
                NnrpPreview4CapabilityTokens.ControlTraceContext,
                NnrpPreview4CapabilityTokens.ControlCancelAbort,
            ],
            4096,
            8);

        Assert.Equal(WireTargetManifestBuilder.SchemaUrl, manifest.Schema);
        Assert.Equal("nnrp-cs-dev", manifest.TargetName);
        Assert.Equal(WireTargetManifestBuilder.ProtocolVersion, manifest.ProtocolVersion);
        Assert.Equal("0.1.0", manifest.SuiteVersion);
        Assert.Equal([WireTargetModes.SuiteAsClient, WireTargetModes.SuiteAsServer], manifest.WireConformance.Modes);
        Assert.Equal(["tcp", "quic", "ipc", "websocket"], manifest.WireConformance.Transports.Select(value => value.Name));
        Assert.Equal(
            [NnrpPreview4CapabilityTokens.ControlCancelAbort, NnrpPreview4CapabilityTokens.ControlTraceContext],
            manifest.WireConformance.Capabilities);
        Assert.Equal(4096, manifest.WireConformance.Limits.MaxFrameBytes);
        Assert.Equal(8, manifest.WireConformance.Limits.MaxInFlight);
    }

    [Fact]
    public void WriteUsesFrozenSchemaPropertyNames()
    {
        string root = CreateTemporaryDirectory();
        string outputPath = Path.Combine(root, "nested", "target.json");
        WireTargetManifest manifest = new WireTargetManifestBuilder(FullSupport()).Build(
            "nnrp-cs",
            "0.1.0",
            [WireTargetModes.SuiteAsClient],
            [new WireTargetTransport("quic", "127.0.0.1:19092", true, Security)],
            [NnrpPreview4CapabilityTokens.ControlCancelAbort]);

        WireTargetManifestBuilder.Write(outputPath, manifest);

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(outputPath));
        JsonElement rootElement = document.RootElement;
        Assert.Equal(WireTargetManifestBuilder.SchemaUrl, rootElement.GetProperty("$schema").GetString());
        Assert.Equal("nnrp-1-preview4", rootElement.GetProperty("protocol_version").GetString());
        JsonElement transport = rootElement.GetProperty("wire_conformance").GetProperty("transports")[0];
        Assert.True(transport.GetProperty("tls").GetBoolean());
        Assert.Equal("localhost", transport.GetProperty("security").GetProperty("server_name").GetString());
        Assert.EndsWith(Environment.NewLine, File.ReadAllText(outputPath));
    }

    [Theory]
    [InlineData("", "0.1.0", 1, 1)]
    [InlineData("target", "", 1, 1)]
    [InlineData("target", "0.1.0", 0, 1)]
    [InlineData("target", "0.1.0", 1, 0)]
    public void BuildRejectsEmptyIdentityAndInvalidLimits(
        string targetName,
        string suiteVersion,
        int maxFrameBytes,
        int maxInFlight)
    {
        WireTargetManifestBuilder builder = new(FullSupport());

        Assert.ThrowsAny<ArgumentException>(() => builder.Build(
            targetName,
            suiteVersion,
            [WireTargetModes.SuiteAsClient],
            [new WireTargetTransport("tcp", "127.0.0.1:1")],
            [NnrpPreview4CapabilityTokens.ControlCancelAbort],
            maxFrameBytes,
            maxInFlight));
    }

    [Fact]
    public void CompiledSupportIncludesEveryPreview4ModeAndTransport()
    {
        WireTargetManifestBuilder builder = new(WireTargetSupport.Compiled);

        WireTargetManifest manifest = builder.Build(
            "target",
            "0.1.0",
            [WireTargetModes.SuiteAsClient, WireTargetModes.SuiteAsServer, WireTargetModes.SuiteAsProxy],
            [
                new WireTargetTransport("tcp", "127.0.0.1:1"),
                new WireTargetTransport("quic", "127.0.0.1:2", true, Security),
                new WireTargetTransport("ipc", "npipe://nnrp"),
                new WireTargetTransport("websocket", "ws://127.0.0.1:3"),
            ],
            [NnrpPreview4CapabilityTokens.ControlCancelAbort]);

        Assert.Equal(3, manifest.WireConformance.Modes.Count);
        Assert.Equal(4, manifest.WireConformance.Transports.Count);
        Assert.Throws<InvalidOperationException>(() => builder.Build(
            "target",
            "0.1.0",
            [WireTargetModes.SuiteAsClient],
            [new WireTargetTransport("tcp", "127.0.0.1:1")],
            ["control.not-implemented"]));
    }

    [Fact]
    public void ReleaseValidationRequiresEveryPreview4ModeAndTransport()
    {
        WireTargetManifestBuilder builder = new(FullSupport());
        WireTargetManifest complete = builder.Build(
            "target",
            "0.1.0",
            [WireTargetModes.SuiteAsClient, WireTargetModes.SuiteAsServer, WireTargetModes.SuiteAsProxy],
            [
                new WireTargetTransport("tcp", "127.0.0.1:1"),
                new WireTargetTransport("quic", "127.0.0.1:2", true, Security),
                new WireTargetTransport("ipc", "npipe://nnrp"),
                new WireTargetTransport("websocket", "ws://127.0.0.1:3"),
            ],
            [NnrpPreview4CapabilityTokens.ControlCancelAbort]);

        WireTargetManifestBuilder.ValidateReleaseTarget(complete);

        WireTargetManifest missingMode = complete with
        {
            WireConformance = complete.WireConformance with
            {
                Modes = [WireTargetModes.SuiteAsClient, WireTargetModes.SuiteAsServer],
            },
        };
        InvalidOperationException modeError = Assert.Throws<InvalidOperationException>(
            () => WireTargetManifestBuilder.ValidateReleaseTarget(missingMode));
        Assert.Contains(WireTargetModes.SuiteAsProxy, modeError.Message, StringComparison.Ordinal);

        WireTargetManifest missingTransport = complete with
        {
            WireConformance = complete.WireConformance with
            {
                Transports = complete.WireConformance.Transports
                    .Where(transport => transport.Name != "websocket")
                    .ToArray(),
            },
        };
        InvalidOperationException transportError = Assert.Throws<InvalidOperationException>(
            () => WireTargetManifestBuilder.ValidateReleaseTarget(missingTransport));
        Assert.Contains("websocket", transportError.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReleaseValidationReportsMissingValuesInOrdinalOrderAcrossCultures()
    {
        WireTargetManifestBuilder builder = new(FullSupport());
        WireTargetManifest manifest = builder.Build(
            "target",
            "0.1.0",
            [WireTargetModes.SuiteAsClient, WireTargetModes.SuiteAsServer, WireTargetModes.SuiteAsProxy],
            [new WireTargetTransport("tcp", "127.0.0.1:1")],
            [NnrpPreview4CapabilityTokens.ControlCancelAbort]);
        CultureInfo originalCulture = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("sv-SE");
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => WireTargetManifestBuilder.ValidateReleaseTarget(manifest));

            Assert.Equal(
                "Release wire target must declare all preview4 transports; missing: ipc, quic, websocket",
                error.Message);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void BuildRejectsUnsupportedModes()
    {
        WireTargetManifestBuilder builder = new(FullSupport());

        ArgumentException error = Assert.Throws<ArgumentException>(() => builder.Build(
            "target",
            "0.1.0",
            ["suite_as_relay"],
            [new WireTargetTransport("tcp", "127.0.0.1:1")],
            [NnrpPreview4CapabilityTokens.ControlCancelAbort]));

        Assert.Contains("Unsupported wire conformance mode", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildRequiresHostRouteCapabilityAndProvidersTogether()
    {
        WireTargetManifestBuilder builder = new(FullSupport());
        WireHostRouteProvider provider = new(
            "ipc",
            "nnrp.transport.ipc.native",
            true,
            ["native"],
            ["plain"]);

        Assert.Throws<ArgumentException>(() => builder.Build(
            "target",
            "0.1.0",
            [WireTargetModes.SuiteAsClient],
            [],
            [WireTargetCapabilities.HostRoutes]));
        Assert.Throws<ArgumentException>(() => builder.Build(
            "target",
            "0.1.0",
            [WireTargetModes.SuiteAsClient],
            [],
            [NnrpPreview4CapabilityTokens.ControlCancelAbort],
            hostRouteProviders: [provider]));

        WireTargetManifest manifest = builder.Build(
            "target",
            "0.1.0",
            [WireTargetModes.SuiteAsClient],
            [],
            [WireTargetCapabilities.HostRoutes],
            hostRouteProviders: [provider]);

        Assert.Empty(manifest.WireConformance.Transports);
        WireHostRouteProvider actual = Assert.Single(manifest.WireConformance.HostRouteProviders!);
        Assert.Equal(provider.Transport, actual.Transport);
        Assert.Equal(provider.ProviderId, actual.ProviderId);
        Assert.Equal(provider.Installed, actual.Installed);
        Assert.Equal(provider.Platforms, actual.Platforms);
        Assert.Equal(provider.SecurityModes, actual.SecurityModes);
    }

    [Fact]
    public void BuildRejectsMalformedHostRouteProviders()
    {
        WireTargetManifestBuilder builder = new(FullSupport());
        WireHostRouteProvider valid = new(
            "ipc",
            "nnrp.transport.ipc.native",
            true,
            ["native"],
            ["plain"]);

        Assert.Throws<ArgumentNullException>(() => builder.Build(
            "target",
            "0.1.0",
            [WireTargetModes.SuiteAsClient],
            [],
            [WireTargetCapabilities.HostRoutes],
            hostRouteProviders: [null!]));
        Assert.Throws<InvalidOperationException>(() => builder.Build(
            "target",
            "0.1.0",
            [WireTargetModes.SuiteAsClient],
            [],
            [WireTargetCapabilities.HostRoutes],
            hostRouteProviders: [valid with { Transport = "udp" }]));
        Assert.Throws<ArgumentException>(() => builder.Build(
            "target",
            "0.1.0",
            [WireTargetModes.SuiteAsClient],
            [],
            [WireTargetCapabilities.HostRoutes],
            hostRouteProviders: [valid with { ProviderId = "" }]));
        Assert.Throws<ArgumentException>(() => builder.Build(
            "target",
            "0.1.0",
            [WireTargetModes.SuiteAsClient],
            [],
            [WireTargetCapabilities.HostRoutes],
            hostRouteProviders: [valid, valid]));
        Assert.Throws<ArgumentException>(() => builder.Build(
            "target",
            "0.1.0",
            [WireTargetModes.SuiteAsClient],
            [],
            [WireTargetCapabilities.HostRoutes],
            hostRouteProviders: [valid with { Platforms = ["runtime"] }]));
        Assert.Throws<ArgumentException>(() => builder.Build(
            "target",
            "0.1.0",
            [WireTargetModes.SuiteAsClient],
            [],
            [WireTargetCapabilities.HostRoutes],
            hostRouteProviders: [valid with { SecurityModes = ["tls"] }]));
    }

    [Theory]
    [MemberData(nameof(InvalidTransportCases))]
    public void BuildRejectsInvalidTransportDeclarations(WireTargetTransport[] transports)
    {
        WireTargetManifestBuilder builder = new(FullSupport());

        Assert.ThrowsAny<ArgumentException>(() => builder.Build(
            "target",
            "0.1.0",
            [WireTargetModes.SuiteAsClient],
            transports,
            [NnrpPreview4CapabilityTokens.ControlCancelAbort]));
    }

    [Fact]
    public void CommandWritesManifestAndRejectsFalseClaims()
    {
        string outputPath = Path.Combine(CreateTemporaryDirectory(), "target.json");
        StringWriter standardOutput = new();
        StringWriter standardError = new();
        string securityJson = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["transport"] = "quic",
            ["server_name"] = "localhost",
            ["trusted_certificate_der_path"] = "trust.der",
            ["certificate_der_path"] = "server.der",
            ["private_key_pkcs8_der_path"] = "key.der",
        });

        int result = WireTargetManifestCommand.Run(
            [
                "manifest",
                "--target-name", "nnrp-cs-local",
                "--suite-version", "0.1.0",
                "--mode", WireTargetModes.SuiteAsClient,
                "--transport", "quic=127.0.0.1:19092",
                "--transport-security", securityJson,
                "--capability", NnrpPreview4CapabilityTokens.ControlCancelAbort,
                "--max-frame-bytes", "4096",
                "--max-in-flight", "8",
                "--output", outputPath,
            ],
            standardOutput,
            standardError);

        Assert.Equal(0, result);
        Assert.Equal(string.Empty, standardError.ToString());
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(outputPath));
        Assert.Equal("nnrp-cs-local", document.RootElement.GetProperty("target_name").GetString());

        result = WireTargetManifestCommand.Run(
            [
                "manifest",
                "--mode", WireTargetModes.SuiteAsClient,
                "--transport", "ipc=npipe://nnrp",
                "--capability", "control.not-implemented",
                "--output", outputPath,
            ],
            standardOutput,
            standardError);
        Assert.Equal(2, result);
        Assert.Contains("not implemented", standardError.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void CommandHelpDocumentsRequiredServeTargetSuite()
    {
        StringWriter standardOutput = new();
        StringWriter standardError = new();

        int result = WireTargetManifestCommand.Run(
            ["--help"],
            standardOutput,
            standardError);

        Assert.Equal(0, result);
        Assert.Equal(string.Empty, standardError.ToString());
        Assert.Contains(
            "serve-target --manifest PATH --suite PATH [--artifact-root PATH]",
            standardOutput.ToString(),
            StringComparison.Ordinal);
    }

    [Fact]
    public void CommandWritesHostRouteProviderManifest()
    {
        string outputPath = Path.Combine(CreateTemporaryDirectory(), "target.json");
        string providerJson = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["transport"] = "ipc",
            ["provider_id"] = "nnrp.transport.ipc.native",
            ["installed"] = true,
            ["platforms"] = new[] { "native" },
            ["security_modes"] = new[] { "plain" },
        });

        int result = WireTargetManifestCommand.Run(
            [
                "manifest",
                "--mode", WireTargetModes.SuiteAsClient,
                "--host-route-provider", providerJson,
                "--capability", WireTargetCapabilities.HostRoutes,
                "--output", outputPath,
            ],
            TextWriter.Null,
            TextWriter.Null);

        Assert.Equal(0, result);
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(outputPath));
        JsonElement provider = document.RootElement
            .GetProperty("wire_conformance")
            .GetProperty("host_route_providers")[0];
        Assert.Equal("ipc", provider.GetProperty("transport").GetString());
        Assert.True(provider.GetProperty("installed").GetBoolean());
    }

    [Fact]
    public void CommandRejectsIncompleteReleaseManifestBeforeWriting()
    {
        string outputPath = Path.Combine(CreateTemporaryDirectory(), "target.json");
        StringWriter error = new();

        int result = WireTargetManifestCommand.Run(
            [
                "manifest",
                "--release",
                "--mode", WireTargetModes.SuiteAsClient,
                "--transport", "tcp=127.0.0.1:19091",
                "--capability", NnrpPreview4CapabilityTokens.ControlCancelAbort,
                "--output", outputPath,
            ],
            TextWriter.Null,
            error);

        Assert.Equal(2, result);
        Assert.Contains("missing", error.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(outputPath));
    }

    [Theory]
    [InlineData("[]", "must be a JSON object")]
    [InlineData("{\"transport\":\"ipc\",\"provider_id\":\"id\",\"installed\":true,\"platforms\":[\"native\"],\"security_modes\":[\"plain\"],\"extra\":1}", "unknown field")]
    [InlineData("{\"transport\":\"ipc\"}", "missing fields")]
    [InlineData("{\"transport\":\"ipc\",\"provider_id\":\"id\",\"installed\":\"yes\",\"platforms\":[\"native\"],\"security_modes\":[\"plain\"]}", "must be a bool")]
    [InlineData("{\"transport\":\"ipc\",\"provider_id\":\"id\",\"installed\":true,\"platforms\":\"native\",\"security_modes\":[\"plain\"]}", "must be an array")]
    [InlineData("{\"transport\":\"ipc\",\"provider_id\":\"id\",\"installed\":true,\"platforms\":[1],\"security_modes\":[\"plain\"]}", "non-empty strings")]
    public void CommandRejectsMalformedHostRouteProvider(string providerJson, string expectedError)
    {
        StringWriter error = new();

        int result = WireTargetManifestCommand.Run(
            [
                "manifest",
                "--mode", WireTargetModes.SuiteAsClient,
                "--host-route-provider", providerJson,
                "--capability", WireTargetCapabilities.HostRoutes,
                "--output", "target.json",
            ],
            TextWriter.Null,
            error);

        Assert.Equal(2, result);
        Assert.Contains(expectedError, error.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData()]
    [InlineData("--help")]
    public void CommandPrintsHelp(params string[] args)
    {
        StringWriter output = new();

        int result = WireTargetManifestCommand.Run(args, output, TextWriter.Null);

        Assert.Equal(0, result);
        Assert.Contains("Nnrp.WireConformance manifest", output.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("manifest", "--mode")]
    [InlineData("manifest", "mode", "suite_as_client")]
    [InlineData("manifest", "--unknown", "value")]
    [InlineData("manifest", "--max-in-flight", "zero")]
    [InlineData("manifest", "--output", "target.json")]
    public void CommandRejectsMalformedArguments(params string[] args)
    {
        StringWriter error = new();

        int result = WireTargetManifestCommand.Run(args, TextWriter.Null, error);

        Assert.Equal(2, result);
        Assert.NotEmpty(error.ToString());
    }

    public static TheoryData<WireTargetTransport[]> InvalidTransportCases => new()
    {
        Array.Empty<WireTargetTransport>(),
        new[] { new WireTargetTransport("udp", "127.0.0.1:1") },
        new[] { new WireTargetTransport("tcp", "") },
        new[] { new WireTargetTransport("tcp", "127.0.0.1:1"), new WireTargetTransport("tcp", "127.0.0.1:2") },
        new[] { new WireTargetTransport("quic", "127.0.0.1:1") },
        new[] { new WireTargetTransport("quic", "127.0.0.1:1", true) },
        new[] { new WireTargetTransport("tcp", "127.0.0.1:1", true, Security) },
        new[] { new WireTargetTransport("tcp", "127.0.0.1:1", false, Security) },
        new[] { new WireTargetTransport("websocket", "http://127.0.0.1:1") },
        new[] { new WireTargetTransport("websocket", "ws://127.0.0.1:1", true, Security) },
        new[] { new WireTargetTransport("websocket", "wss://127.0.0.1:1") },
    };

    private static WireTargetSupport FullSupport() => new(
        [WireTargetModes.SuiteAsClient, WireTargetModes.SuiteAsServer, WireTargetModes.SuiteAsProxy],
        NnrpPreview4CapabilityTokens.Transports,
        NnrpPreview4CapabilityTokens.AllCapabilities.Append(WireTargetCapabilities.HostRoutes));

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "nnrp-cs-wire-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
