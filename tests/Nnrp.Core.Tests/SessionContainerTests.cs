using System.Linq;
using Xunit;

namespace Nnrp.Core.Tests
{
    public sealed class SessionContainerTests
    {
        [Fact]
        public void OpenSessionRegistersIndependentActiveSessions()
        {
            var container = new NnrpSessionContainer();

            Assert.True(container.TryOpenSession(41, out var firstFailure));
            Assert.Equal(NnrpProtocolFailure.None, firstFailure);
            Assert.True(container.TryOpenSession(42, out var secondFailure));
            Assert.Equal(NnrpProtocolFailure.None, secondFailure);

            Assert.Equal(2, container.SessionCount);
            Assert.Equal(new uint[] { 41, 42 }, container.SessionIds.OrderBy(id => id).ToArray());
            Assert.True(container.TryAcceptFrameSubmit(41, out var firstSubmitFailure));
            Assert.Equal(NnrpProtocolFailure.None, firstSubmitFailure);
            Assert.True(container.TryAcceptFrameSubmit(42, out var secondSubmitFailure));
            Assert.Equal(NnrpProtocolFailure.None, secondSubmitFailure);
        }

        [Fact]
        public void CloseSessionRejectsOnlyThatSession()
        {
            var container = new NnrpSessionContainer();
            Assert.True(container.TryOpenSession(41, out _));
            Assert.True(container.TryOpenSession(42, out _));

            Assert.True(container.TryCloseSession(41, out var closeFailure));
            Assert.Equal(NnrpProtocolFailure.None, closeFailure);

            Assert.True(container.TryGetSessionState(41, out var closedState));
            Assert.Equal(NnrpSessionState.Closed, closedState);
            Assert.False(container.TryAcceptFrameSubmit(41, out var closedSubmitFailure));
            Assert.Equal(ErrorCode.InvalidState, closedSubmitFailure.ErrorCode);
            Assert.True(container.TryAcceptFrameSubmit(42, out var siblingFailure));
            Assert.Equal(NnrpProtocolFailure.None, siblingFailure);
            Assert.True(container.TryGetSessionState(42, out var siblingState));
            Assert.Equal(NnrpSessionState.Active, siblingState);
        }

        [Fact]
        public void CloseConnectionCascadesRemainingSessions()
        {
            var container = new NnrpSessionContainer();
            Assert.True(container.TryOpenSession(42, out _));
            Assert.True(container.TryOpenSession(43, out _));

            var closed = container.CloseConnection();

            Assert.True(container.IsConnectionClosed);
            Assert.Equal(new uint[] { 42, 43 }, closed.ToArray());
            Assert.True(container.TryGetSessionState(42, out var firstState));
            Assert.Equal(NnrpSessionState.Closed, firstState);
            Assert.True(container.TryGetSessionState(43, out var secondState));
            Assert.Equal(NnrpSessionState.Closed, secondState);
            Assert.False(container.TryAcceptFrameSubmit(42, out var submitFailure));
            Assert.Equal(NnrpErrorScope.Connection, submitFailure.Scope);
            Assert.True(submitFailure.IsFatal);
            Assert.Empty(container.CloseConnection());
        }

        [Fact]
        public void OpenSessionRejectsInvalidDuplicateAndClosedConnectionRoutes()
        {
            var container = new NnrpSessionContainer();

            Assert.False(container.TryOpenSession(0, out var zeroFailure));
            Assert.Equal(ErrorCode.InvalidState, zeroFailure.ErrorCode);
            Assert.True(container.TryOpenSession(42, out _));
            Assert.False(container.TryOpenSession(42, out var duplicateFailure));
            Assert.Equal(NnrpErrorScope.Session, duplicateFailure.Scope);
            Assert.False(container.TryAcceptFrameSubmit(99, out var missingFailure));
            Assert.Equal(NnrpErrorScope.Session, missingFailure.Scope);
            Assert.False(container.TryGetSessionState(99, out _));

            container.CloseConnection();
            Assert.False(container.TryOpenSession(43, out var closedFailure));
            Assert.Equal(NnrpErrorScope.Connection, closedFailure.Scope);
            Assert.True(closedFailure.IsFatal);
        }

        [Fact]
        public void CloseSessionRejectsMissingAndRepeatedClose()
        {
            var container = new NnrpSessionContainer();

            Assert.False(container.TryCloseSession(42, out var missingFailure));
            Assert.Equal(NnrpErrorScope.Session, missingFailure.Scope);

            Assert.True(container.TryOpenSession(42, out _));
            Assert.True(container.TryCloseSession(42, out _));
            Assert.False(container.TryCloseSession(42, out var repeatedFailure));
            Assert.Equal(ErrorCode.InvalidState, repeatedFailure.ErrorCode);
        }
    }
}
