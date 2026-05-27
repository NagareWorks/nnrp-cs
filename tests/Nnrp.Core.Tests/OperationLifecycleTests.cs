using System;
using Xunit;

namespace Nnrp.Core.Tests
{
    public sealed class OperationLifecycleTests
    {
        [Fact]
        public void OperationLifecycleProgressesThroughPartialToCompleted()
        {
            var lifecycle = new NnrpOperationLifecycle(303);

            Assert.Equal(NnrpOperationState.Accepted, lifecycle.State);
            Assert.False(lifecycle.IsTerminal);
            Assert.True(lifecycle.TryStart(out var startFailure));
            Assert.Equal(NnrpProtocolFailure.None, startFailure);
            Assert.True(lifecycle.TryMarkPartial(out var partialFailure));
            Assert.Equal(NnrpProtocolFailure.None, partialFailure);
            Assert.True(lifecycle.TryComplete(out var completeFailure));
            Assert.Equal(NnrpProtocolFailure.None, completeFailure);
            Assert.True(lifecycle.IsTerminal);
            Assert.Equal(NnrpOperationState.Completed, lifecycle.State);
        }

        [Fact]
        public void OperationLifecycleAllowsWaitingToolResumeAndTerminalOutcomes()
        {
            var lifecycle = new NnrpOperationLifecycle(303);

            Assert.True(lifecycle.TryStart(out _));
            Assert.True(lifecycle.TryWaitForTool(out var waitFailure));
            Assert.Equal(NnrpProtocolFailure.None, waitFailure);
            Assert.Equal(NnrpOperationState.WaitingTool, lifecycle.State);
            Assert.True(lifecycle.TryResumeFromTool(out var resumeFailure));
            Assert.Equal(NnrpProtocolFailure.None, resumeFailure);
            Assert.Equal(NnrpOperationState.Running, lifecycle.State);
            Assert.True(lifecycle.TrySupersede(out var supersedeFailure));
            Assert.Equal(NnrpProtocolFailure.None, supersedeFailure);
            Assert.True(lifecycle.IsTerminal);
            Assert.True(NnrpOperationLifecycle.IsTerminalState(NnrpOperationState.Superseded));
        }

        [Theory]
        [InlineData(NnrpOperationState.Cancelled)]
        [InlineData(NnrpOperationState.Failed)]
        public void OperationLifecycleKeepsTerminalStatesDistinct(NnrpOperationState terminalState)
        {
            var lifecycle = new NnrpOperationLifecycle(303);
            Assert.True(lifecycle.TryStart(out _));

            NnrpProtocolFailure failure;
            var moved = terminalState == NnrpOperationState.Cancelled
                ? lifecycle.TryCancel(out failure)
                : lifecycle.TryFail(out failure);

            Assert.True(moved);
            Assert.Equal(NnrpProtocolFailure.None, failure);
            Assert.Equal(terminalState, lifecycle.State);
            Assert.False(lifecycle.TryComplete(out var afterTerminalFailure));
            Assert.Equal(ErrorCode.InvalidState, afterTerminalFailure.ErrorCode);
            Assert.Equal(NnrpErrorScope.Frame, afterTerminalFailure.Scope);
        }

        [Fact]
        public void OperationLifecycleRejectsInvalidTransitions()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new NnrpOperationLifecycle(0));
            var lifecycle = new NnrpOperationLifecycle(303);

