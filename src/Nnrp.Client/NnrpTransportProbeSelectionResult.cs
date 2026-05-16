using System;
using Nnrp.Core;

namespace Nnrp.Client
{
    public readonly struct NnrpTransportProbeSelectionResult
    {
        public NnrpTransportProbeSelectionResult(
            TransportId selectedTransportId,
            string selectedBindingName,
            NnrpTransportProbeBindingSummary[] summaries)
        {
            if (selectedTransportId == TransportId.Unspecified)
            {
                throw new ArgumentOutOfRangeException(nameof(selectedTransportId));
            }

            if (string.IsNullOrWhiteSpace(selectedBindingName))
            {
                throw new ArgumentException("Selected binding name must not be null or empty.", nameof(selectedBindingName));
            }

            SelectedTransportId = selectedTransportId;
            SelectedBindingName = selectedBindingName;
            Summaries = summaries ?? throw new ArgumentNullException(nameof(summaries));
        }

        public TransportId SelectedTransportId { get; }

        public string SelectedBindingName { get; }

        public NnrpTransportProbeBindingSummary[] Summaries { get; }
    }
}
