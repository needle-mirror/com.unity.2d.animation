using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine.Rendering;
using UnityEngine.U2D.Common;

#if ENABLE_URP
using UnityEngine.Rendering.Universal;
#endif

namespace UnityEngine.U2D.Animation
{
    internal class NativeByteArray
    {
        public int Length => array.Length;
        public bool IsCreated => array.IsCreated;
        public byte this[int index] => array[index];

        public NativeArray<byte> array { get; }

        public NativeByteArray(NativeArray<byte> array)
        {
            this.array = array;
        }

        public void Dispose() => array.Dispose();
    }

    internal static class SpriteSkinUtility
    {
#if UNITY_INCLUDE_TESTS
        private static bool? s_IsUsingGpuDeformationForTest;
        public static void SetUsingGpuDeformationForTest(bool? flag)
        {
            s_IsUsingGpuDeformationForTest = flag;
        }
#endif

        internal static bool CanUseGpuDeformation()
        {
            return SystemInfo.supportsComputeShaders;
        }

        internal static bool IsUsingGpuDeformation()
        {
#if UNITY_INCLUDE_TESTS
            if (s_IsUsingGpuDeformationForTest.HasValue)
            {
                return s_IsUsingGpuDeformationForTest.Value;
            }
#endif

#if ENABLE_URP
            return CanUseGpuDeformation() &&
                GraphicsSettings.currentRenderPipeline != null &&
                UniversalRenderPipeline.asset != null && UniversalRenderPipeline.asset.useSRPBatcher &&
                InternalEngineBridge.IsGPUSkinningEnabled();
#else
            return false;
#endif
        }

        internal static bool IsGpuDeformationActive(SpriteRenderer spriteRenderer)
        {
#if UNITY_INCLUDE_TESTS
            if (s_IsUsingGpuDeformationForTest.HasValue)
            {
                return s_IsUsingGpuDeformationForTest.Value;
            }
#endif

#if ENABLE_URP
            return CanUseGpuDeformation() &&
                InternalEngineBridge.IsSRPBatchingEnabled(spriteRenderer) &&
                InternalEngineBridge.IsGPUSkinningEnabled();
#else
            return false;
#endif
        }

        internal static bool CanSpriteSkinUseGpuDeformation(SpriteSkin spriteSkin)
        {
#if ENABLE_URP
            return IsGpuDeformationActive(spriteSkin.spriteRenderer) &&
                GpuDeformationSystem.DoesShaderSupportGpuDeformation(spriteSkin.spriteRenderer.sharedMaterial);
#else
            return false;
#endif
        }

        internal static SpriteSkinState Validate(this SpriteSkin spriteSkin)
        {
            Sprite sprite = spriteSkin.sprite;
            if (sprite == null)
                return SpriteSkinState.SpriteNotFound;

            NativeArray<Matrix4x4> bindPoses = sprite.GetBindPoses();
            int bindPoseCount = bindPoses.Length;

            if (bindPoseCount == 0)
                return SpriteSkinState.SpriteHasNoSkinningInformation;

            if (spriteSkin.rootBone == null)
                return SpriteSkinState.RootTransformNotFound;

            if (spriteSkin.boneTransforms == null)
                return SpriteSkinState.InvalidTransformArray;

            if (bindPoseCount != spriteSkin.boneTransforms.Length)
                return SpriteSkinState.InvalidTransformArrayLength;

            foreach (Transform boneTransform in spriteSkin.boneTransforms)
            {
                if (boneTransform == null)
                    return SpriteSkinState.TransformArrayContainsNull;
            }

            NativeCustomSlice<BoneWeight> boneWeights = spriteSkin.spriteBoneWeights;
            if (!BurstedSpriteSkinUtilities.ValidateBoneWeights(in boneWeights, bindPoseCount))
                return SpriteSkinState.InvalidBoneWeights;

            return SpriteSkinState.Ready;
        }

