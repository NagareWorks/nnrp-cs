using System;
using System.Runtime.InteropServices;
using Nnrp.Runtime;

namespace Nnrp.NativeBridge
{
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct NnrpRuntimeObjectDescriptor
    {
        public NnrpRuntimeObjectDescriptor(ObjectDescriptorMetadata metadata)
        {
            ObjectId = metadata.ObjectId;
            ObjectKind = (ushort)metadata.ObjectKind;
            ProducerRole = (byte)metadata.ProducerRole;
            ConsumerRole = (byte)metadata.ConsumerRole;
            SessionId = metadata.SessionId;
            ByteSize = metadata.ByteSize;
            ComputeCostUnits = metadata.ComputeCostUnits;
            MemoryLocationHint = (ushort)metadata.MemoryLocationHint;
            OwnershipHint = (ushort)metadata.OwnershipHint;
            LifetimeHintMs = metadata.LifetimeHintMs;
            MetadataBytes = metadata.MetadataBytes;
        }

        public readonly ulong ObjectId;
        public readonly ushort ObjectKind;
        public readonly byte ProducerRole;
        public readonly byte ConsumerRole;
        public readonly uint SessionId;
        public readonly ulong ByteSize;
        public readonly uint ComputeCostUnits;
        public readonly ushort MemoryLocationHint;
        public readonly ushort OwnershipHint;
        public readonly uint LifetimeHintMs;
        public readonly uint MetadataBytes;

        public ObjectDescriptorMetadata ToMetadata()
        {
            return new ObjectDescriptorMetadata(
                ObjectId,
                (RuntimeObjectKind)ObjectKind,
                (RuntimeRole)ProducerRole,
                (RuntimeRole)ConsumerRole,
                SessionId,
                ByteSize,
                ComputeCostUnits,
                (MemoryLocationHint)MemoryLocationHint,
                (OwnershipHint)OwnershipHint,
                LifetimeHintMs,
                MetadataBytes);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct NnrpCacheReferenceDescriptor
    {
        public NnrpCacheReferenceDescriptor(CacheReferenceMetadata metadata)
        {
            CacheNamespace = metadata.CacheNamespace;
            ProfileId = metadata.ProfileId;
            ReuseScope = (ushort)metadata.ReuseScope;
            CacheKeyHi = metadata.CacheKeyHi;
            CacheKeyLo = metadata.CacheKeyLo;
            LeaseId = metadata.LeaseId;
            ProducerTraceId = metadata.ProducerTraceId;
            ExpirationHintMs = metadata.ExpirationHintMs;
            MetadataBytes = metadata.MetadataBytes;
            Flags = metadata.Flags;
        }

        public readonly uint CacheNamespace;
        public readonly ushort ProfileId;
        public readonly ushort ReuseScope;
        public readonly ulong CacheKeyHi;
        public readonly ulong CacheKeyLo;
        public readonly ulong LeaseId;
        public readonly ulong ProducerTraceId;
        public readonly uint ExpirationHintMs;
        public readonly uint MetadataBytes;
        public readonly uint Flags;

        public CacheReferenceMetadata ToMetadata()
        {
            return new CacheReferenceMetadata(
                CacheNamespace,
                CacheKeyHi,
                CacheKeyLo,
                ProfileId,
                (CacheReuseScope)ReuseScope,
                LeaseId,
                ProducerTraceId,
                ExpirationHintMs,
                MetadataBytes,
                Flags);
        }
    }

    public abstract class NnrpNativeRuntimeSafeHandle : SafeHandle
    {
        private readonly NnrpNativeRuntimeEntrypoints.HandleStatusInvoker _release;
        private readonly NnrpHandle _nativeHandle;

        internal NnrpNativeRuntimeSafeHandle(
            NnrpHandle nativeHandle,
            NnrpHandleKind expectedKind,
            NnrpNativeRuntimeEntrypoints.HandleStatusInvoker release)
            : base(IntPtr.Zero, true)
        {
            nativeHandle.RequireKind(expectedKind);
            _nativeHandle = nativeHandle;
            _release = release ?? throw new ArgumentNullException(nameof(release));
            SetHandle(new IntPtr(1));
        }

        public override bool IsInvalid => handle == IntPtr.Zero;

        public NnrpHandle NativeHandle
        {
            get
            {
                if (IsClosed || IsInvalid)
                {
                    throw new ObjectDisposedException(GetType().Name);
                }

                return _nativeHandle;
            }
        }

        protected override bool ReleaseHandle()
        {
            return _release(_nativeHandle).StatusCode == NnrpFfiStatusCode.Ok;
        }
    }

    public sealed class NnrpNativeObjectDescriptorHandle : NnrpNativeRuntimeSafeHandle
    {
        internal NnrpNativeObjectDescriptorHandle(
            NnrpHandle handle,
            NnrpNativeRuntimeEntrypoints.HandleStatusInvoker release)
            : base(handle, NnrpHandleKind.ObjectDescriptor, release)
        {
        }
    }

    public sealed class NnrpNativeCacheReferenceDescriptorHandle : NnrpNativeRuntimeSafeHandle
    {
        internal NnrpNativeCacheReferenceDescriptorHandle(
            NnrpHandle handle,
            NnrpNativeRuntimeEntrypoints.HandleStatusInvoker release)
            : base(handle, NnrpHandleKind.CacheReferenceDescriptor, release)
        {
        }
    }

    public sealed class NnrpNativeObjectMetadataBufferHandle : NnrpNativeRuntimeSafeHandle
    {
        internal NnrpNativeObjectMetadataBufferHandle(
            NnrpHandle handle,
            NnrpNativeRuntimeEntrypoints.HandleStatusInvoker release)
            : base(handle, NnrpHandleKind.Buffer, release)
        {
        }
    }

    public sealed class NnrpNativeObjectMetadataBuffer : IDisposable
    {
        private readonly NnrpNativeRuntimeEntrypoints _entrypoints;
        private NnrpBufferView _view;

        internal NnrpNativeObjectMetadataBuffer(
            NnrpNativeRuntimeEntrypoints entrypoints,
            NnrpNativeObjectMetadataBufferHandle handle,
            NnrpBufferView view)
        {
            _entrypoints = entrypoints ?? throw new ArgumentNullException(nameof(entrypoints));
            Handle = handle ?? throw new ArgumentNullException(nameof(handle));
            _view = view;
        }

        public NnrpNativeObjectMetadataBufferHandle Handle { get; }

        public void RefreshView()
        {
            NnrpBufferView view;
            _entrypoints.ObjectMetadataBufferView(Handle.NativeHandle, out view).ThrowIfError();
            _view = view;
        }

        public byte[] CopyToArray()
        {
            Handle.NativeHandle.RequireKind(NnrpHandleKind.Buffer);
            if (_view.Length == UIntPtr.Zero)
            {
                return Array.Empty<byte>();
            }

            var length = checked((int)_view.Length.ToUInt64());
            var copy = new byte[length];
            Marshal.Copy(_view.Pointer, copy, 0, length);
            return copy;
        }

        public void Dispose()
        {
            Handle.Dispose();
        }
    }

    public sealed class NnrpNativeObjectDescriptorSnapshot
    {
        internal NnrpNativeObjectDescriptorSnapshot(ObjectDescriptorMetadata descriptor, ReadOnlyMemory<byte> metadata)
        {
            Descriptor = descriptor;
            Metadata = metadata;
        }

        public ObjectDescriptorMetadata Descriptor { get; }
        public ReadOnlyMemory<byte> Metadata { get; }
    }

    public sealed class NnrpNativeCacheReferenceSnapshot
    {
        internal NnrpNativeCacheReferenceSnapshot(CacheReferenceMetadata descriptor, ReadOnlyMemory<byte> metadata)
        {
            Descriptor = descriptor;
            Metadata = metadata;
        }

        public CacheReferenceMetadata Descriptor { get; }
        public ReadOnlyMemory<byte> Metadata { get; }
    }

    public sealed class NnrpNativeObjectDescriptor : IDisposable
    {
        private readonly NnrpNativeRuntimeEntrypoints _entrypoints;

        internal NnrpNativeObjectDescriptor(
            NnrpNativeRuntimeEntrypoints entrypoints,
            NnrpNativeObjectDescriptorHandle handle)
        {
            _entrypoints = entrypoints ?? throw new ArgumentNullException(nameof(entrypoints));
            Handle = handle ?? throw new ArgumentNullException(nameof(handle));
        }

        public NnrpNativeObjectDescriptorHandle Handle { get; }

        public ObjectDescriptorMetadata ReadDescriptor()
        {
            NnrpRuntimeObjectDescriptor descriptor;
            NnrpBufferView metadata;
            _entrypoints.ObjectDescriptorView(Handle.NativeHandle, out descriptor, out metadata).ThrowIfError();
            return descriptor.ToMetadata();
        }

        public NnrpNativeObjectMetadataBuffer AcquireMetadataSnapshot()
        {
            NnrpHandle buffer;
            NnrpBufferView view;
            _entrypoints.ObjectDescriptorMetadataSnapshot(Handle.NativeHandle, out buffer, out view).ThrowIfError();
            return new NnrpNativeObjectMetadataBuffer(
                _entrypoints,
                new NnrpNativeObjectMetadataBufferHandle(buffer, _entrypoints.ObjectMetadataBufferRelease),
                view);
        }

        public NnrpNativeObjectDescriptorSnapshot Snapshot()
        {
            var descriptor = ReadDescriptor();
            using (var metadata = AcquireMetadataSnapshot())
            {
                return new NnrpNativeObjectDescriptorSnapshot(descriptor, metadata.CopyToArray());
            }
        }

        public void Dispose()
        {
            Handle.Dispose();
        }
    }

    public sealed class NnrpNativeCacheReferenceDescriptor : IDisposable
    {
        private readonly NnrpNativeRuntimeEntrypoints _entrypoints;

        internal NnrpNativeCacheReferenceDescriptor(
            NnrpNativeRuntimeEntrypoints entrypoints,
            NnrpNativeCacheReferenceDescriptorHandle handle)
        {
            _entrypoints = entrypoints ?? throw new ArgumentNullException(nameof(entrypoints));
            Handle = handle ?? throw new ArgumentNullException(nameof(handle));
        }

        public NnrpNativeCacheReferenceDescriptorHandle Handle { get; }

        public CacheReferenceMetadata ReadDescriptor()
        {
            NnrpCacheReferenceDescriptor descriptor;
            NnrpBufferView metadata;
            _entrypoints.CacheReferenceDescriptorView(Handle.NativeHandle, out descriptor, out metadata).ThrowIfError();
            return descriptor.ToMetadata();
        }

        public NnrpNativeObjectMetadataBuffer AcquireMetadataSnapshot()
        {
            NnrpHandle buffer;
            NnrpBufferView view;
            _entrypoints.CacheReferenceDescriptorMetadataSnapshot(Handle.NativeHandle, out buffer, out view).ThrowIfError();
            return new NnrpNativeObjectMetadataBuffer(
                _entrypoints,
                new NnrpNativeObjectMetadataBufferHandle(buffer, _entrypoints.ObjectMetadataBufferRelease),
                view);
        }

        public NnrpNativeCacheReferenceSnapshot Snapshot()
        {
            var descriptor = ReadDescriptor();
            using (var metadata = AcquireMetadataSnapshot())
            {
                return new NnrpNativeCacheReferenceSnapshot(descriptor, metadata.CopyToArray());
            }
        }

        public void Dispose()
        {
            Handle.Dispose();
        }
    }

    public sealed class NnrpNativeRuntimeObjects
    {
        public NnrpNativeRuntimeObjects(NnrpNativeRuntimeEntrypoints entrypoints)
        {
            Entrypoints = entrypoints ?? throw new ArgumentNullException(nameof(entrypoints));
        }

        public NnrpNativeRuntimeEntrypoints Entrypoints { get; }

        public NnrpNativeObjectMetadataBuffer AcquireMetadataCopy(byte[] metadata)
        {
            if (metadata == null)
            {
                throw new ArgumentNullException(nameof(metadata));
            }

            NnrpHandle handle = NnrpHandle.Invalid;
            NnrpBufferView view = NnrpBufferView.Empty;
            WithPinnedView(metadata, source => Entrypoints.ObjectMetadataBufferAcquireCopy(source, out handle, out view).ThrowIfError());
            return new NnrpNativeObjectMetadataBuffer(
                Entrypoints,
                new NnrpNativeObjectMetadataBufferHandle(handle, Entrypoints.ObjectMetadataBufferRelease),
                view);
        }

        public NnrpNativeObjectDescriptor CreateObjectDescriptor(
            ObjectDescriptorMetadata descriptor,
            byte[] metadata)
        {
            if (metadata == null)
            {
                throw new ArgumentNullException(nameof(metadata));
            }

            RequireLength(descriptor.MetadataBytes, metadata.Length, nameof(descriptor));
            NnrpHandle handle = NnrpHandle.Invalid;
            WithPinnedView(
                metadata,
                view => Entrypoints.ObjectDescriptorCreate(new NnrpRuntimeObjectDescriptor(descriptor), view, out handle).ThrowIfError());
            return new NnrpNativeObjectDescriptor(
                Entrypoints,
                new NnrpNativeObjectDescriptorHandle(handle, Entrypoints.ObjectDescriptorRelease));
        }

        public NnrpNativeCacheReferenceDescriptor CreateCacheReference(
            CacheReferenceMetadata descriptor,
            byte[] metadata)
        {
            if (metadata == null)
            {
                throw new ArgumentNullException(nameof(metadata));
            }

            RequireLength(descriptor.MetadataBytes, metadata.Length, nameof(descriptor));
            NnrpHandle handle = NnrpHandle.Invalid;
            WithPinnedView(
                metadata,
                view => Entrypoints.CacheReferenceDescriptorCreate(new NnrpCacheReferenceDescriptor(descriptor), view, out handle).ThrowIfError());
            return new NnrpNativeCacheReferenceDescriptor(
                Entrypoints,
                new NnrpNativeCacheReferenceDescriptorHandle(handle, Entrypoints.CacheReferenceDescriptorRelease));
        }

        private static void RequireLength(uint declaredLength, int actualLength, string parameterName)
        {
            if (declaredLength != (uint)actualLength)
            {
                throw new ArgumentException("Descriptor metadata length does not match MetadataBytes.", parameterName);
            }
        }

        private static void WithPinnedView(byte[] source, Action<NnrpBufferView> action)
        {
            GCHandle pinned = default(GCHandle);
            try
            {
                var view = NnrpBufferView.Empty;
                if (source.Length > 0)
                {
                    pinned = GCHandle.Alloc(source, GCHandleType.Pinned);
                    view = new NnrpBufferView(pinned.AddrOfPinnedObject(), new UIntPtr((uint)source.Length));
                }

                action(view);
            }
            finally
            {
                if (pinned.IsAllocated)
                {
                    pinned.Free();
                }
            }
        }
    }
}
