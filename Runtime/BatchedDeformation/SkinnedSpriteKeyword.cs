using System.Collections.Generic;

namespace UnityEngine.U2D.Animation
{
    // Shared owner of the SKINNED_SPRITE material keyword. A sharedMaterial can be used by Sprite
    // Skins living in different deformation systems at the same time, so no single system may
    // disable the keyword outright; it is disabled only when the last system that acquired it
    // releases it. A material whose keyword was already enabled externally (e.g. saved into the
    // material asset) is still tracked while in use, so the CPU system cannot take it away from a
    // GPU-deformed Sprite Skin, but releasing it leaves the keyword as the material had it.
    internal static class SkinnedSpriteKeyword
    {
        internal const string keyword = "SKINNED_SPRITE";

        struct Entry
        {
            public Material material;
            public int refCount;
            public bool keepEnabledOnRelease;
        }

        static readonly Dictionary<int, Entry> s_Entries = new Dictionary<int, Entry>();
        static readonly List<int> s_ReleaseScratch = new List<int>();

        // acquiredByCaller is the caller's own set of acquired materials; a caller holds at most
        // one reference per material and passes the same set back to ReleaseAll.
        public static void Acquire(Material material, HashSet<int> acquiredByCaller)
        {
            if (material == null)
                return;

            int id = material.GetInstanceID();
            if (!acquiredByCaller.Add(id))
                return;

            if (s_Entries.TryGetValue(id, out Entry entry))
            {
                entry.refCount++;
                s_Entries[id] = entry;
                return;
            }

            bool wasEnabledExternally = material.IsKeywordEnabled(keyword);
            if (!wasEnabledExternally)
                material.EnableKeyword(keyword);

            s_Entries[id] = new Entry { material = material, refCount = 1, keepEnabledOnRelease = wasEnabledExternally };
        }

        // For the CPU system's material updates: turn off a stray keyword, but never one that a
        // GPU deformation system still holds a reference on.
        public static void DisableIfUnowned(Material material)
        {
            if (material == null)
                return;

            if (s_Entries.ContainsKey(material.GetInstanceID()))
                return;

            if (material.IsKeywordEnabled(keyword))
                material.DisableKeyword(keyword);
        }

        public static void ReleaseAll(HashSet<int> acquiredByCaller)
        {
            foreach (int id in acquiredByCaller)
                ReleaseEntry(id);

            acquiredByCaller.Clear();
        }

        // Releases the caller's references to every material not in materialsInUse.
        public static void ReleaseUnused(HashSet<int> acquiredByCaller, HashSet<int> materialsInUse)
        {
            s_ReleaseScratch.Clear();
            foreach (int id in acquiredByCaller)
            {
                if (!materialsInUse.Contains(id))
                    s_ReleaseScratch.Add(id);
            }

            foreach (int id in s_ReleaseScratch)
            {
                acquiredByCaller.Remove(id);
                ReleaseEntry(id);
            }
        }

        static void ReleaseEntry(int id)
        {
            if (!s_Entries.TryGetValue(id, out Entry entry))
                return;

            entry.refCount--;
            if (entry.refCount > 0)
            {
                s_Entries[id] = entry;
                return;
            }

            s_Entries.Remove(id);
            if (!entry.keepEnabledOnRelease && entry.material != null)
                entry.material.DisableKeyword(keyword);
        }
    }
}
