using Nnrp.Core;
using Xunit;

namespace Nnrp.Core.Tests
{
    public sealed class ControlExtensionTests
    {
        [Fact]
        public void ControlExtensionTypeIdsAreStable()
        {
            Assert.Equal(0x0103, (ushort)ControlExtensionType.ClientLossTolerance);
            Assert.Equal(0x0104, (ushort)ControlExtensionType.ServerLossToleranceAck);
            Assert.Equal(0x0105, (ushort)ControlExtensionType.ClientPayloadCapabilities);
            Assert.Equal(0x0106, (ushort)ControlExtensionType.ServerPayloadCapabilitiesAck);
            Assert.Equal(0x0101, (ushort)ControlExtensionType.ClientTransportPolicy);
            Assert.Equal(0x0102, (ushort)ControlExtensionType.ServerTransportPolicyAck);
        }

        [Fact]
        public void ClientTransportPolicyExtensionRoundTripsThroughControlBlock()
        {
            var extension = new ClientTransportPolicyExtension(TransportPolicy.PreferQuic, TransportId.Quic);
            var block = extension.ToControlExtension();

            Assert.True(ClientTransportPolicyExtension.TryParse(block, out var parsed, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(extension, parsed);
        }

        [Fact]
        public void ServerTransportPolicyAckExtensionRoundTripsThroughControlBlock()
        {
            var extension = new ServerTransportPolicyAckExtension(
                TransportPolicy.PreferTcp,
                TransportPolicy.ForceTcp,
                TransportId.Tcp);
            var block = extension.ToControlExtension();

            Assert.True(ServerTransportPolicyAckExtension.TryParse(block, out var parsed, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(extension, parsed);
        }

        [Fact]
        public void ClientLossToleranceExtensionRoundTripsThroughControlBlock()
        {
            var extension = new ClientLossToleranceExtension(LossTolerance.FireAndForget);
            var block = extension.ToControlExtension();

            Assert.True(ClientLossToleranceExtension.TryParse(block, out var parsed, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(extension, parsed);
        }

        [Fact]
        public void ServerLossToleranceAckExtensionRoundTripsThroughControlBlock()
        {
            var extension = new ServerLossToleranceAckExtension(LossTolerance.BestEffort);
            var block = extension.ToControlExtension();

            Assert.True(ServerLossToleranceAckExtension.TryParse(block, out var parsed, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(extension, parsed);
        }

        [Fact]
        public void PayloadCapabilitiesExtensionsRoundTripThroughControlBlock()
        {
            var clientExtension = new ClientPayloadCapabilitiesExtension(PayloadKind.Tensor | PayloadKind.TokenChunk);
            var serverExtension = new ServerPayloadCapabilitiesAckExtension(PayloadKind.Tensor | PayloadKind.StructuredEvent);

            var clientBlock = clientExtension.ToControlExtension();
            var serverBlock = serverExtension.ToControlExtension();

            Assert.True(ClientPayloadCapabilitiesExtension.TryParse(clientBlock, out var parsedClient, out var clientError));
            Assert.Equal(NnrpParseError.None, clientError);
            Assert.Equal(clientExtension, parsedClient);

            Assert.True(ServerPayloadCapabilitiesAckExtension.TryParse(serverBlock, out var parsedServer, out var serverError));
            Assert.Equal(NnrpParseError.None, serverError);
            Assert.Equal(serverExtension, parsedServer);
        }

        [Fact]
        public void LossToleranceExtensionsRejectNonZeroReservedBytes()
        {
            var clientBlock = new ControlExtensionBlock(
                ControlExtensionType.ClientLossTolerance,
                new byte[] { (byte)LossTolerance.LowLatency, 1, 0, 0 });
            var serverBlock = new ControlExtensionBlock(
                ControlExtensionType.ServerLossToleranceAck,
                new byte[] { (byte)LossTolerance.Strict, 0, 2, 0 });

            Assert.False(ClientLossToleranceExtension.TryParse(clientBlock, out _, out var clientError));
            Assert.Equal(NnrpParseError.NonZeroReservedField, clientError);

            Assert.False(ServerLossToleranceAckExtension.TryParse(serverBlock, out _, out var serverError));
            Assert.Equal(NnrpParseError.NonZeroReservedField, serverError);
        }

        [Fact]
        public void ExtensionsRejectUnexpectedPayloadLength()
        {
            var shortTransportPolicy = new ControlExtensionBlock(
                ControlExtensionType.ClientTransportPolicy,
                new byte[] { (byte)TransportPolicy.Auto });
            var shortTransportAck = new ControlExtensionBlock(
                ControlExtensionType.ServerTransportPolicyAck,
                new byte[] { (byte)TransportPolicy.Auto, 0, 0, 0 });
            var shortClientLoss = new ControlExtensionBlock(
                ControlExtensionType.ClientLossTolerance,
                new byte[] { (byte)LossTolerance.Strict, 0, 0 });
            var shortServerLoss = new ControlExtensionBlock(
                ControlExtensionType.ServerLossToleranceAck,
                new byte[] { (byte)LossTolerance.Strict, 0 });
            var shortClientPayloadCapabilities = new ControlExtensionBlock(
                ControlExtensionType.ClientPayloadCapabilities,
                new byte[] { 1, 0, 0, 0, 0, 0, 0 });
            var shortServerPayloadCapabilities = new ControlExtensionBlock(
                ControlExtensionType.ServerPayloadCapabilitiesAck,
                new byte[] { 1, 0, 0, 0, 0, 0 });

            Assert.False(ClientTransportPolicyExtension.TryParse(shortTransportPolicy, out _, out var policyError));
            Assert.Equal(NnrpParseError.InvalidMessageLayout, policyError);

            Assert.False(ServerTransportPolicyAckExtension.TryParse(shortTransportAck, out _, out var ackError));
            Assert.Equal(NnrpParseError.InvalidMessageLayout, ackError);

            Assert.False(ClientLossToleranceExtension.TryParse(shortClientLoss, out _, out var clientLossError));
            Assert.Equal(NnrpParseError.InvalidMessageLayout, clientLossError);

            Assert.False(ServerLossToleranceAckExtension.TryParse(shortServerLoss, out _, out var serverLossError));
            Assert.Equal(NnrpParseError.InvalidMessageLayout, serverLossError);

            Assert.False(ClientPayloadCapabilitiesExtension.TryParse(shortClientPayloadCapabilities, out _, out var clientPayloadError));
            Assert.Equal(NnrpParseError.InvalidMessageLayout, clientPayloadError);

            Assert.False(ServerPayloadCapabilitiesAckExtension.TryParse(shortServerPayloadCapabilities, out _, out var serverPayloadError));
            Assert.Equal(NnrpParseError.InvalidMessageLayout, serverPayloadError);
        }

        [Fact]
        public void PayloadCapabilitiesExtensionsRejectUnknownBitsAndNonZeroCriticalBitmap()
        {
            var unknownBitsClient = new ControlExtensionBlock(
                ControlExtensionType.ClientPayloadCapabilities,
                new byte[] { 0x80, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 });
            var reservedBitmapServer = new ControlExtensionBlock(
                ControlExtensionType.ServerPayloadCapabilitiesAck,
                new byte[] { 0x01, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00 });

            Assert.False(ClientPayloadCapabilitiesExtension.TryParse(unknownBitsClient, out _, out var clientError));
            Assert.Equal(NnrpParseError.InvalidMessageLayout, clientError);

            Assert.False(ServerPayloadCapabilitiesAckExtension.TryParse(reservedBitmapServer, out _, out var serverError));
            Assert.Equal(NnrpParseError.NonZeroReservedField, serverError);
        }

        [Fact]
        public void ControlExtensionBlockUsesCurrentWireHeaderWithFlagsAndPadding()
        {
            var block = new ControlExtensionBlock(ControlExtensionType.ClientTransportPolicy, new byte[] { 1, 2, 3 });

            var bytes = block.ToArray();

            Assert.Equal(16, bytes.Length);
            Assert.Equal(0x01, bytes[0]);
            Assert.Equal(0x01, bytes[1]);
            Assert.Equal(0x00, bytes[2]);
            Assert.Equal(0x00, bytes[3]);
            Assert.Equal(0x03, bytes[4]);
            Assert.Equal(0x00, bytes[5]);
            Assert.Equal(0x00, bytes[6]);
            Assert.Equal(0x00, bytes[7]);
            Assert.Equal(1, bytes[8]);
            Assert.Equal(2, bytes[9]);
            Assert.Equal(3, bytes[10]);
            Assert.Equal(0, bytes[11]);
            Assert.Equal(0, bytes[12]);
            Assert.Equal(0, bytes[13]);
            Assert.Equal(0, bytes[14]);
            Assert.Equal(0, bytes[15]);

            Assert.True(ControlExtensionBlock.TryParse(bytes, out var parsed, out var consumed, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(bytes.Length, consumed);
            Assert.Equal((ushort)ControlExtensionType.ClientTransportPolicy, parsed.TypeCode);
            Assert.Equal(new byte[] { 1, 2, 3 }, parsed.Value.ToArray());
        }
    }
}