        internal static void CreateBoneHierarchy(this SpriteSkin spriteSkin)
        {
            if (spriteSkin.spriteRenderer.sprite == null)
                throw new InvalidOperationException("SpriteRenderer has no Sprite set");

            SpriteBone[] spriteBones = spriteSkin.spriteRenderer.sprite.GetBones();
            Transform[] transforms = new Transform[spriteBones.Length];
            Transform root = null;

            for (int i = 0; i < spriteBones.Length; ++i)
            {
                CreateGameObject(i, spriteBones, transforms, spriteSkin.transform);
                if (spriteBones[i].parentId < 0 && root == null)
                    root = transforms[i];
            }

            spriteSkin.SetRootBone(root);
            spriteSkin.SetBoneTransforms(transforms);
        }

        internal static int GetVertexStreamSize(this Sprite sprite)
        {
            int vertexStreamSize = 12;
            if (sprite.HasVertexAttribute(Rendering.VertexAttribute.Normal))
                vertexStreamSize = vertexStreamSize + 12;
            if (sprite.HasVertexAttribute(Rendering.VertexAttribute.Tangent))
                vertexStreamSize = vertexStreamSize + 16;
            return vertexStreamSize;
        }

        internal static int GetVertexStreamOffset(this Sprite sprite, Rendering.VertexAttribute channel)
        {
            bool hasPosition = sprite.HasVertexAttribute(Rendering.VertexAttribute.Position);
            bool hasNormals = sprite.HasVertexAttribute(Rendering.VertexAttribute.Normal);
            bool hasTangents = sprite.HasVertexAttribute(Rendering.VertexAttribute.Tangent);

            switch (channel)
            {
                case Rendering.VertexAttribute.Position:
                    return hasPosition ? 0 : -1;
                case Rendering.VertexAttribute.Normal:
                    return hasNormals ? 12 : -1;
                case Rendering.VertexAttribute.Tangent:
                    return hasTangents ? (hasNormals ? 24 : 12) : -1;
            }

            return -1;
        }

        static void CreateGameObject(int index, SpriteBone[] spriteBones, Transform[] transforms, Transform root)
        {
            if (transforms[index] == null)
            {
                SpriteBone spriteBone = spriteBones[index];
                if (spriteBone.parentId >= 0)
                    CreateGameObject(spriteBone.parentId, spriteBones, transforms, root);

                GameObject go = new GameObject(spriteBone.name);
                Transform transform = go.transform;
                if (spriteBone.parentId >= 0)
                    transform.SetParent(transforms[spriteBone.parentId]);
                else
                    transform.SetParent(root);
                transform.localPosition = spriteBone.position;
                transform.localRotation = spriteBone.rotation;
                transform.localScale = Vector3.one;
                transforms[index] = transform;
            }
        }

        static int GetHash(Matrix4x4 matrix)
        {
            unsafe
            {
                uint* b = (uint*)&matrix;
                {
                    char* c = (char*)b;
                    return (int)math.hash(c, 16 * sizeof(float));
                }
            }
        }

        internal static int CalculateTransformHash(this SpriteSkin spriteSkin)
        {
            int bits = 0;
            int boneTransformHash = GetHash(spriteSkin.transform.localToWorldMatrix) >> bits;
            bits++;
            foreach (Transform transform in spriteSkin.boneTransforms)
            {
                boneTransformHash ^= GetHash(transform.localToWorldMatrix) >> bits;
                bits = (bits + 1) % 8;
            }

            return boneTransformHash;
        }

