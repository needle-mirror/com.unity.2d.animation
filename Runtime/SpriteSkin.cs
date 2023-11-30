#pragma warning disable 0168 // variable declared but not used.

using System;
using System.Collections.Generic;
using UnityEngine.Scripting;
using UnityEngine.U2D.Common;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Profiling;
using UnityEngine.Rendering;
using UnityEngine.Scripting.APIUpdating;

namespace UnityEngine.U2D.Animation
{
    /// <summary>
    /// Represents vertex position.
    /// </summary>
    internal struct PositionVertex
    {
        /// <summary>
        /// Vertex position.
        /// </summary>
        public Vector3 position;
    }

    /// <summary>
    /// Represents vertex position and tangent.
    /// </summary>
    internal struct PositionTangentVertex
    {
        /// <summary>
        /// Vertex position.
        /// </summary>
        public Vector3 position;
        
        /// <summary>
        /// Vertex tangent.
        /// </summary>
        public Vector4 tangent;
    }

    /// <summary>
    /// The state of the Sprite Skin.
    /// </summary>
    public enum SpriteSkinState
    {
        /// <summary>
        /// Sprite Renderer doesn't contain a sprite.
        /// </summary>
        SpriteNotFound,

        /// <summary>
        /// Sprite referenced in the Sprite Renderer doesn't have skinning information.
        /// </summary>
        SpriteHasNoSkinningInformation,

        /// <summary>
        /// Sprite referenced in the Sprite Renderer doesn't have weights.
        /// </summary>
        SpriteHasNoWeights,

        /// <summary>
        /// Root transform is not assigned.
        /// </summary>
        RootTransformNotFound,

        /// <summary>
        /// Bone transform array is not assigned.
        /// </summary>
        InvalidTransformArray,

        /// <summary>
        /// Bone transform array has incorrect length.
        /// </summary>
        InvalidTransformArrayLength,

        /// <summary>
        /// One or more bone transforms is not assigned.
        /// </summary>
        TransformArrayContainsNull,

        /// <summary>
        /// Bone weights are invalid.
        /// </summary>
        InvalidBoneWeights,

        /// <summary>
        /// Sprite Skin is ready for deformation.
        /// </summary>
        Ready
    }

