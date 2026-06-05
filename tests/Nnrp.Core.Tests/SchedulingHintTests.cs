using System;
using Nnrp.Core;
using Xunit;

namespace Nnrp.Core.Tests
{
    public sealed class SchedulingHintTests
    {
        [Fact]
        public void OperationSchedulingHintValidatesOperationAndPriority()
        {
            var hint = new OperationSchedulingHint(
                operationId: 99,
                priorityClass: SessionPriorityClass.Interactive,
                deadlineWindowMilliseconds: 16);
            var same = new OperationSchedulingHint(99, SessionPriorityClass.Interactive, 16);

            Assert.Equal((ulong)99, hint.OperationId);
            Assert.Equal(SessionPriorityClass.Interactive, hint.PriorityClass);
            Assert.Equal(16u, hint.DeadlineWindowMilliseconds);
            Assert.Equal(hint, same);
            Assert.Equal(hint.GetHashCode(), same.GetHashCode());
            Assert.True(hint.Equals((object)same));
            Assert.NotEqual(hint, new OperationSchedulingHint(100, SessionPriorityClass.Interactive, 16));
            Assert.Equal(0u, new OperationSchedulingHint(1, SessionPriorityClass.Balanced).DeadlineWindowMilliseconds);
            Assert.Throws<ArgumentOutOfRangeException>(() => new OperationSchedulingHint(0, SessionPriorityClass.Balanced));
            Assert.Throws<ArgumentOutOfRangeException>(() => new OperationSchedulingHint(99, (SessionPriorityClass)0xFF));
        }

        [Fact]
        public void SessionSchedulingOptionsBuildSessionOpenMetadata()
        {
            var options = new SessionSchedulingOptions(
                SessionPriorityClass.Background,
                defaultDeadlineMilliseconds: 250,
                maxInFlightOperations: 7);

            var metadata = options.CreateSessionOpenMetadata(
                requestedSessionId: 41,
                profileId: 2,
                sessionFlags: SessionFlags.AllowBackgroundResults,
                schemaId: 4097,
                schemaVersion: 3,
                leaseTtlHintMilliseconds: 1000,
                clientSessionTag: 1234);

            Assert.Equal(SessionPriorityClass.Background, metadata.PriorityClass);
            Assert.Equal(250u, metadata.DefaultDeadlineMilliseconds);
            Assert.Equal(7, metadata.MaxInFlightOperations);
            Assert.Equal(SessionFlags.AllowBackgroundResults, metadata.SessionFlags);
            Assert.Equal(4097u, metadata.SchemaId);
            Assert.Equal(3u, metadata.SchemaVersion);
            Assert.Equal(1000u, metadata.LeaseTtlHintMilliseconds);
            Assert.Equal((ulong)1234, metadata.ClientSessionTag);
            Assert.Equal(new SessionSchedulingOptions(SessionPriorityClass.Background, 250, 7), options);
            Assert.Equal(options.GetHashCode(), new SessionSchedulingOptions(SessionPriorityClass.Background, 250, 7).GetHashCode());
            Assert.True(options.Equals((object)new SessionSchedulingOptions(SessionPriorityClass.Background, 250, 7)));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SessionSchedulingOptions((SessionPriorityClass)0xFF));
        }

        [Fact]
        public void SessionSchedulingOptionsDefaultMatchesDocumentedBaseline()
        {
            Assert.Equal(SessionPriorityClass.Balanced, SessionSchedulingOptions.Default.PriorityClass);
            Assert.Equal(500u, SessionSchedulingOptions.Default.DefaultDeadlineMilliseconds);
            Assert.Equal(4, SessionSchedulingOptions.Default.MaxInFlightOperations);
        }
    }
}
