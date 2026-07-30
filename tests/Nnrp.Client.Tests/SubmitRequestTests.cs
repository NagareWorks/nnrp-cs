using System;
using Nnrp.Core;
using Xunit;

namespace Nnrp.Client.Tests
{
    public sealed class SubmitRequestTests
    {
        [Fact]
        public void TensorSubmitEncodesInlineMixedCodecAndFixedStrideSections()
        {
            var identity = new NnrpSubmitIdentity(
                101,
                11,
                new NnrpSubmitHeaderContext(HeaderFlags.AckRequired, 2, 3, 4));
            var policy = new NnrpSubmitPolicy(
                frameClass: 1,
                latencyBudgetMilliseconds: 16,
                targetFpsTimes100: 6000,
                retryOfFrame: 9,
                budgetPolicy: BudgetPolicy.AllowPartial,
                lossTolerancePolicy: LossTolerancePolicy.LowLatency,
                dependencyFrameId: 8);
            var section = new NnrpTensorSection(
                roleId: 1,
                defaultCodecId: (byte)CodecId.Raw,
                dtypeId: (byte)DTypeId.UInt8,
                layoutId: (byte)TensorLayoutId.Nhwc,
                tilePayloads: new ReadOnlyMemory<byte>[]
                {
                    new byte[] { 1, 2 },
                    new byte[] { 3 },
                },
                scalePolicy: (byte)ScalePolicy.None,
                elementCountPerTile: 4,
                codecIds: new byte[] { (byte)CodecId.Raw, (byte)CodecId.Lz4 },
                payloadStrideBytes: 4);
            var input = new NnrpTensorSubmitInput(
                identity,
                policy,
                sourceWidth: 8,
                sourceHeight: 4,
                tileWidth: 4,
                tileHeight: 4,
                tileIds: new ushort[] { 5, 6 },
                sections: new[] { section },
                inputProfile: InputProfile.DenseLumaFrame,
                tileIndexMode: TileIndexMode.RawUInt16,
                cameraBlock: new byte[] { 7, 8 },
                tileBaseId: 5);

            var request = NnrpSubmitRequest.CreateTensor(input);
            var payload = request.EncodePayload();

            Assert.Equal((ulong)101, request.OperationId);
            Assert.Equal((uint)11, request.FrameId);
            Assert.Equal(HeaderFlags.AckRequired, request.Header.Flags);
            Assert.Equal((ushort)2, request.Header.ViewId);
            Assert.Equal((ushort)3, request.Header.RouteId);
            Assert.Equal((ulong)4, request.Header.TraceId);
            Assert.Equal(SubmitMode.Inline, request.Metadata.SubmitMode);
            Assert.Equal(BudgetPolicy.AllowPartial, request.Metadata.BudgetPolicy);
            Assert.Equal(LossTolerancePolicy.LowLatency, request.Metadata.LossTolerancePolicy);
            Assert.Equal((uint)8, request.Metadata.DependencyFrameId);
            Assert.Equal(PayloadKind.Tensor, request.Metadata.PayloadKindBitmap);
            Assert.Equal(FrameSubmitMetadata.MetadataLength + request.Body.Length, payload.Length);
        }

        [Fact]
        public void TensorSubmitSupportsReferenceAndMixedModes()
        {
            var camera = BodyCodec.BuildObjectReferenceBlock(CacheObjectKind.CameraBlock, 1, 2, 3);
            var tileIndex = BodyCodec.BuildObjectReferenceBlock(CacheObjectKind.TileIndexBlock, 1, 4, 5);
            var sectionTable = BodyCodec.BuildObjectReferenceBlock(CacheObjectKind.TensorSectionTable, 1, 6, 7);
            var references = new NnrpSubmitObjectReferences(camera, tileIndex, sectionTable);
            var referenceOnly = NnrpSubmitRequest.CreateTensor(new NnrpTensorSubmitInput(
                new NnrpSubmitIdentity(102, 12),
                new NnrpSubmitPolicy(),
                8,
                8,
                4,
                4,
                new ushort[] { 1 },
                Array.Empty<NnrpTensorSection>(),
                InputProfile.DenseLumaFrame,
                TileIndexMode.RawUInt16,
                references: references));

            var mixed = NnrpSubmitRequest.CreateTensor(new NnrpTensorSubmitInput(
                new NnrpSubmitIdentity(103, 13),
                new NnrpSubmitPolicy(),
                8,
                8,
                4,
                4,
                new ushort[] { 1 },
                Array.Empty<NnrpTensorSection>(),
                InputProfile.DenseLumaFrame,
                TileIndexMode.RawUInt16,
                cameraBlock: new byte[] { 9 },
                references: new NnrpSubmitObjectReferences(tileIndex: tileIndex)));

            Assert.Equal(SubmitMode.Reference, referenceOnly.Metadata.SubmitMode);
            Assert.Equal((uint)7, referenceOnly.Metadata.ObjectRefMask);
            Assert.Equal(SubmitMode.Mixed, mixed.Metadata.SubmitMode);
            Assert.Equal((uint)2, mixed.Metadata.ObjectRefMask);
            Assert.Throws<ArgumentException>(() =>
                new NnrpSubmitObjectReferences(camera: tileIndex));
            Assert.Throws<ArgumentException>(() =>
                new NnrpSubmitObjectReferences(tileIndex: camera));
            Assert.Throws<ArgumentException>(() =>
                new NnrpSubmitObjectReferences(tensorSectionTable: camera));
        }

