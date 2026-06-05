using System;
using Nnrp.Core;
using Xunit;

namespace Nnrp.Core.Tests
{
    public sealed class FlowCreditUpdateTests
    {
        [Theory]
        [InlineData(FlowUpdateScopeKind.Connection, 0u, 0ul, 6)]
        [InlineData(FlowUpdateScopeKind.Session, 41u, 0ul, 4)]
        [InlineData(FlowUpdateScopeKind.Operation, 41u, 99ul, 2)]
        public void FromMetadataSelectsScopeCreditAndTarget(
            FlowUpdateScopeKind scopeKind,
            uint sessionId,
            ulong operationId,
            ushort expectedCredit)
        {
            var metadata = CreateMetadata(scopeKind, operationId);
            var update = FlowCreditUpdate.FromMetadata(sessionId, metadata);

            Assert.Equal(scopeKind, update.ScopeKind);
            Assert.Equal(sessionId, update.SessionId);
            Assert.Equal(operationId, update.OperationId);
            Assert.Equal(expectedCredit, update.Credit);
            Assert.Equal(FlowUpdateReason.Congestion, update.UpdateReason);
            Assert.Equal(FlowUpdateBackpressureLevel.Hard, update.BackpressureLevel);
            Assert.Equal(125u, update.RetryAfterMilliseconds);
            Assert.Equal(8u, update.CreditEpoch);
            Assert.True(update.HasCredit);
            Assert.True(update.HasRetryAfter);
            Assert.True(update.IsBackgroundOnly);
            Assert.True(update.IsDrainInFlightOnly);
        }

        [Fact]
        public void FromMessageUsesHeaderSessionAndMetadata()
        {
            var metadata = CreateMetadata(FlowUpdateScopeKind.Session, operationId: 0);
            var message = new FlowUpdateMessage(
                CreateHeader(sessionId: 41),
                metadata);

            Assert.Equal(FlowCreditUpdate.FromMetadata(41, metadata), message.CreditUpdate);
            Assert.Equal(message.CreditUpdate.GetHashCode(), FlowCreditUpdate.FromMessage(message).GetHashCode());
            Assert.True(message.CreditUpdate.Equals((object)FlowCreditUpdate.FromMessage(message)));
        }

        [Fact]
        public void ConstructorRejectsInvalidTargets()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new FlowCreditUpdate(
                (FlowUpdateScopeKind)99,
                sessionId: 0,
                operationId: 0,
                credit: 0,
                updateReason: FlowUpdateReason.Grant,
                backpressureLevel: FlowUpdateBackpressureLevel.None,
                retryAfterMilliseconds: 0,
                creditEpoch: 0,
                flags: FlowUpdateFlags.None));

            Assert.Throws<ArgumentException>(() => new FlowCreditUpdate(
                FlowUpdateScopeKind.Connection,
                sessionId: 41,
                operationId: 0,
                credit: 0,
                updateReason: FlowUpdateReason.Grant,
                backpressureLevel: FlowUpdateBackpressureLevel.None,
                retryAfterMilliseconds: 0,
                creditEpoch: 0,
                flags: FlowUpdateFlags.None));

            Assert.Throws<ArgumentException>(() => new FlowCreditUpdate(
                FlowUpdateScopeKind.Session,
                sessionId: 0,
                operationId: 0,
                credit: 0,
                updateReason: FlowUpdateReason.Grant,
                backpressureLevel: FlowUpdateBackpressureLevel.None,
                retryAfterMilliseconds: 0,
                creditEpoch: 0,
                flags: FlowUpdateFlags.None));

            Assert.Throws<ArgumentException>(() => new FlowCreditUpdate(
                FlowUpdateScopeKind.Session,
                sessionId: 41,
                operationId: 99,
                credit: 0,
                updateReason: FlowUpdateReason.Grant,
                backpressureLevel: FlowUpdateBackpressureLevel.None,
                retryAfterMilliseconds: 0,
                creditEpoch: 0,
                flags: FlowUpdateFlags.None));

