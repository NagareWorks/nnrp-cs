using System;
using System.Collections.Generic;
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
    public void CompiledSupportRejectsUnimplementedModeTransportAndCapability()
    {
        WireTargetManifestBuilder builder = new(WireTargetSupport.Compiled);

        Assert.Throws<InvalidOperationException>(() => builder.Build(
            "target",
            "0.1.0",
            [WireTargetModes.SuiteAsProxy],
            [new WireTargetTransport("tcp", "127.0.0.1:1")],
            [NnrpPreview4CapabilityTokens.ControlCancelAbort]));
        Assert.Throws<InvalidOperationException>(() => builder.Build(
            "target",
            "0.1.0",
            [WireTargetModes.SuiteAsClient],
            [new WireTargetTransport("ipc", "npipe://nnrp")],
            [NnrpPreview4CapabilityTokens.ControlCancelAbort]));
        Assert.Throws<InvalidOperationException>(() => builder.Build(
            "target",
            "0.1.0",
            [WireTargetModes.SuiteAsClient],
            [new WireTargetTransport("tcp", "127.0.0.1:1")],
            ["control.not-implemented"]));
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
                "--capability", NnrpPreview4CapabilityTokens.ControlCancelAbort,
                "--output", outputPath,
            ],
            standardOutput,
            standardError);
        Assert.Equal(2, result);
        Assert.Contains("not compiled", standardError.ToString(), StringComparison.Ordinal);
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
        NnrpPreview4CapabilityTokens.AllCapabilities);

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "nnrp-cs-wire-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
