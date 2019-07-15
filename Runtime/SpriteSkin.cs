using Unity.Collections;
using UnityEngine.Scripting;
using UnityEngine.Experimental.U2D.Common;

namespace UnityEngine.Experimental.U2D.Animation
{
    [Preserve]
    [ExecuteInEditMode]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(SpriteSkinEntity))]
    internal class SpriteSkin : MonoBehaviour
    {
        [SerializeField]
        private Transform m_RootBone;
        [SerializeField]
        private Transform[] m_BoneTransforms;
        [SerializeField]
        private Bounds m_Bounds;

        private NativeArray<Vector3> m_DeformedVertices;
        private SpriteRenderer m_SpriteRenderer;
        private int m_TransformsHash = 0;
        private bool m_ForceSkinning;
        private Sprite m_CurrentDeformSprite;

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

#if ENABLE_ENTITIES

        [SerializeField]
        private bool m_EnableEntities = false;
        private SpriteSkinEntity m_SpriteSkinEntity;

        SpriteSkinEntity spriteSkinEntity
        {
            get
            {
                if (m_SpriteSkinEntity == null)
                    m_SpriteSkinEntity = GetComponent<SpriteSkinEntity>();

                return m_SpriteSkinEntity;
            }
        }

        public bool entitiesEnabled
        {
            get { return m_EnableEntities; }
            set { m_EnableEntities = value; }
        }

        private void Awake()
        {
            if (spriteSkinEntity != null)
            {
                spriteSkinEntity.enabled = false;
                spriteSkinEntity.enabled = true;
            }
        }

#endif
        NativeArray<Vector3> deformedVertices
        {
            get
            {
                if (sprite != null)
                {
                    var spriteVertexCount = sprite.GetVertexCount();
                    if (m_DeformedVertices.IsCreated)
                    {
                        if (m_DeformedVertices.Length != spriteVertexCount)
                        {
                            m_DeformedVertices.Dispose();
                            m_DeformedVertices = new NativeArray<Vector3>(spriteVertexCount, Allocator.Persistent);
                            m_TransformsHash = 0;
                        }
                    }
                    else
                    {
                        m_DeformedVertices = new NativeArray<Vector3>(spriteVertexCount, Allocator.Persistent);
                        m_TransformsHash = 0;
                    }
                }
                return m_DeformedVertices;
            }
        }

        void OnDisable()
        {
            if (m_DeformedVertices.IsCreated)
                m_DeformedVertices.Dispose();
        }

        void LateUpdate()
        {
#if ENABLE_ENTITIES
            if(entitiesEnabled)
                return;
#endif
            if (m_CurrentDeformSprite != sprite)
            {
                DeactivateSkinning();
                m_CurrentDeformSprite = sprite;
            }
            if (isValid)
            {
                var inputVertices = deformedVertices;
                var transformHash = SpriteSkinUtility.CalculateTransformHash(this);
                if (inputVertices.Length > 0 && m_TransformsHash != transformHash)
                {
                    SpriteSkinUtility.Deform(sprite, gameObject.transform.worldToLocalMatrix, boneTransforms, ref inputVertices);
                    SpriteSkinUtility.UpdateBounds(this);
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
            set { m_BoneTransforms = value; }
        }

        internal Transform rootBone
        {
            get { return m_RootBone; }
            set { m_RootBone = value; }
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
