using Nnrp.Core;
using Xunit;

namespace Nnrp.Core.Tests
{
    public sealed class ProtocolFailureTests
    {
        [Fact]
        public void HeaderParseErrorsMapToStableProtocolErrorCodes()
        {
            var malformedHeader = NnrpProtocolFailure.FromHeaderParseError(NnrpParseError.InvalidMagic);
            var unsupportedVersion = NnrpProtocolFailure.FromHeaderParseError(NnrpParseError.UnsupportedVersion);
            var oversizedHeaderMessage = NnrpProtocolFailure.FromHeaderParseError(NnrpParseError.MessageTooLarge);

            Assert.Equal(ErrorCode.MalformedHeader, malformedHeader.ErrorCode);
            Assert.Equal(NnrpErrorScope.Connection, malformedHeader.Scope);
            Assert.True(malformedHeader.IsFatal);
            Assert.Equal(NnrpParseError.InvalidMagic, malformedHeader.ParseError);
            Assert.Equal(ErrorCode.UnsupportedVersion, unsupportedVersion.ErrorCode);
            Assert.Equal(ErrorCode.LimitExceeded, oversizedHeaderMessage.ErrorCode);
        }

        [Fact]
        public void BodyParseErrorsMapToMalformedBodyOrLimitExceeded()
        {
            var malformedBody = NnrpProtocolFailure.FromBodyParseError(NnrpParseError.NonZeroReservedField);
            var oversizedBody = NnrpProtocolFailure.FromBodyParseError(NnrpParseError.MessageTooLarge);

            Assert.Equal(ErrorCode.MalformedBody, malformedBody.ErrorCode);
            Assert.Equal(NnrpErrorScope.Frame, malformedBody.Scope);
            Assert.False(malformedBody.IsFatal);
            Assert.Equal(NnrpParseError.NonZeroReservedField, malformedBody.ParseError);
            Assert.Equal(ErrorCode.LimitExceeded, oversizedBody.ErrorCode);
            Assert.Equal(NnrpErrorScope.Frame, oversizedBody.Scope);
        }

        [Fact]
        public void FailureFactoriesPreserveScopeFatalityAndMessages()
        {
            var invalidState = NnrpProtocolFailure.InvalidState(NnrpErrorScope.Session, "Invalid session state.");
            var unsupportedCapability = NnrpProtocolFailure.UnsupportedCapability("Unsupported dtype.");
            var limitExceeded = NnrpProtocolFailure.LimitExceeded(NnrpErrorScope.Frame, "Frame body too large.");

            Assert.True(invalidState.IsFailure);
            Assert.Equal(ErrorCode.InvalidState, invalidState.ErrorCode);
            Assert.Equal(NnrpErrorScope.Session, invalidState.Scope);
            Assert.False(invalidState.IsFatal);
            Assert.Equal(ErrorCode.UnsupportedCapability, unsupportedCapability.ErrorCode);
            Assert.Equal(NnrpErrorScope.Session, unsupportedCapability.Scope);
            Assert.True(unsupportedCapability.IsFatal);
            Assert.Equal(ErrorCode.LimitExceeded, limitExceeded.ErrorCode);
            Assert.Equal(NnrpErrorScope.Frame, limitExceeded.Scope);
            Assert.Contains("Frame body", limitExceeded.Message);
        }

        [Fact]
        public void FailureEqualityAndNoneAreStable()
        {
            var first = NnrpProtocolFailure.FromBodyParseError(NnrpParseError.InconsistentSectionDescriptor, "Bad tensor section.");
            var second = NnrpProtocolFailure.FromBodyParseError(NnrpParseError.InconsistentSectionDescriptor, "Bad tensor section.");

            Assert.Equal(first, second);
            Assert.Equal(first.GetHashCode(), second.GetHashCode());
            Assert.False(NnrpProtocolFailure.None.IsFailure);
            Assert.Equal(NnrpProtocolFailure.None, NnrpProtocolFailure.FromHeaderParseError(NnrpParseError.None));
            Assert.Equal(NnrpProtocolFailure.None, NnrpProtocolFailure.FromBodyParseError(NnrpParseError.None));
        }

        [Fact]
        public void ErrorCodeNumericValuesRemainStable()
        {
            Assert.Equal(0x0001, (ushort)ErrorCode.UnsupportedVersion);
            Assert.Equal(0x0002, (ushort)ErrorCode.AuthFailed);
            Assert.Equal(0x0003, (ushort)ErrorCode.InvalidState);
            Assert.Equal(0x0004, (ushort)ErrorCode.MalformedHeader);
            Assert.Equal(0x0005, (ushort)ErrorCode.MalformedBody);
            Assert.Equal(0x0006, (ushort)ErrorCode.UnsupportedCapability);
            Assert.Equal(0x0007, (ushort)ErrorCode.LimitExceeded);
            Assert.Equal(0x0008, (ushort)ErrorCode.FrameExpired);
            Assert.Equal(0x0009, (ushort)ErrorCode.FrameCancelled);
            Assert.Equal(0x000A, (ushort)ErrorCode.CacheMiss);
            Assert.Equal(0x000B, (ushort)ErrorCode.ServerBusy);
            Assert.Equal(0x000C, (ushort)ErrorCode.InternalError);
        }
    }
}
