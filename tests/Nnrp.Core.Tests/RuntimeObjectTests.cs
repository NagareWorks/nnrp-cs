using System;
using System.Collections.Generic;
using Nnrp.Core;
using Nnrp.Runtime;
using Xunit;

namespace Nnrp.Core.Tests
{
    public sealed class RuntimeObjectTests
    {
        public static IEnumerable<object[]> RuntimeObjectCases()
        {
            var metadata = new byte[] { 1, 2 };
            var delta = new byte[] { 3, 4, 5 };
            yield return Case(MessageType.ObjectDeclare, new ObjectDescriptorMetadata(1, RuntimeObjectKind.Tensor, RuntimeRole.Runtime, RuntimeRole.Client, 2, 1024, 7, MemoryLocationHint.DeviceMemory, OwnershipHint.Borrowed, 500, 2), metadata);
            yield return Case(MessageType.ObjectRef, new ObjectReferenceMetadata(1, 2, 3, 4, 5, 1, 2), metadata);
            yield return Case(MessageType.ObjectRelease, new ObjectReleaseMetadata(1, 2, ObjectReleaseReason.Completed, RuntimeRole.Runtime, 1, 2), metadata);
            yield return Case(MessageType.ObjectPatch, new ObjectDeltaMetadata(1, 2, 3, 4, 3, 1, 2), Combine(metadata, delta));
            yield return Case(MessageType.ObjectDelta, new ObjectDeltaMetadata(1, 3, 4, 5, 3, 2, 2), Combine(metadata, delta));
            yield return Case(MessageType.CacheReference, new CacheReferenceMetadata(1, 2, 3, 4, CacheReuseScope.Session, 5, 6, 7, 2, 1), metadata);
            yield return Case(MessageType.CacheMiss, new CacheMissMetadata(1, 2, 3, CacheMissReason.SchemaMismatch, 4, 2), metadata);
        }

        [Theory]
        [MemberData(nameof(RuntimeObjectCases))]
        public void FrozenRuntimeObjectMetadataRoundTrips(MessageType messageType, IRuntimeObjectMetadata metadata, byte[] tail)
        {
            var encoded = NnrpRuntimeObject.Encode(messageType, metadata, tail);
            var decoded = NnrpRuntimeObject.Decode(messageType, encoded);

            Assert.Equal(metadata, decoded.Metadata);
            Assert.Equal(tail, decoded.Tail.ToArray());
            Assert.Equal(encoded, NnrpRuntimeObject.Encode(messageType, decoded.Metadata, decoded.Tail.Span));
        }

        [Fact]
        public void FrozenDocumentationNamedArgumentsConstructObjectMetadata()
        {
            var metadata = new CacheMissMetadata(CacheNamespace: 1, CacheKeyHi: 2, CacheKeyLo: 3, MissReason: CacheMissReason.NotFound, ProfileId: 4, DiagnosticBytes: 0);

            Assert.Equal(1U, metadata.CacheNamespace);
            Assert.Equal(metadata, metadata with { ProfileId = 4 });
        }

        [Fact]
        public void RuntimeObjectRejectsWrongTypeTailAndMessageType()
        {
            Assert.Throws<ArgumentException>(() => NnrpRuntimeObject.Encode(MessageType.ObjectRef, new CacheMissMetadata(1, 2, 3, CacheMissReason.NotFound, 4, 0)));
            Assert.Throws<ArgumentException>(() => NnrpRuntimeObject.Encode(MessageType.CacheMiss, new CacheMissMetadata(1, 2, 3, CacheMissReason.NotFound, 4, 1)));
            Assert.Throws<ArgumentOutOfRangeException>(() => NnrpRuntimeObject.Encode(MessageType.Progress, new CacheMissMetadata(1, 2, 3, CacheMissReason.NotFound, 4, 0)));
            Assert.Throws<ArgumentException>(() => NnrpRuntimeObject.Decode(MessageType.ObjectDeclare, Array.Empty<byte>()));
        }

