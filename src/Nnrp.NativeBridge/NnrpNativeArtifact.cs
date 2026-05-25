using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices;

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
            ushort sdkPreview,
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
            SdkPreview = sdkPreview;
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

        public ushort SdkPreview { get; }

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
            ushort sdkPreview,
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
            SdkPreview = sdkPreview;
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

        public readonly ushort SdkPreview;

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
                capabilities.SdkPreview,
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

        [ExcludeFromCodeCoverage]
        private static class NativeDynamicLibrary
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
}
