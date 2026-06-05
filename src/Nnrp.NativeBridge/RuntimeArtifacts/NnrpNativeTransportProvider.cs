using System;
using System.Collections.Generic;
using System.Linq;
using Nnrp.Core;

namespace Nnrp.NativeBridge
{
    public interface INnrpNativeTransportProvider
    {
        TransportId TransportId { get; }

        string BindingName { get; }

        uint NativeTransportSlot { get; }

        int ProbePriority { get; }
    }

    public sealed class NnrpNativeTransportResolution
    {
        internal NnrpNativeTransportResolution(
            INnrpNativeTransportProvider selectedProvider,
            INnrpNativeTransportProvider[] availableProviders,
            bool shouldProbe)
        {
            SelectedProvider = selectedProvider ?? throw new ArgumentNullException(nameof(selectedProvider));
            AvailableProviders = availableProviders ?? throw new ArgumentNullException(nameof(availableProviders));
            ShouldProbe = shouldProbe;
        }

        public INnrpNativeTransportProvider SelectedProvider { get; }

        public INnrpNativeTransportProvider[] AvailableProviders { get; }

        public bool ShouldProbe { get; }

        public uint TransportId => (uint)SelectedProvider.TransportId;
    }

    public static class NnrpNativeTransportResolver
    {
        public static NnrpNativeTransportResolution Resolve(
            NnrpNativeProbeResult probeResult,
            IEnumerable<INnrpNativeTransportProvider> providers,
            TransportPolicy policy = TransportPolicy.Auto)
        {
            if (providers == null)
            {
                throw new ArgumentNullException(nameof(providers));
            }

            var available = ValidateProviders(providers)
                .Where(provider => (probeResult.TransportSlots & provider.NativeTransportSlot) == provider.NativeTransportSlot)
                .OrderByDescending(provider => provider.ProbePriority)
                .ThenBy(provider => provider.BindingName, StringComparer.Ordinal)
                .ToArray();
            if (available.Length == 0)
            {
                throw new InvalidOperationException("No installed native transport provider matches the native artifact transport slots.");
            }

            var preferred = ResolvePreferredTransport(policy);
            INnrpNativeTransportProvider selected;
            if (preferred == TransportId.Unspecified)
            {
                selected = available[0];
            }
            else
            {
                selected = available.FirstOrDefault(provider => provider.TransportId == preferred);
                if (selected == null)
                {
                    throw new InvalidOperationException($"No installed native transport provider is available for {preferred}.");
                }
            }

            bool shouldProbe = ShouldProbe(policy) && available.Length > 1;
            return new NnrpNativeTransportResolution(selected, available, shouldProbe);
        }

        private static INnrpNativeTransportProvider[] ValidateProviders(
            IEnumerable<INnrpNativeTransportProvider> providers)
        {
            var providerArray = providers.ToArray();
            if (providerArray.Length == 0)
            {
                throw new ArgumentException("At least one native transport provider must be supplied.", nameof(providers));
            }

            var seen = new HashSet<TransportId>();
            foreach (var provider in providerArray)
            {
                if (provider == null)
                {
                    throw new ArgumentException("Native transport providers must not contain null entries.", nameof(providers));
                }

                if (provider.TransportId == TransportId.Unspecified)
                {
                    throw new ArgumentException("Native transport provider id must be specified.", nameof(providers));
                }

                if (provider.NativeTransportSlot == 0)
                {
                    throw new ArgumentException("Native transport provider slot must be non-zero.", nameof(providers));
                }

                if (string.IsNullOrWhiteSpace(provider.BindingName))
                {
                    throw new ArgumentException("Native transport provider binding name must not be empty.", nameof(providers));
                }

                if (!seen.Add(provider.TransportId))
                {
                    throw new ArgumentException($"Duplicate native transport provider id {provider.TransportId} is not allowed.", nameof(providers));
                }
            }

            return providerArray;
        }

        private static bool ShouldProbe(TransportPolicy policy)
        {
            return policy switch
            {
                TransportPolicy.Auto => true,
                TransportPolicy.PreferQuic => true,
                TransportPolicy.PreferTcp => true,
                TransportPolicy.ForceQuic => false,
                TransportPolicy.ForceTcp => false,
                _ => throw new ArgumentOutOfRangeException(nameof(policy), policy, "Unknown transport policy."),
            };
        }

        private static TransportId ResolvePreferredTransport(TransportPolicy policy)
        {
            return policy switch
            {
                TransportPolicy.Auto => TransportId.Unspecified,
                TransportPolicy.PreferQuic => TransportId.Quic,
                TransportPolicy.PreferTcp => TransportId.Tcp,
                TransportPolicy.ForceQuic => TransportId.Quic,
                TransportPolicy.ForceTcp => TransportId.Tcp,
                _ => throw new ArgumentOutOfRangeException(nameof(policy), policy, "Unknown transport policy."),
            };
        }
    }
}
