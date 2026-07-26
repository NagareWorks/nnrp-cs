using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Nnrp.Client")]
[assembly: InternalsVisibleTo("Nnrp.Server")]
[assembly: InternalsVisibleTo("Nnrp.NativeBridge")]
[assembly: InternalsVisibleTo("Nnrp.Core.Tests")]

namespace Nnrp.Core
{
    public sealed class NnrpEndpoint : IEquatable<NnrpEndpoint>
    {
        private readonly string value;

        private NnrpEndpoint(string value, Uri uri)
        {
            this.value = value;
            Authority = uri.Authority;
            PathAndQuery = string.IsNullOrEmpty(uri.PathAndQuery) ? "/" : uri.PathAndQuery;
            IsSecure = string.Equals(uri.Scheme, "nnrps", StringComparison.OrdinalIgnoreCase);
        }

        public string Authority { get; }

        public string PathAndQuery { get; }

        public bool IsSecure { get; }

        public static NnrpEndpoint Parse(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new FormatException("NNRP endpoint must not be empty.");
            }

            if (value.IndexOf('#') >= 0
                || !Uri.TryCreate(value, UriKind.Absolute, out var uri)
                || string.IsNullOrEmpty(uri.Host))
            {
                throw new FormatException("NNRP endpoint must be an absolute URI with an authority and no fragment.");
            }

            if (!string.Equals(uri.Scheme, "nnrp", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(uri.Scheme, "nnrps", StringComparison.OrdinalIgnoreCase))
            {
                throw new FormatException("Application endpoint must use nnrp:// or nnrps://.");
            }

            if (!string.IsNullOrEmpty(uri.UserInfo))
            {
                throw new FormatException("NNRP endpoint credentials are not allowed.");
            }

            return new NnrpEndpoint(value, uri);
        }

        public bool Equals(NnrpEndpoint? other)
        {
            return other != null && string.Equals(value, other.value, StringComparison.Ordinal);
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as NnrpEndpoint);
        }

        public override int GetHashCode()
        {
            return StringComparer.Ordinal.GetHashCode(value);
        }

