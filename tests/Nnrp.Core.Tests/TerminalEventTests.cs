using System;
using Nnrp.Core;
using Nnrp.Runtime;
using Xunit;

namespace Nnrp.Core.Tests
{
    public sealed class TerminalEventTests
    {
        [Fact]
        public void RuntimeTerminalEvidenceCoversEveryFrozenWireVariant()
        {
            var resultPush = NnrpRuntimeEvent.Decode(
                new RuntimeFrameHeader(MessageType.ResultPush),
                ResultPushPayload(ResultStatusCode.Failed));
            var resultDrop = NnrpRuntimeEvent.Decode(
                new RuntimeFrameHeader(MessageType.ResultDrop),
                Array.Empty<byte>());
            var dropMetadata = new ResultDropReasonMetadata(
                91,
                2,
                NnrpResultDropReasonCode.Backpressure,
                RuntimeRole.Server,
                0,
                2);
            var resultDropReason = NnrpRuntimeEvent.Decode(
                new RuntimeFrameHeader(MessageType.ResultDropReason),
                NnrpRuntimeControl.Encode(
                    MessageType.ResultDropReason,
                    dropMetadata,
                    new byte[] { 1, 2 }));

            AssertRuntimeTerminal(resultPush, NnrpResultTerminalState.Success, 700);
            AssertRuntimeTerminal(resultDrop, NnrpResultTerminalState.Dropped, 701);
            AssertRuntimeTerminal(resultDropReason, NnrpResultTerminalState.Dropped, 91);
        }

        [Fact]
        public void RuntimeTerminalEvidenceRejectsMissingOrNonTerminalEvents()
        {
            Assert.Throws<ArgumentNullException>(() => NnrpTerminalEvent.FromRuntime(null!));
            var progress = NnrpRuntimeEvent.Decode(
                new RuntimeFrameHeader(MessageType.Progress),
                NnrpRuntimeControl.Encode(
                    MessageType.Progress,
                    new ProgressMetadata(1, 2, 3, 4, 0, 0)));

            Assert.Throws<ArgumentException>(() => NnrpTerminalEvent.FromRuntime(progress));
        }

        [Theory]
        [InlineData(NnrpOperationState.Completed, NnrpResultTerminalState.Success)]
        [InlineData(NnrpOperationState.Cancelled, NnrpResultTerminalState.Cancelled)]
        [InlineData(NnrpOperationState.Superseded, NnrpResultTerminalState.Dropped)]
        [InlineData(NnrpOperationState.Failed, NnrpResultTerminalState.Error)]
        public void LifecycleTerminalEvidenceCoversEveryFrozenMapping(
            NnrpOperationState operationState,
            NnrpResultTerminalState terminalState)
        {
            var lifecycle = new NnrpOperationLifecycleEvent(92, operationState);
            var terminal = NnrpTerminalEvent.FromLifecycle(lifecycle);

            Assert.Equal(NnrpTerminalEventKind.Lifecycle, terminal.Kind);
            Assert.Equal(terminalState, terminal.ExpectedTerminalState);
            Assert.Same(
                lifecycle,
                terminal.Match(
                    _ => throw new InvalidOperationException("Expected lifecycle evidence."),
                    value => value));
            terminal.ValidateResult(92, terminalState);
        }

        [Fact]
        public void LifecycleTerminalEvidenceRejectsInvalidIdentityStateAndHandlers()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new NnrpOperationLifecycleEvent(0, NnrpOperationState.Completed));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new NnrpOperationLifecycleEvent(1, (NnrpOperationState)byte.MaxValue));
            Assert.Throws<ArgumentNullException>(() => NnrpTerminalEvent.FromLifecycle(null!));
            Assert.Throws<ArgumentException>(() =>
                NnrpTerminalEvent.FromLifecycle(
                    new NnrpOperationLifecycleEvent(1, NnrpOperationState.Running)));

            var terminal = NnrpTerminalEvent.FromLifecycle(
                new NnrpOperationLifecycleEvent(1, NnrpOperationState.Completed));
            Func<NnrpRuntimeEvent, int> runtime = _ => 1;
            Func<NnrpOperationLifecycleEvent, int> lifecycle = _ => 2;
            Assert.Throws<ArgumentNullException>(() => terminal.Match(null!, lifecycle));
            Assert.Throws<ArgumentNullException>(() => terminal.Match(runtime, null!));
            Assert.Throws<ArgumentException>(() =>
                terminal.ValidateResult(1, NnrpResultTerminalState.Error));
            Assert.Throws<ArgumentException>(() =>
                terminal.ValidateResult(2, NnrpResultTerminalState.Success));
        }

        private static void AssertRuntimeTerminal(
            NnrpRuntimeEvent runtimeEvent,
            NnrpResultTerminalState terminalState,
            ulong operationId)
        {
            var terminal = NnrpTerminalEvent.FromRuntime(runtimeEvent);

            Assert.Equal(NnrpTerminalEventKind.Runtime, terminal.Kind);
            Assert.Equal(terminalState, terminal.ExpectedTerminalState);
            Assert.Same(
                runtimeEvent,
                terminal.Match(
                    value => value,
                    _ => throw new InvalidOperationException("Expected runtime evidence.")));
            terminal.ValidateResult(operationId, terminalState);
        }

        private static byte[] ResultPushPayload(ResultStatusCode statusCode)
        {
            return new ResultPushMetadata(
                statusCode,
                ResultFlags.None,
                0,
                PayloadKind.None,
                0,
                1,
                2,
                3,
                0,
                0,
                0,
                0).ToArray();
        }
    }
}