            Assert.False(lifecycle.TryMarkPartial(out var earlyPartialFailure));
            Assert.Equal(ErrorCode.InvalidState, earlyPartialFailure.ErrorCode);
            Assert.False(lifecycle.TryResumeFromTool(out var earlyResumeFailure));
            Assert.Equal(ErrorCode.InvalidState, earlyResumeFailure.ErrorCode);
            Assert.True(lifecycle.TryStart(out _));
            Assert.False(lifecycle.TryStart(out var duplicateStartFailure));
            Assert.Equal(ErrorCode.InvalidState, duplicateStartFailure.ErrorCode);
        }

        [Fact]
        public void OperationLifecycleAppliesDropRouting()
        {
            var lifecycle = new NnrpOperationLifecycle(303);
            Assert.True(lifecycle.TryStart(out _));
            var wrongDrop = ResultDropMessage.Create(sessionId: 42, frameId: 304);

            Assert.False(lifecycle.TryApplyDrop(wrongDrop, out var wrongDropFailure));
            Assert.Equal(ErrorCode.InvalidState, wrongDropFailure.ErrorCode);

            var drop = ResultDropMessage.Create(sessionId: 42, frameId: 303);
            Assert.True(lifecycle.TryApplyDrop(drop, out var dropFailure));
            Assert.Equal(NnrpProtocolFailure.None, dropFailure);
            Assert.Equal(NnrpOperationState.Superseded, lifecycle.State);
        }

        [Fact]
        public void OperationLifecycleAppliesFirstPartialResultFromAccepted()
        {
            var lifecycle = new NnrpOperationLifecycle(303);
            var result = CreateResultPush(
                frameId: 303,
                resultClass: ResultClass.Partial,
                resultFlags: ResultFlags.Partial,
                coveredTileCount: 1,
                droppedTileCount: 1);

            Assert.True(lifecycle.TryApplyResult(result, out var failure));
            Assert.Equal(NnrpProtocolFailure.None, failure);
            Assert.Equal(NnrpOperationState.Partial, lifecycle.State);
            Assert.False(lifecycle.IsTerminal);
        }

        [Fact]
        public void OperationLifecycleAppliesFirstCompleteResultFromAccepted()
        {
            var lifecycle = new NnrpOperationLifecycle(303);
            var result = CreateResultPush(
                frameId: 303,
                resultClass: ResultClass.Complete,
                resultFlags: ResultFlags.None,
                coveredTileCount: 2,
                droppedTileCount: 0);

            Assert.True(lifecycle.TryApplyResult(result, out var failure));
            Assert.Equal(NnrpProtocolFailure.None, failure);
            Assert.Equal(NnrpOperationState.Completed, lifecycle.State);
            Assert.True(lifecycle.IsTerminal);
        }

        private static ResultPushMessage CreateResultPush(
            uint frameId,
            ResultClass resultClass,
            ResultFlags resultFlags,
            ushort coveredTileCount,
            ushort droppedTileCount)
        {
            var tileIds = new ushort[] { 5, 6 };
            var lengthTable = new byte[] { 1, 0, 0, 0, 1, 0, 0, 0 };
            var payload = new byte[] { 0x11, 0x22 };
            var section = new TensorSectionBlock(
                new TensorSectionDescriptor(
                    role: TensorRole.SrResidual,
                    codec: CodecId.Raw,
                    dtype: DTypeId.UInt8,
                    layout: TensorLayoutId.Nhwc,
                    scalePolicy: ScalePolicy.None,
                    flags: 0,
                    elementCountPerTile: 1,
                    codecTableBytes: 0,
                    lengthTableBytes: (uint)lengthTable.Length,
                    payloadBytes: (uint)payload.Length,
                    payloadStrideBytes: 0),
                Array.Empty<byte>(),
                lengthTable,
                payload);
            var tileIndexBytes = TileIndexBlockCodec.GetEncodedLength(tileIds, TileIndexMode.RawUInt16);
            var metadata = new ResultPushMetadata(
                statusCode: ResultStatusCode.Success,
                resultFlags: resultFlags,
                sectionCount: 1,
                tileCount: (ushort)tileIds.Length,
                activeProfileId: 0,
                inferenceMilliseconds: 1,
                queueMilliseconds: 1,
                serverTotalMilliseconds: 2,
                tileBaseId: 0,
                tileIndexBytes: (uint)tileIndexBytes,
                resultClass: resultClass,
                coveredTileCount: coveredTileCount,
                droppedTileCount: droppedTileCount);

            return new ResultPushMessage(
                new NnrpHeader(
                    versionMajor: NnrpHeader.CurrentVersionMajor,
                    messageType: MessageType.ResultPush,
                    flags: HeaderFlags.None,
                    metaLength: ResultPushMetadata.MetadataLength,
                    bodyLength: 0,
                    sessionId: 42,
                    frameId: frameId,
                    viewId: 0,
                    routeId: 0,
                    traceId: 0),
                metadata,
                tileIds,
                new[] { section });
        }
    }
}
