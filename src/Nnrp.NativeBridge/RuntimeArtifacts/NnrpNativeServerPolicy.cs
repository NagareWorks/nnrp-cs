using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Nnrp.Core;

namespace Nnrp.NativeBridge
{
    internal sealed class NnrpNativeServerBindOptions
    {
        internal NnrpNativeServerBindOptions(
            ulong serverId,
            uint generation,
            IReadOnlyList<ushort> supportedProfiles,
            IReadOnlyList<CacheObjectKind> supportedCacheObjects,
            ulong maxCacheObjects,
            uint maxCacheObjectBytes,
            uint resumeTokenBytes,
            ushort maxInFlightOperations,
            ushort grantedOperationCredit,
            uint leaseTtlMilliseconds,
            uint resumeWindowMilliseconds,
            NnrpSchemaRegistry schemaRegistry,
            Func<SessionOpenMetadata, ValueTask<NnrpNativeServerPolicyDecision>> applicationPolicy)
        {
            ServerId = serverId;
            Generation = generation;
            SupportedProfiles = supportedProfiles ?? throw new ArgumentNullException(nameof(supportedProfiles));
            SupportedCacheObjects = supportedCacheObjects ?? throw new ArgumentNullException(nameof(supportedCacheObjects));
            MaxCacheObjects = maxCacheObjects;
            MaxCacheObjectBytes = maxCacheObjectBytes;
            ResumeTokenBytes = resumeTokenBytes;
            MaxInFlightOperations = maxInFlightOperations;
            GrantedOperationCredit = grantedOperationCredit;
            LeaseTtlMilliseconds = leaseTtlMilliseconds;
            ResumeWindowMilliseconds = resumeWindowMilliseconds;
            SchemaRegistry = schemaRegistry ?? throw new ArgumentNullException(nameof(schemaRegistry));
            ApplicationPolicy = applicationPolicy ?? throw new ArgumentNullException(nameof(applicationPolicy));
        }

        internal ulong ServerId { get; }
        internal uint Generation { get; }
        internal IReadOnlyList<ushort> SupportedProfiles { get; }
        internal IReadOnlyList<CacheObjectKind> SupportedCacheObjects { get; }
        internal ulong MaxCacheObjects { get; }
        internal uint MaxCacheObjectBytes { get; }
        internal uint ResumeTokenBytes { get; }
        internal ushort MaxInFlightOperations { get; }
        internal ushort GrantedOperationCredit { get; }
        internal uint LeaseTtlMilliseconds { get; }
        internal uint ResumeWindowMilliseconds { get; }
        internal NnrpSchemaRegistry SchemaRegistry { get; }
        internal Func<SessionOpenMetadata, ValueTask<NnrpNativeServerPolicyDecision>> ApplicationPolicy { get; }
    }

    internal readonly struct NnrpNativeServerPolicyDecision
    {
        private NnrpNativeServerPolicyDecision(bool accepted, SessionErrorCode sessionErrorCode, string? diagnostic)
        {
            Accepted = accepted;
            SessionErrorCode = sessionErrorCode;
            Diagnostic = diagnostic;
        }

        internal bool Accepted { get; }

        internal SessionErrorCode SessionErrorCode { get; }

        internal string? Diagnostic { get; }

        internal static NnrpNativeServerPolicyDecision Accept() =>
            new NnrpNativeServerPolicyDecision(true, SessionErrorCode.None, null);

        internal static NnrpNativeServerPolicyDecision Reject(SessionErrorCode errorCode, string? diagnostic) =>
            errorCode == SessionErrorCode.None
                ? throw new ArgumentOutOfRangeException(nameof(errorCode))
                : new NnrpNativeServerPolicyDecision(false, errorCode, diagnostic);
    }

    internal sealed class NnrpNativeServerPolicyDispatcher : IDisposable
    {
        private const int MaxConcurrency = 4;
        private static readonly TimeSpan DefaultShutdownTimeout = TimeSpan.FromSeconds(5);

        private readonly object gate = new object();
        private readonly NnrpNativeRuntimeEntrypoints entrypoints;
        private readonly Func<SessionOpenMetadata, ValueTask<NnrpNativeServerPolicyDecision>> evaluate;
        private readonly ConcurrentExclusiveSchedulerPair schedulers;
        private readonly HashSet<Task> pending = new HashSet<Task>();
        private readonly TimeSpan shutdownTimeout;
        private IDisposable? nativeOwnership;
        private Exception? firstError;
        private bool closed;

        internal NnrpNativeServerPolicyDispatcher(
            NnrpNativeRuntimeEntrypoints entrypoints,
            Func<SessionOpenMetadata, ValueTask<NnrpNativeServerPolicyDecision>> evaluate,
            IDisposable? nativeOwnership = null,
            TimeSpan? shutdownTimeout = null)
        {
            this.entrypoints = entrypoints ?? throw new ArgumentNullException(nameof(entrypoints));
            this.evaluate = evaluate ?? throw new ArgumentNullException(nameof(evaluate));
            this.shutdownTimeout = shutdownTimeout ?? DefaultShutdownTimeout;
            if (this.shutdownTimeout <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(shutdownTimeout));
            }

