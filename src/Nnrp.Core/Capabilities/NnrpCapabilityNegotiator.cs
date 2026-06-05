using System.Collections.Generic;

namespace Nnrp.Core
{
    public static class NnrpCapabilityNegotiator
    {
        public static NnrpCapabilityNegotiationResult Negotiate(
            NnrpClientCapabilities clientCapabilities,
            NnrpServerCapabilities serverCapabilities)
        {
            if (clientCapabilities == null)
            {
                return NnrpCapabilityNegotiationResult.Rejected(
                    ErrorCode.UnsupportedCapability,
                    CapabilityRejectionReason.InvalidClientCapabilities,
                    "Client capabilities are required.");
            }

            if (serverCapabilities == null)
            {
                return NnrpCapabilityNegotiationResult.Rejected(
                    ErrorCode.UnsupportedCapability,
                    CapabilityRejectionReason.InvalidServerCapabilities,
                    "Server capabilities are required.");
            }

            if (!clientCapabilities.TryValidate(out var validationError))
            {
                return NnrpCapabilityNegotiationResult.Rejected(
                    ErrorCode.UnsupportedCapability,
                    CapabilityRejectionReason.InvalidClientCapabilities,
                    validationError);
            }

            if (!serverCapabilities.TryValidate(out validationError))
            {
                return NnrpCapabilityNegotiationResult.Rejected(
                    ErrorCode.UnsupportedCapability,
                    CapabilityRejectionReason.InvalidServerCapabilities,
                    validationError);
            }

            if (clientCapabilities.MaxViews > serverCapabilities.MaxViews)
            {
                return NnrpCapabilityNegotiationResult.Rejected(
                    ErrorCode.LimitExceeded,
                    CapabilityRejectionReason.MaxViewsExceeded,
                    "Client requested more views than the server allows.");
            }

            if (!TrySelectServerPreferred(serverCapabilities.AcceptedCodecs, clientCapabilities.SupportedCodecs, out var selectedCodec))
            {
                return NnrpCapabilityNegotiationResult.Rejected(
                    ErrorCode.UnsupportedCapability,
                    CapabilityRejectionReason.NoCommonCodec,
                    "No common codec is available.");
            }

            if (!TrySelectServerPreferred(serverCapabilities.AcceptedDTypes, clientCapabilities.SupportedDTypes, out var selectedDType))
            {
                return NnrpCapabilityNegotiationResult.Rejected(
                    ErrorCode.UnsupportedCapability,
                    CapabilityRejectionReason.NoCommonDType,
                    "No common dtype is available.");
            }

            if (!TrySelectServerPreferred(serverCapabilities.AcceptedTensorLayouts, clientCapabilities.SupportedTensorLayouts, out var selectedTensorLayout))
            {
                return NnrpCapabilityNegotiationResult.Rejected(
                    ErrorCode.UnsupportedCapability,
                    CapabilityRejectionReason.NoCommonTensorLayout,
                    "No common tensor layout is available.");
            }

            var payloadKindBitmap = clientCapabilities.SupportedPayloadKindBitmap & serverCapabilities.AcceptedPayloadKindBitmap;
            if (payloadKindBitmap == 0)
            {
                return NnrpCapabilityNegotiationResult.Rejected(
                    ErrorCode.UnsupportedCapability,
                    CapabilityRejectionReason.NoCommonPayloadKind,
                    "No common payload kind is available.");
            }

            var enableCache = clientCapabilities.EnableCache && serverCapabilities.EnableCache;
            var maxCacheEntries = enableCache
                ? Min(clientCapabilities.MaxCacheEntries, serverCapabilities.MaxCacheEntries)
                : 0;
            var cacheObjectBitmap = enableCache
                ? clientCapabilities.SupportedCacheObjectBitmap & serverCapabilities.AcceptedCacheObjectBitmap
                : 0u;
            var degradePolicies = clientCapabilities.SupportedDegradePolicies & serverCapabilities.AcceptedDegradePolicies;

            var selection = new NnrpCapabilitySelection(
                selectedCodec,
                selectedDType,
                selectedTensorLayout,
                payloadKindBitmap,
                cacheObjectBitmap,
                degradePolicies,
                clientCapabilities.MaxViews,
                enableCache,
                maxCacheEntries,
                serverCapabilities.MaxConcurrentFrames,
                serverCapabilities.MaxBodyBytes,
                serverCapabilities.MaxSectionCount,
                serverCapabilities.MaxTileCount,
                serverCapabilities.TokenTtlSeconds,
                serverCapabilities.AllowSessionRenewal);

            return NnrpCapabilityNegotiationResult.Accepted(selection);
        }

        private static bool TrySelectServerPreferred<T>(
            IReadOnlyList<T> serverValues,
            IReadOnlyList<T> clientValues,
            out T selectedValue)
            where T : struct
        {
            for (var serverValueIndex = 0; serverValueIndex < serverValues.Count; serverValueIndex++)
            {
                var serverValue = serverValues[serverValueIndex];
                if (NnrpCapabilityValidation.Contains(clientValues, serverValue))
                {
                    selectedValue = serverValue;
                    return true;
                }
            }

            selectedValue = default;
            return false;
        }

        private static int Min(int left, int right)
        {
            return left < right ? left : right;
        }
    }
}
