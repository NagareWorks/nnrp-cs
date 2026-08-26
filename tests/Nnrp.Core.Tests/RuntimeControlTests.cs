using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using Nnrp.Core;
using Nnrp.Runtime;
using Xunit;

namespace Nnrp.Core.Tests
{
    public sealed class RuntimeControlTests
    {
        public static IEnumerable<object[]> RuntimeControlCases()
        {
            var diagnostic = new byte[] { 1, 2 };
            var body = new byte[] { 3, 4, 5 };

            yield return Case(
                MessageType.Cancel,
                new ControlRequestMetadata(11, 12, 1, RuntimeRole.Client, 1, 2),
                diagnostic);
            yield return Case(
                MessageType.Abort,
                new ControlRequestMetadata(21, 22, 2, RuntimeRole.Server, 2, 2),
                diagnostic);
            yield return Case(
                MessageType.PriorityUpdate,
                new SchedulingMetadata(31, 32, 3, -2, 33, 1));
            yield return Case(
                MessageType.Deadline,
                new SchedulingMetadata(41, 42, 4, 0, 43, 2));
            yield return Case(
                MessageType.ExpireAt,
                new SchedulingMetadata(51, 52, 5, 2, 53, 3));
            yield return Case(
                MessageType.Supersede,
                new SupersedeMetadata(61, 62, 63, NnrpResultDropReasonCode.Superseded, 1, 2),
                diagnostic);
            yield return Case(
                MessageType.BudgetUpdate,
                new BudgetMetadata(71, 72, 73, 74, 75, 1));
            yield return Case(
                MessageType.Progress,
                new ProgressMetadata(81, 82, 5, 5000, 83, 3),
                body);
            yield return Case(
                MessageType.PartialResult,
                new PartialResultMetadata(91, 92, 93, 94, 3, 3),
                body);
            yield return Case(
                MessageType.Backpressure,
                new PressureMetadata(101, 102, 2, 3, 104, 1));
            yield return Case(
                MessageType.CreditUpdate,
                new PressureMetadata(111, 112, 1, 4, 114, 2));
            yield return Capability(
                MessageType.CapabilityNegotiation,
                1,
                NnrpPreview4CapabilityTokens.ControlCapabilityCosts,
                NnrpPreview4CapabilityTokens.ControlRouteExecutionHint);
            yield return Capability(
                MessageType.DegradeProfile,
                2,
                NnrpPreview4CapabilityTokens.ControlCapabilityCosts,
                NnrpPreview4CapabilityTokens.ControlDegradeProfile,
                NnrpPreview4CapabilityTokens.ControlRouteExecutionHint);
            yield return Case(
                MessageType.RouteHint,
                new RouteHintMetadata(141, 142, 3, 4, 143, 3, 1),
                body);
            yield return Case(
                MessageType.ExecutionHint,
                new RouteHintMetadata(151, 152, 4, 5, 153, 3, 2),
                body);
            yield return Case(
                MessageType.TraceContext,
                new TraceContextMetadata(161, 162, 163, 5, 3, 3),
                body);
            yield return Case(
                MessageType.ResultDropReason,
                new ResultDropReasonMetadata(171, 172, NnrpResultDropReasonCode.Backpressure, RuntimeRole.Runtime, 3, 2),
                diagnostic);
            yield return Case(
                MessageType.ErrorRecoverable,
                new RecoverableErrorMetadata(181, 182, 3, RuntimeRole.Server, 3, 183, 184, 185, 186, 2),
                diagnostic);
            yield return Case(
                MessageType.RetryAfter,
                new RetryAfterMetadata(191, 192, 193, 194, 5, RuntimeRole.Scheduler, 3, 2),
                diagnostic);
        }

        [Theory]
        [MemberData(nameof(RuntimeControlCases))]
        public void FrozenRuntimeControlMetadataRoundTrips(
            MessageType messageType,
            IRuntimeControlMetadata metadata,
            byte[] tail)
        {
            var encoded = NnrpRuntimeControl.Encode(messageType, metadata, tail);
            var decoded = NnrpRuntimeControl.Decode(messageType, encoded);

            Assert.Equal(metadata.GetType(), decoded.Metadata.GetType());
            Assert.Equal(tail, decoded.Tail.ToArray());
            Assert.Equal(encoded, NnrpRuntimeControl.Encode(messageType, decoded.Metadata, decoded.Tail.Span));
        }

        [Fact]
        public void SchedulingMetadataPreservesSignedPriorityDelta()
        {
            var metadata = new SchedulingMetadata(1, 2, 3, -7, 4, 0);

            var decoded = NnrpRuntimeControl.Decode(
                MessageType.PriorityUpdate,
                NnrpRuntimeControl.Encode(MessageType.PriorityUpdate, metadata));

            Assert.Equal(-7, decoded.GetMetadata<SchedulingMetadata>().PriorityDelta);
        }

