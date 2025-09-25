using System;
using System.Collections.Generic;

namespace UnityEngine.U2D.Animation
{
    internal static class SpriteSkinHelpers
    {
        // Caches the hierarchy of transforms under the root bone.
        // The cache stores all children of a bone, and stores their full path name and transform.
        // The key of the cache is the hashcode of the transform name.
        // Note: This cache is further processed in SpriteSkin.CacheHierarchy
        public static void CacheChildren(Transform current, Dictionary<int, List<SpriteSkin.TransformData>> cache)
        {
            int nameHash = current.name.GetHashCode();
            SpriteSkin.TransformData entry = new SpriteSkin.TransformData()
            {
                fullName = string.Empty,
                transform = current
            };
            // Have we already cached this transform (or a transform name with the same hashcode)?
            // This will happen when the transform name is not unique, which will be often the case.
            if (cache.TryGetValue(nameHash, out List<SpriteSkin.TransformData> value))
                value.Add(entry);
            else
                cache.Add(nameHash, new List<SpriteSkin.TransformData>(1) { entry });

            for (int i = 0; i < current.childCount; ++i)
                CacheChildren(current.GetChild(i), cache);
        }

        // This method generates a string path from the root bone to the child transform.
        public static string GenerateTransformPath(Transform rootBone, Transform child)
        {
            string path = child.name;
            if (child == rootBone)
                return path;
            Transform parent = child.parent;
            do
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            } while (parent != rootBone && parent != null);

            return path;
        }

        // This method is used during runtime to map sprite bones to transforms.
        // It uses a guid on the sprite bone to find the transform using a linear search.
        // If it cannot find the transform using the guid, it will fall back to using the path of the transform
        // and use the hierarchyCache.
        public static bool GetSpriteBonesTransforms(SpriteSkin spriteSkin, out Transform[] outTransform, bool forceCreateCache = false)
        {
            Transform rootBone = spriteSkin.rootBone;
            SpriteBone[] spriteBones = spriteSkin.sprite.GetBones();

            if (rootBone == null)
                throw new ArgumentException("rootBone parameter cannot be null");
            if (spriteBones == null)
                throw new ArgumentException("spriteBones parameter cannot be null");

            outTransform = new Transform[spriteBones.Length];

            Bone[] boneObjects = rootBone.GetComponentsInChildren<Bone>();
            if (boneObjects != null && boneObjects.Length >= spriteBones.Length)
            {
                using (SpriteSkin.Profiling.getSpriteBonesTransformFromGuid.Auto())
                {
                    int i = 0;
                    for (; i < spriteBones.Length; ++i)
                    {
                        string boneHash = spriteBones[i].guid;
                        Bone boneTransform = Array.Find(boneObjects, x => (x.guid == boneHash));
                        if (boneTransform == null)
                            break;

                        outTransform[i] = boneTransform.transform;
                    }

                    if (i >= spriteBones.Length)
                        return true;
                }
            }

            Dictionary<int, List<SpriteSkin.TransformData>> hierarchyCache = spriteSkin.hierarchyCache;
            if (hierarchyCache.Count == 0)
                spriteSkin.CacheHierarchy(forceCreateCache);

            // If unable to successfully map via guid, fall back to path
            return GetSpriteBonesTransformFromPath(spriteBones, hierarchyCache, outTransform);
        }

        static bool GetSpriteBonesTransformFromPath(SpriteBone[] spriteBones, Dictionary<int, List<SpriteSkin.TransformData>> hierarchyCache, Transform[] outNewBoneTransform)
        {
            using (SpriteSkin.Profiling.getSpriteBonesTransformFromPath.Auto())
            {
                string[] bonePath = null;
                bool foundBones = true;
                for (int i = 0; i < spriteBones.Length; ++i)
                {
                    int nameHash = spriteBones[i].name.GetHashCode();
                    if (!hierarchyCache.TryGetValue(nameHash, out List<SpriteSkin.TransformData> children))
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

                        int m = 0;
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
            SpriteBone spriteBone = spriteBones[index];
            int parentId = spriteBone.parentId;
            string bonePath = spriteBone.name;
            if (parentId != -1)
            {
                if (paths[parentId] == null)
                    CalculateBoneTransformsPath(spriteBone.parentId, spriteBones, paths);
                paths[index] = $"{paths[parentId]}/{bonePath}";
            }
            else
                paths[index] = bonePath;
        }
    }
}
