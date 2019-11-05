#pragma warning disable 0168 // variable declared but not used.
#if ENABLE_ANIMATION_COLLECTION && ENABLE_ANIMATION_BURST
#define ENABLE_SPRITESKIN_COMPOSITE
#endif

using UnityEngine.Scripting;
using UnityEngine.U2D.Common;
using Unity.Collections;
using UnityEngine.Scripting.APIUpdating;

namespace UnityEngine.U2D.Animation
{
    [Preserve]
    [ExecuteInEditMode]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    [AddComponentMenu("2D Animation/Sprite Skin")]
    [MovedFrom("UnityEngine.U2D.Experimental.Animation")]

    internal partial class SpriteSkin : MonoBehaviour
    {
        [SerializeField]
        private Transform m_RootBone;
        [SerializeField]
        private Transform[] m_BoneTransforms = new Transform[0];
        [SerializeField]
        private Bounds m_Bounds;
        [SerializeField]
        private bool m_UseBatching = true;

        // The deformed m_SpriteVertices stores all 'HOT' channels only in single-stream and essentially depends on Sprite  Asset data.
        // The order of storage if present is POSITION, NORMALS, TANGENTS.
        private NativeArray<byte> m_DeformedVertices;
        private SpriteRenderer m_SpriteRenderer;
        private Sprite m_CurrentDeformSprite;
        private bool m_ForceSkinning;
        private bool m_BatchSkinning = false;
        bool m_IsValid;
        int m_TransformsHash = 0;

        public bool batchSkinning
        {
            get { return m_BatchSkinning; }
            set { m_BatchSkinning = value; }
        }

#if UNITY_EDITOR
        internal static Events.UnityEvent onDrawGizmos = new Events.UnityEvent();
        private void OnDrawGizmos() { onDrawGizmos.Invoke(); }

        private bool m_IgnoreNextSpriteChange = true;
        public bool ignoreNextSpriteChange
        {
            get { return m_IgnoreNextSpriteChange; }
            set { m_IgnoreNextSpriteChange = value; }
        }
#endif


        void OnEnable()
        {
            CacheValidFlag();
            UpdateSpriteDeform();
            OnEnableBatch();
        }

        void CacheValidFlag()
        {
            m_IsValid = isValid;
        }

        void Reset()
        {
            CacheValidFlag();
            OnResetBatch();
        }

        internal void UseBatching(bool value)
        {
            m_UseBatching = value;
            UseBatchingBatch();
        }

        internal NativeArray<byte> deformedVertices
        {
            get
            {
                if (sprite != null)
                {
                    var spriteVertexCount = sprite.GetVertexStreamSize() * sprite.GetVertexCount();
                    if (m_DeformedVertices.IsCreated)
                    {
                        if (m_DeformedVertices.Length != spriteVertexCount)
                        {
                            m_DeformedVertices.Dispose();
                            m_DeformedVertices = new NativeArray<byte>(spriteVertexCount, Allocator.Persistent);
                            m_TransformsHash = 0;
                        }
                    }
                    else
                    {
                        m_DeformedVertices = new NativeArray<byte>(spriteVertexCount, Allocator.Persistent);
                        m_TransformsHash = 0;
                    }
                }
                return m_DeformedVertices;
            }
        }

        void OnDisable()
        {
            DeactivateSkinning();
            if (m_DeformedVertices.IsCreated)
                m_DeformedVertices.Dispose();
            OnDisableBatch();
       }

#if ENABLE_SPRITESKIN_COMPOSITE
        internal void OnLateUpdate()
#else
        void LateUpdate()
#endif
        {
            if (m_CurrentDeformSprite != sprite)
            {
                DeactivateSkinning();
                m_CurrentDeformSprite = sprite;
                UpdateSpriteDeform();
            }
            if (isValid && !batchSkinning && this.enabled)
            {
                var inputVertices = deformedVertices;
                var transformHash = SpriteSkinUtility.CalculateTransformHash(this);
                if (inputVertices.Length > 0 && m_TransformsHash != transformHash)
                {
                    SpriteSkinUtility.Deform(sprite, gameObject.transform.worldToLocalMatrix, boneTransforms, ref inputVertices);
                    SpriteSkinUtility.UpdateBounds(this, transform.worldToLocalMatrix, rootBone.localToWorldMatrix);
                    InternalEngineBridge.SetDeformableBuffer(spriteRenderer, inputVertices);
                    m_TransformsHash = transformHash;
                    m_CurrentDeformSprite = sprite;
                }
            }
        }

        internal Sprite sprite
        {
            get
            {
                if (spriteRenderer == null)
                    return null;
                return spriteRenderer.sprite;
            }
        }

        internal SpriteRenderer spriteRenderer
        {
            get
            {
                if (m_SpriteRenderer == null)
                    m_SpriteRenderer = GetComponent<SpriteRenderer>();
                return m_SpriteRenderer;
            }
        }

        internal Transform[] boneTransforms
        {
            get { return m_BoneTransforms; }
            set
            {
                m_BoneTransforms = value;
                CacheValidFlag();
                OnBoneTransformChanged();
            }
        }

        internal Transform rootBone
        {
            get { return m_RootBone; }
            set
            {
                m_RootBone = value;
                CacheValidFlag();
                OnRootBoneTransformChanged();
            }
        }

        internal Bounds bounds
        {
            get { return m_Bounds; }
            set { m_Bounds = value; }
        }

        internal bool isValid
        {
            get { return this.Validate() == SpriteSkinValidationResult.Ready; }
        }

        protected virtual void OnDestroy()
        {
            DeactivateSkinning();
        }

        internal void DeactivateSkinning()
        {
            var sprite = spriteRenderer.sprite;

            if (sprite != null)
                InternalEngineBridge.SetLocalAABB(spriteRenderer, sprite.bounds);

            SpriteRendererDataAccessExtensions.DeactivateDeformableBuffer(spriteRenderer);
        }


    }
}