        [Fact]
        public void RuntimeObjectRejectsUnknownEnumsReservedFlagsAndBytes()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => NnrpRuntimeObject.Encode(MessageType.ObjectDeclare, new ObjectDescriptorMetadata(1, (RuntimeObjectKind)99, RuntimeRole.Runtime, RuntimeRole.Client, 2, 3, 4, MemoryLocationHint.HostMemory, OwnershipHint.Borrowed, 5, 0)));
            Assert.Throws<ArgumentOutOfRangeException>(() => NnrpRuntimeObject.Encode(MessageType.ObjectRef, new ObjectReferenceMetadata(1, 2, 3, 4, 5, 8, 0)));

            var descriptor = NnrpRuntimeObject.Encode(MessageType.ObjectDeclare, new ObjectDescriptorMetadata(1, RuntimeObjectKind.Tensor, RuntimeRole.Runtime, RuntimeRole.Client, 2, 3, 4, MemoryLocationHint.HostMemory, OwnershipHint.Borrowed, 5, 0));
            descriptor[47] = 1;
            Assert.Throws<ArgumentException>(() => NnrpRuntimeObject.Decode(MessageType.ObjectDeclare, descriptor));

            var cache = NnrpRuntimeObject.Encode(MessageType.CacheReference, new CacheReferenceMetadata(1, 2, 3, 4, CacheReuseScope.Session, 5, 6, 7, 0, 0));
            cache[55] = 1;
            Assert.Throws<ArgumentException>(() => NnrpRuntimeObject.Decode(MessageType.CacheReference, cache));
        }

        [Fact]
        public void DecodedRuntimeObjectRejectsWrongRequestedType()
        {
            var decoded = NnrpRuntimeObject.Decode(MessageType.CacheMiss, NnrpRuntimeObject.Encode(MessageType.CacheMiss, new CacheMissMetadata(1, 2, 3, CacheMissReason.NotFound, 4, 0)));

            Assert.Throws<InvalidOperationException>(() => decoded.GetMetadata<ObjectReferenceMetadata>());
        }

        [Fact]
        public void CachePolicyOptionsRequireExplicitEnabledSettings()
        {
            Assert.Equal(default(CachePolicyOptions), new CachePolicyOptions());

            var policy = new CachePolicyOptions(
                enabled: true,
                reuseScope: CacheReuseScope.Session,
                expirationHintMilliseconds: 500,
                invalidationReason: CachePolicyInvalidationReason.VersionMismatch);
            var samePolicy = new CachePolicyOptions(
                enabled: true,
                reuseScope: CacheReuseScope.Session,
                expirationHintMilliseconds: 500,
                invalidationReason: CachePolicyInvalidationReason.VersionMismatch);
            var disabledPolicy = new CachePolicyOptions();

            Assert.True(policy.Enabled);
            Assert.Equal(CacheReuseScope.Session, policy.ReuseScope);
            Assert.Equal(500UL, policy.ExpirationHintMilliseconds);
            Assert.Equal(CachePolicyInvalidationReason.VersionMismatch, policy.InvalidationReason);
            Assert.Equal(policy, samePolicy);
            Assert.True(policy == samePolicy);
            Assert.True(policy != disabledPolicy);
            Assert.Equal(policy.GetHashCode(), samePolicy.GetHashCode());
        }

        [Fact]
        public void CachePolicyOptionsRejectImplicitOrUnknownSettings()
        {
            Assert.Throws<ArgumentException>(() => new CachePolicyOptions(enabled: true));
            Assert.Throws<ArgumentException>(() => new CachePolicyOptions(reuseScope: CacheReuseScope.Session));
            Assert.Throws<ArgumentException>(() => new CachePolicyOptions(expirationHintMilliseconds: 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CachePolicyOptions(true, (CacheReuseScope)99));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CachePolicyOptions(invalidationReason: (CachePolicyInvalidationReason)99));
        }

        private static object[] Case(MessageType messageType, IRuntimeObjectMetadata metadata, byte[] tail) => new object[] { messageType, metadata, tail };

        private static byte[] Combine(byte[] first, byte[] second)
        {
            var result = new byte[first.Length + second.Length];
            first.CopyTo(result, 0);
            second.CopyTo(result, first.Length);
            return result;
        }
    }
}