        public override string ToString()
        {
            return value;
        }
    }

    public sealed class NnrpProviderEndpoint : IEquatable<NnrpProviderEndpoint>
    {
        private readonly string value;

        private NnrpProviderEndpoint(string value, string scheme)
        {
            this.value = value;
            Scheme = scheme;
        }

        public string Scheme { get; }

        public bool IsSecure => string.Equals(Scheme, "wss", StringComparison.Ordinal);

        public static NnrpProviderEndpoint Parse(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.IndexOf('#') >= 0)
            {
                throw new FormatException("Provider endpoint must not be empty or contain a fragment.");
            }

            var separator = value.IndexOf("://", StringComparison.Ordinal);
            if (separator <= 0 || separator + 3 >= value.Length)
            {
                throw new FormatException("Provider endpoint must contain a supported scheme and locator.");
            }

            var scheme = value.Substring(0, separator).ToLowerInvariant();
            var locator = value.Substring(separator + 3);
            switch (scheme)
            {
                case "unix":
                    if (!locator.StartsWith("/", StringComparison.Ordinal)
                        || string.Equals(locator, "/", StringComparison.Ordinal)
                        || locator.IndexOf('?') >= 0)
                    {
                        throw new FormatException("Unix provider endpoint must contain an absolute socket path.");
                    }

                    break;
                case "npipe":
                    if (locator.IndexOfAny(new[] { '?', '@' }) >= 0)
                    {
                        throw new FormatException("Named-pipe provider endpoint contains an invalid locator.");
                    }

                    break;
                case "tcp":
                case "quic":
                case "ws":
                case "wss":
                    ValidateNetworkProviderEndpoint(value);
                    break;
                default:
                    throw new FormatException(
                        "Provider endpoint must use tcp://, quic://, unix://, npipe://, ws://, or wss://.");
            }

            return new NnrpProviderEndpoint(value, scheme);
        }

        public bool MatchesTransport(TransportId transportId)
        {
            return transportId switch
            {
                TransportId.Tcp => string.Equals(Scheme, "tcp", StringComparison.Ordinal),
                TransportId.Quic => string.Equals(Scheme, "quic", StringComparison.Ordinal),
                TransportId.Ipc => string.Equals(Scheme, "unix", StringComparison.Ordinal)
                    || string.Equals(Scheme, "npipe", StringComparison.Ordinal),
                TransportId.WebSocket => string.Equals(Scheme, "ws", StringComparison.Ordinal)
                    || string.Equals(Scheme, "wss", StringComparison.Ordinal),
                _ => false,
            };
        }

        public bool Equals(NnrpProviderEndpoint? other)
        {
            return other != null && string.Equals(value, other.value, StringComparison.Ordinal);
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as NnrpProviderEndpoint);
        }

        public override int GetHashCode()
        {
            return StringComparer.Ordinal.GetHashCode(value);
        }

        public override string ToString()
        {
            return value;
        }

        private static void ValidateNetworkProviderEndpoint(string value)
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
                || string.IsNullOrEmpty(uri.Host)
                || !string.IsNullOrEmpty(uri.UserInfo))
            {
                throw new FormatException("Provider endpoint must contain a valid authority without credentials.");
            }
        }
    }

    public sealed class NnrpTransportClientSecurity
    {
        private readonly byte[] trustedCertificateDer;

        public NnrpTransportClientSecurity(string serverName, ReadOnlyMemory<byte> trustedCertificateDer)
        {
            if (string.IsNullOrWhiteSpace(serverName))
            {
                throw new ArgumentException("Server name must not be empty.", nameof(serverName));
            }

            if (trustedCertificateDer.IsEmpty)
            {
                throw new ArgumentException("Trusted certificate DER must not be empty.", nameof(trustedCertificateDer));
            }

            ServerName = serverName;
            this.trustedCertificateDer = trustedCertificateDer.ToArray();
        }

        public string ServerName { get; }

        public ReadOnlyMemory<byte> TrustedCertificateDer => trustedCertificateDer;
    }

    public sealed class NnrpTransportServerSecurity
    {
        private readonly byte[] certificateDer;
        private readonly byte[] privateKeyPkcs8Der;

        public NnrpTransportServerSecurity(
            ReadOnlyMemory<byte> certificateDer,
            ReadOnlyMemory<byte> privateKeyPkcs8Der)
        {
            if (certificateDer.IsEmpty)
            {
                throw new ArgumentException("Certificate DER must not be empty.", nameof(certificateDer));
            }

            if (privateKeyPkcs8Der.IsEmpty)
            {
                throw new ArgumentException("Private key PKCS#8 DER must not be empty.", nameof(privateKeyPkcs8Der));
            }

            this.certificateDer = certificateDer.ToArray();
            this.privateKeyPkcs8Der = privateKeyPkcs8Der.ToArray();
        }

        public ReadOnlyMemory<byte> CertificateDer => certificateDer;

        public ReadOnlyMemory<byte> PrivateKeyPkcs8Der => privateKeyPkcs8Der;
    }

    public sealed class NnrpClientProviderRoute
    {
        public NnrpProviderEndpoint? ProviderEndpoint { get; init; }

        public NnrpTransportClientSecurity? Security { get; init; }
    }

    public sealed class NnrpServerProviderRoute
    {
        public NnrpProviderEndpoint? ProviderEndpoint { get; init; }

        public NnrpTransportServerSecurity? Security { get; init; }
    }

    internal static class NnrpTransportRouteSet
    {
        internal static IReadOnlyDictionary<TransportId, NnrpClientProviderRoute> CopyClient(
            IReadOnlyDictionary<TransportId, NnrpClientProviderRoute>? routes)
        {
            var copy = new Dictionary<TransportId, NnrpClientProviderRoute>();
            if (routes != null)
            {
                foreach (var pair in routes)
                {
                    ValidateTransportId(pair.Key);
                    var route = pair.Value ?? throw new ArgumentException(
                        "Client provider routes must not contain null values.",
                        nameof(routes));
                    copy.Add(pair.Key, new NnrpClientProviderRoute
                    {
                        ProviderEndpoint = route.ProviderEndpoint,
                        Security = route.Security,
                    });
                }
            }

            return new ReadOnlyDictionary<TransportId, NnrpClientProviderRoute>(copy);
        }

        internal static IReadOnlyDictionary<TransportId, NnrpServerProviderRoute> CopyServer(
            IReadOnlyDictionary<TransportId, NnrpServerProviderRoute>? routes)
        {
            var copy = new Dictionary<TransportId, NnrpServerProviderRoute>();
            if (routes != null)
            {
                foreach (var pair in routes)
                {
                    ValidateTransportId(pair.Key);
                    var route = pair.Value ?? throw new ArgumentException(
                        "Server provider routes must not contain null values.",
                        nameof(routes));
                    copy.Add(pair.Key, new NnrpServerProviderRoute
                    {
                        ProviderEndpoint = route.ProviderEndpoint,
                        Security = route.Security,
                    });
                }
            }

            return new ReadOnlyDictionary<TransportId, NnrpServerProviderRoute>(copy);
        }

        private static void ValidateTransportId(TransportId transportId)
        {
            if (transportId == TransportId.Unspecified || !Enum.IsDefined(typeof(TransportId), transportId))
            {
                throw new ArgumentException("Provider route keys must be known non-zero transport ids.");
            }
        }
    }

    internal static class NnrpTransportRouteResolver
    {
        internal static bool TryResolveClient(
            NnrpEndpoint endpoint,
            TransportId transportId,
            NnrpClientProviderRoute? route,
            out NnrpProviderEndpoint? providerEndpoint,
            out NnrpTransportRejectionReason? rejectionReason,
            out string? diagnostic)
        {
            providerEndpoint = ResolveEndpoint(endpoint, transportId, route?.ProviderEndpoint, out diagnostic);
            if (providerEndpoint == null)
            {
                rejectionReason = NnrpTransportRejectionReason.RouteUnresolved;
                return false;
            }

            if (!IsClientSecurityValid(endpoint, transportId, providerEndpoint, route?.Security, out diagnostic))
            {
                rejectionReason = NnrpTransportRejectionReason.SecurityUnsatisfied;
                return false;
            }

            rejectionReason = null;
            diagnostic = null;
            return true;
        }

        internal static bool TryResolveServer(
            NnrpEndpoint endpoint,
            TransportId transportId,
            NnrpServerProviderRoute? route,
            out NnrpProviderEndpoint? providerEndpoint,
            out NnrpTransportRejectionReason? rejectionReason,
            out string? diagnostic)
        {
            providerEndpoint = ResolveEndpoint(endpoint, transportId, route?.ProviderEndpoint, out diagnostic);
            if (providerEndpoint == null)
            {
                rejectionReason = NnrpTransportRejectionReason.RouteUnresolved;
                return false;
            }

            if (!IsServerSecurityValid(endpoint, transportId, providerEndpoint, route?.Security, out diagnostic))
            {
                rejectionReason = NnrpTransportRejectionReason.SecurityUnsatisfied;
                return false;
            }

            rejectionReason = null;
            diagnostic = null;
            return true;
        }

        private static NnrpProviderEndpoint? ResolveEndpoint(
            NnrpEndpoint endpoint,
            TransportId transportId,
            NnrpProviderEndpoint? providerEndpoint,
            out string? diagnostic)
        {
            if (endpoint == null)
            {
                throw new ArgumentNullException(nameof(endpoint));
            }

            if (transportId == TransportId.Unspecified || !Enum.IsDefined(typeof(TransportId), transportId))
            {
                throw new ArgumentOutOfRangeException(nameof(transportId));
            }

            if (providerEndpoint == null)
            {
                if (transportId == TransportId.Tcp || transportId == TransportId.Quic)
                {
                    diagnostic = null;
                    var scheme = transportId == TransportId.Tcp ? "tcp" : "quic";
                    return NnrpProviderEndpoint.Parse(scheme + "://" + endpoint.Authority);
                }

                diagnostic = transportId + " requires an explicit provider endpoint.";
                return null;
            }

            if (!providerEndpoint.MatchesTransport(transportId))
            {
                diagnostic = "Provider endpoint scheme does not match " + transportId + ".";
                return null;
            }

            if (transportId == TransportId.Ipc
                && ((string.Equals(providerEndpoint.Scheme, "unix", StringComparison.Ordinal)
                        && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    || (string.Equals(providerEndpoint.Scheme, "npipe", StringComparison.Ordinal)
                        && !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))))
            {
                diagnostic = "IPC provider endpoint is not supported on this platform.";
                return null;
            }

            diagnostic = null;
            return providerEndpoint;
        }

        private static bool IsClientSecurityValid(
            NnrpEndpoint endpoint,
            TransportId transportId,
            NnrpProviderEndpoint providerEndpoint,
            NnrpTransportClientSecurity? security,
            out string? diagnostic)
        {
            var valid = IsSecurityValid(endpoint, transportId, providerEndpoint, security != null);
            diagnostic = valid ? null : "Client route security does not satisfy the application endpoint.";
            return valid;
        }

        private static bool IsServerSecurityValid(
            NnrpEndpoint endpoint,
            TransportId transportId,
            NnrpProviderEndpoint providerEndpoint,
            NnrpTransportServerSecurity? security,
            out string? diagnostic)
        {
            var valid = IsSecurityValid(endpoint, transportId, providerEndpoint, security != null);
            diagnostic = valid ? null : "Server route security does not satisfy the application endpoint.";
            return valid;
        }

        internal static bool IsSecurityValid(
            NnrpEndpoint endpoint,
            TransportId transportId,
            NnrpProviderEndpoint providerEndpoint,
            bool hasSecurity)
        {
            switch (transportId)
            {
                case TransportId.Tcp:
                    return !endpoint.IsSecure || hasSecurity;
                case TransportId.Quic:
                    return hasSecurity;
                case TransportId.Ipc:
                    return !endpoint.IsSecure && !hasSecurity;
                case TransportId.WebSocket:
                    return providerEndpoint.IsSecure == hasSecurity
                        && (!endpoint.IsSecure || providerEndpoint.IsSecure);
                default:
                    throw new ArgumentOutOfRangeException(nameof(transportId));
            }
        }
    }
}