        internal unsafe static void Deform(Sprite sprite, Matrix4x4 rootInv, NativeSlice<Vector3> vertices, NativeSlice<Vector4> tangents, NativeSlice<BoneWeight> boneWeights, NativeArray<Matrix4x4> boneTransforms, NativeSlice<Matrix4x4> bindPoses, NativeArray<byte> deformableVertices)
        {
            NativeSlice<float3> verticesFloat3 = vertices.SliceWithStride<float3>();
            NativeSlice<float4> tangentsFloat4 = tangents.SliceWithStride<float4>();
            NativeSlice<float4x4> bindPosesFloat4x4 = bindPoses.SliceWithStride<float4x4>();
            int spriteVertexCount = sprite.GetVertexCount();
            int spriteVertexStreamSize = sprite.GetVertexStreamSize();
            NativeArray<float4x4> boneTransformsFloat4x4 = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<float4x4>(boneTransforms.GetUnsafePtr(), boneTransforms.Length, Allocator.None);

            byte* deformedPosOffset = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(deformableVertices);
            NativeSlice<float3> deformableVerticesFloat3 = NativeSliceUnsafeUtility.ConvertExistingDataToNativeSlice<float3>(deformedPosOffset, spriteVertexStreamSize, spriteVertexCount);
            NativeSlice<float4> deformableTangentsFloat4 = NativeSliceUnsafeUtility.ConvertExistingDataToNativeSlice<float4>(deformedPosOffset, spriteVertexStreamSize, 1); // Just Dummy.
            if (sprite.HasVertexAttribute(Rendering.VertexAttribute.Tangent))
            {
                byte* deformedTanOffset = deformedPosOffset + sprite.GetVertexStreamOffset(Rendering.VertexAttribute.Tangent);
                deformableTangentsFloat4 = NativeSliceUnsafeUtility.ConvertExistingDataToNativeSlice<float4>(deformedTanOffset, spriteVertexStreamSize, spriteVertexCount);
            }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle handle1 = CreateSafetyChecks<float4x4>(ref boneTransformsFloat4x4);
            AtomicSafetyHandle handle2 = CreateSafetyChecks<float3>(ref deformableVerticesFloat3);
            AtomicSafetyHandle handle3 = CreateSafetyChecks<float4>(ref deformableTangentsFloat4);
#endif

            if (sprite.HasVertexAttribute(Rendering.VertexAttribute.Tangent))
                Deform(rootInv, verticesFloat3, tangentsFloat4, boneWeights, boneTransformsFloat4x4, bindPosesFloat4x4, deformableVerticesFloat3, deformableTangentsFloat4);
            else
                Deform(rootInv, verticesFloat3, boneWeights, boneTransformsFloat4x4, bindPosesFloat4x4, deformableVerticesFloat3);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            DisposeSafetyChecks(handle1);
            DisposeSafetyChecks(handle2);
            DisposeSafetyChecks(handle3);
#endif
        }

        internal static void Deform(float4x4 rootInv, NativeSlice<float3> vertices, NativeSlice<BoneWeight> boneWeights, NativeArray<float4x4> boneTransforms, NativeSlice<float4x4> bindPoses, NativeSlice<float3> deformed)
        {
            if (boneTransforms.Length == 0)
                return;

            for (int i = 0; i < boneTransforms.Length; i++)
            {
                float4x4 bindPoseMat = bindPoses[i];
                float4x4 boneTransformMat = boneTransforms[i];
                boneTransforms[i] = math.mul(rootInv, math.mul(boneTransformMat, bindPoseMat));
            }

            for (int i = 0; i < vertices.Length; i++)
            {
                int bone0 = boneWeights[i].boneIndex0;
                int bone1 = boneWeights[i].boneIndex1;
                int bone2 = boneWeights[i].boneIndex2;
                int bone3 = boneWeights[i].boneIndex3;

                float3 vertex = vertices[i];
                deformed[i] =
                    math.transform(boneTransforms[bone0], vertex) * boneWeights[i].weight0 +
                    math.transform(boneTransforms[bone1], vertex) * boneWeights[i].weight1 +
                    math.transform(boneTransforms[bone2], vertex) * boneWeights[i].weight2 +
                    math.transform(boneTransforms[bone3], vertex) * boneWeights[i].weight3;
            }
        }

