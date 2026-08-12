using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Nnrp.Core;
using Nnrp.Runtime;

namespace Nnrp.NativeBridge
{
    public sealed class NnrpNativeArtifactException : InvalidOperationException
    {
        public NnrpNativeArtifactException(string message)
            : base(message)
        {
        }

        public NnrpNativeArtifactException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    public readonly struct NnrpNativePlatform : IEquatable<NnrpNativePlatform>
    {
        public NnrpNativePlatform(string osName, string architecture)
        {
            OsName = NormalizeOs(osName);
            Architecture = NormalizeArchitecture(architecture);
        }

        public string OsName { get; }

        public string Architecture { get; }

        public string RuntimeIdentifier
        {
            get
            {
                string ridOs;
                switch (OsName)
                {
                    case "windows":
                        ridOs = "win";
                        break;
                    case "macos":
                        ridOs = "osx";
                        break;
                    case "linux":
                    case "android":
                    case "ios":
                    case "iossimulator":
                        ridOs = OsName;
                        break;
                    default:
                        throw new NnrpNativeArtifactException("Unsupported native artifact OS: " + OsName);
                }

                string ridArch;
                switch (Architecture)
                {
                    case "x86_64":
                        ridArch = "x64";
                        break;
                    case "arm64":
                        ridArch = "arm64";
                        break;
                    case "x86":
                        ridArch = "x86";
                        break;
                    case "arm":
                        ridArch = "arm";
                        break;
                    default:
                        throw new NnrpNativeArtifactException("Unsupported native artifact architecture: " + Architecture);
                }

                return ridOs + "-" + ridArch;
            }
        }

        [ExcludeFromCodeCoverage]
        public static NnrpNativePlatform Current
        {
            get
            {
                string osName;
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    osName = "windows";
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    osName = "macos";
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    osName = "linux";
                }
                else
                {
                    throw new NnrpNativeArtifactException("Unsupported native artifact OS: " + RuntimeInformation.OSDescription);
                }

                return new NnrpNativePlatform(osName, RuntimeInformation.ProcessArchitecture.ToString());
            }
        }

        public bool Equals(NnrpNativePlatform other)
        {
            return string.Equals(OsName, other.OsName, StringComparison.Ordinal)
                && string.Equals(Architecture, other.Architecture, StringComparison.Ordinal);
        }

        public override bool Equals(object? obj)
        {
            return obj is NnrpNativePlatform other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((OsName != null ? OsName.GetHashCode() : 0) * 397)
                    ^ (Architecture != null ? Architecture.GetHashCode() : 0);
            }
        }

        public static bool operator ==(NnrpNativePlatform left, NnrpNativePlatform right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(NnrpNativePlatform left, NnrpNativePlatform right)
        {
            return !left.Equals(right);
        }

        private static string NormalizeOs(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("OS name is required.", nameof(value));
            }

            string normalized = value.Trim().ToLowerInvariant();
            switch (normalized)
            {
                case "win":
                case "win32":
                case "windows":
                    return "windows";
                case "darwin":
                case "macosx":
                case "osx":
                case "macos":
                    return "macos";
                case "linux":
                case "android":
                case "ios":
                case "iossimulator":
                    return normalized;
                default:
                    throw new NnrpNativeArtifactException("Unsupported native artifact OS: " + value);
            }
        }

        private static string NormalizeArchitecture(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Architecture is required.", nameof(value));
            }