    /// <summary>
    /// Deforms the Sprite that is currently assigned to the SpriteRenderer in the same GameObject.
    /// </summary>
    [Preserve]
    [ExecuteInEditMode]
    [DefaultExecutionOrder(UpdateOrder.spriteSkinUpdateOrder)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    [AddComponentMenu("2D Animation/Sprite Skin")]
    [IconAttribute(IconUtility.IconPath + "Animation.SpriteSkin.png")]
    [MovedFrom("UnityEngine.U2D.Experimental.Animation")]
    [HelpURL("https://docs.unity3d.com/Packages/com.unity.2d.animation@latest/index.html?subfolder=/manual/SpriteSkin.html")]
    public sealed class SpriteSkin : MonoBehaviour, IPreviewable, ISerializationCallbackReceiver
    {
        static class Profiling
        {
            public static readonly ProfilerMarker cacheCurrentSprite = new ProfilerMarker("SpriteSkin.CacheCurrentSprite");
            public static readonly ProfilerMarker cacheHierarchy = new ProfilerMarker("SpriteSkin.CacheHierarchy");
            public static readonly ProfilerMarker getSpriteBonesTransformFromGuid = new ProfilerMarker("SpriteSkin.GetSpriteBoneTransformsFromGuid");
            public static readonly ProfilerMarker getSpriteBonesTransformFromPath = new ProfilerMarker("SpriteSkin.GetSpriteBoneTransformsFromPath");
        }
        
        struct TransformData
        {
            public string fullName;
            public Transform transform;
        }

        [SerializeField]
        Transform m_RootBone;
        [SerializeField]
        Transform[] m_BoneTransforms = new Transform[0];
        [SerializeField]
        Bounds m_Bounds;
        [SerializeField] 
        bool m_AlwaysUpdate = true;
        [SerializeField] 
        bool m_AutoRebind = false;

        // The deformed m_SpriteVertices stores all 'HOT' channels only in single-stream and essentially depends on Sprite Asset data.
        // The order of storage if present is POSITION, NORMALS, TANGENTS.
        NativeByteArray m_DeformedVertices;
        int m_CurrentDeformVerticesLength = 0;
        SpriteRenderer m_SpriteRenderer;
        int m_CurrentDeformSprite = 0;
        bool m_IsValid = false;
        SpriteSkinState m_State;
        int m_TransformsHash = 0;
        bool m_ForceCpuDeformation = false;

        int m_TransformId;
        NativeArray<int> m_BoneTransformId;
        int m_RootBoneTransformId;
        NativeCustomSlice<Vector2> m_SpriteUVs;
        NativeCustomSlice<Vector3> m_SpriteVertices;
        NativeCustomSlice<Vector4> m_SpriteTangents;
        NativeCustomSlice<BoneWeight> m_SpriteBoneWeights;
        NativeCustomSlice<Matrix4x4> m_SpriteBindPoses;
        NativeCustomSlice<int> m_BoneTransformIdNativeSlice;
        bool m_SpriteHasTangents;
        int m_SpriteVertexStreamSize;
        int m_SpriteVertexCount;
        int m_SpriteTangentVertexOffset;
        int m_DataIndex = -1;
        bool m_BoneCacheUpdateToDate = false;
        
        Dictionary<int, List<TransformData>> m_HierarchyCache = new Dictionary<int, List<TransformData>>();

        NativeArray<int> m_OutlineIndexCache;
        NativeArray<Vector3> m_StaticOutlineVertexCache;
        NativeArray<Vector3> m_DeformedOutlineVertexCache;
        int m_VertexDeformationHash = 0;

        internal NativeArray<int> boneTransformId => m_BoneTransformId;
        internal int rootBoneTransformId => m_RootBoneTransformId; 
        internal DeformationMethods currentDeformationMethod { get; set; }

#if ENABLE_URP
        /// <summary>
        /// Returns an array of the outline indices.
        /// The indices are sorted and laid out with line topology.
        /// </summary>
        internal NativeArray<int> outlineIndices => m_OutlineIndexCache;
        /// <summary>
        /// Returns an array of the deformed outline vertices.
        /// </summary>
        internal NativeArray<Vector3> outlineVertices => m_DeformedOutlineVertexCache;
#endif
        
        /// <summary>
        /// Returns a hash which is updated every time the mesh is deformed. 
        /// </summary>
        internal int vertexDeformationHash => m_VertexDeformationHash;
        
        internal Sprite sprite => spriteRenderer.sprite;
        internal SpriteRenderer spriteRenderer => m_SpriteRenderer;
        internal NativeCustomSlice<BoneWeight> spriteBoneWeights => m_SpriteBoneWeights;

        /// <summary>
        /// Get and set the Auto Rebind property.
        /// When enabled, Sprite Skin attempts to automatically locate the Transform that is needed for the current Sprite assigned to the Sprite Renderer.
        /// </summary>
        public bool autoRebind
        {
            get => m_AutoRebind;
            set
            {
                m_AutoRebind = value;
                CacheHierarchy();
                CacheCurrentSprite(m_AutoRebind);
            }
        }

        /// <summary>
        /// Returns the Transform Components that are used for deformation.
        /// Do not modify elements of the returned array.
        /// </summary>
        /// <returns>An array of Transform Components.</returns>
        public Transform[] boneTransforms => m_BoneTransforms;

        /// <summary>
        /// Sets the Transform Components that are used for deformation.
        /// </summary>
        /// <param name="boneTransformsArray">Array of new bone Transforms.</param>
        /// <returns>The state of the Sprite Skin.</returns>
        public SpriteSkinState SetBoneTransforms(Transform[] boneTransformsArray)
        {
            m_BoneTransforms = boneTransformsArray;

            var result = CacheValidFlag();
            OnBoneTransformChanged();

            return result;
        }

        /// <summary>
        /// Returns the Transform Component that represents the root bone for deformation.
        /// </summary>
        /// <returns>A Transform Component.</returns>
        public Transform rootBone => m_RootBone;

        /// <summary>
        /// Sets the Transform Component that represents the root bone for deformation.
        /// </summary>
        /// <param name="rootBoneTransform">Root bone Transform Component.</param>
        /// <returns>The state of the Sprite Skin.</returns>
        public SpriteSkinState SetRootBone(Transform rootBoneTransform)
        {
            m_RootBone = rootBoneTransform;

            var result = CacheValidFlag();
            CacheHierarchy();
            OnRootBoneTransformChanged();

            return result;
        }

        internal Bounds bounds
        {
            get => m_Bounds;
            set => m_Bounds = value;
        }

        /// <summary>
        /// Determines if the SpriteSkin executes even if the associated
        /// SpriteRenderer has been culled from view.
        /// </summary>
        public bool alwaysUpdate
        {
            get => m_AlwaysUpdate;
            set => m_AlwaysUpdate = value;
        }

        /// <summary>
        /// Always run a deformation pass on the CPU.<br/>
        /// If GPU deformation is enabled, enabling forceCpuDeformation will cause the deformation to run twice, one time on the CPU and one time on the GPU.
        /// </summary>
        public bool forceCpuDeformation
        {
            get => m_ForceCpuDeformation;
            set
            {
                if (m_ForceCpuDeformation == value)
                    return;
                m_ForceCpuDeformation = value;
                
                UpdateSpriteDeformationData();
                DeformationManager.instance.CopyToSpriteSkinData(this);
            }
        }

        /// <summary>
        /// Resets the bone transforms to the bind pose.
        /// </summary>
        /// <returns>True if successful.</returns>
        public bool ResetBindPose()
        {
            if (!isValid)
                return false;

            var spriteBones = spriteRenderer.sprite.GetBones();
            for (var i = 0; i < boneTransforms.Length; ++i)
            {
                var boneTransform = boneTransforms[i];
                var spriteBone = spriteBones[i];

                if (spriteBone.parentId != -1)
                {
                    boneTransform.localPosition = spriteBone.position;
                    boneTransform.localRotation = spriteBone.rotation;
                    boneTransform.localScale = Vector3.one;
                }
            }

            return true;
        }

        internal bool isValid => this.Validate() == SpriteSkinState.Ready;

#if UNITY_EDITOR
        internal static Events.UnityEvent onDrawGizmos = new Events.UnityEvent();
        void OnDrawGizmos() 
        { onDrawGizmos.Invoke(); }

        internal bool ignoreNextSpriteChange
        {
            get; 
            set;
        } = true;
#endif

        int GetSpriteInstanceID()
        {
            return sprite != null ? sprite.GetInstanceID() : 0;
        }

        internal void Awake()
        {
            m_SpriteRenderer = GetComponent<SpriteRenderer>();
        }

        void OnEnable()
        {
            m_TransformId = gameObject.transform.GetInstanceID();
            m_TransformsHash = 0;
            currentDeformationMethod = SpriteSkinUtility.CanSpriteSkinUseGpuDeformation(this) ? DeformationMethods.Gpu : DeformationMethods.Cpu;
            
            Awake();
            
            CacheCurrentSprite(false);
            UpdateSpriteDeformationData();
            
            if (m_HierarchyCache.Count == 0)
                CacheHierarchy();
            
            CacheBoneTransformIds();
            
            DeformationManager.instance.AddSpriteSkin(this);
            SpriteSkinContainer.instance.AddSpriteSkin(this);
        }
        
        void OnDisable()
        {
            DeactivateSkinning();
            BufferManager.instance.ReturnBuffer(GetInstanceID());
            DeformationManager.instance.RemoveSpriteSkin(this);
            SpriteSkinContainer.instance.RemoveSpriteSkin(this);
            ResetBoneTransformIdCache();
            DisposeOutlineCaches();
        }

        void OnBoneTransformChanged()
        {
            if (enabled)
            {
                RemoveBoneTransformIdsFromDeformationManager();
                CacheBoneTransformIds();
                SpriteSkinContainer.instance.BoneTransformsChanged(this);
            }
        }

        void OnRootBoneTransformChanged()
        {
            if (enabled)
            {
                RemoveBoneTransformIdsFromDeformationManager();
                CacheBoneTransformIds();
                SpriteSkinContainer.instance.BoneTransformsChanged(this);
            }
        }

        /// <summary>
        /// Called before object is serialized.
        /// </summary>
        public void OnBeforeSerialize()
        {
            OnBeforeSerializeBatch();
        }

        /// <summary>
        /// Called after object is deserialized.
        /// </summary>
        public void OnAfterDeserialize()
        {
            OnAfterSerializeBatch();
        }     
        
        void OnBeforeSerializeBatch() {}

        void OnAfterSerializeBatch()
        {
#if UNITY_EDITOR
            m_BoneCacheUpdateToDate = false;
#endif
        }           

        internal void OnEditorEnable()
        {
            Awake();
        }

        SpriteSkinState CacheValidFlag()
        {
            m_State = this.Validate();
            m_IsValid = m_State == SpriteSkinState.Ready;
            if(!m_IsValid)
                DeactivateSkinning();

            return m_State;
        }
        
        internal bool BatchValidate()
        {
            if (!m_BoneCacheUpdateToDate)
            {
                RemoveBoneTransformIdsFromDeformationManager();
                CacheBoneTransformIds();
            }

            CacheCurrentSprite(m_AutoRebind);
            var hasSprite = m_CurrentDeformSprite != 0;
            return (m_IsValid && hasSprite && spriteRenderer.enabled && (alwaysUpdate || spriteRenderer.isVisible));
        }

        void Reset()
        {
            Awake();
            if (isActiveAndEnabled)
            {
                CacheValidFlag();
                
                if (!m_BoneCacheUpdateToDate)
                {
                    RemoveBoneTransformIdsFromDeformationManager();
                    CacheBoneTransformIds();
                }

                DeformationManager.instance.CopyToSpriteSkinData(this);
            }
        }
        
        void CacheBoneTransformIds()
        {
            m_BoneCacheUpdateToDate = true;
            
            var boneCount = 0;
            for (var i = 0; i < boneTransforms?.Length; ++i)
            {
                if (boneTransforms[i] != null)
                    ++boneCount;
            }
            
            if (m_BoneTransformId != default && m_BoneTransformId.IsCreated)
                NativeArrayHelpers.ResizeIfNeeded(ref m_BoneTransformId, boneCount);
            else
                m_BoneTransformId = new NativeArray<int>(boneCount, Allocator.Persistent);            
            
            m_RootBoneTransformId = rootBone != null ? rootBone.GetInstanceID() : 0;
            m_BoneTransformIdNativeSlice = new NativeCustomSlice<int>(m_BoneTransformId);
            for (int i = 0, j = 0; i < boneTransforms?.Length; ++i)
            {
                if (boneTransforms[i] != null)
                {
                    m_BoneTransformId[j] = boneTransforms[i].GetInstanceID();
                    ++j;
                }
            }
            
            if (enabled)
            {
                DeformationManager.instance.AddSpriteSkinRootBoneTransform(this);
                DeformationManager.instance.AddSpriteSkinBoneTransform(this);
            }

            CacheValidFlag();
            
            DeformationManager.instance.CopyToSpriteSkinData(this);
        }
        
        void RemoveBoneTransformIdsFromDeformationManager()
        {
            DeformationManager.instance.RemoveBoneTransforms(this);
            ResetBoneTransformIdCache();
        }

        void ResetBoneTransformIdCache()
        {
            m_BoneTransformId.DisposeIfCreated();
            m_BoneTransformId = default;
            
            m_RootBoneTransformId = -1;
            m_BoneCacheUpdateToDate = false;            
        }

        internal NativeByteArray GetDeformedVertices(int spriteVertexCount)
        {
            if (sprite != null)
            {
                if (m_CurrentDeformVerticesLength != spriteVertexCount)
                {
                    m_TransformsHash = 0;
                    m_CurrentDeformVerticesLength = spriteVertexCount;
                }
            }
            else
            {
                m_CurrentDeformVerticesLength = 0;
            }
            
            m_DeformedVertices = BufferManager.instance.GetBuffer(GetInstanceID(), m_CurrentDeformVerticesLength);
            return m_DeformedVertices;
        }

        /// <summary>
        /// Returns whether this SpriteSkin has currently deformed vertices.
        /// </summary>
        /// <returns>Returns true if this SpriteSkin has currently deformed vertices. Returns false otherwise.</returns>
        public bool HasCurrentDeformedVertices()
        {
            if (!m_IsValid)
                return false;
            
            return m_DataIndex >= 0 && DeformationManager.instance.IsSpriteSkinActiveForDeformation(this);
        }

        /// <summary>
        /// Gets a byte array to the currently deformed vertices for this SpriteSkin. 
        /// </summary>
        /// <returns>Returns a reference to the currently deformed vertices. This is valid only for this calling frame.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when there are no currently deformed vertices.
        /// HasCurrentDeformedVertices can be used to verify if there are any deformed vertices available.
        /// </exception>
        internal NativeArray<byte> GetCurrentDeformedVertices()
        {
            if (!m_IsValid)
                throw new InvalidOperationException("The SpriteSkin deformation is not valid.");
            if (m_DataIndex < 0)
                throw new InvalidOperationException("There are no currently deformed vertices.");
            
            var buffer = DeformationManager.instance.GetDeformableBufferForSpriteSkin(this);
            if (buffer == default)
                throw new InvalidOperationException("There are no currently deformed vertices.");
            
            return buffer;
        }

        /// <summary>
        /// Gets an array of currently deformed position vertices for this SpriteSkin.
        /// </summary>
        /// <returns>Returns a reference to the currently deformed vertices. This is valid only for this calling frame.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when there are no currently deformed vertices or if the deformed vertices does not contain only
        /// position data. HasCurrentDeformedVertices can be used to verify if there are any deformed vertices available.
        /// </exception>
        internal NativeSlice<PositionVertex> GetCurrentDeformedVertexPositions()
        {
            if (!m_IsValid)
                throw new InvalidOperationException("The SpriteSkin deformation is not valid.");
            
            if (sprite.HasVertexAttribute(VertexAttribute.Tangent))
                throw new InvalidOperationException("This SpriteSkin has deformed tangents");
            if (!sprite.HasVertexAttribute(VertexAttribute.Position))
                throw new InvalidOperationException("This SpriteSkin does not have deformed positions.");

            var deformedBuffer = GetCurrentDeformedVertices();
            return deformedBuffer.Slice().SliceConvert<PositionVertex>();
        }

        /// <summary>
        /// Gets an array of currently deformed position and tangent vertices for this SpriteSkin.
        /// </summary>
        /// <returns>
        /// Returns a reference to the currently deformed position and tangent vertices. This is valid only for this calling frame.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when there are no currently deformed vertices or if the deformed vertices does not contain only
        /// position and tangent data. HasCurrentDeformedVertices can be used to verify if there are any deformed vertices available.
        /// </exception>
        internal NativeSlice<PositionTangentVertex> GetCurrentDeformedVertexPositionsAndTangents()
        {
            if (!m_IsValid)
                throw new InvalidOperationException("The SpriteSkin deformation is not valid.");
            
            if (!sprite.HasVertexAttribute(VertexAttribute.Tangent))
                throw new InvalidOperationException("This SpriteSkin does not have deformed tangents");
            if (!sprite.HasVertexAttribute(VertexAttribute.Position))
                throw new InvalidOperationException("This SpriteSkin does not have deformed positions.");

            var deformedBuffer = GetCurrentDeformedVertices();
            return deformedBuffer.Slice().SliceConvert<PositionTangentVertex>();
        }

        /// <summary>
        /// Gets an enumerable to iterate through all deformed vertex positions of this SpriteSkin.
        /// </summary>
        /// <returns>Returns an IEnumerable to deformed vertex positions.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when there is no vertex positions or deformed vertices.
        /// HasCurrentDeformedVertices can be used to verify if there are any deformed vertices available.
        /// </exception>
        public IEnumerable<Vector3> GetDeformedVertexPositionData()
        {
            if (!m_IsValid)
                throw new InvalidOperationException("The SpriteSkin deformation is not valid.");
            
            var hasPosition = sprite.HasVertexAttribute(VertexAttribute.Position);
            if (!hasPosition)
                throw new InvalidOperationException("Sprite does not have vertex position data.");

            var rawBuffer = GetCurrentDeformedVertices();
            var rawSlice = rawBuffer.Slice(sprite.GetVertexStreamOffset(VertexAttribute.Position));
            return new NativeCustomSliceEnumerator<Vector3>(rawSlice, m_SpriteVertexCount, m_SpriteVertexStreamSize);
        }

        /// <summary>
        /// Gets an enumerable to iterate through all deformed vertex tangents of this SpriteSkin. 
        /// </summary>
        /// <returns>Returns an IEnumerable to deformed vertex tangents.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when there is no vertex tangents or deformed vertices.
        /// HasCurrentDeformedVertices can be used to verify if there are any deformed vertices available.
        /// </exception>
        public IEnumerable<Vector4> GetDeformedVertexTangentData()
        {
            if (!m_IsValid)
                throw new InvalidOperationException("The SpriteSkin deformation is not valid.");
            
            var hasTangent = sprite.HasVertexAttribute(VertexAttribute.Tangent);
            if (!hasTangent)
                throw new InvalidOperationException("Sprite does not have vertex tangent data.");

            var rawBuffer = GetCurrentDeformedVertices();
            var rawSlice = rawBuffer.Slice(sprite.GetVertexStreamOffset(VertexAttribute.Tangent));
            return new NativeCustomSliceEnumerator<Vector4>(rawSlice, m_SpriteVertexCount, m_SpriteVertexStreamSize);
        }

        void DisposeOutlineCaches()
        {
            m_OutlineIndexCache.DisposeIfCreated();
            m_StaticOutlineVertexCache.DisposeIfCreated();
            m_DeformedOutlineVertexCache.DisposeIfCreated();

            m_OutlineIndexCache = default;
            m_StaticOutlineVertexCache = default;
            m_DeformedOutlineVertexCache = default;
        }

        /// <summary>
        /// Used by the animation clip preview window.
        /// Recommended to not use outside of this purpose.
        /// </summary>
        public void OnPreviewUpdate()
        {
#if UNITY_EDITOR 
            if(IsInGUIUpdateLoop())
                Deform();
#endif
        }

        static bool IsInGUIUpdateLoop() => Event.current != null;

        void Deform()
        {
            CacheCurrentSprite(m_AutoRebind);
            if (isValid && this.enabled && (this.alwaysUpdate || this.spriteRenderer.isVisible))
            {
                var transformHash = SpriteSkinUtility.CalculateTransformHash(this);
                var spriteVertexCount = sprite.GetVertexStreamSize() * sprite.GetVertexCount();
                if (spriteVertexCount > 0 && m_TransformsHash != transformHash)
                {
                    var inputVertices = GetDeformedVertices(spriteVertexCount);
                    SpriteSkinUtility.Deform(sprite, gameObject.transform.worldToLocalMatrix, boneTransforms, inputVertices.array);
                    SpriteSkinUtility.UpdateBounds(this, inputVertices.array);
                    InternalEngineBridge.SetDeformableBuffer(spriteRenderer, inputVertices.array);
                    m_TransformsHash = transformHash;
                    m_CurrentDeformSprite = GetSpriteInstanceID();

                    PostDeform(true);
                }
            }
            else if(!InternalEngineBridge.IsUsingDeformableBuffer(spriteRenderer, IntPtr.Zero))
            {
                DeactivateSkinning();
            }
        }

        internal void PostDeform(bool didDeform)
        {
            if (didDeform)
            {
                UpdateDeformedOutlineCache();
                m_VertexDeformationHash = Time.timeSinceLevelLoad.GetHashCode();
            }
        }
        
        void UpdateDeformedOutlineCache()
        {
#if ENABLE_URP            
            if (sprite == null)
                return;
            if (!m_OutlineIndexCache.IsCreated || !m_DeformedOutlineVertexCache.IsCreated)
                return;
            if (!HasCurrentDeformedVertices())
                return;
            
            var buffer = GetCurrentDeformedVertices();
            var indexCache = m_OutlineIndexCache;
            var vertexCache = m_DeformedOutlineVertexCache;
            BurstedSpriteSkinUtilities.SetVertexPositionFromByteBuffer(in buffer, in indexCache, ref vertexCache, m_SpriteVertexStreamSize);
            m_DeformedOutlineVertexCache = vertexCache;
#endif
        }          

        void CacheCurrentSprite(bool rebind)
        {
            if (m_CurrentDeformSprite == GetSpriteInstanceID()) 
                return;
            
            using (Profiling.cacheCurrentSprite.Auto())
            {
                DeactivateSkinning();
                m_CurrentDeformSprite = GetSpriteInstanceID();
                if (rebind && m_CurrentDeformSprite > 0 && rootBone != null)
                {
                    if (!GetSpriteBonesTransforms(this, out var transforms))
                        Debug.LogWarning($"Rebind failed for {name}. Could not find all bones required by the Sprite: {sprite.name}.");
                    SetBoneTransforms(transforms);
                }
                    
                UpdateSpriteDeformationData();
                DeformationManager.instance.CopyToSpriteSkinData(this);
                    
                CacheValidFlag();
                m_TransformsHash = 0;
            }
        }

        void UpdateSpriteDeformationData()
        {
            CacheSpriteOutline();
            
            if (sprite == null)
            {
                m_SpriteUVs = NativeCustomSlice<Vector2>.Default();
                m_SpriteVertices = NativeCustomSlice<Vector3>.Default();
                m_SpriteTangents = NativeCustomSlice<Vector4>.Default();
                m_SpriteBoneWeights = NativeCustomSlice<BoneWeight>.Default();
                m_SpriteBindPoses = NativeCustomSlice<Matrix4x4>.Default();
                m_SpriteHasTangents = false;
                m_SpriteVertexStreamSize = 0;
                m_SpriteVertexCount = 0;
                m_SpriteTangentVertexOffset = 0;
            }
            else
            {
                var cacheFullMesh = currentDeformationMethod == DeformationMethods.Cpu || forceCpuDeformation;
                if (cacheFullMesh)
                {
                    m_SpriteVertices = new NativeCustomSlice<Vector3>(sprite.GetVertexAttribute<Vector3>(VertexAttribute.Position));
                    m_SpriteVertexCount = sprite.GetVertexCount();
                    m_SpriteVertexStreamSize = sprite.GetVertexStreamSize();
                    
                    m_SpriteTangents = new NativeCustomSlice<Vector4>(sprite.GetVertexAttribute<Vector4>(VertexAttribute.Tangent));
                    m_SpriteHasTangents = sprite.HasVertexAttribute(VertexAttribute.Tangent);
                    m_SpriteTangentVertexOffset = sprite.GetVertexStreamOffset(VertexAttribute.Tangent);   
                }
                else
                {
                    m_SpriteVertices = new NativeCustomSlice<Vector3>(m_StaticOutlineVertexCache);
                    m_SpriteVertexCount = m_SpriteVertices.length;
                    m_SpriteVertexStreamSize = sizeof(float) * 3;

                    m_SpriteTangents = new NativeCustomSlice<Vector4>(sprite.GetVertexAttribute<Vector4>(VertexAttribute.Tangent));
                    m_SpriteHasTangents = false;
                    m_SpriteTangentVertexOffset = 0;                    
                }

                m_SpriteUVs = new NativeCustomSlice<Vector2>(sprite.GetVertexAttribute<Vector2>(VertexAttribute.TexCoord0));
                m_SpriteBoneWeights = new NativeCustomSlice<BoneWeight>(sprite.GetVertexAttribute<BoneWeight>(VertexAttribute.BlendWeight));
                m_SpriteBindPoses = new NativeCustomSlice<Matrix4x4>(sprite.GetBindPoses());
            }
        }  
        
        void CacheSpriteOutline()
        {
#if ENABLE_URP
            DisposeOutlineCaches();

            if (sprite == null)
                return;

            CacheOutlineIndices(out var maxIndex);
            var cacheSize = maxIndex + 1;
            CacheOutlineVertices(cacheSize);
#endif
        }

        void CacheOutlineIndices(out int maxIndex)
        {
            var indices = sprite.GetIndices();
            var edgeNativeArr = MeshUtilities.GetOutlineEdges(in indices);

            m_OutlineIndexCache = new NativeArray<int>(edgeNativeArr.Length * 2, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            maxIndex = 0;
            for (var i = 0; i < edgeNativeArr.Length; ++i)
            {
                var indexX = edgeNativeArr[i].x;
                var indexY = edgeNativeArr[i].y;
                m_OutlineIndexCache[i * 2] = indexX;
                m_OutlineIndexCache[(i * 2) + 1] = indexY;

                if (indexX > maxIndex)
                    maxIndex = indexX;
                if (indexY > maxIndex)
                    maxIndex = indexY;
            }
            edgeNativeArr.Dispose();            
        }

        void CacheOutlineVertices(int cacheSize)
        {
            m_DeformedOutlineVertexCache = new NativeArray<Vector3>(cacheSize, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            m_StaticOutlineVertexCache = new NativeArray<Vector3>(cacheSize, Allocator.Persistent);
                
            var vertices = sprite.GetVertexAttribute<Vector3>(VertexAttribute.Position);
            var vertexCache = m_StaticOutlineVertexCache;
            for (var i = 0; i < m_OutlineIndexCache.Length; ++i)
            {
                var index = m_OutlineIndexCache[i];
                vertexCache[index] = vertices[index];
            }

            m_StaticOutlineVertexCache = vertexCache;            
        }

        internal void CopyToSpriteSkinData(ref SpriteSkinData data, int spriteSkinIndex)
        {
            if (!m_BoneCacheUpdateToDate)
            {
                RemoveBoneTransformIdsFromDeformationManager();
                CacheBoneTransformIds();
            }

            CacheCurrentSprite(m_AutoRebind);

            data.vertices = m_SpriteVertices;
            data.boneWeights = m_SpriteBoneWeights;
            data.bindPoses = m_SpriteBindPoses;
            data.tangents = m_SpriteTangents;
            data.hasTangents = m_SpriteHasTangents;
            data.spriteVertexStreamSize = m_SpriteVertexStreamSize;
            data.spriteVertexCount = m_SpriteVertexCount;
            data.tangentVertexOffset = m_SpriteTangentVertexOffset;
            data.transformId = m_TransformId;
            data.boneTransformId = m_BoneTransformIdNativeSlice;
            m_DataIndex = spriteSkinIndex;
        }

        internal bool NeedToUpdateDeformationCache()
        {
            unsafe
            {
                var iptr = new IntPtr(sprite.GetVertexAttribute<Vector2>(VertexAttribute.TexCoord0).GetUnsafeReadOnlyPtr());
                var rs = m_SpriteUVs.data != iptr;
                if (rs)
                {
                    UpdateSpriteDeformationData();
                    DeformationManager.instance.CopyToSpriteSkinData(this);
                }
                return rs;
            }
        }

        void CacheHierarchy()
        {
            using (Profiling.cacheHierarchy.Auto())
            {
                m_HierarchyCache.Clear();
                if (rootBone == null || !m_AutoRebind)
                    return;
                
                m_HierarchyCache.EnsureCapacity(rootBone.hierarchyCount);
                CacheChildren(rootBone, m_HierarchyCache);

                foreach (var entry in m_HierarchyCache)
                {
                    if (entry.Value.Count == 1)
                        continue;
                    var count = entry.Value.Count;
                    for (var i = 0; i < count; ++i)
                    {
                        var transformEntry = entry.Value[i];
                        transformEntry.fullName = GenerateTransformPath(rootBone, transformEntry.transform);
                        entry.Value[i] = transformEntry;
                    }
                }
            }
        }

        static void CacheChildren(Transform current, Dictionary<int, List<TransformData>> cache)
        {
            var nameHash = current.name.GetHashCode();
            var entry = new TransformData()
            {
                fullName = String.Empty,
                transform = current
            };
            if (cache.ContainsKey(nameHash))
                cache[nameHash].Add(entry);
            else
                cache.Add(nameHash, new List<TransformData>(1) { entry });
            
            for (var i = 0; i < current.childCount; ++i)
                CacheChildren(current.GetChild(i), cache);
        }

        static string GenerateTransformPath(Transform rootBone, Transform child)
        {
            var path = child.name;
            if (child == rootBone)
                return path;
            var parent = child.parent;
            do
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            while (parent != rootBone && parent != null);
            return path;
        }

        internal static bool GetSpriteBonesTransforms(SpriteSkin spriteSkin, out Transform[] outTransform)
        {
            var rootBone = spriteSkin.rootBone;
            var spriteBones = spriteSkin.sprite.GetBones();

            if(rootBone == null)
                throw new ArgumentException("rootBone parameter cannot be null");
            if(spriteBones == null)
                throw new ArgumentException("spriteBones parameter cannot be null");

            outTransform = new Transform[spriteBones.Length];

            var boneObjects = rootBone.GetComponentsInChildren<Bone>();
            if (boneObjects != null && boneObjects.Length >= spriteBones.Length)
            {
                using (Profiling.getSpriteBonesTransformFromGuid.Auto())
                {
                    var i = 0;
                    for (; i < spriteBones.Length; ++i)
                    {
                        var boneHash = spriteBones[i].guid;
                        var boneTransform = Array.Find(boneObjects, x => (x.guid == boneHash));
                        if (boneTransform == null)
                            break;

                        outTransform[i] = boneTransform.transform;
                    }
                    if (i >= spriteBones.Length)
                        return true;
                }
            }
                
            var hierarchyCache = spriteSkin.m_HierarchyCache;
            if (hierarchyCache.Count == 0)
                spriteSkin.CacheHierarchy();
            
            // If unable to successfully map via guid, fall back to path
            return GetSpriteBonesTransformFromPath(spriteBones, hierarchyCache, outTransform);
        }
        
        static bool GetSpriteBonesTransformFromPath(SpriteBone[] spriteBones, Dictionary<int, List<TransformData>> hierarchyCache, Transform[] outNewBoneTransform)
        {
            using (Profiling.getSpriteBonesTransformFromPath.Auto())
            {
                string[] bonePath = null;
                var foundBones = true;
                for (var i = 0; i < spriteBones.Length; ++i)
                {
                    var nameHash = spriteBones[i].name.GetHashCode();
                    if (!hierarchyCache.TryGetValue(nameHash, out var children))
                    {
                        outNewBoneTransform[i] = null;
                        foundBones = false;
                        continue;
                    }
                    
                    if (children.Count == 1)
                        outNewBoneTransform[i] = children[0].transform;
                    else
                    {
                        if (bonePath == null)
                            bonePath = new string[spriteBones.Length];
                        if (bonePath[i] == null)
                            CalculateBoneTransformsPath(i, spriteBones, bonePath);

                        var m = 0;
                        for (; m < children.Count; ++m)
                        {
                            if (children[m].fullName.Contains(bonePath[i]))
                            {
                                outNewBoneTransform[i] = children[m].transform;
                                break;
                            }
                        }
                        if (m >= children.Count)
                        {
                            outNewBoneTransform[i] = null;
                            foundBones = false;
                        }
                    }
                }

                return foundBones;
            }
        }
        
        static void CalculateBoneTransformsPath(int index, SpriteBone[] spriteBones, string[] paths)
        {
            var spriteBone = spriteBones[index];
            var parentId = spriteBone.parentId;
            var bonePath = spriteBone.name;
            if (parentId != -1)
            {
                if (paths[parentId] == null)
                    CalculateBoneTransformsPath(spriteBone.parentId, spriteBones, paths);
                paths[index] = $"{paths[parentId]}/{bonePath}";
            }
            else
                paths[index] = bonePath;
        }

        internal void DeactivateSkinning()
        {
            var currentSprite = spriteRenderer.sprite;
            if (currentSprite != null)
                InternalEngineBridge.SetLocalAABB(spriteRenderer, currentSprite.bounds);

            spriteRenderer.DeactivateDeformableBuffer();

            m_DataIndex = -1;
            m_TransformsHash = 0;
        }

        internal void ResetSprite()
        {
            m_CurrentDeformSprite = 0;
            CacheValidFlag();
        }
    }
}
