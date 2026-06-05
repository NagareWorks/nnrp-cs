namespace Nnrp.Core
{
    public readonly struct NnrpCapabilityNegotiationResult
    {
        private NnrpCapabilityNegotiationResult(
            bool isAccepted,
            NnrpCapabilitySelection selection,
            ErrorCode errorCode,
            CapabilityRejectionReason rejectionReason,
            string rejectionMessage)
        {
            IsAccepted = isAccepted;
            Selection = selection;
            ErrorCode = errorCode;
            RejectionReason = rejectionReason;
            RejectionMessage = rejectionMessage ?? string.Empty;
        }

        public bool IsAccepted { get; }

        public NnrpCapabilitySelection Selection { get; }

        public ErrorCode ErrorCode { get; }

        public CapabilityRejectionReason RejectionReason { get; }

        public string RejectionMessage { get; }

        public static NnrpCapabilityNegotiationResult Accepted(NnrpCapabilitySelection selection)
        {
            return new NnrpCapabilityNegotiationResult(
                true,
                selection,
                default,
                CapabilityRejectionReason.None,
                string.Empty);
        }

        public static NnrpCapabilityNegotiationResult Rejected(
            ErrorCode errorCode,
            CapabilityRejectionReason rejectionReason,
            string rejectionMessage)
        {
            return new NnrpCapabilityNegotiationResult(
                false,
                default,
                errorCode,
                rejectionReason,
                rejectionMessage);
        }
    }
}