        [Fact]
        public void FrozenDocumentationNamedArgumentsConstructMetadata()
        {
            var metadata = new ProgressMetadata(
                OperationId: 42,
                ProgressSequence: 1,
                StageCode: 2,
                PercentX100: 2500,
                ObjectId: 0,
                BodyBytes: 0);

            Assert.Equal(42UL, metadata.OperationId);
            Assert.Equal(metadata, metadata with { ProgressSequence = 1 });
        }

        [Fact]
        public void RuntimeControlRejectsWrongMetadataTailAndMessageType()
        {
            Assert.Throws<ArgumentException>(() => NnrpRuntimeControl.Encode(
                MessageType.Progress,
                new SchedulingMetadata(1, 2, 3, 0, 4, 0)));
            Assert.Throws<ArgumentException>(() => NnrpRuntimeControl.Encode(
                MessageType.Progress,
                new ProgressMetadata(1, 2, 3, 4, 5, 1)));
            Assert.Throws<ArgumentOutOfRangeException>(() => NnrpRuntimeControl.Encode(
                MessageType.FrameSubmit,
                new ProgressMetadata(1, 2, 3, 4, 5, 0)));
        }

        [Fact]
        public void RuntimeControlRejectsReservedBitsRangesAndBytes()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => NnrpRuntimeControl.Encode(
                MessageType.Cancel,
                new ControlRequestMetadata(1, 2, 3, RuntimeRole.Client, 0x04, 0)));
            Assert.Throws<ArgumentOutOfRangeException>(() => NnrpRuntimeControl.Encode(
                MessageType.Progress,
                new ProgressMetadata(1, 2, 3, 10001, 4, 0)));

            var encoded = NnrpRuntimeControl.Encode(
                MessageType.ResultDropReason,
                new ResultDropReasonMetadata(1, 2, NnrpResultDropReasonCode.PeerCancelled, RuntimeRole.Server, 0, 0));
            encoded[31] = 1;
            Assert.Throws<ArgumentException>(() => NnrpRuntimeControl.Decode(MessageType.ResultDropReason, encoded));

