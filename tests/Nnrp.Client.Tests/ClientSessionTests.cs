using System;
using System.Threading;
using System.Threading.Tasks;
using Nnrp.Client;
using Nnrp.Core;
using Xunit;

namespace Nnrp.Client.Tests
{
    public sealed class ClientSessionTests
    {
        [Fact]
        public async Task ConnectAsyncNegotiatesCapabilitiesAndActivatesSession()
        {
            var profile = new ClientProfile
            {
                SupportedCodecs = new[] { CodecId.Raw, CodecId.Lz4 },
                SupportedDTypes = new[] { DTypeId.UInt8, DTypeId.Float16 },
                SupportedTensorLayouts = new[] { TensorLayoutId.Nhwc },
                MaxViews = 1,
            };
            var session = new NnrpClientSession(profile);

            var result = await session.ConnectAsync(CreateServerCapabilities(), CancellationToken.None);

            Assert.True(result.IsConnected);
            Assert.True(result.NegotiationResult.IsAccepted);
            Assert.Equal(NnrpSessionState.Active, session.State);
            Assert.Equal(CodecId.Lz4, session.NegotiatedCapabilities.Codec);
            Assert.Equal(DTypeId.Float16, session.NegotiatedCapabilities.DType);
            Assert.Equal(result.NegotiationResult.Selection, session.NegotiatedCapabilities);
            Assert.True(session.TryAcceptFrameSubmit(out var frameFailure));
            Assert.Equal(NnrpProtocolFailure.None, frameFailure);
        }

        [Fact]
        public async Task ConnectAsyncRejectsUnsupportedCapabilitiesAndClosesSession()
        {
            var profile = new ClientProfile
            {
                SupportedCodecs = new[] { CodecId.Raw },
                SupportedDTypes = new[] { DTypeId.UInt8 },
                SupportedTensorLayouts = new[] { TensorLayoutId.Nhwc },
            };
            var session = new NnrpClientSession(profile);

            var result = await session.ConnectAsync(
                CreateServerCapabilities(acceptedCodecs: new[] { CodecId.Lz4 }),
                CancellationToken.None);

            Assert.False(result.IsConnected);
            Assert.False(result.NegotiationResult.IsAccepted);
            Assert.Equal(ErrorCode.UnsupportedCapability, result.Failure.ErrorCode);
            Assert.Equal(NnrpErrorScope.Session, result.Failure.Scope);
            Assert.True(result.Failure.IsFatal);
            Assert.Equal(NnrpSessionState.Closed, session.State);
            Assert.Equal(result.Failure, session.LastFailure);
            Assert.False(session.TryAcceptFrameSubmit(out var frameFailure));
            Assert.Equal(ErrorCode.InvalidState, frameFailure.ErrorCode);
        }

        [Fact]
        public async Task ConnectAsyncMapsLimitRejectionsToLimitExceededFailure()
        {
            var session = new NnrpClientSession(new ClientProfile { MaxViews = 2 });

            var result = await session.ConnectAsync(CreateServerCapabilities(maxViews: 1), CancellationToken.None);

            Assert.False(result.IsConnected);
            Assert.Equal(ErrorCode.LimitExceeded, result.Failure.ErrorCode);
            Assert.Equal(NnrpErrorScope.Session, result.Failure.Scope);
            Assert.True(result.Failure.IsFatal);
            Assert.Equal(CapabilityRejectionReason.MaxViewsExceeded, result.NegotiationResult.RejectionReason);
        }

        [Fact]
        public async Task CloseAsyncDrainsThenClosesActiveSession()
        {
            var session = new NnrpClientSession(new ClientProfile());
            Assert.True((await session.ConnectAsync(CreateServerCapabilities(), CancellationToken.None)).IsConnected);

            var firstClose = await session.CloseAsync(CancellationToken.None);
            Assert.Equal(NnrpProtocolFailure.None, firstClose);
            Assert.Equal(NnrpSessionState.Draining, session.State);
            Assert.False(session.TryAcceptFrameSubmit(out var frameFailure));
            Assert.Equal(ErrorCode.InvalidState, frameFailure.ErrorCode);

            var secondClose = await session.CloseAsync(CancellationToken.None);
            Assert.Equal(NnrpProtocolFailure.None, secondClose);
            Assert.Equal(NnrpSessionState.Closed, session.State);

            var thirdClose = await session.CloseAsync(CancellationToken.None);
            Assert.Equal(ErrorCode.InvalidState, thirdClose.ErrorCode);
            Assert.Equal(NnrpSessionState.Closed, session.State);
        }

        [Fact]
        public async Task SessionReportsInvalidRepeatedConnectAndHonorsCancellation()
        {
            Assert.Throws<ArgumentNullException>(() => new NnrpClientSession(null!));

            var session = new NnrpClientSession(new ClientProfile());
            Assert.True((await session.ConnectAsync(CreateServerCapabilities(), CancellationToken.None)).IsConnected);

            var repeatedConnect = await session.ConnectAsync(CreateServerCapabilities(), CancellationToken.None);
            Assert.False(repeatedConnect.IsConnected);
            Assert.Equal(ErrorCode.InvalidState, repeatedConnect.Failure.ErrorCode);
            Assert.Equal(NnrpSessionState.Active, session.State);

            using (var cancellationSource = new CancellationTokenSource())
            {
                cancellationSource.Cancel();
                await Assert.ThrowsAsync<OperationCanceledException>(async () =>
                    await session.CloseAsync(cancellationSource.Token));
                await Assert.ThrowsAsync<OperationCanceledException>(async () =>
                    await new NnrpClientSession(new ClientProfile()).ConnectAsync(CreateServerCapabilities(), cancellationSource.Token));
            }
        }

        [Fact]
        public void ClientConnectResultFactoriesValidateInputs()
        {
            var accepted = NnrpCapabilityNegotiationResult.Accepted(default);
            var rejected = NnrpCapabilityNegotiationResult.Rejected(
                ErrorCode.UnsupportedCapability,
                CapabilityRejectionReason.NoCommonCodec,
                "No codec.");
            var failure = NnrpProtocolFailure.UnsupportedCapability("No codec.");

            Assert.True(NnrpClientConnectResult.Connected(accepted).IsConnected);
            Assert.False(NnrpClientConnectResult.Rejected(rejected, failure).IsConnected);
            Assert.Equal(failure, NnrpClientConnectResult.Failed(failure).Failure);
            Assert.Throws<ArgumentException>(() => NnrpClientConnectResult.Connected(rejected));
            Assert.Throws<ArgumentException>(() => NnrpClientConnectResult.Rejected(accepted, failure));
        }

        private static NnrpServerCapabilities CreateServerCapabilities(
            CodecId[]? acceptedCodecs = null,
            int maxViews = 1)
        {
            return new NnrpServerCapabilities(
                acceptedCodecs ?? new[] { CodecId.Lz4, CodecId.Raw },
                new[] { DTypeId.Float16, DTypeId.UInt8 },
                new[] { TensorLayoutId.Nhwc },
                (uint)PayloadKind.Tensor,
                ControlMetadataBitmaps.LowFrequencyObjectBitmap,
                BudgetPolicy.AllowPartial | BudgetPolicy.AllowDegraded,
                maxConcurrentFrames: 2,
                enableCache: true,
                maxCacheEntries: 128,
                maxBodyBytes: 1024 * 1024,
                maxSectionCount: 8,
                maxTileCount: 4096,
                maxViews: maxViews,
                tokenTtlSeconds: 60,
                allowSessionRenewal: true);
        }
    }
}
