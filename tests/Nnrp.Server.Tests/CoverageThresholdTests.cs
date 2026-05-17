using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nnrp.Core;
using Nnrp.Server;
using Xunit;

namespace Nnrp.Server.Tests
{
    public sealed class CoverageThresholdTests
    {
        [Fact]
        public async Task AcceptAsyncAttachesTransportPolicyAckWhenTransportIsKnownTcp()
        {
            var transport = new Nnrp.Transport.Tcp.NnrpTcpMessageTransport(
                CreateClientHello(
                    requestedSessionId: 41,
                    transportPolicy: TransportPolicy.Auto,
                    preferredTransportId: TransportId.Tcp).ToFramedMessage());
            var server = new NnrpServerSession(new ServerProfile(), transport);

            var failure = await server.AcceptAsync(CancellationToken.None);

            Assert.Equal(NnrpProtocolFailure.None, failure);
            Assert.Equal(NnrpProtocolFailure.None, server.LastFailure);
            Assert.True(ServerHelloAckMessage.TryParse(transport.Sent[0].ToArray(), out var ack, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.True(ack.TryGetServerTransportPolicyAckExtension(out var extension, out error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(TransportPolicy.Auto, extension.TransportPolicy);
            Assert.Equal(TransportPolicy.Auto, extension.AcceptedTransportPolicy);
            Assert.Equal(TransportId.Tcp, extension.ActiveTransportId);
        }

        [Fact]
        public async Task AcceptAsyncRejectsClientThatExceedsServerViewLimit()
        {
            var transport = new Nnrp.Transport.Tcp.NnrpTcpMessageTransport(
                CreateClientHello(
                    requestedSessionId: 41,
                    maxLaneCount: 2).ToFramedMessage());
            var server = new NnrpServerSession(new ServerProfile { MaxViews = 1 }, transport);

            var failure = await server.AcceptAsync(CancellationToken.None);

            Assert.True(failure.IsFailure);
            Assert.Equal(ErrorCode.LimitExceeded, failure.ErrorCode);
            Assert.True(failure.IsFatal);
            Assert.Equal(NnrpSessionState.Closed, server.State);
            Assert.False(server.LastNegotiationResult.IsAccepted);
            Assert.Equal(failure, server.LastFailure);
            Assert.True(ErrorMessage.TryParse(transport.Sent[0].ToArray(), out var errorMessage, out var parseError));
            Assert.Equal(NnrpParseError.None, parseError);
            Assert.Equal(ErrorCode.LimitExceeded, errorMessage.Metadata.ErrorCode);
        }

        [Fact]
        public async Task AcceptAsyncRejectsClientThatOmitsCurrentWireFormat()
        {
            var transport = new Nnrp.Transport.Tcp.NnrpTcpMessageTransport(
                CreateClientHello(
                    requestedSessionId: 41,
                    supportedWireFormatBitmap: 0).ToFramedMessage());
            var server = new NnrpServerSession(new ServerProfile(), transport);

            var failure = await server.AcceptAsync(CancellationToken.None);

            Assert.True(failure.IsFailure);
            Assert.Equal(ErrorCode.UnsupportedVersion, failure.ErrorCode);
            Assert.True(failure.IsFatal);
            Assert.Equal(NnrpSessionState.Closed, server.State);
            Assert.True(ErrorMessage.TryParse(transport.Sent[0].ToArray(), out var errorMessage, out var parseError));
            Assert.Equal(NnrpParseError.None, parseError);
            Assert.Equal(ErrorCode.UnsupportedVersion, errorMessage.Metadata.ErrorCode);
        }

        [Fact]
        public void NnrpFrameSubmitExposesConstructorValues()
        {
            var section = CreateSection();
            var frameSubmit = new NnrpFrameSubmit(
                sessionId: 7,
                frameId: 9,
                viewId: 2,
                traceId: 11,
                sourceWidth: 1920,
                sourceHeight: 1080,
                tileWidth: 64,
                tileHeight: 64,
                cameraBlock: new byte[] { 1, 2, 3 },
                tileIds: new ushort[] { 5, 7 },
                sections: new[] { section },
                frameClass: FrameClass.Keyframe,
                inputProfile: InputProfile.DenseLumaFrame,
                tileIndexMode: TileIndexMode.RawUInt16,
                latencyBudgetMilliseconds: 16,
                cadenceHintX100: 6000,
                dependencyFrameId: 3,
                tileBaseId: 12);

            Assert.Equal(7u, frameSubmit.SessionId);
            Assert.Equal(9u, frameSubmit.FrameId);
            Assert.Equal((ushort)2, frameSubmit.ViewId);
            Assert.Equal(11ul, frameSubmit.TraceId);
            Assert.Equal((ushort)1920, frameSubmit.SourceWidth);
            Assert.Equal((ushort)1080, frameSubmit.SourceHeight);
            Assert.Equal((ushort)64, frameSubmit.TileWidth);
            Assert.Equal((ushort)64, frameSubmit.TileHeight);
            Assert.Equal(new byte[] { 1, 2, 3 }, frameSubmit.CameraBlock.ToArray());
            Assert.Equal(new ushort[] { 5, 7 }, frameSubmit.TileIds.ToArray());
            Assert.Single(frameSubmit.Sections.ToArray());
            Assert.Equal(FrameClass.Keyframe, frameSubmit.FrameClass);
            Assert.Equal(InputProfile.DenseLumaFrame, frameSubmit.InputProfile);
            Assert.Equal(TileIndexMode.RawUInt16, frameSubmit.TileIndexMode);
            Assert.Equal((ushort)16, frameSubmit.LatencyBudgetMilliseconds);
            Assert.Equal((ushort)6000, frameSubmit.CadenceHintX100);
            Assert.Equal(3u, frameSubmit.DependencyFrameId);
            Assert.Equal(12u, frameSubmit.TileBaseId);
        }

        private static ClientHelloMessage CreateClientHello(
            uint requestedSessionId,
            uint? supportedWireFormatBitmap = null,
            TransportPolicy? transportPolicy = null,
            TransportId preferredTransportId = TransportId.Unspecified,
            uint maxLaneCount = 1)
        {
            var extensions = transportPolicy.HasValue
                ? new[] { new ClientTransportPolicyExtension(transportPolicy.Value, preferredTransportId).ToControlExtension() }
                : Array.Empty<ControlExtensionBlock>();
            var extensionBytes = GetExtensionBodyLength(extensions);

            var metadata = new ClientHelloMetadata(
                minVersionMajor: NnrpHeader.CurrentVersionMajor,
                maxVersionMajor: NnrpHeader.CurrentVersionMajor,
                supportedWireFormatBitmap: supportedWireFormatBitmap ?? ControlMetadataBitmaps.EncodeCurrentWireFormatBitmap(),
                supportedProfileBitmap: ControlMetadataBitmaps.TensorProfileBitmap,
                supportedPayloadKindBitmap: ControlMetadataBitmaps.TensorPayloadKindBitmap,
                supportedCodecBitmap: ControlMetadataBitmaps.EncodeCodecBitmap(new[] { CodecId.Raw }),
                supportedCompressionBitmap: ControlMetadataBitmaps.EncodeCodecBitmap(new[] { CodecId.Raw }),
                supportedDTypeBitmap: ControlMetadataBitmaps.EncodeDTypeBitmap(new[] { DTypeId.UInt8, DTypeId.Float16 }),
                supportedLayoutBitmap: ControlMetadataBitmaps.EncodeTensorLayoutBitmap(new[] { TensorLayoutId.Nhwc }),
                cacheDigestBitmap: ControlMetadataBitmaps.CacheDigestBitmap,
                cacheObjectBitmap: ControlMetadataBitmaps.TensorProfileCacheObjectBitmap,
                cacheNamespaceCount: 1,
                maxLaneCount: maxLaneCount,
                maxCacheEntries: 128,
                maxCacheBytes: ControlMetadataBitmaps.DefaultCacheBytes,
                targetCadenceX100: 6000,
                latencyBudgetMilliseconds: 100,
                qualityTier: ControlMetadataBitmaps.DefaultQualityTier,
                degradePolicy: 0,
                requestedSessionId: requestedSessionId,
                authBytes: 0,
                controlExtensionBytes: (uint)extensionBytes);
            var header = new NnrpHeader(
                versionMajor: NnrpHeader.CurrentVersionMajor,
                wireFormat: NnrpHeader.CurrentWireFormat,
                messageType: MessageType.ClientHello,
                flags: HeaderFlags.AckRequired,
                metaLength: ClientHelloMetadata.MetadataLength,
                bodyLength: (uint)extensionBytes,
                sessionId: 0,
                frameId: 0,
                viewId: 0,
                routeId: 0,
                traceId: 0);
            return new ClientHelloMessage(header, metadata, Array.Empty<byte>(), extensions);
        }

        private static int GetExtensionBodyLength(ControlExtensionBlock[] extensions)
        {
            var total = 0;
            for (var index = 0; index < extensions.Length; index++)
            {
                total += extensions[index].TotalLength;
            }

            return total;
        }

        private static TensorSectionBlock CreateSection()
        {
            return new TensorSectionBlock(
                new TensorSectionDescriptor(
                    TensorRole.SrResidual,
                    CodecId.Raw,
                    DTypeId.UInt8,
                    TensorLayoutId.Nhwc,
                    ScalePolicy.None,
                    0,
                    1,
                    0,
                    8,
                    2,
                    0),
                Array.Empty<byte>(),
                new byte[] { 1, 0, 0, 0, 2, 0, 0, 0 },
                new byte[] { 0xAA, 0xBB });
        }
    }
}

namespace Nnrp.Transport.Tcp
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Nnrp.Core;

    internal sealed class NnrpTcpMessageTransport : INnrpMessageTransport
    {
        private readonly Queue<NnrpFramedMessage> inbound;

        public NnrpTcpMessageTransport(params NnrpFramedMessage[] inbound)
        {
            this.inbound = new Queue<NnrpFramedMessage>(inbound ?? Array.Empty<NnrpFramedMessage>());
        }

        public List<NnrpFramedMessage> Sent { get; } = new List<NnrpFramedMessage>();

        public ValueTask SendAsync(NnrpFramedMessage message, CancellationToken cancellationToken)
        {
            Sent.Add(message);
            return default;
        }

        public ValueTask<NnrpFramedMessage> ReceiveAsync(CancellationToken cancellationToken)
        {
            if (inbound.Count == 0)
            {
                throw new InvalidOperationException("No inbound messages queued.");
            }

            return new ValueTask<NnrpFramedMessage>(inbound.Dequeue());
        }
    }
}
