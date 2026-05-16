using Nnrp.Core;
using Xunit;

namespace Nnrp.Core.Tests
{
    public sealed class FrameLifecycleTests
    {
        [Fact]
        public void FrameLifecycleTracksSubmitToDeliverFlow()
        {
            var lifecycle = new NnrpFrameLifecycle();

            Assert.True(lifecycle.TryAnnounce(10, 0, out var announceFailure));
            Assert.Equal(NnrpProtocolFailure.None, announceFailure);
            Assert.True(lifecycle.TryGetState(10, 0, out var announcedState));
            Assert.Equal(NnrpFrameState.Announced, announcedState);

            Assert.True(lifecycle.TrySubmit(10, 0, retryOfFrame: 0, out var submitFailure));
            Assert.Equal(NnrpProtocolFailure.None, submitFailure);
            Assert.True(lifecycle.TryStartProcessing(10, 0, out var processingFailure));
            Assert.Equal(NnrpProtocolFailure.None, processingFailure);
            Assert.True(lifecycle.TryMarkReady(10, 0, out var readyFailure));
            Assert.Equal(NnrpProtocolFailure.None, readyFailure);
            Assert.True(lifecycle.TryDeliver(10, 0, out var deliverFailure));
            Assert.Equal(NnrpProtocolFailure.None, deliverFailure);

            Assert.True(lifecycle.TryGetState(10, 0, out var deliveredState));
            Assert.Equal(NnrpFrameState.Delivered, deliveredState);
            Assert.False(lifecycle.TryDeliver(10, 0, out var duplicateDeliverFailure));
            Assert.Equal(ErrorCode.InvalidState, duplicateDeliverFailure.ErrorCode);
            Assert.Equal(NnrpErrorScope.Frame, duplicateDeliverFailure.Scope);
        }

        [Fact]
        public void FrameLifecycleSupportsCancelDropAndExpiryTerminals()
        {
            var lifecycle = new NnrpFrameLifecycle();

            Assert.True(lifecycle.TrySubmit(1, 0, retryOfFrame: 0, out _));
            Assert.True(lifecycle.TryCancel(1, 0, out var cancelFailure));
            Assert.Equal(NnrpProtocolFailure.None, cancelFailure);
            Assert.True(lifecycle.TryGetState(1, 0, out var cancelledState));
            Assert.Equal(NnrpFrameState.Cancelled, cancelledState);
            Assert.False(lifecycle.TryDrop(1, 0, out var dropCancelledFailure));
            Assert.Equal(ErrorCode.InvalidState, dropCancelledFailure.ErrorCode);

            Assert.True(lifecycle.TrySubmit(2, 0, retryOfFrame: 0, out _));
            Assert.True(lifecycle.TryStartProcessing(2, 0, out _));
            Assert.True(lifecycle.TryMarkReady(2, 0, out _));
            Assert.True(lifecycle.TryDrop(2, 0, out var dropFailure));
            Assert.Equal(NnrpProtocolFailure.None, dropFailure);
            Assert.True(lifecycle.TryGetState(2, 0, out var droppedState));
            Assert.Equal(NnrpFrameState.Dropped, droppedState);

            Assert.True(lifecycle.TrySubmit(3, 0, retryOfFrame: 0, out _));
            Assert.True(lifecycle.TryExpire(3, 0, out var expireFailure));
            Assert.Equal(NnrpProtocolFailure.None, expireFailure);
            Assert.True(lifecycle.TryGetState(3, 0, out var expiredState));
            Assert.Equal(NnrpFrameState.Expired, expiredState);
        }

        [Fact]
        public void FrameLifecycleValidatesRetransmitsAndKeepsViewsIndependent()
        {
            var lifecycle = new NnrpFrameLifecycle();

            Assert.True(lifecycle.TrySubmit(100, 0, retryOfFrame: 0, out _));
            Assert.True(lifecycle.TryDrop(100, 0, out _));
            Assert.True(lifecycle.TrySubmit(101, 0, retryOfFrame: 100, out var retryFailure));
            Assert.Equal(NnrpProtocolFailure.None, retryFailure);
            Assert.True(lifecycle.TryGetState(101, 0, out var retryState));
            Assert.Equal(NnrpFrameState.Submitted, retryState);

            Assert.False(lifecycle.TrySubmit(101, 1, retryOfFrame: 100, out var missingViewFailure));
            Assert.Equal(ErrorCode.InvalidState, missingViewFailure.ErrorCode);

            Assert.True(lifecycle.TrySubmit(100, 1, retryOfFrame: 0, out _));
            Assert.True(lifecycle.TrySubmit(101, 1, retryOfFrame: 100, out var viewRetryFailure));
            Assert.Equal(NnrpProtocolFailure.None, viewRetryFailure);
            Assert.True(lifecycle.TryGetState(101, 1, out var viewRetryState));
            Assert.Equal(NnrpFrameState.Submitted, viewRetryState);
            Assert.Equal(4, lifecycle.Count);
        }

        [Fact]
        public void FrameLifecycleRejectsUnknownAndInvalidStateChanges()
        {
            var lifecycle = new NnrpFrameLifecycle();

            Assert.False(lifecycle.TryStartProcessing(9, 0, out var missingProcessFailure));
            Assert.Equal(ErrorCode.InvalidState, missingProcessFailure.ErrorCode);
            Assert.False(lifecycle.TryMarkReady(9, 0, out var missingReadyFailure));
            Assert.Equal(ErrorCode.InvalidState, missingReadyFailure.ErrorCode);
            Assert.False(lifecycle.TryCancel(9, 0, out var missingCancelFailure));
            Assert.Equal(ErrorCode.InvalidState, missingCancelFailure.ErrorCode);
            Assert.False(lifecycle.TryExpire(9, 0, out var missingExpireFailure));
            Assert.Equal(ErrorCode.InvalidState, missingExpireFailure.ErrorCode);

            Assert.True(lifecycle.TrySubmit(9, 0, retryOfFrame: 0, out _));
            Assert.False(lifecycle.TryAnnounce(9, 0, out var duplicateAnnounceFailure));
            Assert.Equal(ErrorCode.InvalidState, duplicateAnnounceFailure.ErrorCode);
            Assert.False(lifecycle.TrySubmit(9, 0, retryOfFrame: 0, out var duplicateSubmitFailure));
            Assert.Equal(ErrorCode.InvalidState, duplicateSubmitFailure.ErrorCode);
            Assert.False(lifecycle.TrySubmit(10, 0, retryOfFrame: 10, out var selfRetryFailure));
            Assert.Equal(ErrorCode.InvalidState, selfRetryFailure.ErrorCode);
        }
    }
}
