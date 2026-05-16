// NNRP/1 Minimal Samples
// Build: dotnet build Nnrp.sln
// Run:   dotnet run --project samples/Nnrp.Samples (coming soon)
//
// These samples show the core wire codec usage without requiring
// a running server or Unity engine.

using System;
using Nnrp.Core;

// ── Header encode / decode ────────────────────────────────────────
var header = new NnrpHeader(
    versionMajor: NnrpHeader.CurrentVersionMajor,
    wireFormat: NnrpHeader.CurrentWireFormat,
    messageType: MessageType.FrameSubmit,
    flags: HeaderFlags.AckRequired | HeaderFlags.Keyframe,
    metaLength: FrameSubmitMetadata.MetadataLength,
    bodyLength: 128,
    sessionId: 42,
    frameId: 7,
    viewId: 1,
    routeId: 0,
    traceId: 0x0102030405060708UL);

byte[] headerBytes = header.ToArray();

if (NnrpHeader.TryParse(headerBytes, NnrpHeaderParseOptions.Strict, out var parsedHeader, out var headerError))
{
    Console.WriteLine($"Parsed header: session={parsedHeader.SessionId}, frame={parsedHeader.FrameId}");
}
else
{
    Console.WriteLine($"Header parse error: {headerError}");
}

// ── Frame submit encode / decode ───────────────────────────────────
var cameraBlock = new byte[] { 0xCA, 0xFE };
var tileIds = new ushort[] { 1, 9 };
var section = new TensorSectionBlock(
    new TensorSectionDescriptor(
        role: TensorRole.LumaHint, codec: CodecId.Raw,
        dtype: DTypeId.UInt8, layout: TensorLayoutId.Nhwc,
        scalePolicy: ScalePolicy.None, flags: 0,
        elementCountPerTile: 0, codecTableBytes: 0,
        lengthTableBytes: 8, payloadBytes: 3, payloadStrideBytes: 0),
    codecTable: Array.Empty<byte>(),
    lengthTable: new byte[] { 1, 0, 0, 0, 2, 0, 0, 0 },
    payload: new byte[] { 0xAA, 0xBB, 0xCC });

var metadata = new FrameSubmitMetadata(
    sourceWidth: 640, sourceHeight: 360,
    tileWidth: 32, tileHeight: 32,
    tileCount: 2, sectionCount: 1,
    frameClass: FrameClass.Keyframe,
    inputProfile: InputProfile.ChangedTilesLuma,
    tileIndexMode: TileIndexMode.RawUInt16,
    latencyBudgetMilliseconds: 16,
    targetFpsTimes100: 6000,
    retryOfFrame: 0, tileBaseId: 0,
    cameraBytes: (uint)cameraBlock.Length,
    tileIndexBytes: 4);

// 8-byte aligned body length
var bodyLength = (uint)(BinaryAlignment.AlignUp(cameraBlock.Length, 8)
    + (int)metadata.TileIndexBytes
    + BinaryAlignment.AlignUp((int)metadata.TileIndexBytes, 8)
    + section.TotalLength);

var frameHeader = new NnrpHeader(
    NnrpHeader.CurrentVersionMajor, NnrpHeader.CurrentWireFormat,
    MessageType.FrameSubmit, HeaderFlags.None,
    FrameSubmitMetadata.MetadataLength, bodyLength,
    1, 100, 0, 0, 0);

var frameMsg = new FrameSubmitMessage(frameHeader, metadata, cameraBlock, tileIds, new[] { section });
byte[] framePacket = frameMsg.ToArray();

if (FrameSubmitMessage.TryParse(framePacket, out var parsedFrame, out var frameError))
{
    Console.WriteLine($"Parsed frame: {parsedFrame.TileIds.Length} tiles, {parsedFrame.Sections.Length} sections");
}
else
{
    Console.WriteLine($"Frame parse error: {frameError}");
}

// ── Fake client / server in-memory negotiation ─────────────────────
Console.WriteLine("\n=== Fake negotiation ===");

var clientProfile = new ClientProfile();
var serverProfile = new ServerProfile();

var clientHello = clientProfile.ToClientHello(requestedSessionId: 1, traceId: 0);
Console.WriteLine($"ClientHello session_id hint: {clientHello.Metadata.RequestedSessionId}");

var negotiation = NnrpCapabilityNegotiator.Negotiate(
    clientHello.ToCapabilities(), serverProfile);

if (negotiation.IsSuccess)
{
    var ack = serverProfile.CreateServerHelloAck(
        sessionId: 99, negotiation: negotiation, traceId: 0);
    Console.WriteLine($"ServerHelloAck assigned session: {ack.Metadata.SessionId}");
    Console.WriteLine("Negotiation succeeded.");
}
else
{
    Console.WriteLine($"Negotiation failed: {negotiation.FailureReason}");
}

Console.WriteLine("Samples complete.");
