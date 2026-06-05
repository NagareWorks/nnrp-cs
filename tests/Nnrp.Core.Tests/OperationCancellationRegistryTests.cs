using System.Linq;
using Xunit;

namespace Nnrp.Core.Tests
{
    public sealed class OperationCancellationRegistryTests
    {
        [Fact]
        public void OperationScopeCancelsOnlyTargetOperation()
        {
            var registry = CreateRegistry();

            Assert.True(registry.TryCancel(
                NnrpOperationCancelScope.Operation,
                operationId: 10,
                out var cancelled,
                out var failure));

            Assert.Equal(NnrpProtocolFailure.None, failure);
            Assert.Equal(new uint[] { 10 }, cancelled.ToArray());
            AssertState(registry, 10, NnrpOperationState.Cancelled);
            AssertState(registry, 11, NnrpOperationState.Running);
            AssertState(registry, 20, NnrpOperationState.Running);
        }

        [Fact]
        public void SubtreeScopeCancelsTargetAndDescendants()
        {
            var registry = CreateRegistry();

            Assert.True(registry.TryCancel(
                NnrpOperationCancelScope.Subtree,
                operationId: 10,
                out var cancelled,
                out var failure));

            Assert.Equal(NnrpProtocolFailure.None, failure);
            Assert.Equal(new uint[] { 10, 11, 12 }, cancelled.ToArray());
            AssertState(registry, 10, NnrpOperationState.Cancelled);
            AssertState(registry, 11, NnrpOperationState.Cancelled);
            AssertState(registry, 12, NnrpOperationState.Cancelled);
            AssertState(registry, 20, NnrpOperationState.Running);
        }

        [Fact]
        public void GroupScopeCancelsMatchingOperationGroup()
        {
            var registry = CreateRegistry();

            Assert.True(registry.TryCancel(
                NnrpOperationCancelScope.Group,
                operationId: 10,
                out var cancelled,
                out var failure));

            Assert.Equal(NnrpProtocolFailure.None, failure);
            Assert.Equal(new uint[] { 10, 11, 12, 30 }, cancelled.ToArray());
            AssertState(registry, 20, NnrpOperationState.Running);
        }

        [Fact]
        public void GroupScopeRejectsUngroupedTargetAndLeavesUngroupedOperationsRunning()
        {
            var registry = new NnrpOperationCancellationRegistry();
            Register(registry, operationId: 10, parentOperationId: 0, operationGroupId: 0);
            Register(registry, operationId: 11, parentOperationId: 0, operationGroupId: 0);
            Register(registry, operationId: 20, parentOperationId: 0, operationGroupId: 7);

            Assert.False(registry.TryCancel(
                NnrpOperationCancelScope.Group,
                operationId: 10,
                out var cancelled,
                out var failure));

            Assert.Equal(ErrorCode.InvalidState, failure.ErrorCode);
            Assert.Empty(cancelled);
            AssertState(registry, 10, NnrpOperationState.Running);
            AssertState(registry, 11, NnrpOperationState.Running);
            AssertState(registry, 20, NnrpOperationState.Running);
        }

        [Fact]
        public void SessionScopeCancelsAllNonTerminalOperations()
        {
            var registry = CreateRegistry();
            Assert.True(registry.TryCancel(NnrpOperationCancelScope.Operation, 30, out _, out _));

            Assert.True(registry.TryCancel(
                NnrpOperationCancelScope.Session,
                operationId: 10,
                out var cancelled,
                out var failure));

            Assert.Equal(NnrpProtocolFailure.None, failure);
            Assert.Equal(new uint[] { 10, 11, 12, 20 }, cancelled.ToArray());
            AssertState(registry, 30, NnrpOperationState.Cancelled);
        }

        [Fact]
        public void RegistryRejectsInvalidRegistrationAndCancelRequests()
        {
            var registry = new NnrpOperationCancellationRegistry();
            var operation = CreateRunningOperation(10);

            Assert.Throws<System.ArgumentNullException>(() => registry.TryRegister(null!, 0, 0, out _));
            Assert.False(registry.TryRegister(operation, parentOperationId: 99, operationGroupId: 1, out var missingParentFailure));
            Assert.Equal(ErrorCode.InvalidState, missingParentFailure.ErrorCode);
            Assert.True(registry.TryRegister(operation, parentOperationId: 0, operationGroupId: 1, out _));
            Assert.False(registry.TryRegister(operation, parentOperationId: 0, operationGroupId: 1, out var duplicateFailure));
            Assert.Equal(ErrorCode.InvalidState, duplicateFailure.ErrorCode);
            Assert.False(registry.TryCancel(NnrpOperationCancelScope.Operation, 99, out _, out var missingCancelFailure));
            Assert.Equal(ErrorCode.InvalidState, missingCancelFailure.ErrorCode);
            Assert.False(registry.TryCancel((NnrpOperationCancelScope)99, 10, out _, out var invalidScopeFailure));
            Assert.Equal(ErrorCode.InvalidState, invalidScopeFailure.ErrorCode);
            Assert.False(registry.TryGetState(99, out _));
        }

        private static NnrpOperationCancellationRegistry CreateRegistry()
        {
            var registry = new NnrpOperationCancellationRegistry();
            Register(registry, operationId: 10, parentOperationId: 0, operationGroupId: 7);
            Register(registry, operationId: 11, parentOperationId: 10, operationGroupId: 7);
            Register(registry, operationId: 12, parentOperationId: 11, operationGroupId: 7);
            Register(registry, operationId: 20, parentOperationId: 0, operationGroupId: 8);
            Register(registry, operationId: 30, parentOperationId: 0, operationGroupId: 7);
            Assert.Equal(5, registry.Count);
            return registry;
        }

        private static void Register(
            NnrpOperationCancellationRegistry registry,
            uint operationId,
            uint parentOperationId,
            uint operationGroupId)
        {
            Assert.True(registry.TryRegister(
                CreateRunningOperation(operationId),
                parentOperationId,
                operationGroupId,
                out var failure));
            Assert.Equal(NnrpProtocolFailure.None, failure);
        }

        private static NnrpOperationLifecycle CreateRunningOperation(uint operationId)
        {
            var operation = new NnrpOperationLifecycle(operationId);
            Assert.True(operation.TryStart(out _));
            return operation;
        }

        private static void AssertState(
            NnrpOperationCancellationRegistry registry,
            uint operationId,
            NnrpOperationState expectedState)
        {
            Assert.True(registry.TryGetState(operationId, out var state));
            Assert.Equal(expectedState, state);
        }
    }
}
