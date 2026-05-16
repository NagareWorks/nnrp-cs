using Nnrp.Core;
using Xunit;

namespace Nnrp.Core.Tests
{
    public sealed class SessionStateMachineTests
    {
        [Fact]
        public void SessionHandshakeActivatesAndCloseDrainsBeforeClosed()
        {
            var session = new NnrpSessionStateMachine();

            Assert.Equal(NnrpSessionState.Init, session.State);
            Assert.False(session.TryAcceptFrameSubmit(out var earlyFrameFailure));
            Assert.Equal(ErrorCode.InvalidState, earlyFrameFailure.ErrorCode);

            Assert.True(session.TryBeginNegotiation(out var beginFailure));
            Assert.Equal(NnrpProtocolFailure.None, beginFailure);
            Assert.Equal(NnrpSessionState.Negotiating, session.State);

            Assert.True(session.TryActivate(out var activateFailure));
            Assert.Equal(NnrpProtocolFailure.None, activateFailure);
            Assert.Equal(NnrpSessionState.Active, session.State);
            Assert.True(session.TryAcceptFrameSubmit(out var activeFrameFailure));
            Assert.Equal(NnrpProtocolFailure.None, activeFrameFailure);

            Assert.True(session.TryClose(out var closeFailure));
            Assert.Equal(NnrpProtocolFailure.None, closeFailure);
            Assert.Equal(NnrpSessionState.Draining, session.State);
            Assert.False(session.TryAcceptFrameSubmit(out var drainingFrameFailure));
            Assert.Equal(ErrorCode.InvalidState, drainingFrameFailure.ErrorCode);

            Assert.True(session.TryClose(out var drainCompleteFailure));
            Assert.Equal(NnrpProtocolFailure.None, drainCompleteFailure);
            Assert.Equal(NnrpSessionState.Closed, session.State);
        }

        [Fact]
        public void SessionRejectsInvalidTransitions()
        {
            var session = new NnrpSessionStateMachine();

            Assert.False(session.TryActivate(out var activateFailure));
            Assert.Equal(ErrorCode.InvalidState, activateFailure.ErrorCode);
            Assert.Equal(NnrpErrorScope.Session, activateFailure.Scope);
            Assert.False(session.TryBeginDraining(out var drainFailure));
            Assert.Equal(ErrorCode.InvalidState, drainFailure.ErrorCode);

            Assert.True(session.TryBeginNegotiation(out _));
            Assert.False(session.TryBeginNegotiation(out var duplicateFailure));
            Assert.Equal(ErrorCode.InvalidState, duplicateFailure.ErrorCode);

            var negotiationFailure = NnrpProtocolFailure.UnsupportedCapability("No common codec.");
            Assert.True(session.TryFailNegotiation(negotiationFailure, out var failTransitionFailure));
            Assert.Equal(NnrpProtocolFailure.None, failTransitionFailure);
            Assert.Equal(NnrpSessionState.Closed, session.State);
            Assert.Equal(negotiationFailure, session.LastFailure);
            Assert.False(session.TryClose(out var closedFailure));
            Assert.Equal(ErrorCode.InvalidState, closedFailure.ErrorCode);
        }

        [Fact]
        public void FatalFailuresMoveSessionToDrainingOrClosed()
        {
            var activeSession = new NnrpSessionStateMachine();
            Assert.True(activeSession.TryBeginNegotiation(out _));
            Assert.True(activeSession.TryActivate(out _));

            var fatalBodyFailure = new NnrpProtocolFailure(
                ErrorCode.MalformedBody,
                NnrpErrorScope.Frame,
                "Fatal frame body failure.",
                isFatal: true,
                parseError: NnrpParseError.InconsistentSectionDescriptor);
            activeSession.ApplyFailure(fatalBodyFailure);

            Assert.Equal(NnrpSessionState.Draining, activeSession.State);
            Assert.Equal(fatalBodyFailure, activeSession.LastFailure);

            var negotiatingSession = new NnrpSessionStateMachine();
            Assert.True(negotiatingSession.TryBeginNegotiation(out _));
            negotiatingSession.ApplyFailure(NnrpProtocolFailure.FromHeaderParseError(NnrpParseError.InvalidMagic));

            Assert.Equal(NnrpSessionState.Closed, negotiatingSession.State);
        }

        [Fact]
        public void NonFatalFailuresPreserveSessionState()
        {
            var session = new NnrpSessionStateMachine();
            Assert.True(session.TryBeginNegotiation(out _));
            Assert.True(session.TryActivate(out _));

            var nonFatalFailure = NnrpProtocolFailure.FromBodyParseError(NnrpParseError.NonZeroReservedField);
            session.ApplyFailure(nonFatalFailure);

            Assert.Equal(NnrpSessionState.Active, session.State);
            Assert.Equal(nonFatalFailure, session.LastFailure);
        }
    }
}