            var pressure = NnrpRuntimeControl.Encode(
                MessageType.Backpressure,
                new PressureMetadata(1, 2, 3, 4, 5, 0));
            pressure[28] = 1;
            Assert.Throws<ArgumentException>(() => NnrpRuntimeControl.Decode(MessageType.Backpressure, pressure));
        }

        [Fact]
        public void CapabilityBodyRejectsMalformedCountAndTokens()
        {
            AssertCapabilityBodyError(Array.Empty<byte>(), 1, "non-zero capability count");
            AssertCapabilityBodyError(new byte[] { 1, 0, (byte)'a' }, 0, "zero capability count");
            AssertCapabilityBodyError(new byte[] { 0, 0 }, 1, "length must be non-zero");
            AssertCapabilityBodyError(new byte[] { 4, 0, (byte)'a' }, 1, "exceeds the declared body");
            AssertCapabilityBodyError(new byte[] { 1, 0, (byte)'A' }, 1, "lowercase ASCII");
        }

        [Fact]
        public void CapabilityBodyRejectsDuplicatesNonCanonicalOrderAndCountMismatch()
        {
            byte[] first = NnrpCapabilityTokenBodyCodec.Encode(
                new[] { NnrpPreview4CapabilityTokens.ControlCancelAbort });
            byte[] second = NnrpCapabilityTokenBodyCodec.Encode(
                new[] { NnrpPreview4CapabilityTokens.CacheReference });
            byte[] duplicate = first.Concat(first).ToArray();
            byte[] reversed = first.Concat(second).ToArray();

            AssertCapabilityBodyError(duplicate, 2, "unique");
            AssertCapabilityBodyError(reversed, 2, "canonical byte order");
            AssertCapabilityBodyError(first, 2, "declares 2 entries but received 1");
        }

        [Fact]
        public void CapabilityCodecRejectsInvalidEncodeInputsAndAcceptsEmptySet()
        {
            Assert.Throws<ArgumentNullException>(
                () => NnrpCapabilityTokenBodyCodec.Encode(null!));
            Assert.Contains(
                "must not be null",
                Assert.Throws<ArgumentException>(
                    () => NnrpCapabilityTokenBodyCodec.Encode(new string[] { null! })).Message,
                StringComparison.Ordinal);
            Assert.Contains(
                "length must be non-zero",
                Assert.Throws<ArgumentException>(
                    () => NnrpCapabilityTokenBodyCodec.Encode(new[] { string.Empty })).Message,
                StringComparison.Ordinal);
            Assert.Contains(
                "lowercase ASCII",
                Assert.Throws<ArgumentException>(
                    () => NnrpCapabilityTokenBodyCodec.Encode(new[] { "Control.Cost" })).Message,
                StringComparison.Ordinal);
            Assert.Contains(
                "must be unique",
                Assert.Throws<ArgumentException>(
                    () => NnrpCapabilityTokenBodyCodec.Encode(new[] { "control.cost", "control.cost" })).Message,
                StringComparison.Ordinal);
            Assert.Contains(
                "u16 length range",
                Assert.Throws<ArgumentException>(
                    () => NnrpCapabilityTokenBodyCodec.Encode(new[] { new string('a', ushort.MaxValue + 1) })).Message,
                StringComparison.Ordinal);

            Assert.Empty(NnrpCapabilityTokenBodyCodec.Decode(ReadOnlySpan<byte>.Empty, 0));
            Assert.Contains(
                "missing its token length",
                Assert.Throws<ArgumentException>(
                    () => NnrpCapabilityTokenBodyCodec.Decode(new byte[] { 1 }, 1)).Message,
                StringComparison.Ordinal);
        }

        [Fact]
        public void DropReasonRejectsReservedRangeAndPreservesPrivateRange()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => NnrpRuntimeControl.Encode(
                MessageType.Supersede,
                new SupersedeMetadata(1, 2, 3, (NnrpResultDropReasonCode)0x000a, 0, 0)));
            Assert.Throws<ArgumentOutOfRangeException>(() => NnrpRuntimeControl.Encode(
                MessageType.ResultDropReason,
                new ResultDropReasonMetadata(1, 2, (NnrpResultDropReasonCode)0x7fff, RuntimeRole.Server, 0, 0)));

            var privateReason = (NnrpResultDropReasonCode)0x8001;
            var decoded = NnrpRuntimeControl.Decode(
                MessageType.ResultDropReason,
                NnrpRuntimeControl.Encode(
                    MessageType.ResultDropReason,
                    new ResultDropReasonMetadata(1, 2, privateReason, RuntimeRole.Server, 0, 0)));

            Assert.Equal(privateReason, decoded.GetMetadata<ResultDropReasonMetadata>().DropReasonCode);
        }

        [Fact]
        public void DecodedMetadataRejectsWrongRequestedType()
        {
            var decoded = NnrpRuntimeControl.Decode(
                MessageType.BudgetUpdate,
                NnrpRuntimeControl.Encode(
                    MessageType.BudgetUpdate,
                    new BudgetMetadata(1, 2, 3, 4, 5, 0)));

            Assert.Throws<InvalidOperationException>(() => decoded.GetMetadata<ProgressMetadata>());
        }

        private static object[] Case(
            MessageType messageType,
            IRuntimeControlMetadata metadata,
            byte[]? tail = null)
        {
            return new object[] { messageType, metadata, tail ?? Array.Empty<byte>() };
        }

        private static object[] Capability(
            MessageType messageType,
            ushort profileId,
            params string[] tokens)
        {
            byte[] body = NnrpCapabilityTokenBodyCodec.Encode(tokens);
            return Case(
                messageType,
                new CapabilityMetadata(
                    profileId,
                    checked((ushort)tokens.Length),
                    3,
                    4,
                    121,
                    122,
                    checked((uint)body.Length),
                    1),
                body);
        }

        private static void AssertCapabilityBodyError(
            byte[] body,
            ushort capabilityCount,
            string expectedMessage)
        {
            var metadata = new CapabilityMetadata(
                1,
                capabilityCount,
                0,
                0,
                0,
                0,
                checked((uint)body.Length),
                0);

            ArgumentException encodeError = Assert.Throws<ArgumentException>(
                () => NnrpRuntimeControl.Encode(MessageType.CapabilityNegotiation, metadata, body));
            Assert.Contains(NnrpCapabilityTokenBodyCodec.ErrorCode, encodeError.Message, StringComparison.Ordinal);
            Assert.Contains(expectedMessage, encodeError.Message, StringComparison.Ordinal);

            var encoded = new byte[CapabilityMetadata.EncodedLength + body.Length];
            BinaryPrimitives.WriteUInt16LittleEndian(encoded.AsSpan(0, 2), metadata.ProfileId);
            BinaryPrimitives.WriteUInt16LittleEndian(encoded.AsSpan(2, 2), metadata.CapabilityCount);
            BinaryPrimitives.WriteUInt32LittleEndian(encoded.AsSpan(24, 4), metadata.BodyBytes);
            body.CopyTo(encoded.AsSpan(CapabilityMetadata.EncodedLength));
            ArgumentException decodeError = Assert.Throws<ArgumentException>(
                () => NnrpRuntimeControl.Decode(MessageType.CapabilityNegotiation, encoded));
            Assert.Contains(NnrpCapabilityTokenBodyCodec.ErrorCode, decodeError.Message, StringComparison.Ordinal);
            Assert.Contains(expectedMessage, decodeError.Message, StringComparison.Ordinal);
        }
    }
}
