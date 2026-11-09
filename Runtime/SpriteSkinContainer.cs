using System;
using System.Collections.Generic;
using Unity.Profiling;
using Unity.Scripting.LifecycleManagement;
namespace UnityEngine.U2D.Animation
{
    internal class SpriteSkinContainer : ScriptableObject
    {
        [NoAutoStaticsCleanup] // Bone visibility groups subscribe when they are created
        public static event Action<SpriteSkin> onAddedSpriteSkin;
        [NoAutoStaticsCleanup]
        public static event Action<SpriteSkin> onRemovedSpriteSkin;
        [NoAutoStaticsCleanup]
        public static event Action<SpriteSkin> onBoneTransformChanged;

        [NoAutoStaticsCleanup] // HideAndDontSave instance outlives Play
        static SpriteSkinContainer s_Instance;

        public static SpriteSkinContainer instance
        {
            get
            {
                if (s_Instance == null)
                {
                    SpriteSkinContainer[] managers = Resources.FindObjectsOfTypeAll<SpriteSkinContainer>();
                    if (managers.Length > 0)
                        s_Instance = managers[0];
                    else
                        s_Instance = CreateInstance<SpriteSkinContainer>();
                    s_Instance.hideFlags = HideFlags.HideAndDontSave;
                }

                return s_Instance;
            }
        }

        List<SpriteSkin> m_SpriteSkin = new List<SpriteSkin>();
        public IReadOnlyList<SpriteSkin> spriteSkins => m_SpriteSkin;

        public void AddSpriteSkin(SpriteSkin spriteSkin)
        {
            m_SpriteSkin.Add(spriteSkin);


            onAddedSpriteSkin?.Invoke(spriteSkin);
        }

        public void RemoveSpriteSkin(SpriteSkin spriteSkin)
        {
            m_SpriteSkin.Remove(spriteSkin);


            onRemovedSpriteSkin?.Invoke(spriteSkin);
        }

        public void BoneTransformsChanged(SpriteSkin spriteSkin)
        {
            onBoneTransformChanged?.Invoke(spriteSkin);
        }
    }
}
