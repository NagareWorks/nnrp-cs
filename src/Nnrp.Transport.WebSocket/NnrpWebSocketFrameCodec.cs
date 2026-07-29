using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Nnrp.Core;
using Nnrp.Runtime;

namespace Nnrp.Transport.WebSocket
{
    /// <summary>Encodes and decodes NNRP frames carried by WebSocket binary messages.</summary>
    public static class NnrpWebSocketFrameCodec
    {
        public static byte[] Encode(
            RuntimeFrameHeader header,
            ReadOnlySpan<byte> metadata = default,
            ReadOnlySpan<byte> body = default)
        {
            var wireHeader = new NnrpHeader(
                header.VersionMajor,
                header.MessageType,
                header.Flags,
                (uint)metadata.Length,
                (uint)body.Length,
                header.SessionId,
                header.FrameId,
                header.ViewId,
                header.RouteId,
                header.TraceId,
                header.WireFormat);
            Span<byte> encodedHeader = stackalloc byte[NnrpHeader.HeaderLength];
            wireHeader.Write(encodedHeader);
            if (!NnrpHeader.TryParse(
                    encodedHeader,
                    NnrpHeaderParseOptions.Strict,
                    out _,
                    out var parseError))
            {
                throw new ArgumentException($"Runtime frame header is invalid: {parseError}.", nameof(header));
            }

            var totalLength = (long)NnrpHeader.HeaderLength + metadata.Length + body.Length;
            if (totalLength > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(body), "Runtime frame exceeds the managed buffer limit.");
            }

            var encoded = new byte[(int)totalLength];
            encodedHeader.CopyTo(encoded);
            metadata.CopyTo(encoded.AsSpan(NnrpHeader.HeaderLength, metadata.Length));
            body.CopyTo(encoded.AsSpan(NnrpHeader.HeaderLength + metadata.Length, body.Length));
            return encoded;
        }

        public static DecodedRuntimeFrame Decode(ReadOnlySpan<byte> frame)
        {
            var frameLength = ParseFrameLength(frame, out var header);
            if (frame.Length != frameLength)
            {
                throw new FormatException("WebSocket binary frame contains trailing bytes.");
            }

            return DecodeOwnedFrame(frame, header);
        }

        public static IReadOnlyList<DecodedRuntimeFrame> DecodeBatch(
            ReadOnlySpan<byte> batch,
            int limit = 0)
        {
            if (limit < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(limit));
            }

            if (batch.IsEmpty)
            {
                return Array.Empty<DecodedRuntimeFrame>();
            }

            var frames = new List<DecodedRuntimeFrame>();
            var offset = 0;
            while (offset < batch.Length)
            {
                if (limit != 0 && frames.Count >= limit)
                {
                    throw new FormatException($"WebSocket batch contains more than {limit} frames.");
                }

                var remaining = batch.Slice(offset);
                var frameLength = ParseFrameLength(remaining, out var header);
                frames.Add(DecodeOwnedFrame(remaining.Slice(0, frameLength), header));
                offset += frameLength;
            }

            return new ReadOnlyCollection<DecodedRuntimeFrame>(frames);
        }

        private static int ParseFrameLength(ReadOnlySpan<byte> frame, out NnrpHeader header)
        {
            if (!NnrpHeader.TryParse(
                    frame,
                    NnrpHeaderParseOptions.Strict,
                    out header,
                    out var parseError))
            {
                throw new FormatException($"WebSocket binary frame has an invalid NNRP header: {parseError}.");
            }

            var frameLength = (ulong)NnrpHeader.HeaderLength + header.MetaLength + header.BodyLength;
            if (frameLength > int.MaxValue || frameLength > (ulong)frame.Length)
            {
                throw new FormatException("WebSocket binary frame metadata/body lengths exceed the available bytes.");
            }

            return (int)frameLength;
        }

        private static DecodedRuntimeFrame DecodeOwnedFrame(ReadOnlySpan<byte> frame, NnrpHeader header)
        {
            var metadataLength = (int)header.MetaLength;
            var bodyLength = (int)header.BodyLength;
            var projection = new RuntimeFrameHeader(
                header.MessageType,
                header.Flags,
                header.SessionId,
                header.FrameId,
                header.ViewId,
                header.RouteId,
                header.TraceId,
                header.VersionMajor,
                header.WireFormat);
            return new DecodedRuntimeFrame(
                projection,
                frame.Slice(NnrpHeader.HeaderLength, metadataLength),
                frame.Slice(NnrpHeader.HeaderLength + metadataLength, bodyLength));
        }
    }
}