        internal static void Deform(float4x4 rootInv, NativeSlice<float3> vertices, NativeSlice<float4> tangents, NativeSlice<BoneWeight> boneWeights, NativeArray<float4x4> boneTransforms, NativeSlice<float4x4> bindPoses, NativeSlice<float3> deformed, NativeSlice<float4> deformedTangents)
        {
            if (boneTransforms.Length == 0)
                return;

            for (int i = 0; i < boneTransforms.Length; i++)
            {
                float4x4 bindPoseMat = bindPoses[i];
                float4x4 boneTransformMat = boneTransforms[i];
                boneTransforms[i] = math.mul(rootInv, math.mul(boneTransformMat, bindPoseMat));
            }

            for (int i = 0; i < vertices.Length; i++)
            {
                int bone0 = boneWeights[i].boneIndex0;
                int bone1 = boneWeights[i].boneIndex1;
                int bone2 = boneWeights[i].boneIndex2;
                int bone3 = boneWeights[i].boneIndex3;

                float3 vertex = vertices[i];
                deformed[i] =
                    math.transform(boneTransforms[bone0], vertex) * boneWeights[i].weight0 +
                    math.transform(boneTransforms[bone1], vertex) * boneWeights[i].weight1 +
                    math.transform(boneTransforms[bone2], vertex) * boneWeights[i].weight2 +
                    math.transform(boneTransforms[bone3], vertex) * boneWeights[i].weight3;

                float4 tangent = new float4(tangents[i].xyz, 0.0f);

                tangent =
                    math.mul(boneTransforms[bone0], tangent) * boneWeights[i].weight0 +
                    math.mul(boneTransforms[bone1], tangent) * boneWeights[i].weight1 +
                    math.mul(boneTransforms[bone2], tangent) * boneWeights[i].weight2 +
                    math.mul(boneTransforms[bone3], tangent) * boneWeights[i].weight3;

                deformedTangents[i] = new float4(math.normalize(tangent.xyz), tangents[i].w);
            }
        }

