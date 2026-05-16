using System;
using Nnrp.Core;
using Xunit;

namespace Nnrp.Core.Tests
{
    public sealed class ControlMessageTests
    {
        [Fact]
        public void ClientHelloMetadataRoundTripsThroughBytes()
        {
            var metadata = new ClientHelloMetadata(
                minVersionMajor: 1,
                maxVersionMajor: 1,
                supportedWireFormatBitmap: 0x1,
                supportedProfileBitmap: ControlMetadataBitmaps.TensorProfileBitmap,
                supportedPayloadKindBitmap: ControlMetadataBitmaps.TensorPayloadKindBitmap,
                supportedCodecBitmap: 0x3,
                supportedCompressionBitmap: 0x3,
                supportedDTypeBitmap: 0x21,
                supportedLayoutBitmap: 0x3,
                cacheDigestBitmap: ControlMetadataBitmaps.CacheDigestBitmap,
                cacheObjectBitmap: ControlMetadataBitmaps.TensorProfileCacheObjectBitmap,
                cacheNamespaceCount: 1,
                maxLaneCount: 2,
                maxCacheEntries: 16,
                maxCacheBytes: 1024,
                targetCadenceX100: 6000,
                latencyBudgetMilliseconds: 50,
                qualityTier: 2,
                degradePolicy: 0,
                requestedSessionId: 7,
                authBytes: 3,
                controlExtensionBytes: 0);

            var payload = metadata.ToArray();

            Assert.Equal(ClientHelloMetadata.MetadataLength, payload.Length);
            Assert.True(ClientHelloMetadata.TryParse(payload, out var parsed, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(metadata, parsed);
        }

        [Fact]
        public void ServerHelloAckMetadataRoundTripsThroughBytes()
        {
            var metadata = new ServerHelloAckMetadata(
                selectedVersionMajor: 1,
                selectedWireFormat: 1,
                authStatus: 0,
                reserved0: 0,
                sessionId: 9,
                acceptedProfileBitmap: ControlMetadataBitmaps.TensorProfileBitmap,
                acceptedPayloadKindBitmap: ControlMetadataBitmaps.TensorPayloadKindBitmap,
                acceptedCodecBitmap: 0x3,
                acceptedCompressionBitmap: 0x3,
                acceptedDTypeBitmap: 0x21,
                acceptedLayoutBitmap: 0x1,
                cacheDigestBitmap: 0x1,
                cacheObjectBitmap: 0x3,
                maxCacheEntries: 16,
                maxCacheBytes: 1024,
                maxLaneCount: 1,
                maxConcurrentFrames: 2,
                targetCadenceX100: 6000,
                latencyBudgetMilliseconds: 16,
                qualityTier: 2,
                degradePolicy: 0,
                maxBodyBytes: 4 * 1024 * 1024,
                tokenTtlMilliseconds: 300000,
                retryAfterMilliseconds: 0,
                controlExtensionBytes: 0,
                serverFlags: 0);

            var payload = metadata.ToArray();

            Assert.Equal(ServerHelloAckMetadata.MetadataLength, payload.Length);
            Assert.True(ServerHelloAckMetadata.TryParse(payload, out var parsed, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(metadata, parsed);
        }

        [Fact]
        public void ClientHelloMessageRoundTripsAuthBlockAndCapabilities()
        {
            var metadata = new ClientHelloMetadata(
                minVersionMajor: 1,
                maxVersionMajor: 1,
                supportedWireFormatBitmap: 0x1,
                supportedProfileBitmap: ControlMetadataBitmaps.TensorProfileBitmap,
                supportedPayloadKindBitmap: ControlMetadataBitmaps.TensorPayloadKindBitmap,
                supportedCodecBitmap: 0x3,
                supportedCompressionBitmap: 0x3,
                supportedDTypeBitmap: 0x21,
                supportedLayoutBitmap: 0x1,
                cacheDigestBitmap: ControlMetadataBitmaps.CacheDigestBitmap,
                cacheObjectBitmap: ControlMetadataBitmaps.TensorProfileCacheObjectBitmap,
                cacheNamespaceCount: 1,
                maxLaneCount: 1,
                maxCacheEntries: 16,
                maxCacheBytes: 1024,
                targetCadenceX100: 6000,
                latencyBudgetMilliseconds: 50,
                qualityTier: 2,
                degradePolicy: 0,
                requestedSessionId: 5,
                authBytes: 3,
                controlExtensionBytes: 0);
            var message = new ClientHelloMessage(
                new NnrpHeader(1, NnrpHeader.CurrentWireFormat, MessageType.ClientHello, HeaderFlags.AckRequired, ClientHelloMetadata.MetadataLength, 3, 0, 0, 0, 0, 99),
                metadata,
                new byte[] { 1, 2, 3 });

            Assert.True(ClientHelloMessage.TryParse(message.ToArray(), out var parsed, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(message.ToArray(), parsed.ToArray());

            var capabilities = parsed.ToCapabilities();
            Assert.Equal(new[] { CodecId.Raw, CodecId.Lz4 }, capabilities.SupportedCodecs);
            Assert.Equal(1, capabilities.MaxViews);
            Assert.True(capabilities.EnableCache);
        }

        [Fact]
        public void ClientHelloMessageRoundTripsTransportPolicyExtension()
        {
            var lossToleranceExtension = new ClientLossToleranceExtension(LossTolerance.LowLatency).ToControlExtension();
            var transportPolicyExtension = new ClientTransportPolicyExtension(TransportPolicy.PreferTcp, TransportId.Tcp).ToControlExtension();
            var payloadCapabilitiesExtension = new ClientPayloadCapabilitiesExtension(PayloadKind.Tensor | PayloadKind.TokenChunk).ToControlExtension();
            var metadata = new ClientHelloMetadata(
                1, 1, 0x2,
                ControlMetadataBitmaps.TensorProfileBitmap,
                ControlMetadataBitmaps.TensorPayloadKindBitmap,
                0x3, 0x3, 0x21, 0x1,
                ControlMetadataBitmaps.CacheDigestBitmap,
                ControlMetadataBitmaps.TensorProfileCacheObjectBitmap,
                1, 1, 16, 1024, 6000, 50, 2, 0, 5, 2,
                (uint)(lossToleranceExtension.TotalLength + transportPolicyExtension.TotalLength + payloadCapabilitiesExtension.TotalLength));
            var message = new ClientHelloMessage(
                new NnrpHeader(
                    1,
                    NnrpHeader.CurrentWireFormat,
                    MessageType.ClientHello,
                    HeaderFlags.AckRequired,
                    ClientHelloMetadata.MetadataLength,
                    (uint)(2 + lossToleranceExtension.TotalLength + transportPolicyExtension.TotalLength + payloadCapabilitiesExtension.TotalLength),
                    0,
                    0,
                    0,
                    0,
                    100),
                metadata,
                new byte[] { 7, 8 },
                new[] { lossToleranceExtension, transportPolicyExtension, payloadCapabilitiesExtension });

            Assert.True(ClientHelloMessage.TryParse(message.ToArray(), out var parsed, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(NnrpHeader.CurrentWireFormat, parsed.Header.WireFormat);
            Assert.True(parsed.TryGetClientLossToleranceExtension(out var parsedLossTolerance, out error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(LossTolerance.LowLatency, parsedLossTolerance.LossTolerance);
            Assert.True(parsed.TryGetClientTransportPolicyExtension(out var parsedExtension, out error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(TransportPolicy.PreferTcp, parsedExtension.TransportPolicy);
            Assert.Equal(TransportId.Tcp, parsedExtension.PreferredTransportId);
            Assert.True(parsed.TryGetClientPayloadCapabilitiesExtension(out var parsedPayloadCapabilities, out error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(PayloadKind.Tensor | PayloadKind.TokenChunk, parsedPayloadCapabilities.PayloadKindBitmap);
            Assert.Equal(0u, parsedPayloadCapabilities.CriticalExtensionFrameBitmap);
        }

        [Fact]
        public void ClientHelloMessageSerializesExtensionsBeforeAuthBlock()
        {
            var lossToleranceExtension = new ClientLossToleranceExtension(LossTolerance.LowLatency).ToControlExtension();
            var transportPolicyExtension = new ClientTransportPolicyExtension(TransportPolicy.PreferTcp, TransportId.Tcp).ToControlExtension();
            var payloadCapabilitiesExtension = new ClientPayloadCapabilitiesExtension(PayloadKind.Tensor | PayloadKind.ToolDelta).ToControlExtension();
            var metadata = new ClientHelloMetadata(
                1, 1, 0x2,
                ControlMetadataBitmaps.TensorProfileBitmap,
                ControlMetadataBitmaps.TensorPayloadKindBitmap,
                0x3, 0x3, 0x21, 0x1,
                ControlMetadataBitmaps.CacheDigestBitmap,
                ControlMetadataBitmaps.TensorProfileCacheObjectBitmap,
                1, 1, 16, 1024, 6000, 50, 2, 0, 5, 2,
                (uint)(lossToleranceExtension.TotalLength + transportPolicyExtension.TotalLength + payloadCapabilitiesExtension.TotalLength));
            var extensions = new[] { lossToleranceExtension, transportPolicyExtension, payloadCapabilitiesExtension };
            var firstExtensionBytes = lossToleranceExtension.ToArray();
            var secondExtensionBytes = transportPolicyExtension.ToArray();
            var thirdExtensionBytes = payloadCapabilitiesExtension.ToArray();
            var authBlock = new byte[] { 7, 8 };
            var message = new ClientHelloMessage(
                new NnrpHeader(
                    1,
                    NnrpHeader.CurrentWireFormat,
                    MessageType.ClientHello,
                    HeaderFlags.AckRequired,
                    ClientHelloMetadata.MetadataLength,
                    (uint)(authBlock.Length + firstExtensionBytes.Length + secondExtensionBytes.Length + thirdExtensionBytes.Length),
                    0,
                    0,
                    0,
                    0,
                    100),
                metadata,
                authBlock,
                extensions);

            var framed = message.ToFramedMessage();

            Assert.Equal(firstExtensionBytes, framed.Body.Slice(0, firstExtensionBytes.Length).ToArray());
            Assert.Equal(secondExtensionBytes, framed.Body.Slice(firstExtensionBytes.Length, secondExtensionBytes.Length).ToArray());
            Assert.Equal(thirdExtensionBytes, framed.Body.Slice(firstExtensionBytes.Length + secondExtensionBytes.Length, thirdExtensionBytes.Length).ToArray());
            Assert.Equal(authBlock, framed.Body.Slice(firstExtensionBytes.Length + secondExtensionBytes.Length + thirdExtensionBytes.Length, authBlock.Length).ToArray());
            Assert.True(ClientHelloMessage.TryParse(message.ToArray(), out var parsed, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(authBlock, parsed.AuthBlock.ToArray());
            Assert.True(parsed.TryGetClientTransportPolicyExtension(out var parsedExtension, out error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(TransportPolicy.PreferTcp, parsedExtension.TransportPolicy);
            Assert.True(parsed.TryGetClientPayloadCapabilitiesExtension(out var parsedPayloadCapabilities, out error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(PayloadKind.Tensor | PayloadKind.ToolDelta, parsedPayloadCapabilities.PayloadKindBitmap);
        }

        [Fact]
        public void ServerHelloAckMessageRoundTripsTransportPolicyExtension()
        {
            var lossToleranceExtension = new ServerLossToleranceAckExtension(LossTolerance.BestEffort).ToControlExtension();
            var transportPolicyExtension = new ServerTransportPolicyAckExtension(
                TransportPolicy.PreferTcp,
                TransportPolicy.ForceTcp,
                TransportId.Tcp).ToControlExtension();
            var payloadCapabilitiesExtension = new ServerPayloadCapabilitiesAckExtension(PayloadKind.Tensor | PayloadKind.StructuredEvent).ToControlExtension();
            var metadata = new ServerHelloAckMetadata(
                1, 2, 0, 0, 9,
                ControlMetadataBitmaps.TensorProfileBitmap,
                ControlMetadataBitmaps.TensorPayloadKindBitmap,
                0x3, 0x3, 0x21, 0x1, 0x1, 0x3,
                16, 1024, 1, 2, 6000, 16, 2, 0,
                4 * 1024 * 1024, 300000, 0,
                (uint)(lossToleranceExtension.TotalLength + transportPolicyExtension.TotalLength + payloadCapabilitiesExtension.TotalLength),
                0);
            var message = new ServerHelloAckMessage(
                new NnrpHeader(
                    1,
                    NnrpHeader.CurrentWireFormat,
                    MessageType.ServerHelloAck,
                    HeaderFlags.None,
                    ServerHelloAckMetadata.MetadataLength,
                    (uint)(lossToleranceExtension.TotalLength + transportPolicyExtension.TotalLength + payloadCapabilitiesExtension.TotalLength),
                    9,
                    0,
                    0,
                    0,
                    101),
                metadata,
                new[] { lossToleranceExtension, transportPolicyExtension, payloadCapabilitiesExtension });

            Assert.True(ServerHelloAckMessage.TryParse(message.ToArray(), out var parsed, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(NnrpHeader.CurrentWireFormat, parsed.Header.WireFormat);
            Assert.True(parsed.TryGetServerLossToleranceAckExtension(out var parsedLossTolerance, out error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(LossTolerance.BestEffort, parsedLossTolerance.AcceptedLossTolerance);
            Assert.True(parsed.TryGetServerTransportPolicyAckExtension(out var parsedExtension, out error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(TransportPolicy.PreferTcp, parsedExtension.TransportPolicy);
            Assert.Equal(TransportPolicy.ForceTcp, parsedExtension.AcceptedTransportPolicy);
            Assert.Equal(TransportId.Tcp, parsedExtension.ActiveTransportId);
            Assert.True(parsed.TryGetServerPayloadCapabilitiesAckExtension(out var parsedPayloadCapabilities, out error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(PayloadKind.Tensor | PayloadKind.StructuredEvent, parsedPayloadCapabilities.AcceptedPayloadKindBitmap);
            Assert.Equal(0u, parsedPayloadCapabilities.AcceptedCriticalExtensionFrameBitmap);
        }

        [Fact]
        public void ServerHelloAckMessageRejectsUnexpectedBody()
        {
            var framed = new NnrpFramedMessage(
                new NnrpHeader(1, NnrpHeader.CurrentWireFormat, MessageType.ServerHelloAck, HeaderFlags.None, ServerHelloAckMetadata.MetadataLength, 1, 4, 0, 0, 0, 9),
                new ServerHelloAckMetadata(
                    selectedVersionMajor: 1,
                    selectedWireFormat: 1,
                    authStatus: 0,
                    reserved0: 0,
                    sessionId: 4,
                    acceptedProfileBitmap: ControlMetadataBitmaps.TensorProfileBitmap,
                    acceptedPayloadKindBitmap: ControlMetadataBitmaps.TensorPayloadKindBitmap,
                    acceptedCodecBitmap: 0x1,
                    acceptedCompressionBitmap: 0x1,
                    acceptedDTypeBitmap: 0x1,
                    acceptedLayoutBitmap: 0x1,
                    cacheDigestBitmap: 0,
                    cacheObjectBitmap: 0,
                    maxCacheEntries: 0,
                    maxCacheBytes: 0,
                    maxLaneCount: 1,
                    maxConcurrentFrames: 1,
                    targetCadenceX100: 6000,
                    latencyBudgetMilliseconds: 16,
                    qualityTier: 2,
                    degradePolicy: 0,
                    maxBodyBytes: 1024,
                    tokenTtlMilliseconds: 1000,
                    retryAfterMilliseconds: 0,
                    controlExtensionBytes: 0,
                    serverFlags: 0).ToArray(),
                new byte[] { 1 });

            Assert.False(ServerHelloAckMessage.TryParse(framed.ToArray(), out _, out var error));
            Assert.Equal(NnrpParseError.InvalidMessageLayout, error);
        }
    }
}
