using System;
using Unity.Scripting.LifecycleManagement;

namespace UnityEngine.U2D.Animation
{
    internal static class SpriteLibraryUtility
    {
        // Allow delegate override for test
        [NoAutoStaticsCleanup]
        internal static Func<string, int> GetStringHash = Bit30Hash_GetStringHash;

        /// <summary>
        /// Used to convert Sprite Key to the new Sprite Hash.
        /// </summary>
        /// <param name="input">Sprite Key to convert</param>
        /// <returns>A 30-bit long hash.</returns>
        internal static int Convert32BitTo30BitHash(int input)
        {
            int output = PreserveFirst30Bits(input);
            return output;
        }

        static int Bit30Hash_GetStringHash(string value)
        {
#if DEBUG_GETSTRINGHASH_CLASH
            if (value == "abc" || value == "123")
                value = "abc";
#endif
            int hash = Animator.StringToHash(value);
            hash = PreserveFirst30Bits(hash);
            return hash;
        }

        // The result is serialized and keyframed, so this arithmetic must never change.
        internal static int CombineHashes(int categoryHash, int labelHash)
        {
            const uint offsetBasis = 2166136261;
            const uint prime = 16777619;
            uint hash = offsetBasis;
            hash = (hash ^ (uint)categoryHash) * prime;
            hash = (hash ^ (uint)labelHash) * prime;
            return PreserveFirst30Bits((int)hash);
        }

        static int PreserveFirst30Bits(int input)
        {
            const int mask = 0x3FFFFFFF;
            return input & mask;
        }

        internal static long GenerateHash()
        {
            long hash = DateTime.Now.Ticks;
            return hash;
        }
    }
}