            Assert.Throws<ArgumentException>(() => new FlowCreditUpdate(
                FlowUpdateScopeKind.Operation,
                sessionId: 41,
                operationId: 0,
                credit: 0,
                updateReason: FlowUpdateReason.Grant,
                backpressureLevel: FlowUpdateBackpressureLevel.None,
                retryAfterMilliseconds: 0,
                creditEpoch: 0,
                flags: FlowUpdateFlags.None));
        }

        [Fact]
        public void ConstructorRejectsInvalidReasonAndBackpressure()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new FlowCreditUpdate(
                FlowUpdateScopeKind.Connection,
                sessionId: 0,
                operationId: 0,
                credit: 0,
                updateReason: (FlowUpdateReason)99,
                backpressureLevel: FlowUpdateBackpressureLevel.None,
                retryAfterMilliseconds: 0,
                creditEpoch: 0,
                flags: FlowUpdateFlags.None));

            Assert.Throws<ArgumentOutOfRangeException>(() => new FlowCreditUpdate(
                FlowUpdateScopeKind.Connection,
                sessionId: 0,
                operationId: 0,
                credit: 0,
                updateReason: FlowUpdateReason.Grant,
                backpressureLevel: (FlowUpdateBackpressureLevel)99,
                retryAfterMilliseconds: 0,
                creditEpoch: 0,
                flags: FlowUpdateFlags.None));

            Assert.Throws<ArgumentOutOfRangeException>(() => new FlowCreditUpdate(
                FlowUpdateScopeKind.Connection,
                sessionId: 0,
                operationId: 0,
                credit: 0,
                updateReason: FlowUpdateReason.Grant,
                backpressureLevel: FlowUpdateBackpressureLevel.None,
                retryAfterMilliseconds: 0,
                creditEpoch: 0,
                flags: (FlowUpdateFlags)0x100));

            Assert.Throws<ArgumentException>(() => new FlowCreditUpdate(
                FlowUpdateScopeKind.Connection,
                sessionId: 0,
                operationId: 0,
                credit: 0,
                updateReason: FlowUpdateReason.Grant,
                backpressureLevel: FlowUpdateBackpressureLevel.None,
                retryAfterMilliseconds: 10,
                creditEpoch: 0,
                flags: FlowUpdateFlags.None));
        }

        [Fact]
        public void EqualityCoversAllFields()
        {
            var first = new FlowCreditUpdate(
                FlowUpdateScopeKind.Operation,
                sessionId: 41,
                operationId: 99,
                credit: 2,
                updateReason: FlowUpdateReason.Reduce,
                backpressureLevel: FlowUpdateBackpressureLevel.Soft,
                retryAfterMilliseconds: 50,
                creditEpoch: 3,
                flags: FlowUpdateFlags.CreditValid | FlowUpdateFlags.RetryAfterValid);
            var same = new FlowCreditUpdate(
                FlowUpdateScopeKind.Operation,
                sessionId: 41,
                operationId: 99,
                credit: 2,
                updateReason: FlowUpdateReason.Reduce,
                backpressureLevel: FlowUpdateBackpressureLevel.Soft,
                retryAfterMilliseconds: 50,
                creditEpoch: 3,
                flags: FlowUpdateFlags.CreditValid | FlowUpdateFlags.RetryAfterValid);

            Assert.Equal(first, same);
            Assert.NotEqual(first, new FlowCreditUpdate(FlowUpdateScopeKind.Operation, 41, 99, 3, FlowUpdateReason.Reduce, FlowUpdateBackpressureLevel.Soft, 50, 3, FlowUpdateFlags.CreditValid | FlowUpdateFlags.RetryAfterValid));
            Assert.NotEqual(first, new FlowCreditUpdate(FlowUpdateScopeKind.Session, 41, 0, 2, FlowUpdateReason.Reduce, FlowUpdateBackpressureLevel.Soft, 50, 3, FlowUpdateFlags.CreditValid | FlowUpdateFlags.RetryAfterValid));
            Assert.False(first.Equals("not-credit-update"));
        }

        [Fact]
        public void FlagHelpersReflectAbsentFields()
        {
            var update = new FlowCreditUpdate(
                FlowUpdateScopeKind.Connection,
                sessionId: 0,
                operationId: 0,
                credit: 0,
                updateReason: FlowUpdateReason.Pause,
                backpressureLevel: FlowUpdateBackpressureLevel.Hard,
                retryAfterMilliseconds: 0,
                creditEpoch: 4,
                flags: FlowUpdateFlags.None);

            Assert.False(update.HasCredit);
            Assert.False(update.HasRetryAfter);
            Assert.False(update.IsBackgroundOnly);
            Assert.False(update.IsDrainInFlightOnly);
        }

        private static FlowUpdateMetadata CreateMetadata(FlowUpdateScopeKind scopeKind, ulong operationId)
        {
            return new FlowUpdateMetadata(
                scopeKind,
                FlowUpdateReason.Congestion,
                FlowUpdateBackpressureLevel.Hard,
                connectionCredit: scopeKind == FlowUpdateScopeKind.Connection ? (ushort)6 : (ushort)0,
                sessionCredit: scopeKind == FlowUpdateScopeKind.Session ? (ushort)4 : (ushort)0,
                operationCredit: scopeKind == FlowUpdateScopeKind.Operation ? (ushort)2 : (ushort)0,
                operationId,
                retryAfterMilliseconds: 125,
                creditEpoch: 8,
                flags: FlowUpdateFlags.CreditValid
                    | FlowUpdateFlags.RetryAfterValid
                    | FlowUpdateFlags.BackgroundOnly
                    | FlowUpdateFlags.DrainInFlightOnly);
        }

        private static NnrpHeader CreateHeader(uint sessionId)
        {
            return new NnrpHeader(
                versionMajor: NnrpHeader.CurrentVersionMajor,
                messageType: MessageType.FlowUpdate,
                flags: HeaderFlags.None,
                metaLength: FlowUpdateMetadata.MetadataLength,
                bodyLength: 0,
                sessionId: sessionId,
                frameId: 0,
                viewId: 0,
                routeId: 1,
                traceId: 2,
                wireFormat: NnrpHeader.CurrentWireFormat);
        }
    }
}