            string normalized = value.Trim().ToLowerInvariant().Replace("-", "_");
            switch (normalized)
            {
                case "amd64":
                case "x64":
                case "x86_64":
                    return "x86_64";
                case "i386":
                case "i686":
                case "x86":
                    return "x86";
                case "aarch64":
                case "arm64":
                    return "arm64";
                case "armv7":
                case "armv7l":
                case "arm":
                    return "arm";
                default:
                    throw new NnrpNativeArtifactException("Unsupported native artifact architecture: " + value);
            }
        }
    }

    public readonly struct NnrpNativeProbeResult
    {
        public NnrpNativeProbeResult(
            string artifactPath,
            ushort abiMajor,
            ushort abiMinor,
            ushort abiPatch,
            byte protocolMajor,
            byte protocolWireFormat,
            ushort sdkMajor,
            ushort sdkMinor,
            ushort sdkPatch,
            ushort sdkChannel,
            ushort sdkRevision,
            uint transportSlots,
            ulong featureFlags)
        {
            ArtifactPath = artifactPath ?? throw new ArgumentNullException(nameof(artifactPath));
            AbiMajor = abiMajor;
            AbiMinor = abiMinor;
            AbiPatch = abiPatch;
            ProtocolMajor = protocolMajor;
            ProtocolWireFormat = protocolWireFormat;
            SdkMajor = sdkMajor;
            SdkMinor = sdkMinor;
            SdkPatch = sdkPatch;
            SdkChannel = sdkChannel;
            SdkRevision = sdkRevision;
            TransportSlots = transportSlots;
            FeatureFlags = featureFlags;
        }

        public string ArtifactPath { get; }

        public ushort AbiMajor { get; }

        public ushort AbiMinor { get; }

        public ushort AbiPatch { get; }

        public byte ProtocolMajor { get; }

        public byte ProtocolWireFormat { get; }

        public ushort SdkMajor { get; }

        public ushort SdkMinor { get; }

        public ushort SdkPatch { get; }

        public ushort SdkChannel { get; }

        public ushort SdkRevision { get; }

        public uint TransportSlots { get; }

        public ulong FeatureFlags { get; }
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct NnrpProtocolVersion
    {
        public NnrpProtocolVersion(byte major, byte wireFormat)
        {
            Major = major;
            WireFormat = wireFormat;
        }

        public readonly byte Major;

        public readonly byte WireFormat;
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct NnrpRuntimeCapabilities
    {
        public NnrpRuntimeCapabilities(
            ushort abiMajor,
            ushort abiMinor,
            ushort abiPatch,
            NnrpProtocolVersion protocolVersion,
            ushort sdkMajor,
            ushort sdkMinor,
            ushort sdkPatch,
            ushort sdkChannel,
            ushort sdkRevision,
            uint transportSlots,
            ulong featureFlags)
        {
            AbiMajor = abiMajor;
            AbiMinor = abiMinor;
            AbiPatch = abiPatch;
            Reserved0 = 0;
            ProtocolVersion = protocolVersion;
            SdkMajor = sdkMajor;
            SdkMinor = sdkMinor;
            SdkPatch = sdkPatch;
            SdkChannel = sdkChannel;
            SdkRevision = sdkRevision;
            Reserved1 = 0;
            TransportSlots = transportSlots;
            FeatureFlags = featureFlags;
        }

        public readonly ushort AbiMajor;

        public readonly ushort AbiMinor;

        public readonly ushort AbiPatch;

        public readonly ushort Reserved0;

        public readonly NnrpProtocolVersion ProtocolVersion;

        public readonly ushort SdkMajor;

        public readonly ushort SdkMinor;

        public readonly ushort SdkPatch;

        public readonly ushort SdkChannel;

        public readonly ushort SdkRevision;

        public readonly ushort Reserved1;

        public readonly uint TransportSlots;

        public readonly ulong FeatureFlags;
    }

    public enum NnrpFfiStatusCode : uint
    {
        Ok = 0,
        InvalidArgument = 1,
        InvalidHandle = 2,
        InvalidState = 3,
        ProtocolError = 4,
        WouldBlock = 5,
        CallbackRejected = 6,
        InternalError = 0xffff,
    }

    public enum NnrpErrorFamily : uint
    {
        None = 0,
        Session = 1,
        Cache = 2,
        Schema = 3,
        Transport = 4,
        Lifecycle = 5,
        Operation = 6,
        Control = 7,
        RuntimeObject = 8,
        Internal = 0xffff,
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct NnrpFfiStatus : IEquatable<NnrpFfiStatus>
    {
        public NnrpFfiStatus(
            NnrpFfiStatusCode statusCode,
            NnrpErrorFamily errorFamily = NnrpErrorFamily.None,
            uint protocolErrorCode = 0,
            uint detailCode = 0)
        {
            StatusCode = statusCode;
            ErrorFamily = errorFamily;
            ProtocolErrorCode = protocolErrorCode;
            DetailCode = detailCode;
        }

        public readonly NnrpFfiStatusCode StatusCode;

        public readonly NnrpErrorFamily ErrorFamily;

        public readonly uint ProtocolErrorCode;

        public readonly uint DetailCode;

        public static NnrpFfiStatus Ok => new NnrpFfiStatus(NnrpFfiStatusCode.Ok);

        public bool Succeeded => StatusCode == NnrpFfiStatusCode.Ok;

        public void ThrowIfError()
        {
            if (Succeeded)
            {
                return;
            }

            switch (StatusCode)
            {
                case NnrpFfiStatusCode.InvalidArgument:
                    throw new NnrpNativeInvalidArgumentException(this);
                case NnrpFfiStatusCode.InvalidHandle:
                    throw new NnrpNativeInvalidHandleException(this);
                case NnrpFfiStatusCode.InvalidState:
                    throw new NnrpNativeInvalidStateException(this);
                case NnrpFfiStatusCode.ProtocolError:
                    throw new NnrpNativeProtocolException(this);
                case NnrpFfiStatusCode.WouldBlock:
                    throw new NnrpNativeWouldBlockException(this);
                case NnrpFfiStatusCode.CallbackRejected:
                    throw new NnrpNativeCallbackRejectedException(this);
                case NnrpFfiStatusCode.InternalError:
                default:
                    throw new NnrpNativeInternalException(this);
            }
        }

        public bool Equals(NnrpFfiStatus other)
        {
            return StatusCode == other.StatusCode
                && ErrorFamily == other.ErrorFamily
                && ProtocolErrorCode == other.ProtocolErrorCode
                && DetailCode == other.DetailCode;
        }

        public override bool Equals(object? obj)
        {
            return obj is NnrpFfiStatus other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)StatusCode;
                hash = (hash * 397) ^ (int)ErrorFamily;
                hash = (hash * 397) ^ ProtocolErrorCode.GetHashCode();
                hash = (hash * 397) ^ DetailCode.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(NnrpFfiStatus left, NnrpFfiStatus right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(NnrpFfiStatus left, NnrpFfiStatus right)
        {
            return !left.Equals(right);
        }
    }

    public class NnrpNativeRuntimeException : InvalidOperationException
    {
        public NnrpNativeRuntimeException(NnrpFfiStatus status)
            : base(FormatMessage(status))
        {
            Status = status;
        }

        public NnrpFfiStatus Status { get; }

        private static string FormatMessage(NnrpFfiStatus status)
        {
            return "Native runtime status failed: status_code="
                + status.StatusCode
                + ", error_family="
                + status.ErrorFamily
                + ", protocol_error_code="
                + status.ProtocolErrorCode
                + ", detail_code="
                + status.DetailCode;
        }
    }

    public sealed class NnrpNativeInvalidArgumentException : NnrpNativeRuntimeException
    {
        public NnrpNativeInvalidArgumentException(NnrpFfiStatus status)
            : base(status)
        {
        }
    }

    public sealed class NnrpNativeInvalidHandleException : NnrpNativeRuntimeException
    {
        public NnrpNativeInvalidHandleException(NnrpFfiStatus status)
            : base(status)
        {
        }
    }

    public sealed class NnrpNativeInvalidStateException : NnrpNativeRuntimeException
    {
        public NnrpNativeInvalidStateException(NnrpFfiStatus status)
            : base(status)
        {
        }
    }

    public sealed class NnrpNativeProtocolException : NnrpNativeRuntimeException
    {
        public NnrpNativeProtocolException(NnrpFfiStatus status)
            : base(status)
        {
        }
    }

    public sealed class NnrpNativeWouldBlockException : NnrpNativeRuntimeException
    {
        public NnrpNativeWouldBlockException(NnrpFfiStatus status)
            : base(status)
        {
        }
    }

    public sealed class NnrpNativeCallbackRejectedException : NnrpNativeRuntimeException
    {
        public NnrpNativeCallbackRejectedException(NnrpFfiStatus status)
            : base(status)
        {
        }
    }

    public sealed class NnrpNativeInternalException : NnrpNativeRuntimeException
    {
        public NnrpNativeInternalException(NnrpFfiStatus status)
            : base(status)
        {
        }
    }

    public enum NnrpHandleKind : uint
    {
        Invalid = 0,
        Connection = 1,
        Session = 2,
        Operation = 3,
        EventPump = 4,
        Buffer = 5,
        SchemaRegistry = 6,
        CacheLease = 7,
        ObjectDescriptor = 8,
        CacheReferenceDescriptor = 9,
        TransportConnection = 10,
        TransportListener = 11,
        TransportSecurityConfig = 12,
        ServerAccept = 13,
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct NnrpHandle : IEquatable<NnrpHandle>
    {
        public NnrpHandle(NnrpHandleKind kind, ulong id, uint generation, uint flags = 0)
        {
            Kind = kind;
            Id = id;
            Generation = generation;
            Flags = flags;

            if (kind == NnrpHandleKind.Invalid)
            {
                if (id != 0 || generation != 0 || flags != 0)
                {
                    throw new ArgumentException("Invalid handles must use zero id, generation, and flags.");
                }

                return;
            }

            if (id == 0 || generation == 0)
            {
                throw new ArgumentException("Native handles require non-zero id and generation.");
            }
        }

        public readonly NnrpHandleKind Kind;

        public readonly ulong Id;

        public readonly uint Generation;

        public readonly uint Flags;

        public static NnrpHandle Invalid => new NnrpHandle(NnrpHandleKind.Invalid, 0, 0);

        public bool IsValid => Kind != NnrpHandleKind.Invalid;

        public void RequireKind(NnrpHandleKind expectedKind)
        {
            if (Kind != expectedKind)
            {
                throw new ArgumentException("Expected native handle kind " + expectedKind + ", got " + Kind + ".");
            }
        }

        public bool Equals(NnrpHandle other)
        {
            return Kind == other.Kind
                && Id == other.Id
                && Generation == other.Generation
                && Flags == other.Flags;
        }

        public override bool Equals(object? obj)
        {
            return obj is NnrpHandle other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Kind;
                hash = (hash * 397) ^ Id.GetHashCode();
                hash = (hash * 397) ^ Generation.GetHashCode();
                hash = (hash * 397) ^ Flags.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(NnrpHandle left, NnrpHandle right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(NnrpHandle left, NnrpHandle right)
        {
            return !left.Equals(right);
        }
    }

    public readonly struct NnrpConnectionHandle
    {
        public NnrpConnectionHandle(NnrpHandle handle)
        {
            handle.RequireKind(NnrpHandleKind.Connection);
            Handle = handle;
        }

        public NnrpHandle Handle { get; }
    }

    public readonly struct NnrpSessionHandle
    {
        public NnrpSessionHandle(NnrpHandle handle)
        {
            handle.RequireKind(NnrpHandleKind.Session);
            Handle = handle;
        }

        public NnrpHandle Handle { get; }
    }

    public readonly struct NnrpOperationHandle
    {
        public NnrpOperationHandle(NnrpHandle handle)
        {
            handle.RequireKind(NnrpHandleKind.Operation);
            Handle = handle;
        }

        public NnrpHandle Handle { get; }
    }

    public readonly struct NnrpEventPumpHandle
    {
        public NnrpEventPumpHandle(NnrpHandle handle)
        {
            handle.RequireKind(NnrpHandleKind.EventPump);
            Handle = handle;
        }

        public NnrpHandle Handle { get; }
    }

    public readonly struct NnrpBufferHandle
    {
        public NnrpBufferHandle(NnrpHandle handle)
        {
            handle.RequireKind(NnrpHandleKind.Buffer);
            Handle = handle;
        }

        public NnrpHandle Handle { get; }
    }

    public readonly struct NnrpSchemaRegistryHandle
    {
        public NnrpSchemaRegistryHandle(NnrpHandle handle)
        {
            handle.RequireKind(NnrpHandleKind.SchemaRegistry);
            Handle = handle;
        }

        public NnrpHandle Handle { get; }
    }

    public readonly struct NnrpCacheLeaseHandle
    {
        public NnrpCacheLeaseHandle(NnrpHandle handle)
        {
            handle.RequireKind(NnrpHandleKind.CacheLease);
            Handle = handle;
        }

        public NnrpHandle Handle { get; }
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct NnrpBufferView
    {
        public NnrpBufferView(IntPtr pointer, UIntPtr length)
        {
            if (length != UIntPtr.Zero && pointer == IntPtr.Zero)
            {
                throw new ArgumentException("Non-empty buffer views require a non-null pointer.", nameof(pointer));
            }

            Pointer = pointer;
            Length = length;
        }

        public readonly IntPtr Pointer;

        public readonly UIntPtr Length;

        public static NnrpBufferView Empty => new NnrpBufferView(IntPtr.Zero, UIntPtr.Zero);
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct NnrpMutableBufferView
    {
        public NnrpMutableBufferView(IntPtr pointer, UIntPtr length)
        {
            if (length != UIntPtr.Zero && pointer == IntPtr.Zero)
            {
                throw new ArgumentException("Non-empty mutable buffer views require a non-null pointer.", nameof(pointer));
            }

            Pointer = pointer;
            Length = length;
        }

        public readonly IntPtr Pointer;

        public readonly UIntPtr Length;

        public static NnrpMutableBufferView Empty => new NnrpMutableBufferView(IntPtr.Zero, UIntPtr.Zero);
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct NnrpSchemaDescriptorHeader
    {
        public NnrpSchemaDescriptorHeader(
            uint schemaId,
            uint schemaVersion,
            ushort profileId,
            ushort schemaFlags,
            byte minVersionMajor,
            byte maxVersionMajor,
            uint bodyBytes,
            ushort dependencyCount,
            ushort defaultStreamSemantics,
            ulong schemaHash)
        {
            SchemaId = schemaId;
            SchemaVersion = schemaVersion;
            ProfileId = profileId;
            SchemaFlags = schemaFlags;
            MinVersionMajor = minVersionMajor;
            MaxVersionMajor = maxVersionMajor;
            Reserved0 = 0;
            BodyBytes = bodyBytes;
            DependencyCount = dependencyCount;
            DefaultStreamSemantics = defaultStreamSemantics;
            SchemaHash = schemaHash;
        }

        public readonly uint SchemaId;

        public readonly uint SchemaVersion;

        public readonly ushort ProfileId;

        public readonly ushort SchemaFlags;

        public readonly byte MinVersionMajor;

        public readonly byte MaxVersionMajor;

        public readonly ushort Reserved0;

        public readonly uint BodyBytes;

        public readonly ushort DependencyCount;

        public readonly ushort DefaultStreamSemantics;

        public readonly ulong SchemaHash;
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct NnrpTypedPayloadDescriptor
    {
        public NnrpTypedPayloadDescriptor(
            ushort profileId,
            byte payloadKind,
            byte descriptorFlags,
            uint schemaId,
            uint schemaVersion,
            ushort streamSemantics,
            uint offset,
            uint length)
        {
            ProfileId = profileId;
            PayloadKind = payloadKind;
            DescriptorFlags = descriptorFlags;
            SchemaId = schemaId;
            SchemaVersion = schemaVersion;
            StreamSemantics = streamSemantics;
            Reserved0 = 0;
            Offset = offset;
            Length = length;
        }

        public readonly ushort ProfileId;

        public readonly byte PayloadKind;

        public readonly byte DescriptorFlags;

        public readonly uint SchemaId;

        public readonly uint SchemaVersion;

        public readonly ushort StreamSemantics;

        public readonly ushort Reserved0;

        public readonly uint Offset;

        public readonly uint Length;
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct NnrpSessionRecoveryOutcome
    {
        public NnrpSessionRecoveryOutcome(uint outcomeCode, uint resumeWindowMilliseconds)
        {
            OutcomeCode = outcomeCode;
            ResumeWindowMilliseconds = resumeWindowMilliseconds;
        }

        public readonly uint OutcomeCode;

        public readonly uint ResumeWindowMilliseconds;
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct NnrpSessionResumeRequest
    {
        public NnrpSessionResumeRequest(
            NnrpSessionOpenRequest open,
            NnrpBufferView recoveryTicket)
        {
            Open = open;
            RecoveryTicket = recoveryTicket;
        }

        public readonly NnrpSessionOpenRequest Open;

        public readonly NnrpBufferView RecoveryTicket;
    }

    public enum NnrpSchemaRegistryAction : uint
    {
        Installed = 0,
        AlreadyInstalled = 1,
        Updated = 2,
        Invalidated = 3,
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct NnrpCacheObjectId
    {
        public NnrpCacheObjectId(uint cacheNamespace, ulong cacheKeyHigh, ulong cacheKeyLow, uint objectKind)
        {
            CacheNamespace = cacheNamespace;
            ObjectKind = objectKind;
            CacheKeyHigh = cacheKeyHigh;
            CacheKeyLow = cacheKeyLow;
        }

        public readonly uint CacheNamespace;

        public readonly uint ObjectKind;

        public readonly ulong CacheKeyHigh;

        public readonly ulong CacheKeyLow;
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct NnrpCacheLeaseRequest
    {
        public NnrpCacheLeaseRequest(
            NnrpHandle owner,
            NnrpCacheObjectId objectId,
            ulong expectedVersion,
            ulong nowMilliseconds,
            uint ttlMilliseconds)
        {
            Owner = owner;
            ObjectId = objectId;
            ExpectedVersion = expectedVersion;
            NowMilliseconds = nowMilliseconds;
            TtlMilliseconds = ttlMilliseconds;
        }

        public readonly NnrpHandle Owner;

        public readonly NnrpCacheObjectId ObjectId;

        public readonly ulong ExpectedVersion;

        public readonly ulong NowMilliseconds;

        public readonly uint TtlMilliseconds;
    }

    public enum NnrpCacheLeaseOutcome : uint
    {
        Valid = 0,
        Miss = 1,
        Expired = 2,
        Released = 3,
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct NnrpCacheLeaseResult
    {
        public NnrpCacheLeaseResult(
            uint outcomeCode,
            NnrpHandle leaseHandle,
            NnrpCacheObjectId objectId,
            ulong objectVersion,
            ulong leaseId,
            uint ownerScope,
            uint ttlMilliseconds,
            ulong ownerId,
            ulong grantedAtMilliseconds)
        {
            OutcomeCode = outcomeCode;
            LeaseHandle = leaseHandle;
            ObjectId = objectId;
            ObjectVersion = objectVersion;
            LeaseId = leaseId;
            OwnerScope = ownerScope;
            TtlMilliseconds = ttlMilliseconds;
            OwnerId = ownerId;
            GrantedAtMilliseconds = grantedAtMilliseconds;
        }

        public readonly uint OutcomeCode;

        public readonly NnrpHandle LeaseHandle;

        public readonly NnrpCacheObjectId ObjectId;

        public readonly ulong ObjectVersion;

        public readonly ulong LeaseId;

        public readonly uint OwnerScope;

        public readonly uint TtlMilliseconds;

        public readonly ulong OwnerId;

        public readonly ulong GrantedAtMilliseconds;
    }

    public sealed class NnrpNativeSchemaRegistry : IDisposable
    {
        private NnrpNativeSchemaRegistry(
            NnrpNativeRuntimeEntrypoints entrypoints,
            NnrpSchemaRegistryHandle handle)
        {
            Entrypoints = entrypoints ?? throw new ArgumentNullException(nameof(entrypoints));
            Handle = handle;
        }

        public NnrpNativeRuntimeEntrypoints Entrypoints { get; }

        public NnrpSchemaRegistryHandle Handle { get; private set; }

        public bool IsReleased { get; private set; }

        public static NnrpNativeSchemaRegistry Create(NnrpNativeRuntimeEntrypoints entrypoints)
        {
            if (entrypoints == null)
            {
                throw new ArgumentNullException(nameof(entrypoints));
            }

            NnrpHandle registry;
            entrypoints.SchemaRegistryCreate(out registry).ThrowIfError();
            return new NnrpNativeSchemaRegistry(entrypoints, new NnrpSchemaRegistryHandle(registry));
        }

        public NnrpSchemaRegistryAction Install(NnrpSchemaDescriptorHeader descriptor)
        {
            EnsureOpen();
            uint action;
            Entrypoints.SchemaRegistryInstall(Handle.Handle, descriptor, out action).ThrowIfError();
            return (NnrpSchemaRegistryAction)action;
        }

        public NnrpSchemaDescriptorHeader Lookup(uint schemaId, uint schemaVersion)
        {
            EnsureOpen();
            NnrpSchemaDescriptorHeader descriptor;
            Entrypoints.SchemaRegistryLookup(Handle.Handle, schemaId, schemaVersion, out descriptor).ThrowIfError();
            return descriptor;
        }

        public NnrpSchemaRegistryAction Invalidate(uint schemaId, uint schemaVersion)
        {
            EnsureOpen();
            uint action;
            Entrypoints.SchemaRegistryInvalidate(Handle.Handle, schemaId, schemaVersion, out action).ThrowIfError();
            return (NnrpSchemaRegistryAction)action;
        }

        public void ValidateBinding(NnrpTypedPayloadDescriptor descriptor)
        {
            EnsureOpen();
            Entrypoints.SchemaRegistryValidateBinding(Handle.Handle, descriptor).ThrowIfError();
        }

        public void Release()
        {
            EnsureOpen();
            Entrypoints.SchemaRegistryRelease(Handle.Handle).ThrowIfError();
            IsReleased = true;
        }

        public void Dispose()
        {
            if (IsReleased)
            {
                return;
            }

            Release();
        }

        private void EnsureOpen()
        {
            if (IsReleased)
            {
                throw new NnrpNativeInvalidStateException(new NnrpFfiStatus(NnrpFfiStatusCode.InvalidState, NnrpErrorFamily.Schema));
            }
        }
    }

    public sealed class NnrpNativeSchemaDescriptors
    {
        public NnrpNativeSchemaDescriptors(NnrpNativeRuntimeEntrypoints entrypoints)
        {
            Entrypoints = entrypoints ?? throw new ArgumentNullException(nameof(entrypoints));
        }

        public NnrpNativeRuntimeEntrypoints Entrypoints { get; }

        public NnrpSchemaDescriptorHeader Parse(byte[] source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            GCHandle sourceHandle = default(GCHandle);
            try
            {
                var sourceView = NnrpBufferView.Empty;
                if (source.Length > 0)
                {
                    sourceHandle = GCHandle.Alloc(source, GCHandleType.Pinned);
                    sourceView = new NnrpBufferView(sourceHandle.AddrOfPinnedObject(), new UIntPtr((uint)source.Length));
                }

                NnrpSchemaDescriptorHeader descriptor;
                Entrypoints.SchemaDescriptorParse(sourceView, out descriptor).ThrowIfError();
                return descriptor;
            }
            finally
            {
                if (sourceHandle.IsAllocated)
                {
                    sourceHandle.Free();
                }
            }
        }

        public void Write(NnrpSchemaDescriptorHeader descriptor, byte[] destination)
        {
            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            GCHandle destinationHandle = default(GCHandle);
            try
            {
                var destinationView = NnrpMutableBufferView.Empty;
                if (destination.Length > 0)
                {
                    destinationHandle = GCHandle.Alloc(destination, GCHandleType.Pinned);
                    destinationView = new NnrpMutableBufferView(destinationHandle.AddrOfPinnedObject(), new UIntPtr((uint)destination.Length));
                }

                Entrypoints.SchemaDescriptorWrite(descriptor, destinationView).ThrowIfError();
            }
            finally
            {
                if (destinationHandle.IsAllocated)
                {
                    destinationHandle.Free();
                }
            }
        }

        public NnrpSchemaDescriptorHeader TokenDelta()
        {
            NnrpSchemaDescriptorHeader descriptor;
            Entrypoints.TokenDeltaSchemaDescriptor(out descriptor).ThrowIfError();
            return descriptor;
        }

        public void ValidateBinding(NnrpSchemaDescriptorHeader[] schemaDescriptors, NnrpTypedPayloadDescriptor descriptor)
        {
            if (schemaDescriptors == null)
            {
                throw new ArgumentNullException(nameof(schemaDescriptors));
            }

            GCHandle descriptorsHandle = default(GCHandle);
            try
            {
                var descriptors = IntPtr.Zero;
                if (schemaDescriptors.Length > 0)
                {
                    descriptorsHandle = GCHandle.Alloc(schemaDescriptors, GCHandleType.Pinned);
                    descriptors = descriptorsHandle.AddrOfPinnedObject();
                }

                Entrypoints.TypedPayloadValidateBinding(
                    descriptors,
                    new UIntPtr((uint)schemaDescriptors.Length),
                    descriptor).ThrowIfError();
            }
            finally
            {
                if (descriptorsHandle.IsAllocated)
                {
                    descriptorsHandle.Free();
                }
            }
        }
    }

    public sealed class NnrpNativeRecovery
    {
        public NnrpNativeRecovery(NnrpNativeRuntimeEntrypoints entrypoints)
        {
            Entrypoints = entrypoints ?? throw new ArgumentNullException(nameof(entrypoints));
        }

        public NnrpNativeRuntimeEntrypoints Entrypoints { get; }

        public void ValidateSessionRecoveryRequest(byte[] sessionOpenMetadata)
        {
            WithBufferView(sessionOpenMetadata, view => Entrypoints.SessionRecoveryRequestValidate(view).ThrowIfError());
        }

        public NnrpSessionRecoveryOutcome ValidateSessionRecoveryAck(byte[] sessionOpenMetadata, byte[] sessionOpenAckMetadata)
        {
            NnrpSessionRecoveryOutcome outcome = default(NnrpSessionRecoveryOutcome);
            WithBufferView(
                sessionOpenMetadata,
                openView => WithBufferView(
                    sessionOpenAckMetadata,
                    ackView =>
                    {
                        Entrypoints.SessionRecoveryAckValidate(openView, ackView, out outcome).ThrowIfError();
                    }));
            return outcome;
        }

        public void ValidateMigrationRecovery(byte[] sessionMigrateMetadata, byte[] sessionMigrateAckMetadata)
        {
            WithBufferView(
                sessionMigrateMetadata,
                migrateView => WithBufferView(
                    sessionMigrateAckMetadata,
                    ackView => Entrypoints.MigrationRecoveryValidate(migrateView, ackView).ThrowIfError()));
        }

        public bool ShouldReplayFrame(byte[] sessionMigrateAckMetadata, ulong frameId)
        {
            byte shouldReplay = 0;
            WithBufferView(
                sessionMigrateAckMetadata,
                ackView => Entrypoints.MigrationShouldReplayFrame(ackView, frameId, out shouldReplay).ThrowIfError());
            return shouldReplay != 0;
        }

        private static void WithBufferView(byte[] value, Action<NnrpBufferView> action)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            GCHandle handle = default(GCHandle);
            try
            {
                var view = NnrpBufferView.Empty;
                if (value.Length > 0)
                {
                    handle = GCHandle.Alloc(value, GCHandleType.Pinned);
                    view = new NnrpBufferView(handle.AddrOfPinnedObject(), new UIntPtr((uint)value.Length));
                }

                action(view);
            }
            finally
            {
                if (handle.IsAllocated)
                {
                    handle.Free();
                }
            }
        }
    }

    public sealed class NnrpNativeBuffer : IDisposable
    {
        internal NnrpNativeBuffer(NnrpNativeRuntimeEntrypoints entrypoints, NnrpBufferHandle handle, NnrpBufferView view)
        {
            Entrypoints = entrypoints ?? throw new ArgumentNullException(nameof(entrypoints));
            Handle = handle;
            View = view;
        }

        public NnrpNativeRuntimeEntrypoints Entrypoints { get; }

        public NnrpBufferHandle Handle { get; private set; }

        public NnrpBufferView View { get; private set; }

        public bool IsReleased { get; private set; }

        public void RefreshView()
        {
            EnsureOpen();
            NnrpBufferView view;
            Entrypoints.BufferView(Handle.Handle, out view).ThrowIfError();
            View = view;
        }

        public byte[] CopyToArray()
        {
            var view = BorrowView();
            if (view.Length == UIntPtr.Zero)
            {
                return Array.Empty<byte>();
            }

            var length = checked((int)view.Length.ToUInt64());
            var copy = new byte[length];
            Marshal.Copy(view.Pointer, copy, 0, length);
            return copy;
        }

        public NnrpBufferView BorrowView()
        {
            EnsureOpen();
            return View;
        }

        public void Release()
        {
            EnsureOpen();
            Entrypoints.BufferRelease(Handle.Handle).ThrowIfError();
            IsReleased = true;
        }

        public void Dispose()
        {
            if (IsReleased)
            {
                return;
            }

            Release();
        }

        private void EnsureOpen()
        {
            if (IsReleased)
            {
                throw new NnrpNativeInvalidStateException(new NnrpFfiStatus(NnrpFfiStatusCode.InvalidState));
            }
        }
    }

    public sealed class NnrpNativeBuffers
    {
        public NnrpNativeBuffers(NnrpNativeRuntimeEntrypoints entrypoints)
        {
            Entrypoints = entrypoints ?? throw new ArgumentNullException(nameof(entrypoints));
        }

        public NnrpNativeRuntimeEntrypoints Entrypoints { get; }

        public NnrpNativeBuffer AcquireCopy(byte[] source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            GCHandle sourceHandle = default(GCHandle);
            try
            {
                var sourceView = NnrpBufferView.Empty;
                if (source.Length > 0)
                {
                    sourceHandle = GCHandle.Alloc(source, GCHandleType.Pinned);
                    sourceView = new NnrpBufferView(sourceHandle.AddrOfPinnedObject(), new UIntPtr((uint)source.Length));
                }

                NnrpHandle buffer;
                NnrpBufferView view;
                Entrypoints.BufferAcquireCopy(sourceView, out buffer, out view).ThrowIfError();
                return new NnrpNativeBuffer(Entrypoints, new NnrpBufferHandle(buffer), view);
            }
            finally
            {
                if (sourceHandle.IsAllocated)
                {
                    sourceHandle.Free();
                }
            }
        }
    }

    public sealed class NnrpNativeCacheLeases
    {
        public NnrpNativeCacheLeases(NnrpNativeRuntimeEntrypoints entrypoints)
        {
            Entrypoints = entrypoints ?? throw new ArgumentNullException(nameof(entrypoints));
        }

        public NnrpNativeRuntimeEntrypoints Entrypoints { get; }

        public NnrpCacheLeaseResult Query(NnrpCacheLeaseRequest request)
        {
            NnrpCacheLeaseResult result;
            Entrypoints.CacheQuery(request, out result).ThrowIfError();
            return result;
        }

        public NnrpCacheLeaseResult Touch(NnrpCacheLeaseRequest request)
        {
            NnrpCacheLeaseResult result;
            Entrypoints.CacheTouch(request, out result).ThrowIfError();
            return result;
        }

        public NnrpCacheLeaseResult[] Prefetch(NnrpHandle owner, NnrpCacheObjectId[] objects, ulong nowMilliseconds, uint ttlMilliseconds)
        {
            if (objects == null)
            {
                throw new ArgumentNullException(nameof(objects));
            }

            if (objects.Length == 0)
            {
                return Array.Empty<NnrpCacheLeaseResult>();
            }

            var results = new NnrpCacheLeaseResult[objects.Length];
            var objectHandle = GCHandle.Alloc(objects, GCHandleType.Pinned);
            var resultHandle = GCHandle.Alloc(results, GCHandleType.Pinned);
            try
            {
                Entrypoints.CachePrefetch(
                    owner,
                    objectHandle.AddrOfPinnedObject(),
                    new UIntPtr((uint)objects.Length),
                    nowMilliseconds,
                    ttlMilliseconds,
                    resultHandle.AddrOfPinnedObject()).ThrowIfError();
            }
            finally
            {
                resultHandle.Free();
                objectHandle.Free();
            }

            return results;
        }

        public NnrpCacheLeaseResult Release(NnrpCacheLeaseHandle lease)
        {
            NnrpCacheLeaseResult result;
            Entrypoints.CacheRelease(lease.Handle, out result).ThrowIfError();
            return result;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct NnrpFfiDiagnostic
    {
        public NnrpFfiDiagnostic(
            NnrpFfiStatus status,
            ulong relatedConnectionId = 0,
            uint relatedSessionId = 0,
            ulong relatedOperationId = 0,
            uint relatedFrameId = 0)
        {
            Status = status;
            RelatedConnectionId = relatedConnectionId;
            RelatedSessionId = relatedSessionId;
            RelatedOperationId = relatedOperationId;
            RelatedFrameId = relatedFrameId;
        }

        public readonly NnrpFfiStatus Status;

        public readonly ulong RelatedConnectionId;

        public readonly uint RelatedSessionId;

        public readonly ulong RelatedOperationId;

        public readonly uint RelatedFrameId;
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct NnrpFfiRuntimeFrameHeader
    {
        public NnrpFfiRuntimeFrameHeader(
            byte messageType,
            uint frameId,
            byte present = 1,
            byte versionMajor = 1,
            byte wireFormat = 0,
            uint flags = 0,
            uint sessionId = 0,
            ushort viewId = 0,
            ushort routeId = 0,
            ulong traceId = 0)
        {
            Present = present;
            VersionMajor = versionMajor;
            WireFormat = wireFormat;
            MessageType = messageType;
            Flags = flags;
            SessionId = sessionId;
            FrameId = frameId;
            ViewId = viewId;
            RouteId = routeId;
            TraceId = traceId;
        }

        public readonly byte Present;

        public readonly byte VersionMajor;

        public readonly byte WireFormat;

        public readonly byte MessageType;

        public readonly uint Flags;

        public readonly uint SessionId;

        public readonly uint FrameId;

        public readonly ushort ViewId;

        public readonly ushort RouteId;

        public readonly ulong TraceId;
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct NnrpEvent
    {
        public NnrpEvent(
            uint kind,
            uint messageType,
            NnrpHandle connection,
            NnrpHandle session,
            NnrpHandle operation,
            uint frameId,
            NnrpHandle payloadOwner,
            NnrpBufferView payload,
            NnrpFfiDiagnostic diagnostic)
            : this(
                kind,
                new NnrpFfiRuntimeFrameHeader(checked((byte)messageType), frameId),
                connection,
                session,
                operation,
                payloadOwner,
                payload,
                diagnostic)
        {
        }

        internal NnrpEvent(
            uint kind,
            NnrpFfiRuntimeFrameHeader header,
            NnrpHandle connection,
            NnrpHandle session,
            NnrpHandle operation,
            NnrpHandle payloadOwner,
            NnrpBufferView payload,
            NnrpFfiDiagnostic diagnostic)
        {
            Kind = kind;
            Header = header;
            Connection = connection;
            Session = session;
            Operation = operation;
            PayloadOwner = payloadOwner;
            Payload = payload;
            Diagnostic = diagnostic;
        }

        public readonly uint Kind;

        public readonly NnrpFfiRuntimeFrameHeader Header;

        public readonly NnrpHandle Connection;

        public readonly NnrpHandle Session;

        public readonly NnrpHandle Operation;

        public readonly NnrpHandle PayloadOwner;

        public readonly NnrpBufferView Payload;

        public readonly NnrpFfiDiagnostic Diagnostic;
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct NnrpCallbackSink
    {
        public static NnrpCallbackSink None => new NnrpCallbackSink(IntPtr.Zero, IntPtr.Zero);

        public NnrpCallbackSink(IntPtr userData, IntPtr onEvent)
        {
            UserData = userData;
            OnEvent = onEvent;
        }

        public readonly IntPtr UserData;

        public readonly IntPtr OnEvent;

        public bool IsEmpty => UserData == IntPtr.Zero && OnEvent == IntPtr.Zero;

        public bool HasDispatcher => OnEvent != IntPtr.Zero;

        public static NnrpCallbackSink Create(IntPtr userData, IntPtr onEvent)
        {
            if (onEvent == IntPtr.Zero)
            {
                throw new ArgumentException("Callback sinks must carry a non-empty dispatcher pointer.", nameof(onEvent));
            }

            return new NnrpCallbackSink(userData, onEvent);
        }

        public void EnsureDispatchable()
        {
            if (!HasDispatcher)
            {
                throw new InvalidOperationException("Callback sink is not bound to a dispatcher.");
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct NnrpPollResult
    {
        public NnrpPollResult(NnrpFfiStatus status, byte hasEvent, NnrpEvent @event)
        {
            Status = status;
            HasEvent = hasEvent;
            Event = @event;
        }

        public readonly NnrpFfiStatus Status;

        public readonly byte HasEvent;

        public readonly NnrpEvent Event;
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct NnrpConnectionBootstrap
    {
        public NnrpConnectionBootstrap(ulong connectionId, uint generation, uint transportId)
        {
            ConnectionId = connectionId;
            Generation = generation;
            TransportId = transportId;
        }

        public readonly ulong ConnectionId;

        public readonly uint Generation;

        public readonly uint TransportId;
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct NnrpClientConnectRequest
    {
        public NnrpClientConnectRequest(ulong connectionId, uint generation, NnrpHandle transportConnection)
        {
            transportConnection.RequireKind(NnrpHandleKind.TransportConnection);
            ConnectionId = connectionId;
            Generation = generation;
            Reserved0 = 0;
            TransportConnection = transportConnection;
        }

        public readonly ulong ConnectionId;

        public readonly uint Generation;

        public readonly uint Reserved0;

        public readonly NnrpHandle TransportConnection;
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct NnrpU16Slice
    {
        public NnrpU16Slice(IntPtr pointer, UIntPtr length)
        {
            Pointer = pointer;
            Length = length;
        }

        public readonly IntPtr Pointer;

        public readonly UIntPtr Length;

        public static NnrpU16Slice Empty => new NnrpU16Slice(IntPtr.Zero, UIntPtr.Zero);
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct NnrpU32Slice
    {
        public NnrpU32Slice(IntPtr pointer, UIntPtr length)
        {
            Pointer = pointer;
            Length = length;
        }

        public readonly IntPtr Pointer;

        public readonly UIntPtr Length;

        public static NnrpU32Slice Empty => new NnrpU32Slice(IntPtr.Zero, UIntPtr.Zero);
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate uint NnrpServerPolicyBeginCallback(
        IntPtr userData,
        ulong requestId,
        NnrpBufferView metadata);

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct NnrpServerPolicySink
    {
        public NnrpServerPolicySink(IntPtr userData, NnrpServerPolicyBeginCallback? begin)
        {
            UserData = userData;
            Begin = begin;
        }

        public readonly IntPtr UserData;

        public readonly NnrpServerPolicyBeginCallback? Begin;

        public static NnrpServerPolicySink AllowAll => new NnrpServerPolicySink(IntPtr.Zero, null);
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct NnrpServerPolicyDecision
    {
        public NnrpServerPolicyDecision(byte accepted, uint sessionErrorCode, NnrpBufferView diagnostic)
        {
            Accepted = accepted;
            Reserved0 = 0;
            Reserved1 = 0;
            Reserved2 = 0;
            SessionErrorCode = sessionErrorCode;
            Diagnostic = diagnostic;
        }

        public readonly byte Accepted;

        public readonly byte Reserved0;

        public readonly byte Reserved1;

        public readonly byte Reserved2;

        public readonly uint SessionErrorCode;

        public readonly NnrpBufferView Diagnostic;
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct NnrpServerPolicyCompleteRequest
    {
        public NnrpServerPolicyCompleteRequest(ulong requestId, NnrpServerPolicyDecision decision)
        {
            RequestId = requestId;
            Decision = decision;
        }

        public readonly ulong RequestId;

        public readonly NnrpServerPolicyDecision Decision;
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct NnrpServerBindRequest
    {
        public NnrpServerBindRequest(
            ulong serverId,
            uint generation,
            NnrpHandle transportListener,
            NnrpU16Slice supportedProfiles,
            NnrpU32Slice supportedCacheObjects,
            ulong maxCacheObjects,
            uint maxCacheObjectBytes,
            uint resumeTokenBytes,
            ushort maxInFlightOperations,
            ushort grantedOperationCredit,
            uint leaseTtlMilliseconds,
            uint resumeWindowMilliseconds,
            NnrpHandle schemaRegistry,
            NnrpServerPolicySink applicationPolicy)
        {
            transportListener.RequireKind(NnrpHandleKind.TransportListener);
            if (schemaRegistry.IsValid)
            {
                schemaRegistry.RequireKind(NnrpHandleKind.SchemaRegistry);
            }

            ServerId = serverId;
            Generation = generation;
            Reserved0 = 0;
            TransportListener = transportListener;
            SupportedProfiles = supportedProfiles;
            SupportedCacheObjects = supportedCacheObjects;
            MaxCacheObjects = maxCacheObjects;
            MaxCacheObjectBytes = maxCacheObjectBytes;
            ResumeTokenBytes = resumeTokenBytes;
            MaxInFlightOperations = maxInFlightOperations;
            GrantedOperationCredit = grantedOperationCredit;
            LeaseTtlMilliseconds = leaseTtlMilliseconds;
            ResumeWindowMilliseconds = resumeWindowMilliseconds;
            SchemaRegistry = schemaRegistry;
            ApplicationPolicy = applicationPolicy;
        }

        public readonly ulong ServerId;

        public readonly uint Generation;

        public readonly uint Reserved0;

        public readonly NnrpHandle TransportListener;

        public readonly NnrpU16Slice SupportedProfiles;

        public readonly NnrpU32Slice SupportedCacheObjects;

        public readonly ulong MaxCacheObjects;

        public readonly uint MaxCacheObjectBytes;

        public readonly uint ResumeTokenBytes;

        public readonly ushort MaxInFlightOperations;

        public readonly ushort GrantedOperationCredit;

        public readonly uint LeaseTtlMilliseconds;

        public readonly uint ResumeWindowMilliseconds;

        public readonly NnrpHandle SchemaRegistry;

        public readonly NnrpServerPolicySink ApplicationPolicy;
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct NnrpTransportOpenRequest
    {
        public NnrpTransportOpenRequest(
            TransportId transportId,
            NnrpBufferView endpoint,
            NnrpHandle config,
            ulong maxPacketBytes,
            uint timeoutMilliseconds)
        {
            if (transportId == Nnrp.Core.TransportId.Unspecified
                || !Enum.IsDefined(typeof(Nnrp.Core.TransportId), transportId))
            {
                throw new ArgumentOutOfRangeException(nameof(transportId));
            }

            if (maxPacketBytes == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxPacketBytes));
            }

            if (config.IsValid)
            {
                config.RequireKind(NnrpHandleKind.TransportSecurityConfig);
            }

            TransportId = (uint)transportId;
            Flags = 0;
            Endpoint = endpoint;
            Config = config;
            MaxPacketBytes = maxPacketBytes;
            TimeoutMilliseconds = timeoutMilliseconds;
            Reserved0 = 0;
        }

        public readonly uint TransportId;

        public readonly uint Flags;

        public readonly NnrpBufferView Endpoint;

        public readonly NnrpHandle Config;

        public readonly ulong MaxPacketBytes;

        public readonly uint TimeoutMilliseconds;

        public readonly uint Reserved0;
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct NnrpTransportAcceptRequest
    {
        public NnrpTransportAcceptRequest(NnrpHandle listener, uint timeoutMilliseconds)
        {
            listener.RequireKind(NnrpHandleKind.TransportListener);
            Listener = listener;
            TimeoutMilliseconds = timeoutMilliseconds;
            Reserved0 = 0;
        }

        public readonly NnrpHandle Listener;

        public readonly uint TimeoutMilliseconds;

        public readonly uint Reserved0;
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct NnrpTransportProbeRequest
    {
        public NnrpTransportProbeRequest(
            NnrpTransportOpenRequest open,
            uint sampleCount,
            uint probePayloadBytes)
        {
            if (sampleCount == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sampleCount));
            }

            if (probePayloadBytes == 0 || probePayloadBytes > open.MaxPacketBytes)
            {
                throw new ArgumentOutOfRangeException(nameof(probePayloadBytes));
            }

            Open = open;
            SampleCount = sampleCount;
            ProbePayloadBytes = probePayloadBytes;
        }

        public readonly NnrpTransportOpenRequest Open;

        public readonly uint SampleCount;

        public readonly uint ProbePayloadBytes;
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct NnrpTransportProbeResult
    {
        public NnrpTransportProbeResult(
            uint sampleCount,
            uint successCount,
            ulong medianThroughputBytesPerSecond,
            ulong medianRttMicroseconds)
        {
            SampleCount = sampleCount;
            SuccessCount = successCount;
            MedianThroughputBytesPerSecond = medianThroughputBytesPerSecond;
            MedianRttMicroseconds = medianRttMicroseconds;
        }

        public readonly uint SampleCount;

        public readonly uint SuccessCount;

        public readonly ulong MedianThroughputBytesPerSecond;

        public readonly ulong MedianRttMicroseconds;
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct NnrpTransportClientSecurityConfigRequest
    {
        public NnrpTransportClientSecurityConfigRequest(
            TransportId transportId,
            NnrpBufferView serverName,
            NnrpBufferView trustedCertificateDer)
        {
            TransportId = (uint)transportId;
            Flags = 0;
            ServerName = serverName;
            TrustedCertificateDer = trustedCertificateDer;
        }

        public readonly uint TransportId;

        public readonly uint Flags;

        public readonly NnrpBufferView ServerName;

        public readonly NnrpBufferView TrustedCertificateDer;
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct NnrpTransportServerSecurityConfigRequest
    {
        public NnrpTransportServerSecurityConfigRequest(
            TransportId transportId,
            NnrpBufferView certificateDer,
            NnrpBufferView privateKeyPkcs8Der)
        {
            TransportId = (uint)transportId;
            Flags = 0;
            CertificateDer = certificateDer;
            PrivateKeyPkcs8Der = privateKeyPkcs8Der;
        }

        public readonly uint TransportId;

        public readonly uint Flags;

        public readonly NnrpBufferView CertificateDer;

        public readonly NnrpBufferView PrivateKeyPkcs8Der;
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct NnrpSessionOpenRequest
    {
        public NnrpSessionOpenRequest(
            NnrpHandle connection,
            uint requestedSessionId,
            ulong sessionHandleId,
            uint generation,
            ushort profileId,
            SessionPriorityClass priorityClass,
            bool allowResume,
            uint schemaId,
            uint schemaVersion,
            uint defaultDeadlineMilliseconds,
            ushort maxInFlightOperations,
            uint leaseTtlHintMilliseconds,
            uint resumeTokenBytes,
            NnrpU32Slice cacheHints)
        {
            Connection = connection;
            RequestedSessionId = requestedSessionId;
            SessionHandleId = sessionHandleId;
            Generation = generation;
            ProfileId = profileId;
            PriorityClass = (byte)priorityClass;
            AllowResume = allowResume ? (byte)1 : (byte)0;
            SchemaId = schemaId;
            SchemaVersion = schemaVersion;
            DefaultDeadlineMilliseconds = defaultDeadlineMilliseconds;
            MaxInFlightOperations = maxInFlightOperations;
            Reserved0 = 0;
            LeaseTtlHintMilliseconds = leaseTtlHintMilliseconds;
            ResumeTokenBytes = resumeTokenBytes;
            CacheHints = cacheHints;
        }

        public readonly NnrpHandle Connection;

        public readonly uint RequestedSessionId;

        public readonly ulong SessionHandleId;

        public readonly uint Generation;

        public readonly ushort ProfileId;

        public readonly byte PriorityClass;

        public readonly byte AllowResume;

        public readonly uint SchemaId;

        public readonly uint SchemaVersion;

        public readonly uint DefaultDeadlineMilliseconds;

        public readonly ushort MaxInFlightOperations;

        public readonly ushort Reserved0;

        public readonly uint LeaseTtlHintMilliseconds;

        public readonly uint ResumeTokenBytes;

        public readonly NnrpU32Slice CacheHints;
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct NnrpFfiSubmitRequest
    {
        public NnrpFfiSubmitRequest(
            NnrpHandle session,
            ulong operationId,
            uint frameId,
            uint headerFlags,
            ushort viewId,
            ushort routeId,
            ulong traceId,
            NnrpBufferView payload)
        {
            Session = session;
            OperationId = operationId;
            FrameId = frameId;
            HeaderFlags = headerFlags;
            ViewId = viewId;
            RouteId = routeId;
            TraceId = traceId;
            Payload = payload;
        }

        public readonly NnrpHandle Session;

        public readonly ulong OperationId;

        public readonly uint FrameId;

        public readonly uint HeaderFlags;

        public readonly ushort ViewId;

        public readonly ushort RouteId;

        public readonly ulong TraceId;

        public readonly NnrpBufferView Payload;
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct NnrpCompactResult
    {
        public NnrpCompactResult(
            NnrpFfiStatus status,
            byte hasResult,
            uint eventKind,
            uint resultState,
            NnrpHandle operation,
            ulong operationId,
            uint frameId,
            NnrpBufferView payload,
            NnrpFfiDiagnostic diagnostic)
        {
            Status = status;
            HasResult = hasResult;
            EventKind = eventKind;
            ResultState = resultState;
            Operation = operation;
            OperationId = operationId;
            FrameId = frameId;
            Payload = payload;
            Diagnostic = diagnostic;
        }

        public readonly NnrpFfiStatus Status;

        public readonly byte HasResult;

        public readonly uint EventKind;

        public readonly uint ResultState;

        public readonly NnrpHandle Operation;

        public readonly ulong OperationId;

        public readonly uint FrameId;

        public readonly NnrpBufferView Payload;

        public readonly NnrpFfiDiagnostic Diagnostic;
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct NnrpClientSubmitResultBatchRequest
    {
        public NnrpClientSubmitResultBatchRequest(
            NnrpHandle session,
            ulong operationIdStart,
            uint frameIdStart,
            uint frameIdStride,
            NnrpBufferView submitPayload,
            NnrpBufferView resultPayload,
            UIntPtr maxEvents,
            UIntPtr iterations)
        {
            Session = session;
            OperationIdStart = operationIdStart;
            FrameIdStart = frameIdStart;
            FrameIdStride = frameIdStride;
            SubmitPayload = submitPayload;
            ResultPayload = resultPayload;
            MaxEvents = maxEvents;
            Iterations = iterations;
        }

        public readonly NnrpHandle Session;

        public readonly ulong OperationIdStart;

        public readonly uint FrameIdStart;

        public readonly uint FrameIdStride;

        public readonly NnrpBufferView SubmitPayload;

        public readonly NnrpBufferView ResultPayload;

        public readonly UIntPtr MaxEvents;

        public readonly UIntPtr Iterations;
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct NnrpClientCancelRequest
    {
        public NnrpClientCancelRequest(NnrpHandle session, uint frameId)
        {
            Session = session;
            FrameId = frameId;
        }

        public readonly NnrpHandle Session;

        public readonly uint FrameId;
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct NnrpServerAcceptBeginRequest
    {
        public NnrpServerAcceptBeginRequest(
            NnrpHandle server,
            ulong acceptHandleId,
            uint generation)
        {
            Server = server;
            AcceptHandleId = acceptHandleId;
            Generation = generation;
            Reserved0 = 0;
        }

        public readonly NnrpHandle Server;

        public readonly ulong AcceptHandleId;

        public readonly uint Generation;

        public readonly uint Reserved0;
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct NnrpServerAcceptWaitRequest
    {
        public NnrpServerAcceptWaitRequest(NnrpHandle accept, uint timeoutMilliseconds)
        {
            Accept = accept;
            TimeoutMilliseconds = timeoutMilliseconds;
            Flags = 0;
        }

        public readonly NnrpHandle Accept;

        public readonly uint TimeoutMilliseconds;

        public readonly uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct NnrpServerAcceptClaimRequest
    {
        public NnrpServerAcceptClaimRequest(
            NnrpHandle accept,
            ulong sessionHandleId,
            uint generation)
        {
            Accept = accept;
            SessionHandleId = sessionHandleId;
            Generation = generation;
            Reserved0 = 0;
        }

        public readonly NnrpHandle Accept;

        public readonly ulong SessionHandleId;

        public readonly uint Generation;

        public readonly uint Reserved0;
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct NnrpServerAcceptResult
    {
        public NnrpServerAcceptResult(NnrpHandle session, uint activeTransportId)
        {
            Session = session;
            ActiveTransportId = activeTransportId;
            Reserved0 = 0;
        }

        public readonly NnrpHandle Session;

        public readonly uint ActiveTransportId;

        public readonly uint Reserved0;
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct NnrpRoleEventPollRequest
    {
        public NnrpRoleEventPollRequest(
            NnrpHandle scope,
            uint maxEvents,
            uint timeoutMilliseconds)
        {
            Scope = scope;
            MaxEvents = maxEvents;
            TimeoutMilliseconds = timeoutMilliseconds;
            Flags = 0;
            Reserved0 = 0;
        }

        public readonly NnrpHandle Scope;

        public readonly uint MaxEvents;

        public readonly uint TimeoutMilliseconds;

        public readonly uint Flags;

        public readonly uint Reserved0;
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct NnrpServerReceiveSubmitRequest
    {
        public NnrpServerReceiveSubmitRequest(NnrpHandle session, ulong operationId, uint frameId, NnrpBufferView payload)
        {
            Session = session;
            OperationId = operationId;
            FrameId = frameId;
            Payload = payload;
        }

        public readonly NnrpHandle Session;

        public readonly ulong OperationId;

        public readonly uint FrameId;

        public readonly NnrpBufferView Payload;
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct NnrpServerSendResultRequest
    {
        public NnrpServerSendResultRequest(NnrpHandle operation, NnrpBufferView payload)
        {
            Operation = operation;
            Payload = payload;
        }

        public readonly NnrpHandle Operation;

        public readonly NnrpBufferView Payload;
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct NnrpResultDropReasonDescriptor
    {
        public NnrpResultDropReasonDescriptor(ResultDropReasonMetadata metadata)
        {
            OperationId = metadata.OperationId;
            ResultSequence = metadata.ResultSequence;
            DropReasonCode = (ushort)metadata.DropReasonCode;
            SourceRole = (byte)metadata.SourceRole;
            Flags = metadata.Flags;
            DiagnosticBytes = metadata.DiagnosticBytes;
        }

        public readonly ulong OperationId;
        public readonly ulong ResultSequence;
        public readonly ushort DropReasonCode;
        public readonly byte SourceRole;
        public readonly byte Flags;
        public readonly uint DiagnosticBytes;
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct NnrpServerDropStaleResultRequest
    {
        public NnrpServerDropStaleResultRequest(
            NnrpHandle operation,
            ResultDropReasonMetadata metadata,
            NnrpBufferView diagnostics,
            UIntPtr maxEvents)
        {
            Operation = operation;
            DropReason = new NnrpResultDropReasonDescriptor(metadata);
            Diagnostics = diagnostics;
            MaxEvents = maxEvents;
        }

        public readonly NnrpHandle Operation;
        public readonly NnrpResultDropReasonDescriptor DropReason;
        public readonly NnrpBufferView Diagnostics;
        public readonly UIntPtr MaxEvents;
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct NnrpServerFlowUpdateRequest
    {
        public NnrpServerFlowUpdateRequest(NnrpHandle session, uint frameId)
        {
            Session = session;
            FrameId = frameId;
        }

        public readonly NnrpHandle Session;

        public readonly uint FrameId;
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct NnrpControlRequest
    {
        public NnrpControlRequest(NnrpHandle handle, uint controlCode, NnrpBufferView payload)
        {
            Handle = handle;
            ControlCode = controlCode;
            Payload = payload;
        }

        public readonly NnrpHandle Handle;

        public readonly uint ControlCode;

        public readonly NnrpBufferView Payload;
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct NnrpRuntimeFrameSendRequest
    {
        public NnrpRuntimeFrameSendRequest(
            NnrpHandle handle,
            uint messageType,
            uint frameId,
            NnrpBufferView payload)
        {
            Handle = handle;
            MessageType = messageType;
            FrameId = frameId;
            Payload = payload;
        }

        public readonly NnrpHandle Handle;

        public readonly uint MessageType;

        public readonly uint FrameId;

        public readonly NnrpBufferView Payload;
    }

    public sealed class NnrpNativeRuntimeEntrypoints : IDisposable
    {
        private static readonly object PinnedLibraryGate = new object();
        private static readonly Dictionary<string, IntPtr> PinnedLibraries = new Dictionary<string, IntPtr>(
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? StringComparer.OrdinalIgnoreCase
                : StringComparer.Ordinal);
        private static readonly Dictionary<string, RuntimeShutdownInvoker> PinnedRuntimeShutdowns =
            new Dictionary<string, RuntimeShutdownInvoker>(
                RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    ? StringComparer.OrdinalIgnoreCase
                    : StringComparer.Ordinal);

        [ExcludeFromCodeCoverage]
        static NnrpNativeRuntimeEntrypoints()
        {
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
        }

        [ExcludeFromCodeCoverage]
        private static void OnProcessExit(object sender, EventArgs eventArgs)
        {
            ShutdownPinnedTransportRuntimes();
        }

        public NnrpNativeRuntimeEntrypoints(
            CurrentProtocolVersionInvoker currentProtocolVersion,
            NnrpNativeArtifact.RuntimeCapabilitiesInvoker runtimeCapabilities,
            ConnectionBootstrapInvoker connectionBootstrap,
            ClientConnectInvoker clientConnect,
            SessionOpenInvoker sessionOpen,
            SessionOpenInvoker clientOpenSession,
            SubmitInvoker submit,
            SubmitInvoker clientSubmit,
            HandleStatusInvoker sessionClose,
            HandleStatusInvoker clientClose,
            ClientCancelInvoker clientCancel,
            AwaitEventInvoker clientAwaitEvent,
            ServerBindInvoker serverBind,
            ServerAcceptBeginInvoker serverAcceptBegin,
            ServerAcceptWaitInvoker serverAcceptWait,
            ServerAcceptClaimInvoker serverAcceptClaim,
            HandleStatusInvoker serverAcceptRelease,
            ServerReceiveSubmitInvoker serverReceiveSubmit,
            ServerSendResultInvoker serverSendResult,
            ServerFlowUpdateInvoker serverSendFlowUpdate,
            HandleStatusInvoker serverClose,
            ControlInvoker control,
            PollEmptyInvoker pollEmpty,
            DispatchEventInvoker dispatchEvent,
            HandleStatusInvoker? connectionClose = null,
            HandleStatusInvoker? clientCloseConnection = null,
            SchemaRegistryCreateInvoker? schemaRegistryCreate = null,
            SchemaRegistryInstallInvoker? schemaRegistryInstall = null,
            SchemaRegistryLookupInvoker? schemaRegistryLookup = null,
            SchemaRegistryInvalidateInvoker? schemaRegistryInvalidate = null,
            SchemaRegistryValidateBindingInvoker? schemaRegistryValidateBinding = null,
            HandleStatusInvoker? schemaRegistryRelease = null,
            ClientResumeSessionInvoker? clientResumeSession = null,
            SchemaDescriptorParseInvoker? schemaDescriptorParse = null,
            SchemaDescriptorWriteInvoker? schemaDescriptorWrite = null,
            TokenDeltaSchemaDescriptorInvoker? tokenDeltaSchemaDescriptor = null,
            TypedPayloadValidateBindingInvoker? typedPayloadValidateBinding = null,
            RecoveryRequestValidateInvoker? sessionRecoveryRequestValidate = null,
            RecoveryAckValidateInvoker? sessionRecoveryAckValidate = null,
            MigrationRecoveryValidateInvoker? migrationRecoveryValidate = null,
            MigrationShouldReplayFrameInvoker? migrationShouldReplayFrame = null,
            BufferAcquireCopyInvoker? bufferAcquireCopy = null,
            BufferViewInvoker? bufferView = null,
            HandleStatusInvoker? bufferRelease = null,
            CacheLeaseRequestInvoker? cacheQuery = null,
            CacheLeaseRequestInvoker? cacheTouch = null,
            CachePrefetchInvoker? cachePrefetch = null,
            CacheReleaseInvoker? cacheRelease = null,
            ClientSubmitResultCompactBatchInvoker? clientSubmitResultCompactBatch = null,
            RuntimeFrameSendInvoker? runtimeFrameSend = null,
            BufferAcquireCopyInvoker? objectMetadataBufferAcquireCopy = null,
            BufferViewInvoker? objectMetadataBufferView = null,
            HandleStatusInvoker? objectMetadataBufferRelease = null,
            ObjectDescriptorCreateInvoker? objectDescriptorCreate = null,
            ObjectDescriptorViewInvoker? objectDescriptorView = null,
            DescriptorMetadataSnapshotInvoker? objectDescriptorMetadataSnapshot = null,
            HandleStatusInvoker? objectDescriptorRelease = null,
            CacheReferenceDescriptorCreateInvoker? cacheReferenceDescriptorCreate = null,
            CacheReferenceDescriptorViewInvoker? cacheReferenceDescriptorView = null,
            DescriptorMetadataSnapshotInvoker? cacheReferenceDescriptorMetadataSnapshot = null,
            HandleStatusInvoker? cacheReferenceDescriptorRelease = null,
            TransportSecurityConfigCreateInvoker? transportClientSecurityConfigCreate = null,
            TransportServerSecurityConfigCreateInvoker? transportServerSecurityConfigCreate = null,
            TransportOpenInvoker? transportConnect = null,
            TransportOpenInvoker? transportListen = null,
            TransportAcceptInvoker? transportAccept = null,
            TransportListenerEndpointInvoker? transportListenerEndpoint = null,
            TransportProbeInvoker? transportProbe = null,
            HandleStatusInvoker? transportClose = null,
            RoleAwaitEventsInvoker? serverAwaitEvents = null,
            ServerDropStaleResultInvoker? serverDropStaleResult = null,
            RoleAwaitEventsInvoker? clientAwaitEvents = null,
            ClientSessionRecoveryTicketInvoker? clientSessionRecoveryTicket = null,
            SessionIdInvoker? sessionId = null,
            ServerPolicyCompleteInvoker? serverPolicyComplete = null)
            : this(
                IntPtr.Zero,
                currentProtocolVersion,
                runtimeCapabilities,
                connectionBootstrap,
                clientConnect,
                sessionOpen,
                clientOpenSession,
                submit,
                clientSubmit,
                sessionClose,
                clientClose,
                clientCancel,
                clientAwaitEvent,
                serverBind,
                serverAcceptBegin,
                serverAcceptWait,
                serverAcceptClaim,
                serverAcceptRelease,
                serverReceiveSubmit,
                serverSendResult,
                serverSendFlowUpdate,
                serverClose,
                control,
                pollEmpty,
                dispatchEvent,
                connectionClose,
                clientCloseConnection,
                schemaRegistryCreate,
                schemaRegistryInstall,
                schemaRegistryLookup,
                schemaRegistryInvalidate,
                schemaRegistryValidateBinding,
                schemaRegistryRelease,
                clientResumeSession,
                schemaDescriptorParse,
                schemaDescriptorWrite,
                tokenDeltaSchemaDescriptor,
                typedPayloadValidateBinding,
                sessionRecoveryRequestValidate,
                sessionRecoveryAckValidate,
                migrationRecoveryValidate,
                migrationShouldReplayFrame,
                bufferAcquireCopy,
                bufferView,
                bufferRelease,
                cacheQuery,
                cacheTouch,
                cachePrefetch,
                cacheRelease,
                clientSubmitResultCompactBatch,
                runtimeFrameSend,
                objectMetadataBufferAcquireCopy,
                objectMetadataBufferView,
                objectMetadataBufferRelease,
                objectDescriptorCreate,
                objectDescriptorView,
                objectDescriptorMetadataSnapshot,
                objectDescriptorRelease,
                cacheReferenceDescriptorCreate,
                cacheReferenceDescriptorView,
                cacheReferenceDescriptorMetadataSnapshot,
                cacheReferenceDescriptorRelease,
                transportClientSecurityConfigCreate,
                transportServerSecurityConfigCreate,
                transportConnect,
                transportListen,
                transportAccept,
                transportListenerEndpoint,
                transportProbe,
                transportClose,
                serverAwaitEvents,
                serverDropStaleResult,
                clientAwaitEvents,
                clientSessionRecoveryTicket,
                sessionId,
                serverPolicyComplete)
        {
        }

        private NnrpNativeRuntimeEntrypoints(
            IntPtr libraryHandle,
            CurrentProtocolVersionInvoker currentProtocolVersion,
            NnrpNativeArtifact.RuntimeCapabilitiesInvoker runtimeCapabilities,
            ConnectionBootstrapInvoker connectionBootstrap,
            ClientConnectInvoker clientConnect,
            SessionOpenInvoker sessionOpen,
            SessionOpenInvoker clientOpenSession,
            SubmitInvoker submit,
            SubmitInvoker clientSubmit,
            HandleStatusInvoker sessionClose,
            HandleStatusInvoker clientClose,
            ClientCancelInvoker clientCancel,
            AwaitEventInvoker clientAwaitEvent,
            ServerBindInvoker serverBind,
            ServerAcceptBeginInvoker serverAcceptBegin,
            ServerAcceptWaitInvoker serverAcceptWait,
            ServerAcceptClaimInvoker serverAcceptClaim,
            HandleStatusInvoker serverAcceptRelease,
            ServerReceiveSubmitInvoker serverReceiveSubmit,
            ServerSendResultInvoker serverSendResult,
            ServerFlowUpdateInvoker serverSendFlowUpdate,
            HandleStatusInvoker serverClose,
            ControlInvoker control,
            PollEmptyInvoker pollEmpty,
            DispatchEventInvoker dispatchEvent,
            HandleStatusInvoker? connectionClose,
            HandleStatusInvoker? clientCloseConnection,
            SchemaRegistryCreateInvoker? schemaRegistryCreate,
            SchemaRegistryInstallInvoker? schemaRegistryInstall,
            SchemaRegistryLookupInvoker? schemaRegistryLookup,
            SchemaRegistryInvalidateInvoker? schemaRegistryInvalidate,
            SchemaRegistryValidateBindingInvoker? schemaRegistryValidateBinding,
            HandleStatusInvoker? schemaRegistryRelease,
            ClientResumeSessionInvoker? clientResumeSession,
            SchemaDescriptorParseInvoker? schemaDescriptorParse,
            SchemaDescriptorWriteInvoker? schemaDescriptorWrite,
            TokenDeltaSchemaDescriptorInvoker? tokenDeltaSchemaDescriptor,
            TypedPayloadValidateBindingInvoker? typedPayloadValidateBinding,
            RecoveryRequestValidateInvoker? sessionRecoveryRequestValidate,
            RecoveryAckValidateInvoker? sessionRecoveryAckValidate,
            MigrationRecoveryValidateInvoker? migrationRecoveryValidate,
            MigrationShouldReplayFrameInvoker? migrationShouldReplayFrame,
            BufferAcquireCopyInvoker? bufferAcquireCopy,
            BufferViewInvoker? bufferView,
            HandleStatusInvoker? bufferRelease,
            CacheLeaseRequestInvoker? cacheQuery,
            CacheLeaseRequestInvoker? cacheTouch,
            CachePrefetchInvoker? cachePrefetch,
            CacheReleaseInvoker? cacheRelease,
            ClientSubmitResultCompactBatchInvoker? clientSubmitResultCompactBatch,
            RuntimeFrameSendInvoker? runtimeFrameSend,
            BufferAcquireCopyInvoker? objectMetadataBufferAcquireCopy,
            BufferViewInvoker? objectMetadataBufferView,
            HandleStatusInvoker? objectMetadataBufferRelease,
            ObjectDescriptorCreateInvoker? objectDescriptorCreate,
            ObjectDescriptorViewInvoker? objectDescriptorView,
            DescriptorMetadataSnapshotInvoker? objectDescriptorMetadataSnapshot,
            HandleStatusInvoker? objectDescriptorRelease,
            CacheReferenceDescriptorCreateInvoker? cacheReferenceDescriptorCreate,
            CacheReferenceDescriptorViewInvoker? cacheReferenceDescriptorView,
            DescriptorMetadataSnapshotInvoker? cacheReferenceDescriptorMetadataSnapshot,
            HandleStatusInvoker? cacheReferenceDescriptorRelease,
            TransportSecurityConfigCreateInvoker? transportClientSecurityConfigCreate,
            TransportServerSecurityConfigCreateInvoker? transportServerSecurityConfigCreate,
            TransportOpenInvoker? transportConnect,
            TransportOpenInvoker? transportListen,
            TransportAcceptInvoker? transportAccept,
            TransportListenerEndpointInvoker? transportListenerEndpoint,
            TransportProbeInvoker? transportProbe,
            HandleStatusInvoker? transportClose,
            RoleAwaitEventsInvoker? serverAwaitEvents,
            ServerDropStaleResultInvoker? serverDropStaleResult,
            RoleAwaitEventsInvoker? clientAwaitEvents,
            ClientSessionRecoveryTicketInvoker? clientSessionRecoveryTicket,
            SessionIdInvoker? sessionId,
            ServerPolicyCompleteInvoker? serverPolicyComplete)
        {
            _libraryHandle = libraryHandle;
            CurrentProtocolVersion = currentProtocolVersion ?? throw new ArgumentNullException(nameof(currentProtocolVersion));
            RuntimeCapabilities = runtimeCapabilities ?? throw new ArgumentNullException(nameof(runtimeCapabilities));
            ConnectionBootstrap = connectionBootstrap ?? throw new ArgumentNullException(nameof(connectionBootstrap));
            ClientConnect = clientConnect ?? throw new ArgumentNullException(nameof(clientConnect));
            SessionOpen = sessionOpen ?? throw new ArgumentNullException(nameof(sessionOpen));
            ClientOpenSession = clientOpenSession ?? throw new ArgumentNullException(nameof(clientOpenSession));
            Submit = submit ?? throw new ArgumentNullException(nameof(submit));
            ClientSubmit = clientSubmit ?? throw new ArgumentNullException(nameof(clientSubmit));
            SessionClose = sessionClose ?? throw new ArgumentNullException(nameof(sessionClose));
            ClientClose = clientClose ?? throw new ArgumentNullException(nameof(clientClose));
            ClientCancel = clientCancel ?? throw new ArgumentNullException(nameof(clientCancel));
            ClientAwaitEvent = clientAwaitEvent ?? throw new ArgumentNullException(nameof(clientAwaitEvent));
            ServerBind = serverBind ?? throw new ArgumentNullException(nameof(serverBind));
            ServerAcceptBegin = serverAcceptBegin ?? throw new ArgumentNullException(nameof(serverAcceptBegin));
            ServerAcceptWait = serverAcceptWait ?? throw new ArgumentNullException(nameof(serverAcceptWait));
            ServerAcceptClaim = serverAcceptClaim ?? throw new ArgumentNullException(nameof(serverAcceptClaim));
            ServerAcceptRelease = serverAcceptRelease ?? throw new ArgumentNullException(nameof(serverAcceptRelease));
            ServerReceiveSubmit = serverReceiveSubmit ?? throw new ArgumentNullException(nameof(serverReceiveSubmit));
            ServerSendResult = serverSendResult ?? throw new ArgumentNullException(nameof(serverSendResult));
            ServerSendFlowUpdate = serverSendFlowUpdate ?? throw new ArgumentNullException(nameof(serverSendFlowUpdate));
            ServerClose = serverClose ?? throw new ArgumentNullException(nameof(serverClose));
            Control = control ?? throw new ArgumentNullException(nameof(control));
            PollEmpty = pollEmpty ?? throw new ArgumentNullException(nameof(pollEmpty));
            DispatchEvent = dispatchEvent ?? throw new ArgumentNullException(nameof(dispatchEvent));
            ConnectionClose = connectionClose ?? ClientClose;
            ClientCloseConnection = clientCloseConnection ?? ConnectionClose;
            SchemaRegistryCreate = schemaRegistryCreate ?? MissingSchemaRegistryCreate;
            SchemaRegistryInstall = schemaRegistryInstall ?? MissingSchemaRegistryInstall;
            SchemaRegistryLookup = schemaRegistryLookup ?? MissingSchemaRegistryLookup;
            SchemaRegistryInvalidate = schemaRegistryInvalidate ?? MissingSchemaRegistryInvalidate;
            SchemaRegistryValidateBinding = schemaRegistryValidateBinding ?? MissingSchemaRegistryValidateBinding;
            SchemaRegistryRelease = schemaRegistryRelease ?? MissingHandleStatus;
            ClientResumeSession = clientResumeSession ?? MissingClientResumeSession;
            ClientSessionRecoveryTicket = clientSessionRecoveryTicket ?? MissingClientSessionRecoveryTicket;
            SessionId = sessionId ?? MissingSessionId;
            ServerPolicyComplete = serverPolicyComplete ?? MissingServerPolicyComplete;
            SchemaDescriptorParse = schemaDescriptorParse ?? MissingSchemaDescriptorParse;
            SchemaDescriptorWrite = schemaDescriptorWrite ?? MissingSchemaDescriptorWrite;
            TokenDeltaSchemaDescriptor = tokenDeltaSchemaDescriptor ?? MissingTokenDeltaSchemaDescriptor;
            TypedPayloadValidateBinding = typedPayloadValidateBinding ?? MissingTypedPayloadValidateBinding;
            SessionRecoveryRequestValidate = sessionRecoveryRequestValidate ?? MissingRecoveryRequestValidate;
            SessionRecoveryAckValidate = sessionRecoveryAckValidate ?? MissingRecoveryAckValidate;
            MigrationRecoveryValidate = migrationRecoveryValidate ?? MissingMigrationRecoveryValidate;
            MigrationShouldReplayFrame = migrationShouldReplayFrame ?? MissingMigrationShouldReplayFrame;
            BufferAcquireCopy = bufferAcquireCopy ?? MissingBufferAcquireCopy;
            BufferView = bufferView ?? MissingBufferView;
            BufferRelease = bufferRelease ?? MissingHandleStatus;
            CacheQuery = cacheQuery ?? MissingCacheLeaseRequest;
            CacheTouch = cacheTouch ?? MissingCacheLeaseRequest;
            CachePrefetch = cachePrefetch ?? MissingCachePrefetch;
            CacheRelease = cacheRelease ?? MissingCacheRelease;
            ClientSubmitResultCompactBatch = clientSubmitResultCompactBatch ?? MissingClientSubmitResultCompactBatch;
            RuntimeFrameSend = runtimeFrameSend ?? MissingRuntimeFrameSend;
            ObjectMetadataBufferAcquireCopy = objectMetadataBufferAcquireCopy ?? MissingObjectMetadataBufferAcquireCopy;
            ObjectMetadataBufferView = objectMetadataBufferView ?? MissingObjectMetadataBufferView;
            ObjectMetadataBufferRelease = objectMetadataBufferRelease ?? MissingRuntimeObjectHandleStatus;
            ObjectDescriptorCreate = objectDescriptorCreate ?? MissingObjectDescriptorCreate;
            ObjectDescriptorView = objectDescriptorView ?? MissingObjectDescriptorView;
            ObjectDescriptorMetadataSnapshot = objectDescriptorMetadataSnapshot ?? MissingDescriptorMetadataSnapshot;
            ObjectDescriptorRelease = objectDescriptorRelease ?? MissingRuntimeObjectHandleStatus;
            CacheReferenceDescriptorCreate = cacheReferenceDescriptorCreate ?? MissingCacheReferenceDescriptorCreate;
            CacheReferenceDescriptorView = cacheReferenceDescriptorView ?? MissingCacheReferenceDescriptorView;
            CacheReferenceDescriptorMetadataSnapshot = cacheReferenceDescriptorMetadataSnapshot ?? MissingDescriptorMetadataSnapshot;
            CacheReferenceDescriptorRelease = cacheReferenceDescriptorRelease ?? MissingRuntimeObjectHandleStatus;
            TransportClientSecurityConfigCreate = transportClientSecurityConfigCreate ?? MissingTransportClientSecurityConfigCreate;
            TransportServerSecurityConfigCreate = transportServerSecurityConfigCreate ?? MissingTransportServerSecurityConfigCreate;
            TransportConnect = transportConnect ?? MissingTransportOpen;
            TransportListen = transportListen ?? MissingTransportOpen;
            TransportAccept = transportAccept ?? MissingTransportAccept;
            TransportListenerEndpoint = transportListenerEndpoint ?? MissingTransportListenerEndpoint;
            TransportProbe = transportProbe ?? MissingTransportProbe;
            TransportClose = transportClose ?? MissingHandleStatus;
            ServerAwaitEvents = serverAwaitEvents ?? MissingRoleAwaitEvents;
            ServerDropStaleResult = serverDropStaleResult ?? MissingServerDropStaleResult;
            ClientAwaitEvents = clientAwaitEvents ?? MissingRoleAwaitEvents;
        }

        private IntPtr _libraryHandle;

        [ExcludeFromCodeCoverage]
        public static NnrpNativeRuntimeEntrypoints Load(
            string? artifactPath = null,
            string? artifactRoot = null,
            NnrpNativePlatform? platform = null,
            uint requiredTransportSlots = NnrpNativeArtifact.RequiredTransportSlots,
            string? transportScope = null)
        {
            string resolvedPath = string.IsNullOrWhiteSpace(artifactPath)
                ? string.IsNullOrWhiteSpace(transportScope)
                    ? NnrpNativeArtifact.Resolve(artifactRoot, platform)
                    : NnrpNativeArtifact.ResolveTransport(transportScope!, artifactRoot, platform)
                : artifactPath!;
            resolvedPath = Path.GetFullPath(resolvedPath);
            lock (PinnedLibraryGate)
            {
                IntPtr handle = IntPtr.Zero;
                bool loadedHere = false;
                try
                {
                    if (!PinnedLibraries.TryGetValue(resolvedPath, out handle))
                    {
                        handle = NativeDynamicLibrary.Load(resolvedPath);
                        loadedHere = true;
                        var runtimeShutdown = Bind<RuntimeShutdownInvoker>(
                            handle,
                            "nnrp_transport_runtime_shutdown");
                        PinnedLibraries.Add(resolvedPath, handle);
                        PinnedRuntimeShutdowns.Add(resolvedPath, runtimeShutdown);
                    }

                    var runtimeCapabilities = Bind<NnrpNativeArtifact.RuntimeCapabilitiesInvoker>(handle, "nnrp_runtime_capabilities");
                    NnrpNativeArtifact.Probe(
                        resolvedPath,
                        runtimeCapabilities: runtimeCapabilities,
                        requiredTransportSlots: requiredTransportSlots);
                    return new NnrpNativeRuntimeEntrypoints(
                        handle,
                        Bind<CurrentProtocolVersionInvoker>(handle, "nnrp_current_protocol_version"),
                        runtimeCapabilities,
                        MissingConnectionBootstrap,
                        Bind<ClientConnectInvoker>(handle, "nnrp_client_connect"),
                        Bind<SessionOpenInvoker>(handle, "nnrp_session_open"),
                        Bind<SessionOpenInvoker>(handle, "nnrp_client_open_session"),
                        Bind<SubmitInvoker>(handle, "nnrp_submit"),
                        Bind<SubmitInvoker>(handle, "nnrp_client_submit"),
                        Bind<HandleStatusInvoker>(handle, "nnrp_session_close"),
                        Bind<HandleStatusInvoker>(handle, "nnrp_client_close"),
                        Bind<ClientCancelInvoker>(handle, "nnrp_client_cancel"),
                        Bind<AwaitEventInvoker>(handle, "nnrp_client_await_event"),
                        Bind<ServerBindInvoker>(handle, "nnrp_server_bind"),
                        Bind<ServerAcceptBeginInvoker>(handle, "nnrp_server_accept_begin"),
                        Bind<ServerAcceptWaitInvoker>(handle, "nnrp_server_accept_wait"),
                        Bind<ServerAcceptClaimInvoker>(handle, "nnrp_server_accept_claim"),
                        Bind<HandleStatusInvoker>(handle, "nnrp_server_accept_release"),
                        MissingServerReceiveSubmit,
                        Bind<ServerSendResultInvoker>(handle, "nnrp_server_send_result"),
                        MissingServerFlowUpdate,
                        Bind<HandleStatusInvoker>(handle, "nnrp_server_close"),
                        MissingControl,
                        Bind<PollEmptyInvoker>(handle, "nnrp_poll_empty"),
                        Bind<DispatchEventInvoker>(handle, "nnrp_dispatch_event"),
                        Bind<HandleStatusInvoker>(handle, "nnrp_connection_close"),
                        Bind<HandleStatusInvoker>(handle, "nnrp_client_close_connection"),
                        Bind<SchemaRegistryCreateInvoker>(handle, "nnrp_schema_registry_create"),
                        Bind<SchemaRegistryInstallInvoker>(handle, "nnrp_schema_registry_install"),
                        Bind<SchemaRegistryLookupInvoker>(handle, "nnrp_schema_registry_lookup"),
                        Bind<SchemaRegistryInvalidateInvoker>(handle, "nnrp_schema_registry_invalidate"),
                        Bind<SchemaRegistryValidateBindingInvoker>(handle, "nnrp_schema_registry_validate_binding"),
                        Bind<HandleStatusInvoker>(handle, "nnrp_schema_registry_release"),
                        Bind<ClientResumeSessionInvoker>(handle, "nnrp_client_resume_session"),
                        Bind<SchemaDescriptorParseInvoker>(handle, "nnrp_schema_descriptor_parse"),
                        Bind<SchemaDescriptorWriteInvoker>(handle, "nnrp_schema_descriptor_write"),
                        Bind<TokenDeltaSchemaDescriptorInvoker>(handle, "nnrp_token_delta_schema_descriptor"),
                        Bind<TypedPayloadValidateBindingInvoker>(handle, "nnrp_typed_payload_validate_binding"),
                        Bind<RecoveryRequestValidateInvoker>(handle, "nnrp_session_recovery_request_validate"),
                        Bind<RecoveryAckValidateInvoker>(handle, "nnrp_session_recovery_ack_validate"),
                        Bind<MigrationRecoveryValidateInvoker>(handle, "nnrp_migration_recovery_validate"),
                        Bind<MigrationShouldReplayFrameInvoker>(handle, "nnrp_migration_should_replay_frame"),
                        Bind<BufferAcquireCopyInvoker>(handle, "nnrp_buffer_acquire_copy"),
                        Bind<BufferViewInvoker>(handle, "nnrp_buffer_view"),
                        Bind<HandleStatusInvoker>(handle, "nnrp_buffer_release"),
                        Bind<CacheLeaseRequestInvoker>(handle, "nnrp_cache_query"),
                        Bind<CacheLeaseRequestInvoker>(handle, "nnrp_cache_touch"),
                        Bind<CachePrefetchInvoker>(handle, "nnrp_cache_prefetch"),
                        Bind<CacheReleaseInvoker>(handle, "nnrp_cache_release"),
                        MissingClientSubmitResultCompactBatch,
                        Bind<RuntimeFrameSendInvoker>(handle, "nnrp_runtime_frame_send"),
                        Bind<BufferAcquireCopyInvoker>(handle, "nnrp_object_metadata_buffer_acquire_copy"),
                        Bind<BufferViewInvoker>(handle, "nnrp_object_metadata_buffer_view"),
                        Bind<HandleStatusInvoker>(handle, "nnrp_object_metadata_buffer_release"),
                        Bind<ObjectDescriptorCreateInvoker>(handle, "nnrp_object_descriptor_create"),
                        Bind<ObjectDescriptorViewInvoker>(handle, "nnrp_object_descriptor_view"),
                        Bind<DescriptorMetadataSnapshotInvoker>(handle, "nnrp_object_descriptor_metadata_snapshot"),
                        Bind<HandleStatusInvoker>(handle, "nnrp_object_descriptor_release"),
                        Bind<CacheReferenceDescriptorCreateInvoker>(handle, "nnrp_cache_reference_descriptor_create"),
                        Bind<CacheReferenceDescriptorViewInvoker>(handle, "nnrp_cache_reference_descriptor_view"),
                        Bind<DescriptorMetadataSnapshotInvoker>(handle, "nnrp_cache_reference_descriptor_metadata_snapshot"),
                        Bind<HandleStatusInvoker>(handle, "nnrp_cache_reference_descriptor_release"),
                        Bind<TransportSecurityConfigCreateInvoker>(handle, "nnrp_transport_client_security_config_create"),
                        Bind<TransportServerSecurityConfigCreateInvoker>(handle, "nnrp_transport_server_security_config_create"),
                        Bind<TransportOpenInvoker>(handle, "nnrp_transport_connect"),
                        Bind<TransportOpenInvoker>(handle, "nnrp_transport_listen"),
                        Bind<TransportAcceptInvoker>(handle, "nnrp_transport_accept"),
                        Bind<TransportListenerEndpointInvoker>(handle, "nnrp_transport_listener_endpoint"),
                        Bind<TransportProbeInvoker>(handle, "nnrp_transport_probe"),
                        Bind<HandleStatusInvoker>(handle, "nnrp_transport_close"),
                        Bind<RoleAwaitEventsInvoker>(handle, "nnrp_server_await_events"),
                        Bind<ServerDropStaleResultInvoker>(handle, "nnrp_server_drop_stale_result"),
                        Bind<RoleAwaitEventsInvoker>(handle, "nnrp_client_await_events"),
                        Bind<ClientSessionRecoveryTicketInvoker>(handle, "nnrp_client_session_recovery_ticket"),
                        Bind<SessionIdInvoker>(handle, "nnrp_session_id"),
                        Bind<ServerPolicyCompleteInvoker>(handle, "nnrp_server_policy_complete"));
                }
                catch (Exception error) when (error is DllNotFoundException || error is EntryPointNotFoundException || error is BadImageFormatException)
                {
                    RemoveFailedPinnedLibrary(resolvedPath, handle, loadedHere);
                    throw new NnrpNativeArtifactException("Failed to load native runtime entrypoints from " + resolvedPath + ": " + error.Message, error);
                }
                catch
                {
                    RemoveFailedPinnedLibrary(resolvedPath, handle, loadedHere);
                    throw;
                }
            }
        }

        public void Dispose()
        {
            if (_libraryHandle == IntPtr.Zero)
            {
                return;
            }

            // Native transports may retain asynchronous runtime workers after their handles close.
            // Keep the module process-pinned so those workers never return through unloaded code.
            _libraryHandle = IntPtr.Zero;
        }

        [ExcludeFromCodeCoverage]
        private static void RemoveFailedPinnedLibrary(string path, IntPtr handle, bool loadedHere)
        {
            if (!loadedHere || handle == IntPtr.Zero)
            {
                return;
            }

            PinnedLibraries.Remove(path);
            PinnedRuntimeShutdowns.Remove(path);
            NativeDynamicLibrary.Free(handle);
        }

        [ExcludeFromCodeCoverage]
        private static void ShutdownPinnedTransportRuntimes()
        {
            RuntimeShutdownInvoker[] shutdowns;
            lock (PinnedLibraryGate)
            {
                shutdowns = new RuntimeShutdownInvoker[PinnedRuntimeShutdowns.Count];
                PinnedRuntimeShutdowns.Values.CopyTo(shutdowns, 0);
            }

            foreach (var shutdown in shutdowns)
            {
                try
                {
                    shutdown();
                }
                catch
                {
                    // Process teardown must continue even if one native module cannot stop cleanly.
                }
            }
        }

        internal static void ShutdownPinnedTransportRuntimesForTesting()
        {
            ShutdownPinnedTransportRuntimes();
        }

        [ExcludeFromCodeCoverage]
        private static T Bind<T>(IntPtr handle, string name)
            where T : Delegate
        {
            IntPtr symbol = NativeDynamicLibrary.GetSymbol(handle, name);
            return Marshal.GetDelegateForFunctionPointer<T>(symbol);
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NnrpProtocolVersion CurrentProtocolVersionInvoker();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate NnrpFfiStatus RuntimeShutdownInvoker();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NnrpFfiStatus ConnectionBootstrapInvoker(NnrpConnectionBootstrap request, out NnrpHandle connection);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NnrpFfiStatus ClientConnectInvoker(NnrpClientConnectRequest request, out NnrpHandle connection);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NnrpFfiStatus SessionOpenInvoker(NnrpSessionOpenRequest request, out NnrpHandle session);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NnrpFfiStatus ClientResumeSessionInvoker(
            NnrpSessionResumeRequest request,
            out NnrpHandle session,
            out NnrpSessionRecoveryOutcome outcome);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NnrpFfiStatus ClientSessionRecoveryTicketInvoker(
            NnrpHandle session,
            out NnrpHandle buffer,
            out NnrpBufferView ticket);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NnrpFfiStatus SessionIdInvoker(NnrpHandle session, out uint sessionId);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NnrpFfiStatus ServerPolicyCompleteInvoker(NnrpServerPolicyCompleteRequest request);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NnrpFfiStatus SubmitInvoker(NnrpFfiSubmitRequest request, out NnrpHandle operation);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NnrpFfiStatus ClientSubmitResultCompactBatchInvoker(
            NnrpClientSubmitResultBatchRequest request,
            out NnrpCompactResult lastResult,
            out UIntPtr completed);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NnrpFfiStatus HandleStatusInvoker(NnrpHandle handle);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NnrpFfiStatus ClientCancelInvoker(NnrpClientCancelRequest request);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NnrpFfiStatus AwaitEventInvoker(NnrpHandle connection, out NnrpPollResult result);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NnrpFfiStatus ServerBindInvoker(NnrpServerBindRequest request, out NnrpHandle server);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NnrpFfiStatus ServerAcceptBeginInvoker(
            NnrpServerAcceptBeginRequest request,
            out NnrpHandle accept);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NnrpFfiStatus ServerAcceptWaitInvoker(NnrpServerAcceptWaitRequest request);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NnrpFfiStatus ServerAcceptClaimInvoker(
            NnrpServerAcceptClaimRequest request,
            out NnrpServerAcceptResult result);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NnrpFfiStatus RoleAwaitEventsInvoker(
            NnrpRoleEventPollRequest request,
            IntPtr events,
            UIntPtr eventCapacity,
            out UIntPtr eventCount);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NnrpFfiStatus ServerReceiveSubmitInvoker(NnrpServerReceiveSubmitRequest request, out NnrpHandle operation);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NnrpFfiStatus ServerSendResultInvoker(NnrpServerSendResultRequest request);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NnrpFfiStatus ServerDropStaleResultInvoker(
            NnrpServerDropStaleResultRequest request,
            out NnrpPollResult result);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NnrpFfiStatus ServerFlowUpdateInvoker(NnrpServerFlowUpdateRequest request);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NnrpFfiStatus ControlInvoker(NnrpControlRequest request);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NnrpFfiStatus RuntimeFrameSendInvoker(NnrpRuntimeFrameSendRequest request);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NnrpFfiStatus PollEmptyInvoker(out NnrpPollResult result);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NnrpFfiStatus DispatchEventInvoker(NnrpCallbackSink sink, ref NnrpEvent @event);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NnrpFfiStatus SchemaRegistryCreateInvoker(out NnrpHandle registry);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NnrpFfiStatus SchemaRegistryInstallInvoker(NnrpHandle registry, NnrpSchemaDescriptorHeader descriptor, out uint action);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NnrpFfiStatus SchemaRegistryLookupInvoker(NnrpHandle registry, uint schemaId, uint schemaVersion, out NnrpSchemaDescriptorHeader descriptor);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NnrpFfiStatus SchemaRegistryInvalidateInvoker(NnrpHandle registry, uint schemaId, uint schemaVersion, out uint action);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NnrpFfiStatus SchemaRegistryValidateBindingInvoker(NnrpHandle registry, NnrpTypedPayloadDescriptor descriptor);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NnrpFfiStatus SchemaDescriptorParseInvoker(NnrpBufferView source, out NnrpSchemaDescriptorHeader descriptor);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NnrpFfiStatus SchemaDescriptorWriteInvoker(NnrpSchemaDescriptorHeader descriptor, NnrpMutableBufferView destination);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NnrpFfiStatus TokenDeltaSchemaDescriptorInvoker(out NnrpSchemaDescriptorHeader descriptor);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NnrpFfiStatus TypedPayloadValidateBindingInvoker(IntPtr schemaDescriptors, UIntPtr schemaCount, NnrpTypedPayloadDescriptor descriptor);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NnrpFfiStatus RecoveryRequestValidateInvoker(NnrpBufferView sessionOpenMetadata);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NnrpFfiStatus RecoveryAckValidateInvoker(
            NnrpBufferView sessionOpenMetadata,
            NnrpBufferView sessionOpenAckMetadata,
            out NnrpSessionRecoveryOutcome outcome);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NnrpFfiStatus MigrationRecoveryValidateInvoker(
            NnrpBufferView sessionMigrateMetadata,
            NnrpBufferView sessionMigrateAckMetadata);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NnrpFfiStatus MigrationShouldReplayFrameInvoker(
            NnrpBufferView sessionMigrateAckMetadata,
            ulong frameId,
            out byte shouldReplay);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NnrpFfiStatus BufferAcquireCopyInvoker(
            NnrpBufferView source,
            out NnrpHandle buffer,
            out NnrpBufferView view);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NnrpFfiStatus BufferViewInvoker(NnrpHandle buffer, out NnrpBufferView view);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NnrpFfiStatus ObjectDescriptorCreateInvoker(
            NnrpRuntimeObjectDescriptor descriptor,
            NnrpBufferView metadata,
            out NnrpHandle handle);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NnrpFfiStatus ObjectDescriptorViewInvoker(
            NnrpHandle handle,
            out NnrpRuntimeObjectDescriptor descriptor,
            out NnrpBufferView metadata);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NnrpFfiStatus DescriptorMetadataSnapshotInvoker(
            NnrpHandle handle,
            out NnrpHandle buffer,
            out NnrpBufferView view);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NnrpFfiStatus CacheReferenceDescriptorCreateInvoker(
            NnrpCacheReferenceDescriptor descriptor,
            NnrpBufferView metadata,
            out NnrpHandle handle);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NnrpFfiStatus CacheReferenceDescriptorViewInvoker(
            NnrpHandle handle,
            out NnrpCacheReferenceDescriptor descriptor,
            out NnrpBufferView metadata);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NnrpFfiStatus CacheLeaseRequestInvoker(NnrpCacheLeaseRequest request, out NnrpCacheLeaseResult result);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NnrpFfiStatus CachePrefetchInvoker(
            NnrpHandle owner,
            IntPtr objects,
            UIntPtr objectCount,
            ulong nowMilliseconds,
            uint ttlMilliseconds,
            IntPtr results);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NnrpFfiStatus CacheReleaseInvoker(NnrpHandle lease, out NnrpCacheLeaseResult result);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NnrpFfiStatus TransportSecurityConfigCreateInvoker(
            NnrpTransportClientSecurityConfigRequest request,
            out NnrpHandle config);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NnrpFfiStatus TransportServerSecurityConfigCreateInvoker(
            NnrpTransportServerSecurityConfigRequest request,
            out NnrpHandle config);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NnrpFfiStatus TransportOpenInvoker(
            NnrpTransportOpenRequest request,
            out NnrpHandle handle);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NnrpFfiStatus TransportAcceptInvoker(
            NnrpTransportAcceptRequest request,
            out NnrpHandle connection);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NnrpFfiStatus TransportListenerEndpointInvoker(
            NnrpHandle listener,
            out NnrpHandle buffer,
            out NnrpBufferView endpoint);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NnrpFfiStatus TransportProbeInvoker(
            NnrpTransportProbeRequest request,
            out NnrpTransportProbeResult result);

        public CurrentProtocolVersionInvoker CurrentProtocolVersion { get; }

        public NnrpNativeArtifact.RuntimeCapabilitiesInvoker RuntimeCapabilities { get; }

        public ConnectionBootstrapInvoker ConnectionBootstrap { get; }

        public ClientConnectInvoker ClientConnect { get; }

        public SessionOpenInvoker SessionOpen { get; }

        public SessionOpenInvoker ClientOpenSession { get; }

        public ClientResumeSessionInvoker ClientResumeSession { get; }

        public ClientSessionRecoveryTicketInvoker ClientSessionRecoveryTicket { get; }

        public SessionIdInvoker SessionId { get; }

        public ServerPolicyCompleteInvoker ServerPolicyComplete { get; }

        public SubmitInvoker Submit { get; }

        public SubmitInvoker ClientSubmit { get; }

        public ClientSubmitResultCompactBatchInvoker ClientSubmitResultCompactBatch { get; }

        public HandleStatusInvoker SessionClose { get; }

        public HandleStatusInvoker ClientClose { get; }

        public HandleStatusInvoker ConnectionClose { get; }

        public HandleStatusInvoker ClientCloseConnection { get; }

        public ClientCancelInvoker ClientCancel { get; }

        public AwaitEventInvoker ClientAwaitEvent { get; }

        public RoleAwaitEventsInvoker ClientAwaitEvents { get; }

        public ServerBindInvoker ServerBind { get; }

        public ServerAcceptBeginInvoker ServerAcceptBegin { get; }

        public ServerAcceptWaitInvoker ServerAcceptWait { get; }

        public ServerAcceptClaimInvoker ServerAcceptClaim { get; }

        public RoleAwaitEventsInvoker ServerAwaitEvents { get; }

        public HandleStatusInvoker ServerAcceptRelease { get; }

        public ServerReceiveSubmitInvoker ServerReceiveSubmit { get; }

        public ServerSendResultInvoker ServerSendResult { get; }

        public ServerDropStaleResultInvoker ServerDropStaleResult { get; }

        public ServerFlowUpdateInvoker ServerSendFlowUpdate { get; }

        public HandleStatusInvoker ServerClose { get; }

        public ControlInvoker Control { get; }

        public RuntimeFrameSendInvoker RuntimeFrameSend { get; }

        public PollEmptyInvoker PollEmpty { get; }

        public DispatchEventInvoker DispatchEvent { get; }

        public SchemaRegistryCreateInvoker SchemaRegistryCreate { get; }

        public SchemaRegistryInstallInvoker SchemaRegistryInstall { get; }

        public SchemaRegistryLookupInvoker SchemaRegistryLookup { get; }

        public SchemaRegistryInvalidateInvoker SchemaRegistryInvalidate { get; }

        public SchemaRegistryValidateBindingInvoker SchemaRegistryValidateBinding { get; }

        public HandleStatusInvoker SchemaRegistryRelease { get; }

        public SchemaDescriptorParseInvoker SchemaDescriptorParse { get; }

        public SchemaDescriptorWriteInvoker SchemaDescriptorWrite { get; }

        public TokenDeltaSchemaDescriptorInvoker TokenDeltaSchemaDescriptor { get; }

        public TypedPayloadValidateBindingInvoker TypedPayloadValidateBinding { get; }

        public RecoveryRequestValidateInvoker SessionRecoveryRequestValidate { get; }

        public RecoveryAckValidateInvoker SessionRecoveryAckValidate { get; }

        public MigrationRecoveryValidateInvoker MigrationRecoveryValidate { get; }

        public MigrationShouldReplayFrameInvoker MigrationShouldReplayFrame { get; }

        public BufferAcquireCopyInvoker BufferAcquireCopy { get; }

        public BufferViewInvoker BufferView { get; }

        public HandleStatusInvoker BufferRelease { get; }

        public BufferAcquireCopyInvoker ObjectMetadataBufferAcquireCopy { get; }

        public BufferViewInvoker ObjectMetadataBufferView { get; }

        public HandleStatusInvoker ObjectMetadataBufferRelease { get; }

        public ObjectDescriptorCreateInvoker ObjectDescriptorCreate { get; }

        public ObjectDescriptorViewInvoker ObjectDescriptorView { get; }

        public DescriptorMetadataSnapshotInvoker ObjectDescriptorMetadataSnapshot { get; }

        public HandleStatusInvoker ObjectDescriptorRelease { get; }

        public CacheReferenceDescriptorCreateInvoker CacheReferenceDescriptorCreate { get; }

        public CacheReferenceDescriptorViewInvoker CacheReferenceDescriptorView { get; }

        public DescriptorMetadataSnapshotInvoker CacheReferenceDescriptorMetadataSnapshot { get; }

        public HandleStatusInvoker CacheReferenceDescriptorRelease { get; }

        public CacheLeaseRequestInvoker CacheQuery { get; }

        public CacheLeaseRequestInvoker CacheTouch { get; }

        public CachePrefetchInvoker CachePrefetch { get; }

        public CacheReleaseInvoker CacheRelease { get; }

        public TransportSecurityConfigCreateInvoker TransportClientSecurityConfigCreate { get; }

        public TransportServerSecurityConfigCreateInvoker TransportServerSecurityConfigCreate { get; }

        public TransportOpenInvoker TransportConnect { get; }

        public TransportOpenInvoker TransportListen { get; }

        public TransportAcceptInvoker TransportAccept { get; }

        public TransportListenerEndpointInvoker TransportListenerEndpoint { get; }

        public TransportProbeInvoker TransportProbe { get; }

        public HandleStatusInvoker TransportClose { get; }

        private static NnrpFfiStatus MissingSchemaRegistryCreate(out NnrpHandle registry)
        {
            registry = NnrpHandle.Invalid;
            return new NnrpFfiStatus(NnrpFfiStatusCode.InternalError, NnrpErrorFamily.Schema);
        }

        private static NnrpFfiStatus MissingSchemaRegistryInstall(NnrpHandle registry, NnrpSchemaDescriptorHeader descriptor, out uint action)
        {
            action = 0;
            return new NnrpFfiStatus(NnrpFfiStatusCode.InternalError, NnrpErrorFamily.Schema);
        }

        private static NnrpFfiStatus MissingSchemaRegistryLookup(NnrpHandle registry, uint schemaId, uint schemaVersion, out NnrpSchemaDescriptorHeader descriptor)
        {
            descriptor = default(NnrpSchemaDescriptorHeader);
            return new NnrpFfiStatus(NnrpFfiStatusCode.InternalError, NnrpErrorFamily.Schema);
        }

        private static NnrpFfiStatus MissingSchemaRegistryInvalidate(NnrpHandle registry, uint schemaId, uint schemaVersion, out uint action)
        {
            action = 0;
            return new NnrpFfiStatus(NnrpFfiStatusCode.InternalError, NnrpErrorFamily.Schema);
        }

        private static NnrpFfiStatus MissingSchemaRegistryValidateBinding(NnrpHandle registry, NnrpTypedPayloadDescriptor descriptor)
        {
            return new NnrpFfiStatus(NnrpFfiStatusCode.InternalError, NnrpErrorFamily.Schema);
        }

        private static NnrpFfiStatus MissingHandleStatus(NnrpHandle handle)
        {
            return new NnrpFfiStatus(NnrpFfiStatusCode.InternalError, NnrpErrorFamily.Schema);
        }

        private static NnrpFfiStatus MissingRuntimeFrameSend(NnrpRuntimeFrameSendRequest request)
        {
            return new NnrpFfiStatus(NnrpFfiStatusCode.InternalError);
        }

        private static NnrpFfiStatus MissingConnectionBootstrap(
            NnrpConnectionBootstrap request,
            out NnrpHandle connection)
        {
            connection = NnrpHandle.Invalid;
            return new NnrpFfiStatus(NnrpFfiStatusCode.InternalError, NnrpErrorFamily.Transport);
        }

        private static NnrpFfiStatus MissingServerReceiveSubmit(
            NnrpServerReceiveSubmitRequest request,
            out NnrpHandle operation)
        {
            operation = NnrpHandle.Invalid;
            return new NnrpFfiStatus(NnrpFfiStatusCode.InternalError, NnrpErrorFamily.Operation);
        }

        private static NnrpFfiStatus MissingServerFlowUpdate(NnrpServerFlowUpdateRequest request)
        {
            return new NnrpFfiStatus(NnrpFfiStatusCode.InternalError, NnrpErrorFamily.Control);
        }

        private static NnrpFfiStatus MissingServerDropStaleResult(
            NnrpServerDropStaleResultRequest request,
            out NnrpPollResult result)
        {
            var status = new NnrpFfiStatus(NnrpFfiStatusCode.InternalError, NnrpErrorFamily.Operation);
            result = new NnrpPollResult(status, 0, default(NnrpEvent));
            return status;
        }

        private static NnrpFfiStatus MissingControl(NnrpControlRequest request)
        {
            return new NnrpFfiStatus(NnrpFfiStatusCode.InternalError, NnrpErrorFamily.Control);
        }

        private static NnrpFfiStatus MissingClientResumeSession(
            NnrpSessionResumeRequest request,
            out NnrpHandle session,
            out NnrpSessionRecoveryOutcome outcome)
        {
            session = NnrpHandle.Invalid;
            outcome = default(NnrpSessionRecoveryOutcome);
            return new NnrpFfiStatus(NnrpFfiStatusCode.InternalError);
        }

        private static NnrpFfiStatus MissingClientSessionRecoveryTicket(
            NnrpHandle session,
            out NnrpHandle buffer,
            out NnrpBufferView ticket)
        {
            buffer = NnrpHandle.Invalid;
            ticket = NnrpBufferView.Empty;
            return new NnrpFfiStatus(NnrpFfiStatusCode.InternalError);
        }

        private static NnrpFfiStatus MissingSessionId(NnrpHandle session, out uint sessionId)
        {
            sessionId = 0;
            return new NnrpFfiStatus(NnrpFfiStatusCode.InternalError);
        }

        private static NnrpFfiStatus MissingServerPolicyComplete(NnrpServerPolicyCompleteRequest request)
        {
            return new NnrpFfiStatus(NnrpFfiStatusCode.InternalError);
        }

        private static NnrpFfiStatus MissingClientSubmitResultCompactBatch(
            NnrpClientSubmitResultBatchRequest request,
            out NnrpCompactResult lastResult,
            out UIntPtr completed)
        {
            lastResult = default(NnrpCompactResult);
            completed = UIntPtr.Zero;
            return new NnrpFfiStatus(NnrpFfiStatusCode.InternalError);
        }

        private static NnrpFfiStatus MissingSchemaDescriptorParse(NnrpBufferView source, out NnrpSchemaDescriptorHeader descriptor)
        {
            descriptor = default(NnrpSchemaDescriptorHeader);
            return new NnrpFfiStatus(NnrpFfiStatusCode.InternalError, NnrpErrorFamily.Schema);
        }

        private static NnrpFfiStatus MissingSchemaDescriptorWrite(NnrpSchemaDescriptorHeader descriptor, NnrpMutableBufferView destination)
        {
            return new NnrpFfiStatus(NnrpFfiStatusCode.InternalError, NnrpErrorFamily.Schema);
        }

        private static NnrpFfiStatus MissingTokenDeltaSchemaDescriptor(out NnrpSchemaDescriptorHeader descriptor)
        {
            descriptor = default(NnrpSchemaDescriptorHeader);
            return new NnrpFfiStatus(NnrpFfiStatusCode.InternalError, NnrpErrorFamily.Schema);
        }

        private static NnrpFfiStatus MissingTypedPayloadValidateBinding(IntPtr schemaDescriptors, UIntPtr schemaCount, NnrpTypedPayloadDescriptor descriptor)
        {
            return new NnrpFfiStatus(NnrpFfiStatusCode.InternalError, NnrpErrorFamily.Schema);
        }

        private static NnrpFfiStatus MissingRecoveryRequestValidate(NnrpBufferView sessionOpenMetadata)
        {
            return new NnrpFfiStatus(NnrpFfiStatusCode.InternalError);
        }

        private static NnrpFfiStatus MissingRecoveryAckValidate(
            NnrpBufferView sessionOpenMetadata,
            NnrpBufferView sessionOpenAckMetadata,
            out NnrpSessionRecoveryOutcome outcome)
        {
            outcome = default(NnrpSessionRecoveryOutcome);
            return new NnrpFfiStatus(NnrpFfiStatusCode.InternalError);
        }

        private static NnrpFfiStatus MissingMigrationRecoveryValidate(
            NnrpBufferView sessionMigrateMetadata,
            NnrpBufferView sessionMigrateAckMetadata)
        {
            return new NnrpFfiStatus(NnrpFfiStatusCode.InternalError);
        }

        private static NnrpFfiStatus MissingMigrationShouldReplayFrame(
            NnrpBufferView sessionMigrateAckMetadata,
            ulong frameId,
            out byte shouldReplay)
        {
            shouldReplay = 0;
            return new NnrpFfiStatus(NnrpFfiStatusCode.InternalError);
        }

        private static NnrpFfiStatus MissingBufferAcquireCopy(
            NnrpBufferView source,
            out NnrpHandle buffer,
            out NnrpBufferView view)
        {
            buffer = NnrpHandle.Invalid;
            view = NnrpBufferView.Empty;
            return new NnrpFfiStatus(NnrpFfiStatusCode.InternalError);
        }

        private static NnrpFfiStatus MissingBufferView(NnrpHandle buffer, out NnrpBufferView view)
        {
            view = NnrpBufferView.Empty;
            return new NnrpFfiStatus(NnrpFfiStatusCode.InternalError);
        }

        private static NnrpFfiStatus MissingRuntimeObjectHandleStatus(NnrpHandle handle)
        {
            return new NnrpFfiStatus(NnrpFfiStatusCode.InternalError, NnrpErrorFamily.RuntimeObject);
        }

        private static NnrpFfiStatus MissingObjectMetadataBufferAcquireCopy(
            NnrpBufferView source,
            out NnrpHandle buffer,
            out NnrpBufferView view)
        {
            buffer = NnrpHandle.Invalid;
            view = NnrpBufferView.Empty;
            return new NnrpFfiStatus(NnrpFfiStatusCode.InternalError, NnrpErrorFamily.RuntimeObject);
        }

        private static NnrpFfiStatus MissingObjectMetadataBufferView(
            NnrpHandle buffer,
            out NnrpBufferView view)
        {
            view = NnrpBufferView.Empty;
            return new NnrpFfiStatus(NnrpFfiStatusCode.InternalError, NnrpErrorFamily.RuntimeObject);
        }

        private static NnrpFfiStatus MissingObjectDescriptorCreate(
            NnrpRuntimeObjectDescriptor descriptor,
            NnrpBufferView metadata,
            out NnrpHandle handle)
        {
            handle = NnrpHandle.Invalid;
            return new NnrpFfiStatus(NnrpFfiStatusCode.InternalError, NnrpErrorFamily.RuntimeObject);
        }

        private static NnrpFfiStatus MissingObjectDescriptorView(
            NnrpHandle handle,
            out NnrpRuntimeObjectDescriptor descriptor,
            out NnrpBufferView metadata)
        {
            descriptor = default(NnrpRuntimeObjectDescriptor);
            metadata = NnrpBufferView.Empty;
            return new NnrpFfiStatus(NnrpFfiStatusCode.InternalError, NnrpErrorFamily.RuntimeObject);
        }

        private static NnrpFfiStatus MissingDescriptorMetadataSnapshot(
            NnrpHandle handle,
            out NnrpHandle buffer,
            out NnrpBufferView view)
        {
            buffer = NnrpHandle.Invalid;
            view = NnrpBufferView.Empty;
            return new NnrpFfiStatus(NnrpFfiStatusCode.InternalError, NnrpErrorFamily.RuntimeObject);
        }

        private static NnrpFfiStatus MissingCacheReferenceDescriptorCreate(
            NnrpCacheReferenceDescriptor descriptor,
            NnrpBufferView metadata,
            out NnrpHandle handle)
        {
            handle = NnrpHandle.Invalid;
            return new NnrpFfiStatus(NnrpFfiStatusCode.InternalError, NnrpErrorFamily.RuntimeObject);
        }

        private static NnrpFfiStatus MissingCacheReferenceDescriptorView(
            NnrpHandle handle,
            out NnrpCacheReferenceDescriptor descriptor,
            out NnrpBufferView metadata)
        {
            descriptor = default(NnrpCacheReferenceDescriptor);
            metadata = NnrpBufferView.Empty;
            return new NnrpFfiStatus(NnrpFfiStatusCode.InternalError, NnrpErrorFamily.RuntimeObject);
        }

        private static NnrpFfiStatus MissingCacheLeaseRequest(NnrpCacheLeaseRequest request, out NnrpCacheLeaseResult result)
        {
            result = default(NnrpCacheLeaseResult);
            return new NnrpFfiStatus(NnrpFfiStatusCode.InternalError, NnrpErrorFamily.Cache);
        }

        private static NnrpFfiStatus MissingCachePrefetch(
            NnrpHandle owner,
            IntPtr objects,
            UIntPtr objectCount,
            ulong nowMilliseconds,
            uint ttlMilliseconds,
            IntPtr results)
        {
            return new NnrpFfiStatus(NnrpFfiStatusCode.InternalError, NnrpErrorFamily.Cache);
        }

        private static NnrpFfiStatus MissingCacheRelease(NnrpHandle lease, out NnrpCacheLeaseResult result)
        {
            result = default(NnrpCacheLeaseResult);
            return new NnrpFfiStatus(NnrpFfiStatusCode.InternalError, NnrpErrorFamily.Cache);
        }

        private static NnrpFfiStatus MissingTransportClientSecurityConfigCreate(
            NnrpTransportClientSecurityConfigRequest request,
            out NnrpHandle config)
        {
            config = NnrpHandle.Invalid;
            return new NnrpFfiStatus(NnrpFfiStatusCode.InternalError, NnrpErrorFamily.Transport);
        }

        private static NnrpFfiStatus MissingTransportServerSecurityConfigCreate(
            NnrpTransportServerSecurityConfigRequest request,
            out NnrpHandle config)
        {
            config = NnrpHandle.Invalid;
            return new NnrpFfiStatus(NnrpFfiStatusCode.InternalError, NnrpErrorFamily.Transport);
        }

        private static NnrpFfiStatus MissingTransportOpen(
            NnrpTransportOpenRequest request,
            out NnrpHandle handle)
        {
            handle = NnrpHandle.Invalid;
            return new NnrpFfiStatus(NnrpFfiStatusCode.InternalError, NnrpErrorFamily.Transport);
        }

        private static NnrpFfiStatus MissingTransportAccept(
            NnrpTransportAcceptRequest request,
            out NnrpHandle connection)
        {
            connection = NnrpHandle.Invalid;
            return new NnrpFfiStatus(NnrpFfiStatusCode.InternalError, NnrpErrorFamily.Transport);
        }

        private static NnrpFfiStatus MissingTransportListenerEndpoint(
            NnrpHandle listener,
            out NnrpHandle buffer,
            out NnrpBufferView endpoint)
        {
            buffer = NnrpHandle.Invalid;
            endpoint = NnrpBufferView.Empty;
            return new NnrpFfiStatus(NnrpFfiStatusCode.InternalError, NnrpErrorFamily.Transport);
        }

        private static NnrpFfiStatus MissingTransportProbe(
            NnrpTransportProbeRequest request,
            out NnrpTransportProbeResult result)
        {
            result = default(NnrpTransportProbeResult);
            return new NnrpFfiStatus(NnrpFfiStatusCode.InternalError, NnrpErrorFamily.Transport);
        }

        private static NnrpFfiStatus MissingRoleAwaitEvents(
            NnrpRoleEventPollRequest request,
            IntPtr events,
            UIntPtr eventCapacity,
            out UIntPtr eventCount)
        {
            eventCount = UIntPtr.Zero;
            return new NnrpFfiStatus(NnrpFfiStatusCode.InternalError, NnrpErrorFamily.Internal);
        }
    }

    public readonly struct NnrpNativeRuntimeDiagnostic
    {
        public NnrpNativeRuntimeDiagnostic(
            NnrpFfiStatus status,
            ulong relatedConnectionId,
            uint relatedSessionId,
            ulong relatedOperationId,
            uint relatedFrameId)
        {
            Status = status;
            RelatedConnectionId = relatedConnectionId;
            RelatedSessionId = relatedSessionId;
            RelatedOperationId = relatedOperationId;
            RelatedFrameId = relatedFrameId;
        }

        public NnrpFfiStatus Status { get; }

        public ulong RelatedConnectionId { get; }

        public uint RelatedSessionId { get; }

        public ulong RelatedOperationId { get; }

        public uint RelatedFrameId { get; }

        public static NnrpNativeRuntimeDiagnostic FromFfi(NnrpFfiDiagnostic diagnostic)
        {
            return new NnrpNativeRuntimeDiagnostic(
                diagnostic.Status,
                diagnostic.RelatedConnectionId,
                diagnostic.RelatedSessionId,
                diagnostic.RelatedOperationId,
                diagnostic.RelatedFrameId);
        }
    }

    public sealed class NnrpNativeRuntimeEvent
    {
        private const uint OperationLifecycleEventKind = 14;

        public NnrpNativeRuntimeEvent(
            uint kind,
            NnrpFfiRuntimeFrameHeader header,
            NnrpHandle connection,
            NnrpHandle session,
            NnrpHandle operation,
            byte[] payload,
            NnrpNativeRuntimeDiagnostic diagnostic)
        {
            Kind = kind;
            Header = header;
            Connection = connection;
            Session = session;
            Operation = operation;
            Payload = payload ?? throw new ArgumentNullException(nameof(payload));
            Diagnostic = diagnostic;
        }

        public uint Kind { get; }

        public NnrpFfiRuntimeFrameHeader Header { get; }

        public bool HasWireHeader => Header.Present != 0;

        public uint MessageType => Header.MessageType;

        public NnrpHandle Connection { get; }

        public NnrpHandle Session { get; }

        public NnrpHandle Operation { get; }

        public uint FrameId => Header.FrameId;

        public byte[] Payload { get; }

        public ReadOnlyMemory<byte> PayloadMemory => Payload;

        public ReadOnlySpan<byte> PayloadSpan => Payload;

        public NnrpNativeRuntimeDiagnostic Diagnostic { get; }

        internal bool IsTerminalClientOperationEvidence
        {
            get
            {
                if (Diagnostic.RelatedOperationId == 0)
                {
                    return false;
                }

                if (!HasWireHeader)
                {
                    if (Kind != OperationLifecycleEventKind || Payload.Length != 1)
                    {
                        return false;
                    }

                    var state = (NnrpOperationState)Payload[0];
                    return state == NnrpOperationState.Completed
                        || state == NnrpOperationState.Cancelled
                        || state == NnrpOperationState.Superseded
                        || state == NnrpOperationState.Failed;
                }

                return MessageType == (uint)global::Nnrp.Core.MessageType.Cancel
                    || MessageType == (uint)global::Nnrp.Core.MessageType.Abort
                    || MessageType == (uint)global::Nnrp.Core.MessageType.Supersede
                    || MessageType == (uint)global::Nnrp.Core.MessageType.ResultPush
                    || MessageType == (uint)global::Nnrp.Core.MessageType.ResultDrop
                    || MessageType == (uint)global::Nnrp.Core.MessageType.ResultDropReason;
            }
        }

        public NnrpRuntimeEvent ToRuntimeEvent()
        {
            if (!HasWireHeader)
            {
                throw new InvalidOperationException(
                    "Native lifecycle events do not carry a wire runtime-frame header.");
            }

            if (!Enum.IsDefined(typeof(MessageType), Header.MessageType))
            {
                throw new InvalidOperationException("Native runtime event carries an unknown message type.");
            }

            return NnrpRuntimeEvent.Decode(
                new RuntimeFrameHeader(
                    (MessageType)Header.MessageType,
                    (HeaderFlags)Header.Flags,
                    Header.SessionId,
                    Header.FrameId,
                    Header.ViewId,
                    Header.RouteId,
                    Header.TraceId,
                    Header.VersionMajor,
                    Header.WireFormat),
                Payload);
        }

        public NnrpOperationLifecycleEvent ToOperationLifecycleEvent()
        {
            Diagnostic.Status.ThrowIfError();
            if (HasWireHeader || Kind != OperationLifecycleEventKind)
            {
                throw new InvalidOperationException(
                    "Native operation-lifecycle events must be headerless and carry the frozen lifecycle kind.");
            }

            if (Diagnostic.RelatedOperationId == 0 || Payload.Length != 1)
            {
                throw new InvalidOperationException(
                    "Native operation-lifecycle events require a non-zero operation id and one state byte.");
            }

            var state = (NnrpOperationState)Payload[0];
            if (!Enum.IsDefined(typeof(NnrpOperationState), state))
            {
                throw new InvalidOperationException("Native operation-lifecycle event carries an unknown state.");
            }

            return new NnrpOperationLifecycleEvent(Diagnostic.RelatedOperationId, state);
        }

        public static NnrpNativeRuntimeEvent FromFfi(
            NnrpEvent @event,
            NnrpNativeRuntimeEntrypoints entrypoints)
        {
            if (entrypoints == null)
            {
                throw new ArgumentNullException(nameof(entrypoints));
            }

            try
            {
                return new NnrpNativeRuntimeEvent(
                    @event.Kind,
                    @event.Header,
                    @event.Connection,
                    @event.Session,
                    @event.Operation,
                    CopyPayload(@event.Payload),
                    NnrpNativeRuntimeDiagnostic.FromFfi(@event.Diagnostic));
            }
            finally
            {
                if (@event.PayloadOwner.IsValid)
                {
                    entrypoints.BufferRelease(@event.PayloadOwner).ThrowIfError();
                }
            }
        }

        public static NnrpNativeRuntimeEvent FromFfi(NnrpEvent @event)
        {
            if (@event.PayloadOwner.IsValid)
            {
                throw new ArgumentException(
                    "An owned native event requires entrypoints so its payload owner can be released.",
                    nameof(@event));
            }

            return new NnrpNativeRuntimeEvent(
                @event.Kind,
                @event.Header,
                @event.Connection,
                @event.Session,
                @event.Operation,
                CopyPayload(@event.Payload),
                NnrpNativeRuntimeDiagnostic.FromFfi(@event.Diagnostic));
        }

        private static byte[] CopyPayload(NnrpBufferView payload)
        {
            if (payload.Length == UIntPtr.Zero)
            {
                return Array.Empty<byte>();
            }

            if (payload.Pointer == IntPtr.Zero)
            {
                throw new ArgumentException("Native event payload has non-empty null pointer.", nameof(payload));
            }

            var bytes = new byte[checked((int)payload.Length.ToUInt64())];
            Marshal.Copy(payload.Pointer, bytes, 0, bytes.Length);
            return bytes;
        }
    }

    public enum NnrpNativeOperationLifecycle
    {
        Completed = 0,
        Partial = 1,
        Degraded = 2,
        StaleReuse = 3,
        Cancelled = 4,
        Failed = 5,
    }

    public sealed class NnrpNativeRuntimeResult
    {
        public NnrpNativeRuntimeResult(
            NnrpNativeOperationLifecycle state,
            ulong operationId,
            uint frameId,
            byte[] payload,
            NnrpNativeRuntimeEvent @event)
        {
            State = state;
            OperationId = operationId;
            FrameId = frameId;
            Payload = payload ?? throw new ArgumentNullException(nameof(payload));
            Event = @event ?? throw new ArgumentNullException(nameof(@event));
        }

        public NnrpNativeOperationLifecycle State { get; }

        public ulong OperationId { get; }

        public uint FrameId { get; }

        public byte[] Payload { get; }

        public ReadOnlyMemory<byte> PayloadMemory => Payload;

        public ReadOnlySpan<byte> PayloadSpan => Payload;

        public NnrpNativeRuntimeEvent Event { get; }

        public static NnrpNativeRuntimeResult FromEvent(
            NnrpNativeRuntimeEvent @event,
            NnrpNativeOperationLifecycle? state = null)
        {
            return new NnrpNativeRuntimeResult(
                state ?? InferLifecycle(@event),
                @event.Operation.Id,
                @event.FrameId,
                @event.Payload,
                @event);
        }

        private static NnrpNativeOperationLifecycle InferLifecycle(NnrpNativeRuntimeEvent @event)
        {
            if (!@event.Diagnostic.Status.Succeeded || @event.Kind == 10)
            {
                return NnrpNativeOperationLifecycle.Failed;
            }

            if (@event.Kind == 7)
            {
                return NnrpNativeOperationLifecycle.Cancelled;
            }

            return NnrpNativeOperationLifecycle.Completed;
        }
    }

    public readonly struct NnrpNativeRuntimePollResult
    {
        public NnrpNativeRuntimePollResult(NnrpFfiStatus status, NnrpNativeRuntimeEvent? @event)
        {
            Status = status;
            Event = @event;
        }

        public NnrpFfiStatus Status { get; }

        public NnrpNativeRuntimeEvent? Event { get; }

        public static NnrpNativeRuntimePollResult FromFfi(
            NnrpPollResult result,
            NnrpNativeRuntimeEntrypoints entrypoints)
        {
            return new NnrpNativeRuntimePollResult(
                result.Status,
                result.HasEvent != 0 ? NnrpNativeRuntimeEvent.FromFfi(result.Event, entrypoints) : null);
        }
    }

    public sealed class NnrpNativeRuntimeClient
    {
        public NnrpNativeRuntimeClient(NnrpNativeRuntimeEntrypoints entrypoints)
        {
            Entrypoints = entrypoints ?? throw new ArgumentNullException(nameof(entrypoints));
        }

        public NnrpNativeRuntimeEntrypoints Entrypoints { get; }

        public NnrpNativeRuntimeConnection Connect(
            ulong connectionId,
            uint generation,
            NnrpTransportConnection transportConnection)
        {
            if (transportConnection == null)
            {
                throw new ArgumentNullException(nameof(transportConnection));
            }

            return transportConnection.AdoptClient(connectionId, generation);
        }

    }

    public sealed class NnrpNativeRuntimeServer : IDisposable
    {
        private NnrpHandle pendingAccept = NnrpHandle.Invalid;
        private readonly NnrpNativeServerPolicyDispatcher? policyDispatcher;
        private readonly NnrpNativeSchemaRegistry? schemaRegistry;
        private readonly IDisposable? nativeOwnership;

        internal NnrpNativeRuntimeServer(
            NnrpNativeRuntimeEntrypoints entrypoints,
            NnrpConnectionHandle handle,
            NnrpNativeServerPolicyDispatcher? policyDispatcher = null,
            NnrpNativeSchemaRegistry? schemaRegistry = null,
            IDisposable? nativeOwnership = null)
        {
            Entrypoints = entrypoints ?? throw new ArgumentNullException(nameof(entrypoints));
            Handle = handle;
            this.policyDispatcher = policyDispatcher;
            this.schemaRegistry = schemaRegistry;
            this.nativeOwnership = nativeOwnership;
        }

        public NnrpNativeRuntimeEntrypoints Entrypoints { get; }

        public NnrpConnectionHandle Handle { get; }

        public bool IsClosed { get; private set; }

        internal static NnrpNativeRuntimeServer Bind(
            NnrpTransportListener transportListener,
            NnrpNativeServerBindOptions options)
        {
            if (transportListener == null)
            {
                throw new ArgumentNullException(nameof(transportListener));
            }

            return transportListener.AdoptServer(options);
        }

        public static NnrpNativeRuntimeServer Bind(
            NnrpTransportListener transportListener,
            NnrpNativeRuntimeServerHostOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            return Bind(
                transportListener,
                new NnrpNativeServerBindOptions(
                    options.ServerId,
                    options.ServerGeneration,
                    new[] { TypedPayloadProfileId.TokenValue },
                    Array.Empty<CacheObjectKind>(),
                    maxCacheObjects: 0,
                    maxCacheObjectBytes: 0,
                    resumeTokenBytes: 24,
                    maxInFlightOperations: 4,
                    grantedOperationCredit: 2,
                    leaseTtlMilliseconds: 30_000,
                    resumeWindowMilliseconds: 120_000,
                    NnrpSchemaRegistry.WithStandardProfiles(),
                    _ => new System.Threading.Tasks.ValueTask<NnrpNativeServerPolicyDecision>(
                        NnrpNativeServerPolicyDecision.Accept())));
        }

        public NnrpNativeRuntimeServerSession AcceptSession(
            ulong sessionHandleId,
            uint generation,
            uint timeoutMilliseconds = 0)
        {
            EnsureOpen();
            if (!pendingAccept.IsValid)
            {
                NnrpHandle accept;
                Entrypoints.ServerAcceptBegin(
                    new NnrpServerAcceptBeginRequest(Handle.Handle, sessionHandleId, generation),
                    out accept).ThrowIfError();
                accept.RequireKind(NnrpHandleKind.ServerAccept);
                pendingAccept = accept;
            }

            Entrypoints.ServerAcceptWait(
                new NnrpServerAcceptWaitRequest(pendingAccept, timeoutMilliseconds)).ThrowIfError();

            NnrpServerAcceptResult result;
            Entrypoints.ServerAcceptClaim(
                new NnrpServerAcceptClaimRequest(pendingAccept, sessionHandleId, generation),
                out result).ThrowIfError();
            pendingAccept = NnrpHandle.Invalid;
            result.Session.RequireKind(NnrpHandleKind.Session);
            if (!Enum.IsDefined(typeof(TransportId), result.ActiveTransportId)
                || result.ActiveTransportId == (uint)TransportId.Unspecified)
            {
                var message = "Native server accept returned unsupported transport id "
                    + result.ActiveTransportId
                    + ".";
                try
                {
                    Entrypoints.ServerClose(result.Session).ThrowIfError();
                }
                catch (Exception closeError)
                {
                    throw new NnrpNativeArtifactException(message, closeError);
                }

                throw new NnrpNativeArtifactException(message);
            }

            return new NnrpNativeRuntimeServerSession(
                Entrypoints,
                Handle,
                new NnrpSessionHandle(result.Session),
                (TransportId)result.ActiveTransportId,
                () => IsClosed);
        }

        internal bool ReleasePendingAccept()
        {
            EnsureOpen();
            if (!pendingAccept.IsValid)
            {
                return false;
            }

            try
            {
                Entrypoints.ServerAcceptRelease(pendingAccept).ThrowIfError();
                return true;
            }
            finally
            {
                pendingAccept = NnrpHandle.Invalid;
            }
        }

        public void Close()
        {
            EnsureOpen();
            Exception? firstError = null;
            try
            {
                policyDispatcher?.Dispose();
            }
            catch (Exception error)
            {
                firstError = error;
            }

            if (pendingAccept.IsValid)
            {
                try
                {
                    ReleasePendingAccept();
                }
                catch (Exception error)
                {
                    firstError ??= error;
                }
            }

            try
            {
                Entrypoints.ConnectionClose(Handle.Handle).ThrowIfError();
            }
            catch (Exception error)
            {
                firstError = firstError ?? error;
            }
            finally
            {
                IsClosed = true;
                try
                {
                    schemaRegistry?.Dispose();
                }
                catch (Exception error)
                {
                    firstError ??= error;
                }

                try
                {
                    nativeOwnership?.Dispose();
                }
                catch (Exception error)
                {
                    firstError ??= error;
                }
            }

            if (firstError != null)
            {
                throw firstError;
            }
        }

        public void Dispose()
        {
            if (IsClosed)
            {
                return;
            }

            Close();
        }

        private void EnsureOpen()
        {
            if (IsClosed)
            {
                throw new NnrpNativeInvalidStateException(new NnrpFfiStatus(NnrpFfiStatusCode.InvalidState));
            }
        }
    }

    public sealed class NnrpNativeRuntimeServerSession
    {
        private readonly object operationGate = new object();
        private readonly Dictionary<ulong, uint> operationFrames = new Dictionary<ulong, uint>();
        private uint nextRuntimeFrameId = 1;

        public NnrpNativeRuntimeServerSession(
            NnrpNativeRuntimeEntrypoints entrypoints,
            NnrpConnectionHandle server,
            NnrpSessionHandle handle,
            TransportId activeTransportId,
            Func<bool>? isServerClosed = null)
        {
            Entrypoints = entrypoints ?? throw new ArgumentNullException(nameof(entrypoints));
            Server = server;
            Handle = handle;
            if (activeTransportId == TransportId.Unspecified)
            {
                throw new ArgumentOutOfRangeException(nameof(activeTransportId));
            }

            ActiveTransportId = activeTransportId;
            IsServerClosed = isServerClosed ?? (() => false);
        }

        public NnrpNativeRuntimeEntrypoints Entrypoints { get; }

        public NnrpConnectionHandle Server { get; }

        public NnrpSessionHandle Handle { get; }

        public TransportId ActiveTransportId { get; }

        public bool IsClosed { get; private set; }

        private Func<bool> IsServerClosed { get; }

        public NnrpNativeRuntimeOperation ReceiveSubmit(ulong operationId, uint frameId, byte[]? payload = null)
        {
            return ReceiveSubmit(operationId, frameId, payload == null ? ReadOnlyMemory<byte>.Empty : payload);
        }

        public NnrpNativeRuntimeOperation ReceiveSubmit(ulong operationId, uint frameId, ReadOnlyMemory<byte> payload)
        {
            EnsureOpen();
            return NnrpNativeRuntimeSession.WithBorrowedView(
                payload,
                payloadView =>
                {
                    NnrpHandle operation;
                    var status = Entrypoints.ServerReceiveSubmit(
                        new NnrpServerReceiveSubmitRequest(Handle.Handle, operationId, frameId, payloadView),
                        out operation);
                    status.ThrowIfError();
                    return new NnrpNativeRuntimeOperation(
                        Entrypoints,
                        Handle,
                        new NnrpOperationHandle(operation),
                        operationId,
                        frameId);
                });
        }

        public NnrpNativeRuntimeOperation ReceiveSubmit(ulong operationId, uint frameId, NnrpNativeBuffer payload)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            EnsureOpen();
            NnrpHandle operation;
            var status = Entrypoints.ServerReceiveSubmit(
                new NnrpServerReceiveSubmitRequest(Handle.Handle, operationId, frameId, payload.BorrowView()),
                out operation);
            status.ThrowIfError();
            return new NnrpNativeRuntimeOperation(
                Entrypoints,
                Handle,
                new NnrpOperationHandle(operation),
                operationId,
                frameId);
        }

        public void SendResult(NnrpNativeRuntimeOperation operation, byte[]? payload = null)
        {
            SendResult(operation, payload == null ? ReadOnlyMemory<byte>.Empty : payload);
        }

        public void SendResult(NnrpNativeRuntimeOperation operation, ReadOnlyMemory<byte> payload)
        {
            if (operation == null)
            {
                throw new ArgumentNullException(nameof(operation));
            }

            EnsureOpen();
            NnrpNativeRuntimeSession.WithBorrowedView(
                payload,
                payloadView =>
                {
                    Entrypoints.ServerSendResult(
                        new NnrpServerSendResultRequest(operation.Handle.Handle, payloadView)).ThrowIfError();
                    return true;
                });
            CompleteOperation(operation.OperationId);
        }

        public void SendResult(NnrpNativeRuntimeOperation operation, NnrpNativeBuffer payload)
        {
            if (operation == null)
            {
                throw new ArgumentNullException(nameof(operation));
            }

            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            EnsureOpen();
            Entrypoints.ServerSendResult(
                new NnrpServerSendResultRequest(operation.Handle.Handle, payload.BorrowView())).ThrowIfError();
            CompleteOperation(operation.OperationId);
        }

        public void SendProgress(
            NnrpNativeRuntimeOperation operation,
            ProgressMetadata metadata,
            ReadOnlyMemory<byte> body = default)
        {
            SendOperationRuntimeFrame(operation, MessageType.Progress, metadata.OperationId, metadata, body);
        }

        public void SendPartialResult(
            NnrpNativeRuntimeOperation operation,
            PartialResultMetadata metadata,
            ReadOnlyMemory<byte> body = default)
        {
            SendOperationRuntimeFrame(operation, MessageType.PartialResult, metadata.OperationId, metadata, body);
        }

        public void DropResult(
            NnrpNativeRuntimeOperation operation,
            ResultDropReasonMetadata metadata,
            ReadOnlyMemory<byte> diagnostic = default)
        {
            if (operation == null)
            {
                throw new ArgumentNullException(nameof(operation));
            }

            if (metadata.OperationId != operation.OperationId)
            {
                throw new ArgumentException(
                    "Result-drop operation id does not match the native operation.",
                    nameof(metadata));
            }

            EnsureOpen();
            var payload = NnrpRuntimeControl.Encode(
                MessageType.ResultDropReason,
                metadata,
                diagnostic.Span);
            NnrpNativeRuntimeSession.WithBorrowedView(
                payload,
                payloadView =>
                {
                    Entrypoints.RuntimeFrameSend(
                        new NnrpRuntimeFrameSendRequest(
                            operation.Handle.Handle,
                            (uint)MessageType.ResultDropReason,
                            operation.FrameId,
                            payloadView)).ThrowIfError();
                    return true;
                });
            CompleteOperation(operation.OperationId);
        }

        public void SendFlowUpdate(uint frameId)
        {
            EnsureOpen();
            Entrypoints.ServerSendFlowUpdate(new NnrpServerFlowUpdateRequest(Handle.Handle, frameId)).ThrowIfError();
        }

        public void SendBackpressure(PressureMetadata metadata)
        {
            SendRuntimeFrame(MessageType.Backpressure, metadata, ReadOnlyMemory<byte>.Empty);
        }

        public void SendCreditUpdate(PressureMetadata metadata)
        {
            SendRuntimeFrame(MessageType.CreditUpdate, metadata, ReadOnlyMemory<byte>.Empty);
        }

        public void SendTraceContext(TraceContextMetadata metadata, ReadOnlyMemory<byte> body = default)
        {
            SendRuntimeFrame(MessageType.TraceContext, metadata, body);
        }

        public void SendRuntimeObject(
            MessageType messageType,
            IRuntimeObjectMetadata metadata,
            ReadOnlyMemory<byte> tail = default)
        {
            EnsureOpen();
            SendEncodedRuntimeFrame(
                messageType,
                NnrpRuntimeObject.Encode(messageType, metadata, tail.Span),
                RuntimeOperationId(messageType, metadata));
        }

        public void SendCacheInvalidate(CacheInvalidateMetadata metadata)
        {
            EnsureOpen();
            SendEncodedRuntimeFrame(MessageType.CacheInvalidate, metadata.ToArray());
        }

        public void SendRecoverableError(
            RecoverableErrorMetadata metadata,
            ReadOnlyMemory<byte> diagnostic = default)
        {
            SendRuntimeFrame(MessageType.ErrorRecoverable, metadata, diagnostic);
        }

        public void SendRetryAfter(RetryAfterMetadata metadata, ReadOnlyMemory<byte> diagnostic = default)
        {
            SendRuntimeFrame(MessageType.RetryAfter, metadata, diagnostic);
        }

        public NnrpCacheLeaseResult QueryCacheLease(
            NnrpCacheObjectId objectId,
            ulong expectedVersion,
            ulong nowMilliseconds,
            uint ttlMilliseconds)
        {
            EnsureOpen();
            return new NnrpNativeCacheLeases(Entrypoints).Query(
                new NnrpCacheLeaseRequest(
                    Handle.Handle,
                    objectId,
                    expectedVersion,
                    nowMilliseconds,
                    ttlMilliseconds));
        }

        public NnrpCacheLeaseResult TouchCacheLease(
            NnrpCacheObjectId objectId,
            ulong expectedVersion,
            ulong nowMilliseconds,
            uint ttlMilliseconds)
        {
            EnsureOpen();
            return new NnrpNativeCacheLeases(Entrypoints).Touch(
                new NnrpCacheLeaseRequest(
                    Handle.Handle,
                    objectId,
                    expectedVersion,
                    nowMilliseconds,
                    ttlMilliseconds));
        }

        public NnrpCacheLeaseResult[] PrefetchCacheLeases(
            NnrpCacheObjectId[] objects,
            ulong nowMilliseconds,
            uint ttlMilliseconds)
        {
            EnsureOpen();
            return new NnrpNativeCacheLeases(Entrypoints).Prefetch(
                Handle.Handle,
                objects,
                nowMilliseconds,
                ttlMilliseconds);
        }

        public NnrpCacheLeaseResult ReleaseCacheLease(NnrpCacheLeaseHandle lease)
        {
            EnsureOpen();
            return new NnrpNativeCacheLeases(Entrypoints).Release(lease);
        }

        public void Control(uint controlCode, byte[]? payload = null)
        {
            EnsureOpen();
            NnrpNativeRuntimeSession.SendControl(Entrypoints, Handle.Handle, controlCode, payload);
        }

        public void Control(uint controlCode, ReadOnlyMemory<byte> payload)
        {
            EnsureOpen();
            NnrpNativeRuntimeSession.SendControl(Entrypoints, Handle.Handle, controlCode, payload);
        }

        public void Control(uint controlCode, NnrpNativeBuffer payload)
        {
            EnsureOpen();
            NnrpNativeRuntimeSession.SendControl(Entrypoints, Handle.Handle, controlCode, payload);
        }

        public IReadOnlyList<NnrpNativeRuntimeEvent> AwaitEvents(
            uint maxEvents = 1,
            uint timeoutMilliseconds = 0)
        {
            EnsureOpen();
            if (maxEvents == 0)
            {
                return Array.Empty<NnrpNativeRuntimeEvent>();
            }

            var eventSize = Marshal.SizeOf<NnrpEvent>();
            var allocationSize = checked(eventSize * checked((int)maxEvents));
            var events = Marshal.AllocHGlobal(allocationSize);
            try
            {
                UIntPtr eventCount;
                var status = Entrypoints.ServerAwaitEvents(
                    new NnrpRoleEventPollRequest(Handle.Handle, maxEvents, timeoutMilliseconds),
                    events,
                    new UIntPtr(maxEvents),
                    out eventCount);
                var count = eventCount.ToUInt64();
                if (count > maxEvents)
                {
                    throw new NnrpNativeArtifactException(
                        "Native server event poll returned more events than the supplied capacity.");
                }

                if (!status.Succeeded)
                {
                    ReleaseRemainingEventPayloads(events, eventSize, checked((int)count));
                    status.ThrowIfError();
                }

                var snapshots = new NnrpNativeRuntimeEvent[checked((int)count)];
                var consumed = 0;
                try
                {
                    for (var index = 0; index < snapshots.Length; index++)
                    {
                        var nativeEvent = Marshal.PtrToStructure<NnrpEvent>(
                            IntPtr.Add(events, checked(index * eventSize)));
                        consumed = index + 1;
                        snapshots[index] = NnrpNativeRuntimeEvent.FromFfi(nativeEvent, Entrypoints);
                        ObserveOperationEvent(snapshots[index]);
                    }
                }
                catch
                {
                    ReleaseRemainingEventPayloads(events, eventSize, snapshots.Length, consumed);
                    throw;
                }

                return snapshots;
            }
            finally
            {
                Marshal.FreeHGlobal(events);
            }
        }

        private void ReleaseRemainingEventPayloads(IntPtr events, int eventSize, int count, int startIndex = 0)
        {
            for (var index = startIndex; index < count; index++)
            {
                var nativeEvent = Marshal.PtrToStructure<NnrpEvent>(
                    IntPtr.Add(events, checked(index * eventSize)));
                if (nativeEvent.PayloadOwner.IsValid)
                {
                    try
                    {
                        Entrypoints.BufferRelease(nativeEvent.PayloadOwner).ThrowIfError();
                    }
                    catch
                    {
                        // Cleanup must not hide the poll or conversion failure that selected this path.
                    }
                }
            }
        }

        public void Close()
        {
            EnsureOpen();
            Entrypoints.ServerClose(Handle.Handle).ThrowIfError();
            lock (operationGate)
            {
                operationFrames.Clear();
            }
            IsClosed = true;
        }

        private void RememberOperationFrame(ulong operationId, uint frameId)
        {
            lock (operationGate)
            {
                operationFrames[operationId] = frameId;
            }
        }

        private void CompleteOperation(ulong operationId)
        {
            lock (operationGate)
            {
                operationFrames.Remove(operationId);
            }
        }

        private void ObserveOperationEvent(NnrpNativeRuntimeEvent @event)
        {
            var operationId = @event.Diagnostic.RelatedOperationId;
            if (operationId == 0)
            {
                return;
            }

            if (@event.HasWireHeader && @event.MessageType == (uint)MessageType.FrameSubmit)
            {
                RememberOperationFrame(operationId, @event.FrameId);
            }
        }

        private void SendRuntimeFrame(
            MessageType messageType,
            IRuntimeControlMetadata metadata,
            ReadOnlyMemory<byte> tail)
        {
            EnsureOpen();
            SendEncodedRuntimeFrame(messageType, NnrpRuntimeControl.Encode(messageType, metadata, tail.Span));
        }

        private void SendOperationRuntimeFrame(
            NnrpNativeRuntimeOperation operation,
            MessageType messageType,
            ulong metadataOperationId,
            IRuntimeControlMetadata metadata,
            ReadOnlyMemory<byte> tail)
        {
            if (operation == null)
            {
                throw new ArgumentNullException(nameof(operation));
            }

            if (metadataOperationId != operation.OperationId)
            {
                throw new ArgumentException(
                    "Operation-scoped runtime metadata does not match the native operation.",
                    nameof(metadata));
            }

            EnsureOpen();
            var payload = NnrpRuntimeControl.Encode(messageType, metadata, tail.Span);
            NnrpNativeRuntimeSession.WithBorrowedView(
                payload,
                payloadView =>
                {
                    Entrypoints.RuntimeFrameSend(
                        new NnrpRuntimeFrameSendRequest(
                            operation.Handle.Handle,
                            (uint)messageType,
                            operation.FrameId,
                            payloadView)).ThrowIfError();
                    return true;
                });
        }

        private void SendEncodedRuntimeFrame(
            MessageType messageType,
            ReadOnlyMemory<byte> payload,
            ulong? operationId = null)
        {
            var frameId = ResolveRuntimeFrameId(messageType, operationId);
            NnrpNativeRuntimeSession.WithBorrowedView(
                payload,
                payloadView =>
                {
                    Entrypoints.RuntimeFrameSend(
                        new NnrpRuntimeFrameSendRequest(
                            Handle.Handle,
                            (uint)messageType,
                            frameId,
                            payloadView)).ThrowIfError();
                    return true;
                });
            if (!operationId.HasValue)
            {
                nextRuntimeFrameId = frameId == uint.MaxValue ? 1 : frameId + 1;
            }
        }

        private uint ResolveRuntimeFrameId(MessageType messageType, ulong? operationId)
        {
            if (!operationId.HasValue)
            {
                return nextRuntimeFrameId;
            }

            if (operationId.Value == 0)
            {
                return 0;
            }

            lock (operationGate)
            {
                if (operationFrames.TryGetValue(operationId.Value, out var frameId))
                {
                    return frameId;
                }
            }

            throw new InvalidOperationException(
                $"{messageType} references inactive operation {operationId.Value}.");
        }

        private static ulong? RuntimeOperationId(MessageType messageType, IRuntimeObjectMetadata metadata)
        {
            return messageType switch
            {
                MessageType.ObjectRef when metadata is ObjectReferenceMetadata value => value.OperationId,
                MessageType.ObjectRelease when metadata is ObjectReleaseMetadata value => value.OperationId,
                _ => null,
            };
        }

        private void EnsureOpen()
        {
            if (IsClosed || IsServerClosed())
            {
                throw new NnrpNativeInvalidStateException(new NnrpFfiStatus(NnrpFfiStatusCode.InvalidState));
            }
        }
    }

    public sealed class NnrpNativeRuntimeConnection : IDisposable
    {
        private readonly object eventGate = new object();
        private readonly Queue<NnrpNativeRuntimeEvent> bufferedEvents = new Queue<NnrpNativeRuntimeEvent>();
        private readonly IDisposable? nativeOwnership;

        public NnrpNativeRuntimeConnection(
            NnrpNativeRuntimeEntrypoints entrypoints,
            NnrpConnectionHandle handle,
            IDisposable? nativeOwnership = null)
        {
            Entrypoints = entrypoints ?? throw new ArgumentNullException(nameof(entrypoints));
            Handle = handle;
            this.nativeOwnership = nativeOwnership;
        }

        public NnrpNativeRuntimeEntrypoints Entrypoints { get; }

        public NnrpConnectionHandle Handle { get; }

        public bool IsClosed { get; private set; }

        public NnrpNativeRuntimeSession OpenSession(
            uint requestedSessionId,
            ulong sessionHandleId,
            uint generation,
            ushort profileId,
            SessionPriorityClass priorityClass,
            uint schemaId,
            uint schemaVersion,
            uint defaultDeadlineMilliseconds,
            ushort maxInFlightOperations,
            uint leaseTtlHintMilliseconds,
            bool allowResume,
            uint resumeTokenBytes,
            IReadOnlyList<CacheObjectKind> cacheHints)
        {
            EnsureOpen();
            return WithPinnedCacheHints(
                cacheHints,
                cacheHintSlice =>
                {
                    Entrypoints.ClientOpenSession(
                        new NnrpSessionOpenRequest(
                            Handle.Handle,
                            requestedSessionId,
                            sessionHandleId,
                            generation,
                            profileId,
                            priorityClass,
                            allowResume,
                            schemaId,
                            schemaVersion,
                            defaultDeadlineMilliseconds,
                            maxInFlightOperations,
                            leaseTtlHintMilliseconds,
                            resumeTokenBytes,
                            cacheHintSlice),
                        out var session).ThrowIfError();
                    return new NnrpNativeRuntimeSession(
                        Entrypoints,
                        Handle,
                        new NnrpSessionHandle(session),
                        () => IsClosed,
                        this);
                });
        }

        public NnrpNativeRuntimeSession ResumeSession(
            uint requestedSessionId,
            ulong sessionHandleId,
            uint generation,
            ushort profileId,
            SessionPriorityClass priorityClass,
            uint schemaId,
            uint schemaVersion,
            uint defaultDeadlineMilliseconds,
            ushort maxInFlightOperations,
            uint leaseTtlHintMilliseconds,
            uint resumeTokenBytes,
            IReadOnlyList<CacheObjectKind> cacheHints,
            ReadOnlyMemory<byte> recoveryTicket,
            out NnrpSessionRecoveryOutcome recoveryOutcome)
        {
            EnsureOpen();
            NnrpSessionRecoveryOutcome outcome = default;
            var resumed = WithPinnedCacheHints(
                cacheHints,
                cacheHintSlice => NnrpNativeRuntimeSession.WithBorrowedView(
                    recoveryTicket,
                    ticketView =>
                    {
                        Entrypoints.ClientResumeSession(
                            new NnrpSessionResumeRequest(
                                new NnrpSessionOpenRequest(
                                    Handle.Handle,
                                    requestedSessionId,
                                    sessionHandleId,
                                    generation,
                                    profileId,
                                    priorityClass,
                                    allowResume: true,
                                    schemaId,
                                    schemaVersion,
                                    defaultDeadlineMilliseconds,
                                    maxInFlightOperations,
                                    leaseTtlHintMilliseconds,
                                    resumeTokenBytes,
                                    cacheHintSlice),
                                ticketView),
                            out var session,
                            out outcome).ThrowIfError();
                        return new NnrpNativeRuntimeSession(
                            Entrypoints,
                            Handle,
                            new NnrpSessionHandle(session),
                            () => IsClosed,
                            this);
                    }));
            recoveryOutcome = outcome;
            return resumed;
        }

        public NnrpNativeRuntimePollResult AwaitEvent()
        {
            EnsureOpen();
            if (TryDequeueBufferedEvent(_ => true, out var bufferedEvent))
            {
                return new NnrpNativeRuntimePollResult(NnrpFfiStatus.Ok, bufferedEvent);
            }

            return AwaitNativeEvent();
        }

        private NnrpNativeRuntimePollResult AwaitNativeEvent()
        {
            NnrpPollResult result;
            var status = Entrypoints.ClientAwaitEvent(Handle.Handle, out result);
            status.ThrowIfError();
            result.Status.ThrowIfError();
            return NnrpNativeRuntimePollResult.FromFfi(result, Entrypoints);
        }

        public NnrpNativeRuntimeEvent? PollEvent()
        {
            return AwaitEvent().Event;
        }

        public IReadOnlyList<NnrpNativeRuntimeEvent> PollAvailableEvents(int maxEvents = 0)
        {
            if (maxEvents < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxEvents), "maxEvents must be non-negative.");
            }

            var events = new List<NnrpNativeRuntimeEvent>();
            while (maxEvents == 0 || events.Count < maxEvents)
            {
                var @event = PollEvent();
                if (@event == null)
                {
                    break;
                }

                events.Add(@event);
            }

            return events;
        }

        public void Control(uint controlCode, byte[]? payload = null)
        {
            EnsureOpen();
            NnrpNativeRuntimeSession.SendControl(Entrypoints, Handle.Handle, controlCode, payload);
        }

        public void Control(uint controlCode, ReadOnlyMemory<byte> payload)
        {
            EnsureOpen();
            NnrpNativeRuntimeSession.SendControl(Entrypoints, Handle.Handle, controlCode, payload);
        }

        public void Control(uint controlCode, NnrpNativeBuffer payload)
        {
            EnsureOpen();
            NnrpNativeRuntimeSession.SendControl(Entrypoints, Handle.Handle, controlCode, payload);
        }

        public void Close()
        {
            EnsureOpen();
            try
            {
                Entrypoints.ClientCloseConnection(Handle.Handle).ThrowIfError();
            }
            finally
            {
                lock (eventGate)
                {
                    bufferedEvents.Clear();
                }

                IsClosed = true;
                nativeOwnership?.Dispose();
            }
        }

        public void Dispose()
        {
            if (IsClosed)
            {
                return;
            }

            Close();
        }

        private void EnsureOpen()
        {
            if (IsClosed)
            {
                throw new NnrpNativeInvalidStateException(new NnrpFfiStatus(NnrpFfiStatusCode.InvalidState));
            }
        }

        private static T WithPinnedCacheHints<T>(
            IReadOnlyList<CacheObjectKind> cacheHints,
            Func<NnrpU32Slice, T> action)
        {
            if (cacheHints == null)
            {
                throw new ArgumentNullException(nameof(cacheHints));
            }

            var values = cacheHints.Select(value => (uint)value).ToArray();
            if (values.Length == 0)
            {
                return action(NnrpU32Slice.Empty);
            }

            var owner = GCHandle.Alloc(values, GCHandleType.Pinned);
            try
            {
                return action(new NnrpU32Slice(owner.AddrOfPinnedObject(), new UIntPtr((uint)values.Length)));
            }
            finally
            {
                owner.Free();
            }
        }

        internal NnrpNativeRuntimeEvent? PollEvent(
            Predicate<NnrpNativeRuntimeEvent> predicate,
            int maxEvents)
        {
            EnsureOpen();
            if (TryDequeueBufferedEvent(predicate, out var bufferedEvent))
            {
                return bufferedEvent;
            }

            var seenEvents = 0;
            while (maxEvents == 0 || seenEvents < maxEvents)
            {
                var snapshot = AwaitNativeEvent();
                var @event = snapshot.Event;
                if (@event == null)
                {
                    break;
                }

                seenEvents++;
                if (predicate(@event))
                {
                    return @event;
                }

                lock (eventGate)
                {
                    bufferedEvents.Enqueue(@event);
                }
            }

            return null;
        }

        private bool TryDequeueBufferedEvent(
            Predicate<NnrpNativeRuntimeEvent> predicate,
            out NnrpNativeRuntimeEvent? @event)
        {
            lock (eventGate)
            {
                @event = default;
                if (bufferedEvents.Count == 0)
                {
                    return false;
                }

                var found = false;
                var remainingEventCount = bufferedEvents.Count;
                for (var i = 0; i < remainingEventCount; i++)
                {
                    var candidate = bufferedEvents.Dequeue();
                    if (!found && predicate(candidate))
                    {
                        @event = candidate;
                        found = true;
                        continue;
                    }

                    bufferedEvents.Enqueue(candidate);
                }

                return found;
            }
        }
    }

    public sealed class NnrpNativeRuntimeSession
    {
        private readonly object operationGate = new object();
        private readonly Dictionary<ulong, uint> operationFrames = new Dictionary<ulong, uint>();
        private uint nextRuntimeFrameId = 1;

        public NnrpNativeRuntimeSession(
            NnrpNativeRuntimeEntrypoints entrypoints,
            NnrpConnectionHandle connection,
            NnrpSessionHandle handle,
            Func<bool>? isConnectionClosed = null,
            NnrpNativeRuntimeConnection? runtimeConnection = null)
        {
            Entrypoints = entrypoints ?? throw new ArgumentNullException(nameof(entrypoints));
            Connection = connection;
            Handle = handle;
            IsConnectionClosed = isConnectionClosed ?? (() => false);
            RuntimeConnection = runtimeConnection;
        }

        public NnrpNativeRuntimeEntrypoints Entrypoints { get; }

        public NnrpConnectionHandle Connection { get; }

        public NnrpSessionHandle Handle { get; }

        public bool IsClosed { get; private set; }

        private Func<bool> IsConnectionClosed { get; }

        private NnrpNativeRuntimeConnection? RuntimeConnection { get; }

        internal byte[]? GetRecoveryTicketBytes()
        {
            EnsureOpen();
            var status = Entrypoints.ClientSessionRecoveryTicket(
                Handle.Handle,
                out var owner,
                out var ticket);
            if (status.StatusCode == NnrpFfiStatusCode.InvalidArgument && status.DetailCode == 104)
            {
                return null;
            }

            status.ThrowIfError();
            try
            {
                if (ticket.Length.ToUInt64() > int.MaxValue)
                {
                    throw new NnrpNativeArtifactException("Native recovery ticket exceeds managed buffer limits.");
                }

                var encoded = new byte[(int)ticket.Length.ToUInt64()];
                if (encoded.Length != 0)
                {
                    if (ticket.Pointer == IntPtr.Zero)
                    {
                        throw new NnrpNativeArtifactException("Native recovery ticket returned a null data pointer.");
                    }

                    Marshal.Copy(ticket.Pointer, encoded, 0, encoded.Length);
                }

                return encoded;
            }
            finally
            {
                if (owner.IsValid)
                {
                    Entrypoints.BufferRelease(owner).ThrowIfError();
                }
            }
        }

        public NnrpOperationHandle Submit(ulong operationId, RuntimeFrameHeader header, byte[]? payload = null)
        {
            EnsureOpen();
            return SubmitOperation(operationId, header, payload).Handle;
        }

        public NnrpOperationHandle Submit(ulong operationId, RuntimeFrameHeader header, ReadOnlyMemory<byte> payload)
        {
            EnsureOpen();
            return SubmitOperation(operationId, header, payload).Handle;
        }

        public NnrpOperationHandle Submit(ulong operationId, RuntimeFrameHeader header, NnrpNativeBuffer payload)
        {
            EnsureOpen();
            return SubmitOperation(operationId, header, payload).Handle;
        }

        public NnrpNativeRuntimeOperation SubmitOperation(
            ulong operationId,
            RuntimeFrameHeader header,
            byte[]? payload = null,
            ulong? parentOperationId = null,
            ulong? operationGroupId = null)
        {
            return SubmitOperation(
                operationId,
                header,
                payload == null ? ReadOnlyMemory<byte>.Empty : payload,
                parentOperationId,
                operationGroupId);
        }

        public NnrpNativeRuntimeOperation SubmitOperation(
            ulong operationId,
            RuntimeFrameHeader header,
            ReadOnlyMemory<byte> payload,
            ulong? parentOperationId = null,
            ulong? operationGroupId = null)
        {
            EnsureOpen();
            ValidateSubmitHeader(header);
            return WithBorrowedView(
                payload,
                payloadView =>
                {
                    NnrpHandle operation;
                    var status = Entrypoints.ClientSubmit(
                        new NnrpFfiSubmitRequest(
                            Handle.Handle,
                            operationId,
                            header.FrameId,
                            (uint)header.Flags,
                            header.ViewId,
                            header.RouteId,
                            header.TraceId,
                            payloadView),
                        out operation);
                    status.ThrowIfError();
                    RememberOperationFrame(operationId, header.FrameId);
                    return new NnrpNativeRuntimeOperation(
                        Entrypoints,
                        Handle,
                        new NnrpOperationHandle(operation),
                        operationId,
                        header.FrameId,
                        parentOperationId,
                        operationGroupId);
                });
        }

        public NnrpNativeRuntimeOperation SubmitOperation(
            ulong operationId,
            RuntimeFrameHeader header,
            NnrpNativeBuffer payload,
            ulong? parentOperationId = null,
            ulong? operationGroupId = null)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            EnsureOpen();
            ValidateSubmitHeader(header);
            NnrpHandle operation;
            var status = Entrypoints.ClientSubmit(
                new NnrpFfiSubmitRequest(
                    Handle.Handle,
                    operationId,
                    header.FrameId,
                    (uint)header.Flags,
                    header.ViewId,
                    header.RouteId,
                    header.TraceId,
                    payload.BorrowView()),
                out operation);
            status.ThrowIfError();
            RememberOperationFrame(operationId, header.FrameId);
            return new NnrpNativeRuntimeOperation(
                Entrypoints,
                Handle,
                new NnrpOperationHandle(operation),
                operationId,
                header.FrameId,
                parentOperationId,
                operationGroupId);
        }

        public Task<NnrpNativeRuntimeOperation> SubmitOperationAsync(
            ulong operationId,
            RuntimeFrameHeader header,
            byte[]? payload = null,
            ulong? parentOperationId = null,
            ulong? operationGroupId = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            EnsureOpen();
            ValidateSubmitHeader(header);
            if (cancellationToken.IsCancellationRequested)
            {
                Cancel(header.FrameId);
                return Task.FromCanceled<NnrpNativeRuntimeOperation>(cancellationToken);
            }

            using (cancellationToken.Register(() => Cancel(header.FrameId)))
            {
                var operation = SubmitOperation(
                    operationId,
                    header,
                    payload,
                    parentOperationId,
                    operationGroupId);
                return Task.FromResult(operation);
            }
        }

        public Task<NnrpNativeRuntimeOperation> SubmitOperationAsync(
            ulong operationId,
            RuntimeFrameHeader header,
            ReadOnlyMemory<byte> payload,
            ulong? parentOperationId = null,
            ulong? operationGroupId = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            EnsureOpen();
            ValidateSubmitHeader(header);
            if (cancellationToken.IsCancellationRequested)
            {
                Cancel(header.FrameId);
                return Task.FromCanceled<NnrpNativeRuntimeOperation>(cancellationToken);
            }

            using (cancellationToken.Register(() => Cancel(header.FrameId)))
            {
                var operation = SubmitOperation(
                    operationId,
                    header,
                    payload,
                    parentOperationId,
                    operationGroupId);
                return Task.FromResult(operation);
            }
        }

        public NnrpNativeRuntimeResult PollResult(
            NnrpNativeRuntimeOperation operation,
            NnrpNativeOperationLifecycle? state = null,
            int maxEvents = 0)
        {
            EnsureOpen();
            if (maxEvents < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxEvents), "maxEvents must be non-negative.");
            }

            if (RuntimeConnection != null)
            {
                var routedEvent = RuntimeConnection.PollEvent(candidate => EventMatchesOperation(candidate, operation), maxEvents);
                if (routedEvent != null)
                {
                    return NnrpNativeRuntimeResult.FromEvent(routedEvent, state);
                }

                throw new NnrpNativeWouldBlockException(new NnrpFfiStatus(NnrpFfiStatusCode.WouldBlock));
            }

            var seenEvents = 0;
            while (maxEvents == 0 || seenEvents < maxEvents)
            {
                NnrpPollResult result;
                var status = Entrypoints.ClientAwaitEvent(Connection.Handle, out result);
                status.ThrowIfError();
                result.Status.ThrowIfError();

                var snapshot = NnrpNativeRuntimePollResult.FromFfi(result, Entrypoints);
                var @event = snapshot.Event;
                if (@event == null)
                {
                    break;
                }

                seenEvents++;
                if (EventMatchesOperation(@event, operation))
                {
                    return NnrpNativeRuntimeResult.FromEvent(@event, state);
                }
            }

            throw new NnrpNativeWouldBlockException(new NnrpFfiStatus(NnrpFfiStatusCode.WouldBlock));
        }

        public NnrpNativeRuntimeResult SubmitAndPollResult(
            ulong operationId,
            RuntimeFrameHeader header,
            byte[]? payload = null,
            ulong? parentOperationId = null,
            ulong? operationGroupId = null,
            NnrpNativeOperationLifecycle? state = null,
            int maxEvents = 0)
        {
            var operation = SubmitOperation(
                operationId,
                header,
                payload,
                parentOperationId,
                operationGroupId);
            return PollResult(operation, state, maxEvents);
        }

        public NnrpNativeRuntimeResult SubmitAndPollResult(
            ulong operationId,
            RuntimeFrameHeader header,
            ReadOnlyMemory<byte> payload,
            ulong? parentOperationId = null,
            ulong? operationGroupId = null,
            NnrpNativeOperationLifecycle? state = null,
            int maxEvents = 0)
        {
            var operation = SubmitOperation(
                operationId,
                header,
                payload,
                parentOperationId,
                operationGroupId);
            return PollResult(operation, state, maxEvents);
        }

        public NnrpNativeRuntimeResult SubmitAndPollResult(
            ulong operationId,
            RuntimeFrameHeader header,
            NnrpNativeBuffer payload,
            ulong? parentOperationId = null,
            ulong? operationGroupId = null,
            NnrpNativeOperationLifecycle? state = null,
            int maxEvents = 0)
        {
            var operation = SubmitOperation(
                operationId,
                header,
                payload,
                parentOperationId,
                operationGroupId);
            return PollResult(operation, state, maxEvents);
        }

        public ulong SubmitResultCompactBatch(
            ulong operationIdStart,
            uint frameIdStart,
            uint frameIdStride,
            ReadOnlyMemory<byte> submitPayload,
            ReadOnlyMemory<byte> resultPayload,
            int maxEvents,
            int iterations)
        {
            EnsureOpen();
            if (frameIdStride == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(frameIdStride), "frameIdStride must be positive.");
            }

            if (maxEvents < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxEvents), "maxEvents must be non-negative.");
            }

            if (iterations <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(iterations), "iterations must be positive.");
            }

            return WithBorrowedView(
                submitPayload,
                submitView => WithBorrowedView(
                    resultPayload,
                    resultView =>
                    {
                        NnrpCompactResult lastResult;
                        UIntPtr completed;
                        var request = new NnrpClientSubmitResultBatchRequest(
                            Handle.Handle,
                            operationIdStart,
                            frameIdStart,
                            frameIdStride,
                            submitView,
                            resultView,
                            new UIntPtr((uint)maxEvents),
                            new UIntPtr((uint)iterations));
                        var status = Entrypoints.ClientSubmitResultCompactBatch(
                            request,
                            out lastResult,
                            out completed);
                        status.ThrowIfError();
                        lastResult.Status.ThrowIfError();
                        return checked((ulong)completed.ToUInt64());
                    }));
        }

        public IReadOnlyList<NnrpNativeRuntimeEvent> AwaitEvents(
            uint maxEvents = 1,
            uint timeoutMilliseconds = 0)
        {
            EnsureOpen();
            if (maxEvents == 0)
            {
                return Array.Empty<NnrpNativeRuntimeEvent>();
            }

            var eventSize = Marshal.SizeOf<NnrpEvent>();
            var allocationSize = checked(eventSize * checked((int)maxEvents));
            var events = Marshal.AllocHGlobal(allocationSize);
            try
            {
                UIntPtr eventCount;
                var status = Entrypoints.ClientAwaitEvents(
                    new NnrpRoleEventPollRequest(Handle.Handle, maxEvents, timeoutMilliseconds),
                    events,
                    new UIntPtr(maxEvents),
                    out eventCount);
                var count = eventCount.ToUInt64();
                if (count > maxEvents)
                {
                    throw new NnrpNativeArtifactException(
                        "Native client event poll returned more events than the supplied capacity.");
                }

                if (!status.Succeeded)
                {
                    ReleaseRemainingEventPayloads(events, eventSize, checked((int)count));
                    status.ThrowIfError();
                }

                var snapshots = new NnrpNativeRuntimeEvent[checked((int)count)];
                var consumed = 0;
                try
                {
                    for (var index = 0; index < snapshots.Length; index++)
                    {
                        var nativeEvent = Marshal.PtrToStructure<NnrpEvent>(
                            IntPtr.Add(events, checked(index * eventSize)));
                        consumed = index + 1;
                        snapshots[index] = NnrpNativeRuntimeEvent.FromFfi(nativeEvent, Entrypoints);
                        ObserveOperationEvent(snapshots[index]);
                    }
                }
                catch
                {
                    ReleaseRemainingEventPayloads(events, eventSize, snapshots.Length, consumed);
                    throw;
                }

                return snapshots;
            }
            finally
            {
                Marshal.FreeHGlobal(events);
            }
        }

        private void ReleaseRemainingEventPayloads(IntPtr events, int eventSize, int count, int startIndex = 0)
        {
            for (var index = startIndex; index < count; index++)
            {
                var nativeEvent = Marshal.PtrToStructure<NnrpEvent>(
                    IntPtr.Add(events, checked(index * eventSize)));
                if (nativeEvent.PayloadOwner.IsValid)
                {
                    try
                    {
                        Entrypoints.BufferRelease(nativeEvent.PayloadOwner).ThrowIfError();
                    }
                    catch
                    {
                        // Cleanup must not hide the poll or conversion failure that selected this path.
                    }
                }
            }
        }

        public void Close()
        {
            EnsureOpen();
            Entrypoints.ClientClose(Handle.Handle).ThrowIfError();
            lock (operationGate)
            {
                operationFrames.Clear();
            }
            IsClosed = true;
        }

        public void Cancel(uint frameId)
        {
            EnsureOpen();
            Entrypoints.ClientCancel(new NnrpClientCancelRequest(Handle.Handle, frameId)).ThrowIfError();
            ForgetOperationFrameByFrame(frameId);
        }

        public void CancelOperation(
            ControlRequestMetadata metadata,
            ReadOnlyMemory<byte> diagnostic = default)
        {
            SendRuntimeFrame(MessageType.Cancel, metadata, diagnostic);
        }

        public void AbortOperation(
            ControlRequestMetadata metadata,
            ReadOnlyMemory<byte> diagnostic = default)
        {
            SendRuntimeFrame(MessageType.Abort, metadata, diagnostic);
        }

        public void UpdatePriority(SchedulingMetadata metadata)
        {
            SendRuntimeFrame(MessageType.PriorityUpdate, metadata, ReadOnlyMemory<byte>.Empty);
        }

        public void UpdateDeadline(SchedulingMetadata metadata)
        {
            SendRuntimeFrame(MessageType.Deadline, metadata, ReadOnlyMemory<byte>.Empty);
        }

        public void ExpireAt(SchedulingMetadata metadata)
        {
            SendRuntimeFrame(MessageType.ExpireAt, metadata, ReadOnlyMemory<byte>.Empty);
        }

        public void Supersede(SupersedeMetadata metadata, ReadOnlyMemory<byte> diagnostic = default)
        {
            SendRuntimeFrame(MessageType.Supersede, metadata, diagnostic);
        }

        public void UpdateBudget(BudgetMetadata metadata)
        {
            SendRuntimeFrame(MessageType.BudgetUpdate, metadata, ReadOnlyMemory<byte>.Empty);
        }

        public void NegotiateCapabilities(CapabilityMetadata metadata, ReadOnlyMemory<byte> body = default)
        {
            SendRuntimeFrame(MessageType.CapabilityNegotiation, metadata, body);
        }

        public void DegradeProfile(CapabilityMetadata metadata, ReadOnlyMemory<byte> body = default)
        {
            SendRuntimeFrame(MessageType.DegradeProfile, metadata, body);
        }

        public void SendRouteHint(RouteHintMetadata metadata, ReadOnlyMemory<byte> body = default)
        {
            SendRuntimeFrame(MessageType.RouteHint, metadata, body);
        }

        public void SendExecutionHint(RouteHintMetadata metadata, ReadOnlyMemory<byte> body = default)
        {
            SendRuntimeFrame(MessageType.ExecutionHint, metadata, body);
        }

        public void SendTraceContext(TraceContextMetadata metadata, ReadOnlyMemory<byte> body = default)
        {
            SendRuntimeFrame(MessageType.TraceContext, metadata, body);
        }

        public void SendRuntimeObject(
            MessageType messageType,
            IRuntimeObjectMetadata metadata,
            ReadOnlyMemory<byte> tail = default)
        {
            EnsureOpen();
            SendEncodedRuntimeFrame(
                messageType,
                NnrpRuntimeObject.Encode(messageType, metadata, tail.Span),
                RuntimeOperationId(messageType, metadata));
        }

        public void SendCacheInvalidate(CacheInvalidateMetadata metadata)
        {
            EnsureOpen();
            SendEncodedRuntimeFrame(MessageType.CacheInvalidate, metadata.ToArray());
        }

        public void Control(uint controlCode, byte[]? payload = null)
        {
            EnsureOpen();
            SendControl(Entrypoints, Handle.Handle, controlCode, payload);
        }

        public void Control(uint controlCode, ReadOnlyMemory<byte> payload)
        {
            EnsureOpen();
            SendControl(Entrypoints, Handle.Handle, controlCode, payload);
        }

        public void Control(uint controlCode, NnrpNativeBuffer payload)
        {
            EnsureOpen();
            SendControl(Entrypoints, Handle.Handle, controlCode, payload);
        }

        public NnrpCacheLeaseResult QueryCacheLease(
            NnrpCacheObjectId objectId,
            ulong expectedVersion,
            ulong nowMilliseconds,
            uint ttlMilliseconds)
        {
            EnsureOpen();
            return new NnrpNativeCacheLeases(Entrypoints).Query(
                new NnrpCacheLeaseRequest(
                    Handle.Handle,
                    objectId,
                    expectedVersion,
                    nowMilliseconds,
                    ttlMilliseconds));
        }

        public NnrpCacheLeaseResult TouchCacheLease(
            NnrpCacheObjectId objectId,
            ulong expectedVersion,
            ulong nowMilliseconds,
            uint ttlMilliseconds)
        {
            EnsureOpen();
            return new NnrpNativeCacheLeases(Entrypoints).Touch(
                new NnrpCacheLeaseRequest(
                    Handle.Handle,
                    objectId,
                    expectedVersion,
                    nowMilliseconds,
                    ttlMilliseconds));
        }

        public NnrpCacheLeaseResult[] PrefetchCacheLeases(
            NnrpCacheObjectId[] objects,
            ulong nowMilliseconds,
            uint ttlMilliseconds)
        {
            EnsureOpen();
            return new NnrpNativeCacheLeases(Entrypoints).Prefetch(
                Handle.Handle,
                objects,
                nowMilliseconds,
                ttlMilliseconds);
        }

        public NnrpCacheLeaseResult ReleaseCacheLease(NnrpCacheLeaseHandle lease)
        {
            EnsureOpen();
            return new NnrpNativeCacheLeases(Entrypoints).Release(lease);
        }

        internal static void SendControl(
            NnrpNativeRuntimeEntrypoints entrypoints,
            NnrpHandle handle,
            uint controlCode,
            byte[]? payload)
        {
            SendControl(entrypoints, handle, controlCode, payload == null ? ReadOnlyMemory<byte>.Empty : payload);
        }

        private void SendRuntimeFrame(
            MessageType messageType,
            IRuntimeControlMetadata metadata,
            ReadOnlyMemory<byte> tail)
        {
            EnsureOpen();
            var operationId = RuntimeOperationId(messageType, metadata);
            SendEncodedRuntimeFrame(
                messageType,
                NnrpRuntimeControl.Encode(messageType, metadata, tail.Span),
                operationId);
            if (operationId.HasValue
                && operationId.Value != 0
                && (messageType == MessageType.Cancel
                    || messageType == MessageType.Abort
                    || messageType == MessageType.Supersede))
            {
                ForgetOperationFrame(operationId.Value);
            }
        }

        private void SendEncodedRuntimeFrame(
            MessageType messageType,
            ReadOnlyMemory<byte> payload,
            ulong? operationId = null)
        {
            var frameId = ResolveRuntimeFrameId(messageType, operationId);
            WithBorrowedView(
                payload,
                payloadView =>
                {
                    Entrypoints.RuntimeFrameSend(
                        new NnrpRuntimeFrameSendRequest(
                            Handle.Handle,
                            (uint)messageType,
                            frameId,
                            payloadView)).ThrowIfError();
                    return true;
                });
            if (!operationId.HasValue)
            {
                nextRuntimeFrameId = frameId == uint.MaxValue ? 1 : frameId + 1;
            }
        }

        private uint ResolveRuntimeFrameId(MessageType messageType, ulong? operationId)
        {
            if (!operationId.HasValue)
            {
                return nextRuntimeFrameId;
            }

            if (operationId.Value == 0)
            {
                if (messageType != MessageType.Cancel
                    && messageType != MessageType.Abort
                    && messageType != MessageType.BudgetUpdate
                    && messageType != MessageType.ObjectRef
                    && messageType != MessageType.ObjectRelease)
                {
                    throw new InvalidOperationException(
                        $"{messageType} requires an operation-scoped non-zero operation id.");
                }

                return 0;
            }

            lock (operationGate)
            {
                if (operationFrames.TryGetValue(operationId.Value, out var frameId))
                {
                    return frameId;
                }
            }

            throw new InvalidOperationException(
                $"{messageType} references inactive operation {operationId.Value}.");
        }

        private void RememberOperationFrame(ulong operationId, uint frameId)
        {
            lock (operationGate)
            {
                operationFrames[operationId] = frameId;
            }
        }

        private void ForgetOperationFrame(ulong operationId)
        {
            lock (operationGate)
            {
                operationFrames.Remove(operationId);
            }
        }

        private void ForgetOperationFrameByFrame(uint frameId)
        {
            lock (operationGate)
            {
                ulong? found = null;
                foreach (var pair in operationFrames)
                {
                    if (pair.Value == frameId)
                    {
                        found = pair.Key;
                        break;
                    }
                }

                if (found.HasValue)
                {
                    operationFrames.Remove(found.Value);
                }
            }
        }

        private void ObserveOperationEvent(NnrpNativeRuntimeEvent @event)
        {
            var operationId = @event.Diagnostic.RelatedOperationId;
            if (@event.IsTerminalClientOperationEvidence)
            {
                ForgetOperationFrame(operationId);
            }
        }

        private static ulong? RuntimeOperationId(MessageType messageType, IRuntimeControlMetadata metadata)
        {
            return messageType switch
            {
                MessageType.Cancel when metadata is ControlRequestMetadata value => value.OperationId,
                MessageType.Abort when metadata is ControlRequestMetadata value => value.OperationId,
                MessageType.PriorityUpdate when metadata is SchedulingMetadata value => value.OperationId,
                MessageType.Deadline when metadata is SchedulingMetadata value => value.OperationId,
                MessageType.ExpireAt when metadata is SchedulingMetadata value => value.OperationId,
                MessageType.Supersede when metadata is SupersedeMetadata value => value.OldOperationId,
                MessageType.BudgetUpdate when metadata is BudgetMetadata value => value.OperationId,
                MessageType.RouteHint when metadata is RouteHintMetadata value => value.OperationId,
                MessageType.ExecutionHint when metadata is RouteHintMetadata value => value.OperationId,
                _ => null,
            };
        }

        private static ulong? RuntimeOperationId(MessageType messageType, IRuntimeObjectMetadata metadata)
        {
            return messageType switch
            {
                MessageType.ObjectRef when metadata is ObjectReferenceMetadata value => value.OperationId,
                MessageType.ObjectRelease when metadata is ObjectReleaseMetadata value => value.OperationId,
                _ => null,
            };
        }

        internal static void SendControl(
            NnrpNativeRuntimeEntrypoints entrypoints,
            NnrpHandle handle,
            uint controlCode,
            ReadOnlyMemory<byte> payload)
        {
            WithBorrowedView(
                payload,
                payloadView =>
                {
                    entrypoints.Control(new NnrpControlRequest(handle, controlCode, payloadView)).ThrowIfError();
                    return true;
                });
        }

        internal static void SendControl(
            NnrpNativeRuntimeEntrypoints entrypoints,
            NnrpHandle handle,
            uint controlCode,
            NnrpNativeBuffer payload)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            entrypoints.Control(new NnrpControlRequest(handle, controlCode, payload.BorrowView())).ThrowIfError();
        }

        internal static T WithBorrowedView<T>(ReadOnlyMemory<byte> payload, Func<NnrpBufferView, T> action)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            if (payload.Length == 0)
            {
                return action(NnrpBufferView.Empty);
            }

            if (!MemoryMarshal.TryGetArray(payload, out var segment) || segment.Array == null)
            {
                throw new NotSupportedException("Borrowed native payload views require array-backed ReadOnlyMemory<byte>. Use NnrpNativeBuffers.AcquireCopy for non-array-backed memory.");
            }

            GCHandle payloadHandle = default(GCHandle);
            try
            {
                payloadHandle = GCHandle.Alloc(segment.Array, GCHandleType.Pinned);
                var pointer = IntPtr.Add(payloadHandle.AddrOfPinnedObject(), segment.Offset);
                return action(new NnrpBufferView(pointer, new UIntPtr((uint)segment.Count)));
            }
            finally
            {
                if (payloadHandle.IsAllocated)
                {
                    payloadHandle.Free();
                }
            }
        }

        private static bool EventMatchesOperation(
            NnrpNativeRuntimeEvent @event,
            NnrpNativeRuntimeOperation operation)
        {
            return @event.Session == operation.Session.Handle
                && (@event.Operation.Id == operation.Handle.Handle.Id
                || @event.Operation.Id == operation.OperationId
                || @event.FrameId == operation.FrameId);
        }

        private static void ValidateSubmitHeader(RuntimeFrameHeader header)
        {
            const HeaderFlags knownFlags = HeaderFlags.AckRequired
                | HeaderFlags.CanDrop
                | HeaderFlags.Stale
                | HeaderFlags.Eos
                | HeaderFlags.Retransmit
                | HeaderFlags.Keyframe;

            if (header.MessageType != MessageType.FrameSubmit
                || header.FrameId == 0
                || header.SessionId != 0
                || header.VersionMajor != NnrpHeader.CurrentVersionMajor
                || header.WireFormat != NnrpHeader.CurrentWireFormat
                || (header.Flags & ~knownFlags) != 0)
            {
                throw new ArgumentException(
                    "Submit headers must describe a current FRAME_SUBMIT frame, leave session id runtime-owned, and contain only known flags.",
                    nameof(header));
            }
        }

        private void EnsureOpen()
        {
            if (IsClosed || IsConnectionClosed())
            {
                throw new NnrpNativeInvalidStateException(new NnrpFfiStatus(NnrpFfiStatusCode.InvalidState));
            }
        }
    }

    public sealed class NnrpNativeRuntimeOperation
    {
        public NnrpNativeRuntimeOperation(
            NnrpNativeRuntimeEntrypoints entrypoints,
            NnrpSessionHandle session,
            NnrpOperationHandle handle,
            ulong operationId,
            uint frameId,
            ulong? parentOperationId = null,
            ulong? operationGroupId = null)
        {
            Entrypoints = entrypoints ?? throw new ArgumentNullException(nameof(entrypoints));
            Session = session;
            Handle = handle;
            OperationId = operationId;
            FrameId = frameId;
            ParentOperationId = parentOperationId;
            OperationGroupId = operationGroupId;
        }

        public NnrpNativeRuntimeEntrypoints Entrypoints { get; }

        public NnrpSessionHandle Session { get; }

        public NnrpOperationHandle Handle { get; }

        public ulong OperationId { get; }

        public uint FrameId { get; }

        public ulong? ParentOperationId { get; }

        public ulong? OperationGroupId { get; }

        public void Cancel()
        {
            Entrypoints.ClientCancel(new NnrpClientCancelRequest(Session.Handle, FrameId)).ThrowIfError();
        }
    }

    public static class NnrpNativeArtifact
    {
        public const string ArtifactRootEnvironmentVariable = "NNRP_NATIVE_ARTIFACT_ROOT";
        public const ushort ExpectedAbiMajor = 4;
        public const ushort ExpectedAbiMinor = 4;
        public const ushort ExpectedAbiPatch = 0;
        public const byte ExpectedProtocolMajor = 1;
        public const byte ExpectedProtocolWireFormat = 0;
        public const uint TransportSlotQuic = 0x00000001;
        public const uint TransportSlotTcp = 0x00000002;
        public const uint TransportSlotIpc = 0x00000004;
        public const uint TransportSlotWebSocket = 0x00000008;
        public const ulong RuntimeFeatureProtocolCore = 0x0000000000000001;
        public const ulong RuntimeFeatureClientApi = 0x0000000000000002;
        public const ulong RuntimeFeatureServerApi = 0x0000000000000004;
        public const ulong RuntimeFeatureEventPolling = 0x0000000000000008;
        public const ulong RuntimeFeatureCallbackDispatch = 0x0000000000000010;
        public const ulong RuntimeFeatureCacheSchema = 0x0000000000000020;
        public const ulong RuntimeFeatureRecovery = 0x0000000000000040;
        public const ulong RuntimeFeatureTypedPayload = 0x0000000000000080;
        public const ulong RuntimeFeatureTransportSlots = 0x0000000000000100;
        public const ulong RuntimeFeatureBatchPolling = 0x0000000000000200;
        public const ulong RuntimeFeatureCacheLeaseOps = 0x0000000000000400;
        public const ulong RuntimeFeatureSchemaRegistryHandles = 0x0000000000000800;
        public const ulong RuntimeFeatureBufferHandles = 0x0000000000001000;
        public const ulong RuntimeFeatureExecutableResume = 0x0000000000002000;
        public const ulong RuntimeFeaturePreview4ControlEvents = 0x0000000000020000;
        public const ulong RuntimeFeaturePreview4ObjectCacheEvents = 0x0000000000040000;
        public const ulong RuntimeFeaturePreview4RuntimeFrameSend = 0x0000000000080000;
        public const ulong RuntimeFeatureTransportFramedIo = 0x0000000000100000;
        public const ulong RequiredRuntimeFeatures =
            RuntimeFeatureProtocolCore
            | RuntimeFeatureClientApi
            | RuntimeFeatureServerApi
            | RuntimeFeatureEventPolling
            | RuntimeFeatureCallbackDispatch
            | RuntimeFeatureCacheSchema
            | RuntimeFeatureRecovery
            | RuntimeFeatureTypedPayload
            | RuntimeFeatureTransportSlots
            | RuntimeFeatureCacheLeaseOps
            | RuntimeFeatureSchemaRegistryHandles
            | RuntimeFeatureBufferHandles
            | RuntimeFeaturePreview4ControlEvents
            | RuntimeFeaturePreview4ObjectCacheEvents
            | RuntimeFeaturePreview4RuntimeFrameSend
            | RuntimeFeatureTransportFramedIo;
        public const uint RequiredTransportSlots = TransportSlotTcp;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NnrpRuntimeCapabilities RuntimeCapabilitiesInvoker();

        public static string DefaultArtifactRoot
        {
            get
            {
                string configured = Environment.GetEnvironmentVariable(ArtifactRootEnvironmentVariable);
                if (!string.IsNullOrWhiteSpace(configured))
                {
                    return configured;
                }

                return Path.Combine(AppContext.BaseDirectory, "native_artifacts");
            }
        }

        public static string LibraryName(string osName)
        {
            string normalized = new NnrpNativePlatform(osName, "x86_64").OsName;
            if (normalized == "windows")
            {
                return "nnrp_ffi.dll";
            }

            if (normalized == "ios" || normalized == "iossimulator")
            {
                return "libnnrp_ffi.a";
            }

            if (normalized == "macos")
            {
                return "libnnrp_ffi.dylib";
            }

            return "libnnrp_ffi.so";
        }

        public static string TransportLibraryName(string osName, string transportScope)
        {
            string scope = NormalizeTransportScope(transportScope);
            string normalized = new NnrpNativePlatform(osName, "x86_64").OsName;
            if (normalized == "windows")
            {
                return "nnrp_ffi_" + scope + ".dll";
            }

            if (normalized == "ios" || normalized == "iossimulator")
            {
                return "libnnrp_ffi_" + scope + ".a";
            }

            if (normalized == "macos")
            {
                return "libnnrp_ffi_" + scope + ".dylib";
            }

            return "libnnrp_ffi_" + scope + ".so";
        }

        public static string Resolve(string? artifactRoot = null, NnrpNativePlatform? platform = null)
        {
            NnrpNativePlatform selectedPlatform = platform ?? NnrpNativePlatform.Current;
            string root = string.IsNullOrWhiteSpace(artifactRoot) ? DefaultArtifactRoot : artifactRoot!;
            string path = Path.Combine(
                root,
                "runtimes",
                selectedPlatform.RuntimeIdentifier,
                "native",
                LibraryName(selectedPlatform.OsName));
            if (!File.Exists(path))
            {
                throw new NnrpNativeArtifactException("Native artifact was not found: " + path);
            }

            return path;
        }

        public static string ResolveTransport(
            string transportScope,
            string? artifactRoot = null,
            NnrpNativePlatform? platform = null)
        {
            string scope = NormalizeTransportScope(transportScope);
            NnrpNativePlatform selectedPlatform = platform ?? NnrpNativePlatform.Current;
            string root = string.IsNullOrWhiteSpace(artifactRoot) ? DefaultArtifactRoot : artifactRoot!;
            string path = Path.Combine(
                root,
                "runtimes",
                selectedPlatform.RuntimeIdentifier,
                "native",
                "nnrp",
                "transport",
                scope,
                TransportLibraryName(selectedPlatform.OsName, scope));
            if (!File.Exists(path))
            {
                throw new NnrpNativeArtifactException("Native transport artifact was not found: " + path);
            }

            return path;
        }

        public static string TransportScopeFromTransportId(uint transportId)
        {
            if (transportId == TransportSlotTcp)
            {
                return "tcp";
            }

            if (transportId == TransportSlotQuic)
            {
                return "quic";
            }

            if (transportId == TransportSlotIpc)
            {
                return "ipc";
            }

            if (transportId == TransportSlotWebSocket)
            {
                return "websocket";
            }

            throw new NnrpNativeArtifactException("Unsupported native transport id: " + transportId);
        }

        private static string NormalizeTransportScope(string value)
        {
            string normalized = (value ?? string.Empty).Trim().ToLowerInvariant().Replace("_", "-");
            if (normalized == "tcp" || normalized == "quic" || normalized == "ipc" || normalized == "websocket")
            {
                return normalized;
            }

            throw new NnrpNativeArtifactException("Unsupported native transport scope: " + value);
        }

        public static NnrpNativeProbeResult Probe(
            string? artifactPath = null,
            string? artifactRoot = null,
            NnrpNativePlatform? platform = null,
            RuntimeCapabilitiesInvoker? runtimeCapabilities = null,
            uint requiredTransportSlots = RequiredTransportSlots)
        {
            string resolvedPath = string.IsNullOrWhiteSpace(artifactPath) ? Resolve(artifactRoot, platform) : artifactPath!;
            NnrpRuntimeCapabilities capabilities = runtimeCapabilities == null
                ? ReadRuntimeCapabilities(resolvedPath)
                : runtimeCapabilities();
            ValidateRuntimeCapabilities(capabilities, requiredTransportSlots);
            return new NnrpNativeProbeResult(
                resolvedPath,
                capabilities.AbiMajor,
                capabilities.AbiMinor,
                capabilities.AbiPatch,
                capabilities.ProtocolVersion.Major,
                capabilities.ProtocolVersion.WireFormat,
                capabilities.SdkMajor,
                capabilities.SdkMinor,
                capabilities.SdkPatch,
                capabilities.SdkChannel,
                capabilities.SdkRevision,
                capabilities.TransportSlots,
                capabilities.FeatureFlags);
        }

        private static void ValidateRuntimeCapabilities(NnrpRuntimeCapabilities capabilities, uint requiredTransportSlots)
        {
            if (capabilities.AbiMajor != ExpectedAbiMajor
                || capabilities.AbiMinor != ExpectedAbiMinor
                || capabilities.AbiPatch != ExpectedAbiPatch)
            {
                throw new NnrpNativeArtifactException(
                    "Native artifact ABI mismatch: expected "
                    + ExpectedAbiMajor
                    + "."
                    + ExpectedAbiMinor
                    + "."
                    + ExpectedAbiPatch
                    + ", got "
                    + capabilities.AbiMajor
                    + "."
                    + capabilities.AbiMinor
                    + "."
                    + capabilities.AbiPatch);
            }

            NnrpProtocolVersion version = capabilities.ProtocolVersion;
            if (version.Major != ExpectedProtocolMajor || version.WireFormat != ExpectedProtocolWireFormat)
            {
                throw new NnrpNativeArtifactException(
                    "Native artifact protocol mismatch: expected "
                    + ExpectedProtocolMajor
                    + "/"
                    + ExpectedProtocolWireFormat
                    + ", got "
                    + version.Major
                    + "/"
                    + version.WireFormat);
            }

            ulong missingFeatures = RequiredRuntimeFeatures & ~capabilities.FeatureFlags;
            if (missingFeatures != 0)
            {
                throw new NnrpNativeArtifactException(
                    "Native artifact is missing required runtime feature flags: 0x" + missingFeatures.ToString("x16"));
            }

            uint missingTransportSlots = requiredTransportSlots & ~capabilities.TransportSlots;
            if (missingTransportSlots != 0)
            {
                throw new NnrpNativeArtifactException(
                    "Native artifact is missing required transport slots: 0x" + missingTransportSlots.ToString("x8"));
            }
        }

        [ExcludeFromCodeCoverage]
        private static NnrpRuntimeCapabilities ReadRuntimeCapabilities(string artifactPath)
        {
            if (string.IsNullOrWhiteSpace(artifactPath))
            {
                throw new ArgumentException("Native artifact path is required.", nameof(artifactPath));
            }

            IntPtr handle = IntPtr.Zero;
            try
            {
                handle = NativeDynamicLibrary.Load(artifactPath);
                IntPtr symbol = NativeDynamicLibrary.GetSymbol(handle, "nnrp_runtime_capabilities");
                var invoker = Marshal.GetDelegateForFunctionPointer<RuntimeCapabilitiesInvoker>(symbol);
                return invoker();
            }
            catch (Exception error) when (error is DllNotFoundException || error is EntryPointNotFoundException || error is BadImageFormatException)
            {
                throw new NnrpNativeArtifactException("Failed to load native artifact probe from " + artifactPath + ": " + error.Message, error);
            }
            finally
            {
                if (handle != IntPtr.Zero)
                {
                    NativeDynamicLibrary.Free(handle);
                }
            }
        }

    }

    [ExcludeFromCodeCoverage]
    internal static class NativeDynamicLibrary
    {
        public static IntPtr Load(string path)
        {
            IntPtr handle;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                handle = LoadLibraryW(path);
            }
            else
            {
                handle = Dlopen(path, 2);
            }

            if (handle == IntPtr.Zero)
            {
                throw new DllNotFoundException(path);
            }

            return handle;
        }

        public static IntPtr GetSymbol(IntPtr handle, string name)
        {
            IntPtr symbol;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                symbol = GetProcAddress(handle, name);
            }
            else
            {
                symbol = Dlsym(handle, name);
            }

            if (symbol == IntPtr.Zero)
            {
                throw new EntryPointNotFoundException(name);
            }

            return symbol;
        }

        public static void Free(IntPtr handle)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                FreeLibrary(handle);
                return;
            }

            Dlclose(handle);
        }

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadLibraryW(string path);

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Ansi)]
        private static extern IntPtr GetProcAddress(IntPtr module, string name);

        [DllImport("kernel32", SetLastError = true)]
        private static extern bool FreeLibrary(IntPtr module);

        [DllImport("libdl")]
        private static extern IntPtr dlopen(string path, int flags);

        [DllImport("libdl")]
        private static extern IntPtr dlsym(IntPtr handle, string symbol);

        [DllImport("libdl")]
        private static extern int dlclose(IntPtr handle);

        private static IntPtr Dlopen(string path, int flags)
        {
            try
            {
                return dlopen(path, flags);
            }
            catch (DllNotFoundException)
            {
                return dlopen2(path, flags);
            }
        }

        private static IntPtr Dlsym(IntPtr handle, string symbol)
        {
            try
            {
                return dlsym(handle, symbol);
            }
            catch (DllNotFoundException)
            {
                return dlsym2(handle, symbol);
            }
        }

        private static void Dlclose(IntPtr handle)
        {
            try
            {
                dlclose(handle);
            }
            catch (DllNotFoundException)
            {
                dlclose2(handle);
            }
        }

        [DllImport("libdl.so.2", EntryPoint = "dlopen")]
        private static extern IntPtr dlopen2(string path, int flags);

        [DllImport("libdl.so.2", EntryPoint = "dlsym")]
        private static extern IntPtr dlsym2(IntPtr handle, string symbol);

        [DllImport("libdl.so.2", EntryPoint = "dlclose")]
        private static extern int dlclose2(IntPtr handle);
    }
}
