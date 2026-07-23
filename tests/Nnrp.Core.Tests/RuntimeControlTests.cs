using System;
using System.Collections.Generic;
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
                new SupersedeMetadata(61, 62, 63, 2, 1, 2),
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
            yield return Case(
                MessageType.CapabilityNegotiation,
                new CapabilityMetadata(1, 2, 3, 4, 121, 122, 3, 1),
                body);
            yield return Case(
                MessageType.DegradeProfile,
                new CapabilityMetadata(2, 3, 4, 5, 131, 132, 3, 2),
                body);
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
                new ResultDropReasonMetadata(171, 172, 4, RuntimeRole.Runtime, 3, 2),
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
                new ResultDropReasonMetadata(1, 2, 3, RuntimeRole.Server, 0, 0));
            encoded[31] = 1;
            Assert.Throws<ArgumentException>(() => NnrpRuntimeControl.Decode(MessageType.ResultDropReason, encoded));

            var pressure = NnrpRuntimeControl.Encode(
                MessageType.Backpressure,
                new PressureMetadata(1, 2, 3, 4, 5, 0));
            pressure[28] = 1;
            Assert.Throws<ArgumentException>(() => NnrpRuntimeControl.Decode(MessageType.Backpressure, pressure));
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
    }
}