        internal static void Deform(Sprite sprite, Matrix4x4 invRoot, Transform[] boneTransformsArray, NativeArray<byte> deformVertexData)
        {
            Debug.Assert(sprite != null);
            Debug.Assert(sprite.GetVertexCount() == (deformVertexData.Length / sprite.GetVertexStreamSize()));

            NativeSlice<Vector3> vertices = sprite.GetVertexAttribute<Vector3>(UnityEngine.Rendering.VertexAttribute.Position);
            NativeSlice<Vector4> tangents = sprite.GetVertexAttribute<Vector4>(UnityEngine.Rendering.VertexAttribute.Tangent);
            NativeSlice<BoneWeight> boneWeights = sprite.GetVertexAttribute<BoneWeight>(UnityEngine.Rendering.VertexAttribute.BlendWeight);
            NativeArray<Matrix4x4> bindPoses = sprite.GetBindPoses();

            Debug.Assert(bindPoses.Length == boneTransformsArray.Length);
            Debug.Assert(boneWeights.Length == sprite.GetVertexCount());

            NativeArray<Matrix4x4> boneTransforms = new NativeArray<Matrix4x4>(boneTransformsArray.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

            for (int i = 0; i < boneTransformsArray.Length; ++i)
                boneTransforms[i] = boneTransformsArray[i].localToWorldMatrix;

            Deform(sprite, invRoot, vertices, tangents, boneWeights, boneTransforms, bindPoses, deformVertexData);

            boneTransforms.Dispose();
        }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        static AtomicSafetyHandle CreateSafetyChecks<T>(ref NativeArray<T> array) where T : struct
        {
            AtomicSafetyHandle handle = AtomicSafetyHandle.Create();
            AtomicSafetyHandle.SetAllowSecondaryVersionWriting(handle, true);
            AtomicSafetyHandle.UseSecondaryVersion(ref handle);
            NativeArrayUnsafeUtility.SetAtomicSafetyHandle<T>(ref array, handle);
            return handle;
        }

        static AtomicSafetyHandle CreateSafetyChecks<T>(ref NativeSlice<T> array) where T : struct
        {
            AtomicSafetyHandle handle = AtomicSafetyHandle.Create();
            AtomicSafetyHandle.SetAllowSecondaryVersionWriting(handle, true);
            AtomicSafetyHandle.UseSecondaryVersion(ref handle);
            NativeSliceUnsafeUtility.SetAtomicSafetyHandle<T>(ref array, handle);
            return handle;
        }

        static void DisposeSafetyChecks(AtomicSafetyHandle handle)
        {
            AtomicSafetyHandle.Release(handle);
        }
#endif

        internal static void Bake(this SpriteSkin spriteSkin, NativeArray<byte> deformVertexData)
        {
            if (!spriteSkin.isValid)
                throw new Exception("Bake error: invalid SpriteSkin");

            Sprite sprite = spriteSkin.spriteRenderer.sprite;
            Transform[] boneTransformsArray = spriteSkin.boneTransforms;
            Deform(sprite, Matrix4x4.identity, boneTransformsArray, deformVertexData);
        }

        internal static unsafe void CalculateBounds(this SpriteSkin spriteSkin)
        {
            Debug.Assert(spriteSkin.isValid);
            Sprite sprite = spriteSkin.sprite;

            NativeArray<byte> deformVertexData = new NativeArray<byte>(sprite.GetVertexStreamSize() * sprite.GetVertexCount(), Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            void* dataPtr = deformVertexData.GetUnsafePtr();
            NativeSlice<Vector3> deformedPosSlice = NativeSliceUnsafeUtility.ConvertExistingDataToNativeSlice<Vector3>(dataPtr, sprite.GetVertexStreamSize(), sprite.GetVertexCount());
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            NativeSliceUnsafeUtility.SetAtomicSafetyHandle(ref deformedPosSlice, NativeArrayUnsafeUtility.GetAtomicSafetyHandle(deformVertexData));
#endif

            spriteSkin.Bake(deformVertexData);
            UpdateBounds(spriteSkin, deformVertexData);
            deformVertexData.Dispose();
        }

        internal static Bounds CalculateSpriteSkinBounds(NativeSlice<float3> deformablePositions)
        {
            float3 min = deformablePositions[0];
            float3 max = deformablePositions[0];

            for (int j = 1; j < deformablePositions.Length; ++j)
            {
                min = math.min(min, deformablePositions[j]);
                max = math.max(max, deformablePositions[j]);
            }

            float3 ext = (max - min) * 0.5F;
            float3 ctr = min + ext;
            Bounds bounds = new Bounds();
            bounds.center = ctr;
            bounds.extents = ext;
            return bounds;
        }

        internal static unsafe void UpdateBounds(this SpriteSkin spriteSkin, NativeArray<byte> deformedVertices)
        {
            byte* deformedPosOffset = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(deformedVertices);
            int spriteVertexCount = spriteSkin.sprite.GetVertexCount();
            int spriteVertexStreamSize = spriteSkin.sprite.GetVertexStreamSize();
            NativeSlice<float3> deformedPositions = NativeSliceUnsafeUtility.ConvertExistingDataToNativeSlice<float3>(deformedPosOffset, spriteVertexStreamSize, spriteVertexCount);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle handle = CreateSafetyChecks<float3>(ref deformedPositions);
#endif
            spriteSkin.bounds = CalculateSpriteSkinBounds(deformedPositions);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            DisposeSafetyChecks(handle);
#endif
            InternalEngineBridge.SetLocalAABB(spriteSkin.spriteRenderer, spriteSkin.bounds);
        }
    }

    [BurstCompile]
    internal static class BurstedSpriteSkinUtilities
    {
        [BurstCompile]
        internal static bool ValidateBoneWeights(in NativeCustomSlice<BoneWeight> boneWeights, int bindPoseCount)
        {
            int boneWeightCount = boneWeights.Length;
            for (int i = 0; i < boneWeightCount; ++i)
            {
                BoneWeight boneWeight = boneWeights[i];
                int idx0 = boneWeight.boneIndex0;
                int idx1 = boneWeight.boneIndex1;
                int idx2 = boneWeight.boneIndex2;
                int idx3 = boneWeight.boneIndex3;

                if ((idx0 < 0 || idx0 >= bindPoseCount) ||
                    (idx1 < 0 || idx1 >= bindPoseCount) ||
                    (idx2 < 0 || idx2 >= bindPoseCount) ||
                    (idx3 < 0 || idx3 >= bindPoseCount))
                    return false;
            }

            return true;
        }

        [BurstCompile]
        internal static void SetVertexPositionFromByteBuffer(in NativeArray<byte> buffer, in NativeArray<int> indices, ref NativeArray<Vector3> vertices, int stride)
        {
            unsafe
            {
                byte* bufferPtr = (byte*)buffer.GetUnsafeReadOnlyPtr();
                for (int i = 0; i < indices.Length; ++i)
                {
                    int index = indices[i];
                    Vector3* vertexPtr = (Vector3*)(bufferPtr + (index * stride));
                    vertices[index] = *vertexPtr;
                }
            }
        }
    }
}
