using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

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
    public readonly struct NnrpEvent
    {
        public NnrpEvent(
            uint kind,
            NnrpHandle connection,
            NnrpHandle session,
            NnrpHandle operation,
            uint frameId,
            NnrpBufferView payload,
            NnrpFfiDiagnostic diagnostic)
        {
            Kind = kind;
            Connection = connection;
            Session = session;
            Operation = operation;
            FrameId = frameId;
            Payload = payload;
            Diagnostic = diagnostic;
        }

        public readonly uint Kind;

        public readonly NnrpHandle Connection;

        public readonly NnrpHandle Session;

        public readonly NnrpHandle Operation;

        public readonly uint FrameId;

        public readonly NnrpBufferView Payload;

        public readonly NnrpFfiDiagnostic Diagnostic;
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct NnrpCallbackSink
    {
        public NnrpCallbackSink(IntPtr userData, IntPtr onEvent)
        {
            UserData = userData;
            OnEvent = onEvent;
        }

        public readonly IntPtr UserData;

        public readonly IntPtr OnEvent;
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
        public NnrpClientConnectRequest(ulong connectionId, uint generation, uint transportId)
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
    public readonly struct NnrpServerBindRequest
    {
        public NnrpServerBindRequest(ulong serverId, uint generation, uint transportId)
        {
            ServerId = serverId;
            Generation = generation;
            TransportId = transportId;
        }

        public readonly ulong ServerId;

        public readonly uint Generation;

        public readonly uint TransportId;
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct NnrpSessionOpenRequest
    {
        public NnrpSessionOpenRequest(
            NnrpHandle connection,
            uint requestedSessionId,
            uint generation,
            ushort profileId,
            uint schemaId,
            uint schemaVersion)
        {
            Connection = connection;
            RequestedSessionId = requestedSessionId;
            Generation = generation;
            ProfileId = profileId;
            SchemaId = schemaId;
            SchemaVersion = schemaVersion;
        }

        public readonly NnrpHandle Connection;

        public readonly uint RequestedSessionId;

        public readonly uint Generation;

        public readonly ushort ProfileId;

        public readonly uint SchemaId;

        public readonly uint SchemaVersion;
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct NnrpFfiSubmitRequest
    {
        public NnrpFfiSubmitRequest(NnrpHandle session, ulong operationId, uint frameId, NnrpBufferView payload)
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
    public readonly struct NnrpServerAcceptRequest
    {
        public NnrpServerAcceptRequest(
            NnrpHandle server,
            uint sessionId,
            uint generation,
            ushort profileId,
            uint schemaId,
            uint schemaVersion)
        {
            Server = server;
            SessionId = sessionId;
            Generation = generation;
            ProfileId = profileId;
            SchemaId = schemaId;
            SchemaVersion = schemaVersion;
        }

        public readonly NnrpHandle Server;

        public readonly uint SessionId;

        public readonly uint Generation;

        public readonly ushort ProfileId;

        public readonly uint SchemaId;

        public readonly uint SchemaVersion;
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

    public sealed class NnrpNativeRuntimeEntrypoints : IDisposable
    {
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
            ServerAcceptInvoker serverAccept,
            ServerReceiveSubmitInvoker serverReceiveSubmit,
            ServerSendResultInvoker serverSendResult,
            ServerFlowUpdateInvoker serverSendFlowUpdate,
            HandleStatusInvoker serverClose,
            ControlInvoker control,
            PollEmptyInvoker pollEmpty,
            DispatchEventInvoker dispatchEvent,
            HandleStatusInvoker? connectionClose = null,
            HandleStatusInvoker? clientCloseConnection = null)
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
                serverAccept,
                serverReceiveSubmit,
                serverSendResult,
                serverSendFlowUpdate,
                serverClose,
                control,
                pollEmpty,
                dispatchEvent,
                connectionClose,
                clientCloseConnection)
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
            ServerAcceptInvoker serverAccept,
            ServerReceiveSubmitInvoker serverReceiveSubmit,
            ServerSendResultInvoker serverSendResult,
            ServerFlowUpdateInvoker serverSendFlowUpdate,
            HandleStatusInvoker serverClose,
            ControlInvoker control,
            PollEmptyInvoker pollEmpty,
            DispatchEventInvoker dispatchEvent,
            HandleStatusInvoker? connectionClose,
            HandleStatusInvoker? clientCloseConnection)
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
            ServerAccept = serverAccept ?? throw new ArgumentNullException(nameof(serverAccept));
            ServerReceiveSubmit = serverReceiveSubmit ?? throw new ArgumentNullException(nameof(serverReceiveSubmit));
            ServerSendResult = serverSendResult ?? throw new ArgumentNullException(nameof(serverSendResult));
            ServerSendFlowUpdate = serverSendFlowUpdate ?? throw new ArgumentNullException(nameof(serverSendFlowUpdate));
            ServerClose = serverClose ?? throw new ArgumentNullException(nameof(serverClose));
            Control = control ?? throw new ArgumentNullException(nameof(control));
            PollEmpty = pollEmpty ?? throw new ArgumentNullException(nameof(pollEmpty));
            DispatchEvent = dispatchEvent ?? throw new ArgumentNullException(nameof(dispatchEvent));
            ConnectionClose = connectionClose ?? ClientClose;
            ClientCloseConnection = clientCloseConnection ?? ConnectionClose;
        }

        private IntPtr _libraryHandle;

        [ExcludeFromCodeCoverage]
        public static NnrpNativeRuntimeEntrypoints Load(
            string? artifactPath = null,
            string? artifactRoot = null,
            NnrpNativePlatform? platform = null)
        {
            string resolvedPath = string.IsNullOrWhiteSpace(artifactPath)
                ? NnrpNativeArtifact.Resolve(artifactRoot, platform)
                : artifactPath!;
            IntPtr handle = IntPtr.Zero;
            try
            {
                handle = NativeDynamicLibrary.Load(resolvedPath);
                var runtimeCapabilities = Bind<NnrpNativeArtifact.RuntimeCapabilitiesInvoker>(handle, "nnrp_runtime_capabilities");
                NnrpNativeArtifact.Probe(resolvedPath, runtimeCapabilities: runtimeCapabilities);
                return new NnrpNativeRuntimeEntrypoints(
                    handle,
                    Bind<CurrentProtocolVersionInvoker>(handle, "nnrp_current_protocol_version"),
                    runtimeCapabilities,
                    Bind<ConnectionBootstrapInvoker>(handle, "nnrp_connection_bootstrap"),
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
                    Bind<ServerAcceptInvoker>(handle, "nnrp_server_accept"),
                    Bind<ServerReceiveSubmitInvoker>(handle, "nnrp_server_receive_submit"),
                    Bind<ServerSendResultInvoker>(handle, "nnrp_server_send_result"),
                    Bind<ServerFlowUpdateInvoker>(handle, "nnrp_server_send_flow_update"),
                    Bind<HandleStatusInvoker>(handle, "nnrp_server_close"),
                    Bind<ControlInvoker>(handle, "nnrp_control"),
                    Bind<PollEmptyInvoker>(handle, "nnrp_poll_empty"),
                    Bind<DispatchEventInvoker>(handle, "nnrp_dispatch_event"),
                    Bind<HandleStatusInvoker>(handle, "nnrp_connection_close"),
                    Bind<HandleStatusInvoker>(handle, "nnrp_client_close_connection"));
            }
            catch (Exception error) when (error is DllNotFoundException || error is EntryPointNotFoundException || error is BadImageFormatException)
            {
                if (handle != IntPtr.Zero)
                {
                    NativeDynamicLibrary.Free(handle);
                }

                throw new NnrpNativeArtifactException("Failed to load native runtime entrypoints from " + resolvedPath + ": " + error.Message, error);
            }
        }

        public void Dispose()
        {
            if (_libraryHandle == IntPtr.Zero)
            {
                return;
            }

            NativeDynamicLibrary.Free(_libraryHandle);
            _libraryHandle = IntPtr.Zero;
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
        public delegate NnrpFfiStatus ConnectionBootstrapInvoker(NnrpConnectionBootstrap request, out NnrpHandle connection);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NnrpFfiStatus ClientConnectInvoker(NnrpClientConnectRequest request, out NnrpHandle connection);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NnrpFfiStatus SessionOpenInvoker(NnrpSessionOpenRequest request, out NnrpHandle session);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NnrpFfiStatus SubmitInvoker(NnrpFfiSubmitRequest request, out NnrpHandle operation);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NnrpFfiStatus HandleStatusInvoker(NnrpHandle handle);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NnrpFfiStatus ClientCancelInvoker(NnrpClientCancelRequest request);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NnrpFfiStatus AwaitEventInvoker(NnrpHandle connection, out NnrpPollResult result);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NnrpFfiStatus ServerBindInvoker(NnrpServerBindRequest request, out NnrpHandle server);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NnrpFfiStatus ServerAcceptInvoker(NnrpServerAcceptRequest request, out NnrpHandle session);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NnrpFfiStatus ServerReceiveSubmitInvoker(NnrpServerReceiveSubmitRequest request, out NnrpHandle operation);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NnrpFfiStatus ServerSendResultInvoker(NnrpServerSendResultRequest request);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NnrpFfiStatus ServerFlowUpdateInvoker(NnrpServerFlowUpdateRequest request);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NnrpFfiStatus ControlInvoker(NnrpControlRequest request);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NnrpFfiStatus PollEmptyInvoker(out NnrpPollResult result);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NnrpFfiStatus DispatchEventInvoker(NnrpCallbackSink sink, ref NnrpEvent @event);

        public CurrentProtocolVersionInvoker CurrentProtocolVersion { get; }

        public NnrpNativeArtifact.RuntimeCapabilitiesInvoker RuntimeCapabilities { get; }

        public ConnectionBootstrapInvoker ConnectionBootstrap { get; }

        public ClientConnectInvoker ClientConnect { get; }

        public SessionOpenInvoker SessionOpen { get; }

        public SessionOpenInvoker ClientOpenSession { get; }

        public SubmitInvoker Submit { get; }

        public SubmitInvoker ClientSubmit { get; }

        public HandleStatusInvoker SessionClose { get; }

        public HandleStatusInvoker ClientClose { get; }

        public HandleStatusInvoker ConnectionClose { get; }

        public HandleStatusInvoker ClientCloseConnection { get; }

        public ClientCancelInvoker ClientCancel { get; }

        public AwaitEventInvoker ClientAwaitEvent { get; }

        public ServerBindInvoker ServerBind { get; }

        public ServerAcceptInvoker ServerAccept { get; }

        public ServerReceiveSubmitInvoker ServerReceiveSubmit { get; }

        public ServerSendResultInvoker ServerSendResult { get; }

        public ServerFlowUpdateInvoker ServerSendFlowUpdate { get; }

        public HandleStatusInvoker ServerClose { get; }

        public ControlInvoker Control { get; }

        public PollEmptyInvoker PollEmpty { get; }

        public DispatchEventInvoker DispatchEvent { get; }
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
        public NnrpNativeRuntimeEvent(
            uint kind,
            NnrpHandle connection,
            NnrpHandle session,
            NnrpHandle operation,
            uint frameId,
            byte[] payload,
            NnrpNativeRuntimeDiagnostic diagnostic)
        {
            Kind = kind;
            Connection = connection;
            Session = session;
            Operation = operation;
            FrameId = frameId;
            Payload = payload ?? throw new ArgumentNullException(nameof(payload));
            Diagnostic = diagnostic;
        }

        public uint Kind { get; }

        public NnrpHandle Connection { get; }

        public NnrpHandle Session { get; }

        public NnrpHandle Operation { get; }

        public uint FrameId { get; }

        public byte[] Payload { get; }

        public NnrpNativeRuntimeDiagnostic Diagnostic { get; }

        public static NnrpNativeRuntimeEvent FromFfi(NnrpEvent @event)
        {
            return new NnrpNativeRuntimeEvent(
                @event.Kind,
                @event.Connection,
                @event.Session,
                @event.Operation,
                @event.FrameId,
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

        public static NnrpNativeRuntimePollResult FromFfi(NnrpPollResult result)
        {
            return new NnrpNativeRuntimePollResult(
                result.Status,
                result.HasEvent != 0 ? NnrpNativeRuntimeEvent.FromFfi(result.Event) : null);
        }
    }

    public interface INnrpNativeRuntimeBackend
    {
        NnrpNativeRuntimeConnection Connect(ulong connectionId, uint generation, uint transportId);

        NnrpNativeRuntimeConnection BootstrapConnection(ulong connectionId, uint generation, uint transportId);
    }

    public static class NnrpNativeRuntimeBackendSelector
    {
        public static INnrpNativeRuntimeBackend Select(
            string? artifactPath = null,
            string? artifactRoot = null,
            NnrpNativePlatform? platform = null,
            INnrpNativeRuntimeBackend? fallback = null,
            bool requireNative = false)
        {
            try
            {
                return new NnrpNativeRuntimeClient(
                    NnrpNativeRuntimeEntrypoints.Load(artifactPath, artifactRoot, platform));
            }
            catch (NnrpNativeArtifactException)
            {
                if (fallback == null || requireNative)
                {
                    throw;
                }

                return fallback;
            }
        }
    }

    public sealed class NnrpNativeRuntimeClient : INnrpNativeRuntimeBackend
    {
        public NnrpNativeRuntimeClient(NnrpNativeRuntimeEntrypoints entrypoints)
        {
            Entrypoints = entrypoints ?? throw new ArgumentNullException(nameof(entrypoints));
        }

        public NnrpNativeRuntimeEntrypoints Entrypoints { get; }

        public NnrpNativeRuntimeConnection Connect(ulong connectionId, uint generation, uint transportId)
        {
            NnrpHandle connection;
            var status = Entrypoints.ClientConnect(
                new NnrpClientConnectRequest(connectionId, generation, transportId),
                out connection);
            status.ThrowIfError();
            return new NnrpNativeRuntimeConnection(Entrypoints, new NnrpConnectionHandle(connection));
        }

        public NnrpNativeRuntimeConnection BootstrapConnection(ulong connectionId, uint generation, uint transportId)
        {
            NnrpHandle connection;
            var status = Entrypoints.ConnectionBootstrap(
                new NnrpConnectionBootstrap(connectionId, generation, transportId),
                out connection);
            status.ThrowIfError();
            return new NnrpNativeRuntimeConnection(Entrypoints, new NnrpConnectionHandle(connection));
        }
    }

    public sealed class NnrpNativeRuntimeConnection : IDisposable
    {
        private readonly object eventGate = new object();
        private readonly Queue<NnrpNativeRuntimeEvent> bufferedEvents = new Queue<NnrpNativeRuntimeEvent>();

        public NnrpNativeRuntimeConnection(NnrpNativeRuntimeEntrypoints entrypoints, NnrpConnectionHandle handle)
        {
            Entrypoints = entrypoints ?? throw new ArgumentNullException(nameof(entrypoints));
            Handle = handle;
        }

        public NnrpNativeRuntimeEntrypoints Entrypoints { get; }

        public NnrpConnectionHandle Handle { get; }

        public bool IsClosed { get; private set; }

        public NnrpNativeRuntimeSession OpenSession(
            uint requestedSessionId,
            uint generation,
            ushort profileId,
            uint schemaId,
            uint schemaVersion)
        {
            EnsureOpen();
            NnrpHandle session;
            var status = Entrypoints.ClientOpenSession(
                new NnrpSessionOpenRequest(
                    Handle.Handle,
                    requestedSessionId,
                    generation,
                    profileId,
                    schemaId,
                    schemaVersion),
                out session);
            status.ThrowIfError();
            return new NnrpNativeRuntimeSession(
                Entrypoints,
                Handle,
                new NnrpSessionHandle(session),
                () => IsClosed,
                this);
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
            return NnrpNativeRuntimePollResult.FromFfi(result);
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

        public void Close()
        {
            EnsureOpen();
            Entrypoints.ClientCloseConnection(Handle.Handle).ThrowIfError();
            lock (eventGate)
            {
                bufferedEvents.Clear();
            }

            IsClosed = true;
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

        public NnrpOperationHandle Submit(ulong operationId, uint frameId, byte[]? payload = null)
        {
            EnsureOpen();
            return SubmitOperation(operationId, frameId, payload).Handle;
        }

        public NnrpNativeRuntimeOperation SubmitOperation(
            ulong operationId,
            uint frameId,
            byte[]? payload = null,
            ulong? parentOperationId = null,
            ulong? operationGroupId = null)
        {
            EnsureOpen();
            GCHandle payloadHandle = default(GCHandle);
            try
            {
                var payloadView = NnrpBufferView.Empty;
                if (payload != null && payload.Length > 0)
                {
                    payloadHandle = GCHandle.Alloc(payload, GCHandleType.Pinned);
                    payloadView = new NnrpBufferView(payloadHandle.AddrOfPinnedObject(), new UIntPtr((uint)payload.Length));
                }

                NnrpHandle operation;
                var status = Entrypoints.ClientSubmit(
                    new NnrpFfiSubmitRequest(Handle.Handle, operationId, frameId, payloadView),
                    out operation);
                status.ThrowIfError();
                return new NnrpNativeRuntimeOperation(
                    Entrypoints,
                    Handle,
                    new NnrpOperationHandle(operation),
                    operationId,
                    frameId,
                    parentOperationId,
                    operationGroupId);
            }
            finally
            {
                if (payloadHandle.IsAllocated)
                {
                    payloadHandle.Free();
                }
            }
        }

        public Task<NnrpNativeRuntimeOperation> SubmitOperationAsync(
            ulong operationId,
            uint frameId,
            byte[]? payload = null,
            ulong? parentOperationId = null,
            ulong? operationGroupId = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            EnsureOpen();
            if (cancellationToken.IsCancellationRequested)
            {
                Cancel(frameId);
                return Task.FromCanceled<NnrpNativeRuntimeOperation>(cancellationToken);
            }

            using (cancellationToken.Register(() => Cancel(frameId)))
            {
                var operation = SubmitOperation(
                    operationId,
                    frameId,
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

                var snapshot = NnrpNativeRuntimePollResult.FromFfi(result);
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
            uint frameId,
            byte[]? payload = null,
            ulong? parentOperationId = null,
            ulong? operationGroupId = null,
            NnrpNativeOperationLifecycle? state = null,
            int maxEvents = 0)
        {
            var operation = SubmitOperation(
                operationId,
                frameId,
                payload,
                parentOperationId,
                operationGroupId);
            return PollResult(operation, state, maxEvents);
        }

        public void Close()
        {
            EnsureOpen();
            Entrypoints.ClientClose(Handle.Handle).ThrowIfError();
            IsClosed = true;
        }

        public void Cancel(uint frameId)
        {
            EnsureOpen();
            Entrypoints.ClientCancel(new NnrpClientCancelRequest(Handle.Handle, frameId)).ThrowIfError();
        }

        public void Control(uint controlCode, byte[]? payload = null)
        {
            EnsureOpen();
            SendControl(Entrypoints, Handle.Handle, controlCode, payload);
        }

        internal static void SendControl(
            NnrpNativeRuntimeEntrypoints entrypoints,
            NnrpHandle handle,
            uint controlCode,
            byte[]? payload)
        {
            GCHandle payloadHandle = default(GCHandle);
            try
            {
                var payloadView = NnrpBufferView.Empty;
                if (payload != null && payload.Length > 0)
                {
                    payloadHandle = GCHandle.Alloc(payload, GCHandleType.Pinned);
                    payloadView = new NnrpBufferView(payloadHandle.AddrOfPinnedObject(), new UIntPtr((uint)payload.Length));
                }

                entrypoints.Control(new NnrpControlRequest(handle, controlCode, payloadView)).ThrowIfError();
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
        public const ushort ExpectedAbiMajor = 1;
        public const ushort MinimumAbiMinor = 0;
        public const byte ExpectedProtocolMajor = 1;
        public const byte ExpectedProtocolWireFormat = 0;
        public const uint TransportSlotQuic = 0x00000001;
        public const uint TransportSlotTcp = 0x00000002;
        public const ulong RuntimeFeatureProtocolCore = 0x0000000000000001;
        public const ulong RuntimeFeatureClientApi = 0x0000000000000002;
        public const ulong RuntimeFeatureServerApi = 0x0000000000000004;
        public const ulong RuntimeFeatureEventPolling = 0x0000000000000008;
        public const ulong RuntimeFeatureCallbackDispatch = 0x0000000000000010;
        public const ulong RuntimeFeatureCacheSchema = 0x0000000000000020;
        public const ulong RuntimeFeatureRecovery = 0x0000000000000040;
        public const ulong RuntimeFeatureTypedPayload = 0x0000000000000080;
        public const ulong RuntimeFeatureTransportSlots = 0x0000000000000100;
        public const ulong RequiredRuntimeFeatures =
            RuntimeFeatureProtocolCore
            | RuntimeFeatureClientApi
            | RuntimeFeatureServerApi
            | RuntimeFeatureEventPolling
            | RuntimeFeatureCallbackDispatch
            | RuntimeFeatureCacheSchema
            | RuntimeFeatureRecovery
            | RuntimeFeatureTypedPayload
            | RuntimeFeatureTransportSlots;
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

            if (normalized == "macos" || normalized == "ios")
            {
                return "libnnrp_ffi.dylib";
            }

            return "libnnrp_ffi.so";
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

        public static NnrpNativeProbeResult Probe(
            string? artifactPath = null,
            string? artifactRoot = null,
            NnrpNativePlatform? platform = null,
            RuntimeCapabilitiesInvoker? runtimeCapabilities = null)
        {
            string resolvedPath = string.IsNullOrWhiteSpace(artifactPath) ? Resolve(artifactRoot, platform) : artifactPath!;
            NnrpRuntimeCapabilities capabilities = runtimeCapabilities == null
                ? ReadRuntimeCapabilities(resolvedPath)
                : runtimeCapabilities();
            ValidateRuntimeCapabilities(capabilities);
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

        private static void ValidateRuntimeCapabilities(NnrpRuntimeCapabilities capabilities)
        {
            if (capabilities.AbiMajor != ExpectedAbiMajor || capabilities.AbiMinor < MinimumAbiMinor)
            {
                throw new NnrpNativeArtifactException(
                    "Native artifact ABI mismatch: expected "
                    + ExpectedAbiMajor
                    + "."
                    + MinimumAbiMinor
                    + ".x, got "
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

            uint missingTransportSlots = RequiredTransportSlots & ~capabilities.TransportSlots;
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
