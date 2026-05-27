using System;
using System.Collections.Generic;
using System.Linq;

namespace Nnrp.Core
{
    public enum NnrpOperationCancelScope : byte
    {
        Operation = 0,
        Subtree = 1,
        Group = 2,
        Session = 3,
    }

    public sealed class NnrpOperationCancellationRegistry
    {
        private readonly Dictionary<uint, OperationEntry> operations = new Dictionary<uint, OperationEntry>();

        public int Count => operations.Count;

        public bool TryRegister(
            NnrpOperationLifecycle operation,
            uint parentOperationId,
            uint operationGroupId,
            out NnrpProtocolFailure failure)
        {
            if (operation == null)
            {
                throw new ArgumentNullException(nameof(operation));
            }

            if (operations.ContainsKey(operation.OperationId))
            {
                failure = InvalidOperation(operation.OperationId, "register an already tracked operation");
                return false;
            }

            if (parentOperationId != 0 && !operations.ContainsKey(parentOperationId))
            {
                failure = InvalidOperation(operation.OperationId, $"register with missing parent operation {parentOperationId}");
                return false;
            }

            operations.Add(operation.OperationId, new OperationEntry(operation, parentOperationId, operationGroupId));
            failure = NnrpProtocolFailure.None;
            return true;
        }

        public bool TryGetState(uint operationId, out NnrpOperationState state)
        {
            if (operations.TryGetValue(operationId, out var entry))
            {
                state = entry.Operation.State;
                return true;
            }

            state = default;
            return false;
        }

        public bool TryCancel(
            NnrpOperationCancelScope scope,
            uint operationId,
            out IReadOnlyList<uint> cancelledOperationIds,
            out NnrpProtocolFailure failure)
        {
            cancelledOperationIds = Array.Empty<uint>();
            if (!Enum.IsDefined(typeof(NnrpOperationCancelScope), scope))
            {
                failure = InvalidOperation(operationId, "cancel with an unknown scope");
                return false;
            }

            if (!operations.TryGetValue(operationId, out var target))
            {
                failure = InvalidOperation(operationId, "cancel an unknown operation");
                return false;
            }

            if (scope == NnrpOperationCancelScope.Group && target.OperationGroupId == 0)
            {
                failure = InvalidOperation(operationId, "group-cancel an ungrouped operation");
                return false;
            }

            var selectedOperationIds = SelectOperationIds(scope, operationId, target.OperationGroupId);
            var cancelled = new List<uint>();
            foreach (var selectedOperationId in selectedOperationIds)
            {
                var selected = operations[selectedOperationId].Operation;
                if (selected.IsTerminal)
                {
                    continue;
                }

                if (!selected.TryCancel(out failure))
                {
                    return false;
                }

                cancelled.Add(selectedOperationId);
            }

            cancelledOperationIds = cancelled;
            failure = NnrpProtocolFailure.None;
            return true;
        }

        private IReadOnlyList<uint> SelectOperationIds(
            NnrpOperationCancelScope scope,
            uint operationId,
            uint operationGroupId)
        {
            switch (scope)
            {
                case NnrpOperationCancelScope.Operation:
                    return new[] { operationId };
                case NnrpOperationCancelScope.Subtree:
                    return operations.Keys
                        .Where(candidateId => candidateId == operationId || IsDescendantOf(candidateId, operationId))
                        .OrderBy(candidateId => candidateId)
                        .ToArray();
                case NnrpOperationCancelScope.Group:
                    return operations
                        .Where(pair => pair.Value.OperationGroupId == operationGroupId)
                        .Select(pair => pair.Key)
                        .OrderBy(candidateId => candidateId)
                        .ToArray();
                case NnrpOperationCancelScope.Session:
                    return operations.Keys.OrderBy(candidateId => candidateId).ToArray();
                default:
                    return Array.Empty<uint>();
            }
        }

        private bool IsDescendantOf(uint candidateId, uint ancestorId)
        {
            var currentId = candidateId;
            var visited = new HashSet<uint>();
            while (operations.TryGetValue(currentId, out var entry) && entry.ParentOperationId != 0)
            {
                if (!visited.Add(currentId))
                {
                    return false;
                }

                if (entry.ParentOperationId == ancestorId)
                {
                    return true;
                }

                currentId = entry.ParentOperationId;
            }

            return false;
        }

        private static NnrpProtocolFailure InvalidOperation(uint operationId, string action)
        {
            return NnrpProtocolFailure.InvalidState(
                NnrpErrorScope.Frame,
                $"Cannot {action} for operation {operationId}.");
        }

        private readonly struct OperationEntry
        {
            public OperationEntry(NnrpOperationLifecycle operation, uint parentOperationId, uint operationGroupId)
            {
                Operation = operation;
                ParentOperationId = parentOperationId;
                OperationGroupId = operationGroupId;
            }

            public NnrpOperationLifecycle Operation { get; }

            public uint ParentOperationId { get; }

            public uint OperationGroupId { get; }
        }
    }
}
