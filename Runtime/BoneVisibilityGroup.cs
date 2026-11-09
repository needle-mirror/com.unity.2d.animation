using System;
using System.Collections.Generic;

namespace UnityEngine.U2D.Animation
{
    /// <summary>
    /// Tracks whether any visible Sprite Skin is deformed by a given set of bones.
    /// Used by components that skip their per-frame work while the Sprite Skins they
    /// drive are off screen.
    /// </summary>
    internal class BoneVisibilityGroup : IDisposable
    {
        readonly HashSet<Transform> m_Bones = new HashSet<Transform>();
        readonly List<SpriteSkin> m_SpriteSkins = new List<SpriteSkin>();

        /// <summary>
        /// True while at least one Sprite Skin deformed by these bones is visible.
        /// False when none of them is visible, and when no Sprite Skin uses these bones at all.
        /// </summary>
        public bool isVisible
        {
            get
            {
                for (int i = 0; i < m_SpriteSkins.Count; i++)
                {
                    SpriteSkin spriteSkin = m_SpriteSkins[i];
                    if (spriteSkin != null && spriteSkin.spriteRenderer != null && spriteSkin.spriteRenderer.isVisible)
                        return true;
                }

                return false;
            }
        }

        public BoneVisibilityGroup()
        {
            SpriteSkinContainer.onAddedSpriteSkin += OnAddedSpriteSkin;
            SpriteSkinContainer.onRemovedSpriteSkin += OnRemovedSpriteSkin;
            SpriteSkinContainer.onBoneTransformChanged += OnBoneTransformChanged;
        }

        public void Dispose()
        {
            SpriteSkinContainer.onAddedSpriteSkin -= OnAddedSpriteSkin;
            SpriteSkinContainer.onRemovedSpriteSkin -= OnRemovedSpriteSkin;
            SpriteSkinContainer.onBoneTransformChanged -= OnBoneTransformChanged;

            m_Bones.Clear();
            m_SpriteSkins.Clear();
        }

        public void SetBones(IReadOnlyList<Transform> bones)
        {
            m_Bones.Clear();

            if (bones != null)
            {
                for (int i = 0; i < bones.Count; i++)
                {
                    Transform bone = bones[i];
                    if (bone != null)
                        m_Bones.Add(bone);
                }
            }

            m_SpriteSkins.Clear();

            if (m_Bones.Count == 0)
                return;

            IReadOnlyList<SpriteSkin> spriteSkins = SpriteSkinContainer.instance.spriteSkins;
            for (int i = 0; i < spriteSkins.Count; i++)
                OnAddedSpriteSkin(spriteSkins[i]);
        }

        // Which Sprite Skins use these bones only changes when a Sprite Skin is added, removed
        // or rebound, so the list is maintained from those events rather than rebuilt per frame.
        void OnAddedSpriteSkin(SpriteSkin spriteSkin)
        {
            if (spriteSkin != null && UsesAnyBone(spriteSkin) && !m_SpriteSkins.Contains(spriteSkin))
                m_SpriteSkins.Add(spriteSkin);
        }

        void OnRemovedSpriteSkin(SpriteSkin spriteSkin) => m_SpriteSkins.Remove(spriteSkin);

        void OnBoneTransformChanged(SpriteSkin spriteSkin)
        {
            m_SpriteSkins.Remove(spriteSkin);
            OnAddedSpriteSkin(spriteSkin);
        }

        bool UsesAnyBone(SpriteSkin spriteSkin)
        {
            if (m_Bones.Count == 0)
                return false;

            Transform[] boneTransforms = spriteSkin.boneTransforms;
            if (boneTransforms == null)
                return false;

            for (int i = 0; i < boneTransforms.Length; i++)
            {
                if (boneTransforms[i] != null && m_Bones.Contains(boneTransforms[i]))
                    return true;
            }

            return false;
        }
    }
}
