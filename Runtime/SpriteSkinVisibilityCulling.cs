using System;
using System.Collections.Generic;

namespace UnityEngine.U2D.Animation
{
    /// <summary>
    /// SpriteSkin registry used to keep visibility state of a SpriteSkin and bone transforms.
    /// </summary>
    class SpriteSkinRegistry
    {
        public int[] boneIds;
        public bool isVisible;

        public SpriteSkinRegistry(int[] boneIds, bool isSkinVisible)
        {
            this.boneIds = boneIds;
            isVisible = isSkinVisible;
        }
    }

    /// <summary>
    /// Class used for checking visibility of SpriteSkins' bones. 
    /// </summary>
    internal class SpriteSkinVisibilityCulling : ScriptableObject
    {
        static SpriteSkinVisibilityCulling s_Instance;

        public static SpriteSkinVisibilityCulling instance
        {
            get
            {
                if (s_Instance == null)
                {
                    var composite = Resources.FindObjectsOfTypeAll<SpriteSkinVisibilityCulling>();
                    s_Instance = composite.Length > 0 ? composite[0] : CreateInstance<SpriteSkinVisibilityCulling>();
                    s_Instance.hideFlags = HideFlags.HideAndDontSave;
                    s_Instance.Initialize();
                }

                return s_Instance;
            }
        }

        /// <summary>
        /// Maps SpriteSkins to SpriteSkinRegistry.
        /// </summary>
        Dictionary<SpriteSkin, SpriteSkinRegistry> m_SpriteSkinRegistries;

        /// <summary>
        /// Counts (value) how many visible Sprite Skins use a given bone (key).
        /// </summary>
        Dictionary<int, int> m_BoneVisibilityCount;

        /// <summary>
        /// Collection of objects that request culling.
        /// </summary>
        HashSet<object> m_RequestingObjects;

        bool m_IsCullingEnabled;

        void Initialize()
        {
            m_SpriteSkinRegistries = new Dictionary<SpriteSkin, SpriteSkinRegistry>();
            m_BoneVisibilityCount = new Dictionary<int, int>();
            m_RequestingObjects = new HashSet<object>();
        }

        public void RequestBoneVisibilityCheck(object requestingObject)
        {
            if (!m_IsCullingEnabled)
            {
                m_IsCullingEnabled = true;

                foreach (var spriteSkin in SpriteSkinComposite.instance.GetSpriteSkins())
                    UpdateSpriteSkinVisibility(spriteSkin);
            }

            m_RequestingObjects.Add(requestingObject);
        }

        public void RemoveBoneVisibilityCheckRequest(object requestingObject)
        {
            m_RequestingObjects.Remove(requestingObject);

            if (m_RequestingObjects.Count == 0)
                DisableBoneVisibilityCheck();
        }

        void DisableBoneVisibilityCheck()
        {
            m_IsCullingEnabled = false;
            
            m_RequestingObjects.Clear();
            m_SpriteSkinRegistries.Clear();
            m_BoneVisibilityCount.Clear();
        }

        public bool IsAnyBoneInfluencingVisibleSprite(IList<int> boneTransformIds)
        {
            for (var i = 0; i < boneTransformIds.Count; i++)
            {
                var boneId = boneTransformIds[i];
                if (m_BoneVisibilityCount.ContainsKey(boneId))
                    return m_BoneVisibilityCount[boneId] > 0;
            }

            return false;
        }

        bool IsSpriteSkinRegistered(SpriteSkin spriteSkin) => m_SpriteSkinRegistries.ContainsKey(spriteSkin);

        public void RegisterSpriteSkin(SpriteSkin spriteSkin)
        {
            if (m_IsCullingEnabled)
                RegisterSpriteSkinBonesMapping(spriteSkin);
        }

        public void UnregisterSpriteSkin(SpriteSkin spriteSkin)
        {
            UnregisterSpriteSkinBonesMapping(spriteSkin);
        }

        public void RefreshBoneMapping(SpriteSkin spriteSkin)
        {
            if (!m_IsCullingEnabled)
                return;

            UnregisterSpriteSkinBonesMapping(spriteSkin);
            RegisterSpriteSkinBonesMapping(spriteSkin);

            UpdateSpriteSkinVisibility(spriteSkin);
        }


        internal void UpdateSpriteSkinVisibility(SpriteSkin spriteSkin)
        {
            if (!m_IsCullingEnabled)
                return;

            var visible = spriteSkin.spriteRenderer.isVisible;
            if (!IsSpriteSkinRegistered(spriteSkin))
            {
                if (visible)
                    RegisterSpriteSkinBonesMapping(spriteSkin);
                else // No need to update visibility if not registered and not visible.
                    return;
            }

            var registry = m_SpriteSkinRegistries[spriteSkin];
            if (registry.isVisible == visible)
                return;

            registry.isVisible = visible;

            RecalculateVisibility(registry);
        }


        void RegisterSpriteSkinBonesMapping(SpriteSkin spriteSkin)
        {
            if (IsSpriteSkinRegistered(spriteSkin))
                return;

            var bones = spriteSkin.boneTransforms ?? Array.Empty<Transform>();
            var records = new int[bones.Length];
            var newRegistry = new SpriteSkinRegistry(records, false);
            for (var i = 0; i < bones.Length; i++)
            {
                var bone = bones[i];
                if (bone == null)
                    continue;
                var id = bone.GetInstanceID();
                records[i] = id;
            }

            m_SpriteSkinRegistries[spriteSkin] = newRegistry;
        }

        void UnregisterSpriteSkinBonesMapping(SpriteSkin spriteSkin)
        {
            if (!IsSpriteSkinRegistered(spriteSkin))
                return;

            var registry = m_SpriteSkinRegistries[spriteSkin];
            registry.isVisible = false;

            m_SpriteSkinRegistries.Remove(spriteSkin);

            RecalculateVisibility(registry);
        }

        void RecalculateVisibility(SpriteSkinRegistry registry)
        {
            var bones = registry.boneIds;

            var visible = registry.isVisible;
            var countOperation = visible ? 1 : -1;

            for (var i = 0; i < bones.Length; i++)
            {
                var bone = bones[i];
                if (m_BoneVisibilityCount.ContainsKey(bone))
                {
                    var count = m_BoneVisibilityCount[bone] + countOperation;
                    if (count <= 0)
                        m_BoneVisibilityCount.Remove(bone);
                    else
                        m_BoneVisibilityCount[bone] = count;
                }
                else if (visible)
                    m_BoneVisibilityCount[bone] = 1;
            }
        }
    }
}