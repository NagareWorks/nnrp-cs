using System;

namespace Nnrp.Core
{
    /// <summary>
    /// Discriminated union outcome of a frame submit. The server either returns
    /// a full <see cref="ResultPushMessage"/> with enhanced tile data, or a lightweight
    /// <see cref="ResultDropMessage"/> signalling that the frame result was stale or
    /// superseded and the client should use its local fallback render.
    /// </summary>
    public readonly struct SubmitOutcome
    {
        private SubmitOutcome(ResultPushMessage push)
        {
            IsResultDrop = false;
            ResultPush = push;
            ResultDrop = default;
        }

        private SubmitOutcome(ResultDropMessage drop)
        {
            IsResultDrop = true;
            ResultPush = default;
            ResultDrop = drop;
        }

        /// <summary>
        /// <see langword="true"/> when the server sent RESULT_DROP; access result via
        /// <see cref="ResultDrop"/>. <see langword="false"/> when the server sent
        /// RESULT_PUSH; access result via <see cref="ResultPush"/>.
        /// </summary>
        public bool IsResultDrop { get; }

        /// <summary>
        /// Set when <see cref="IsResultDrop"/> is <see langword="false"/>.
        /// </summary>
        public ResultPushMessage ResultPush { get; }

        /// <summary>
        /// Set when <see cref="IsResultDrop"/> is <see langword="true"/>.
        /// </summary>
        public ResultDropMessage ResultDrop { get; }

        /// <summary>Creates a <see cref="SubmitOutcome"/> wrapping a RESULT_PUSH.</summary>
        public static SubmitOutcome FromResultPush(ResultPushMessage message) => new SubmitOutcome(message);

        /// <summary>Creates a <see cref="SubmitOutcome"/> wrapping a RESULT_DROP.</summary>
        public static SubmitOutcome FromResultDrop(ResultDropMessage message) => new SubmitOutcome(message);

        /// <summary>
        /// Parses raw result packet bytes into a <see cref="SubmitOutcome"/>, accepting
        /// either RESULT_PUSH or RESULT_DROP.
        /// </summary>
        public static bool TryParse(ReadOnlyMemory<byte> source, out SubmitOutcome outcome, out NnrpParseError error)
        {
            outcome = default;
            if (!NnrpFramedMessage.TryParse(source, NnrpHeaderParseOptions.Strict, out var framed, out error))
            {
                return false;
            }

            if (framed.Header.MessageType == MessageType.ResultDrop)
            {
                if (!ResultDropMessage.TryParse(framed, out var drop, out error))
                {
                    return false;
                }

                outcome = FromResultDrop(drop);
                return true;
            }

            if (framed.Header.MessageType == MessageType.ResultPush)
            {
                if (!ResultPushMessage.TryParse(framed, out var push, out error))
                {
                    return false;
                }

                outcome = FromResultPush(push);
                return true;
            }

            error = NnrpParseError.InvalidMessageLayout;
            return false;
        }
    }
}