            this.nativeOwnership = nativeOwnership;
            schedulers = new ConcurrentExclusiveSchedulerPair(TaskScheduler.Default, MaxConcurrency);
            BeginCallback = Begin;
            Sink = new NnrpServerPolicySink(IntPtr.Zero, BeginCallback);
        }

        internal NnrpServerPolicyBeginCallback BeginCallback { get; }

        internal NnrpServerPolicySink Sink { get; }

        private uint Begin(IntPtr userData, ulong requestId, NnrpBufferView metadata)
        {
            byte[] encoded;
            try
            {
                if (requestId == 0 || metadata.Length.ToUInt64() > int.MaxValue)
                {
                    return (uint)NnrpFfiStatusCode.CallbackRejected;
                }

                encoded = new byte[(int)metadata.Length.ToUInt64()];
                if (encoded.Length != 0)
                {
                    if (metadata.Pointer == IntPtr.Zero)
                    {
                        return (uint)NnrpFfiStatusCode.CallbackRejected;
                    }

                    Marshal.Copy(metadata.Pointer, encoded, 0, encoded.Length);
                }
            }
            catch (Exception)
            {
                return (uint)NnrpFfiStatusCode.CallbackRejected;
            }

            Task task;
            lock (gate)
            {
                if (closed)
                {
                    return (uint)NnrpFfiStatusCode.CallbackRejected;
                }

                try
                {
                    task = Task.Factory.StartNew(
                        () => EvaluateAndCompleteAsync(requestId, encoded),
                        default,
                        TaskCreationOptions.DenyChildAttach,
                        schedulers.ConcurrentScheduler).Unwrap();
                }
                catch (Exception)
                {
                    return (uint)NnrpFfiStatusCode.CallbackRejected;
                }

                pending.Add(task);
            }

            task.ContinueWith(
                completed => RecordCompletion(completed),
                default,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            return (uint)NnrpFfiStatusCode.Ok;
        }

        private async Task EvaluateAndCompleteAsync(ulong requestId, byte[] encoded)
        {
            NnrpNativeServerPolicyDecision decision;
            try
            {
                if (!SessionOpenMetadata.TryParse(encoded, strict: true, out var open, out _))
                {
                    throw new ArgumentException("Native session policy metadata is invalid.", nameof(encoded));
                }

                decision = await evaluate(open).ConfigureAwait(false);
            }
            catch (Exception)
            {
                decision = NnrpNativeServerPolicyDecision.Reject(
                    SessionErrorCode.SessionLimitReached,
                    "application policy evaluation failed");
            }

            var diagnostic = decision.Diagnostic == null
                ? Array.Empty<byte>()
                : Encoding.UTF8.GetBytes(decision.Diagnostic);
            GCHandle owner = default;
            try
            {
                var view = NnrpBufferView.Empty;
                if (diagnostic.Length != 0)
                {
                    owner = GCHandle.Alloc(diagnostic, GCHandleType.Pinned);
                    view = new NnrpBufferView(owner.AddrOfPinnedObject(), new UIntPtr((uint)diagnostic.Length));
                }

                entrypoints.ServerPolicyComplete(
                    new NnrpServerPolicyCompleteRequest(
                        requestId,
                        new NnrpServerPolicyDecision(
                            decision.Accepted ? (byte)1 : (byte)0,
                            (uint)decision.SessionErrorCode,
                            view))).ThrowIfError();
            }
            finally
            {
                if (owner.IsAllocated)
                {
                    owner.Free();
                }
            }
        }

        private void RecordCompletion(Task task)
        {
            lock (gate)
            {
                pending.Remove(task);
                if (task.Exception != null && firstError == null)
                {
                    firstError = task.Exception.InnerException ?? task.Exception;
                }
            }
        }

        public void Dispose()
        {
            Task drain;
            lock (gate)
            {
                if (closed)
                {
                    return;
                }

                closed = true;
                var tasks = new Task[pending.Count];
                pending.CopyTo(tasks);
                schedulers.Complete();
                drain = Task.WhenAll(Task.WhenAll(tasks), schedulers.Completion);
            }

            var completed = false;
            try
            {
                completed = drain.Wait(shutdownTimeout);
            }
            catch (AggregateException)
            {
                completed = true;
            }

            if (!completed)
            {
                drain.ContinueWith(
                    CompleteTimedOutDrain,
                    default,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
                throw new TimeoutException("Native server policy shutdown exceeded the bounded drain timeout.");
            }

            try
            {
                drain.GetAwaiter().GetResult();
            }
            catch (Exception error)
            {
                RecordDrainError(error);
            }
            finally
            {
                ReleaseNativeOwnership();
            }

            lock (gate)
            {
                if (firstError != null)
                {
                    throw firstError;
                }
            }
        }

        private void CompleteTimedOutDrain(Task drain)
        {
            if (drain.Exception != null)
            {
                RecordDrainError(drain.Exception);
            }

            ReleaseNativeOwnership();
        }

        private void RecordDrainError(Exception error)
        {
            lock (gate)
            {
                firstError ??= error is AggregateException aggregate
                    ? aggregate.Flatten().InnerExceptions[0]
                    : error;
            }
        }

        private void ReleaseNativeOwnership() =>
            System.Threading.Interlocked.Exchange(ref nativeOwnership, null)?.Dispose();
    }
}