        [Fact]
        public void TensorSubmitRejectsInvalidSectionShapesAndOrdering()
        {
            NnrpTensorSubmitInput Input(params NnrpTensorSection[] sections) => new NnrpTensorSubmitInput(
                new NnrpSubmitIdentity(104, 14),
                new NnrpSubmitPolicy(),
                8,
                8,
                4,
                4,
                new ushort[] { 1, 2 },
                sections,
                InputProfile.DenseLumaFrame,
                TileIndexMode.RawUInt16);

            var valid = new NnrpTensorSection(
                2,
                (byte)CodecId.Raw,
                (byte)DTypeId.UInt8,
                (byte)TensorLayoutId.Nhwc,
                new ReadOnlyMemory<byte>[] { new byte[] { 1 }, new byte[] { 2 } });
            var duplicateRole = new NnrpTensorSection(
                2,
                (byte)CodecId.Raw,
                (byte)DTypeId.UInt8,
                (byte)TensorLayoutId.Nhwc,
                new ReadOnlyMemory<byte>[] { new byte[] { 1 }, new byte[] { 2 } });
            var wrongPayloadCount = new NnrpTensorSection(
                3,
                (byte)CodecId.Raw,
                (byte)DTypeId.UInt8,
                (byte)TensorLayoutId.Nhwc,
                new ReadOnlyMemory<byte>[] { new byte[] { 1 } });
            var wrongCodecCount = new NnrpTensorSection(
                3,
                (byte)CodecId.Raw,
                (byte)DTypeId.UInt8,
                (byte)TensorLayoutId.Nhwc,
                new ReadOnlyMemory<byte>[] { new byte[] { 1 }, new byte[] { 2 } },
                codecIds: new byte[] { (byte)CodecId.Raw });
            var strideOverflow = new NnrpTensorSection(
                3,
                (byte)CodecId.Raw,
                (byte)DTypeId.UInt8,
                (byte)TensorLayoutId.Nhwc,
                new ReadOnlyMemory<byte>[] { new byte[] { 1, 2 }, new byte[] { 3 } },
                payloadStrideBytes: 1);

            Assert.Throws<ArgumentException>(() => NnrpSubmitRequest.CreateTensor(Input(valid, duplicateRole)));
            Assert.Throws<ArgumentException>(() => NnrpSubmitRequest.CreateTensor(Input(wrongPayloadCount)));
            Assert.Throws<ArgumentException>(() => NnrpSubmitRequest.CreateTensor(Input(wrongCodecCount)));
            Assert.Throws<ArgumentException>(() => NnrpSubmitRequest.CreateTensor(Input(strideOverflow)));
            Assert.Throws<ArgumentException>(() => NnrpSubmitRequest.CreateTensor(default));
        }

        [Fact]
        public void TypedAndTokenSubmitsEncodeFrozenPayloadMetadata()
        {
            var identity = new NnrpSubmitIdentity(105, 15);
            var policy = new NnrpSubmitPolicy(
                frameClass: 2,
                latencyBudgetMilliseconds: 20,
                targetFpsTimes100: 3000,
                retryOfFrame: 7,
                budgetPolicy: BudgetPolicy.AllowDrop,
                lossTolerancePolicy: LossTolerancePolicy.Strict,
                dependencyFrameId: 6);
            var typed = NnrpSubmitRequest.CreateTypedPayload(new NnrpTypedPayloadSubmitInput(
                identity,
                policy,
                new[]
                {
                    new NnrpTypedPayloadInputFrame(
                        9,
                        PayloadKind.ToolDelta,
                        new byte[] { 1, 2 },
                        TypedPayloadDescriptorFlags.Partial,
                        11,
                        12,
                        TypedPayloadDescriptor.StreamSemanticsAppend),
                    new NnrpTypedPayloadInputFrame(
                        10,
                        PayloadKind.OpaqueBytes,
                        new byte[] { 3 }),
                }));
            var token = NnrpSubmitRequest.CreateToken(new NnrpTokenSubmitInput(
                new NnrpSubmitIdentity(106, 16),
                new NnrpSubmitPolicy(),
                new[]
                {
                    new NnrpTokenChunk(new byte[] { 4, 5 }),
                    new NnrpTokenChunk(new byte[] { 6 }, TypedPayloadDescriptorFlags.Terminal),
                }));

            Assert.Equal(PayloadKind.ToolDelta | PayloadKind.OpaqueBytes, typed.Metadata.PayloadKindBitmap);
            Assert.Equal((ushort)2, typed.Metadata.PayloadFrameCount);
            Assert.Equal((uint)6, typed.Metadata.DependencyFrameId);
            Assert.Equal(BudgetPolicy.AllowDrop, typed.Metadata.BudgetPolicy);
            Assert.Equal(PayloadKind.TokenChunk, token.Metadata.PayloadKindBitmap);
            Assert.Equal((ushort)2, token.Metadata.PayloadFrameCount);
            Assert.NotEmpty(typed.EncodePayload());
            Assert.NotEmpty(token.EncodePayload());
        }

        [Fact]
        public void SubmitBuildersRejectMissingIdentityOrPayload()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new NnrpSubmitIdentity(0, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new NnrpSubmitIdentity(1, 0));
            Assert.Throws<ArgumentException>(() =>
                NnrpSubmitRequest.CreateToken(new NnrpTokenSubmitInput(
                    new NnrpSubmitIdentity(1, 1),
                    new NnrpSubmitPolicy(),
                    Array.Empty<NnrpTokenChunk>())));
            Assert.Throws<ArgumentException>(() =>
                NnrpSubmitRequest.CreateTypedPayload(new NnrpTypedPayloadSubmitInput(
                    new NnrpSubmitIdentity(1, 1),
                    new NnrpSubmitPolicy(),
                    Array.Empty<NnrpTypedPayloadInputFrame>())));
            Assert.Throws<ArgumentException>(() =>
                NnrpSubmitRequest.CreateTypedPayload(default));
        }
    }
}
