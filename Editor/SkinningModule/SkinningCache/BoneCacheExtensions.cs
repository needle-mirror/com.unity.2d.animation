using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEditor.U2D.Animation
{
    internal static class BoneCacheExtensions
    {
        public static BoneCache[] ToCharacterIfNeeded(this BoneCache[] bones)
        {
            return Array.ConvertAll(bones, ToCharacterIfNeeded);
        }

        public static BoneCache[] ToSpriteSheetIfNeeded(this BoneCache[] bones)
        {
            return Array.ConvertAll(bones, ToSpriteSheetIfNeeded);
        }

        public static BoneCache ToCharacterIfNeeded(this BoneCache bone)
        {
            if (bone == null)
                return null;

            SkinningCache skinningCache = bone.skinningCache;

            if (skinningCache.hasCharacter)
            {
                if (bone.skeleton != skinningCache.character.skeleton)
                {
                    SpriteCache selectedSprite = skinningCache.selectedSprite;

                    if (selectedSprite == null)
                        return null;

                    SkeletonCache skeleton = selectedSprite.GetSkeleton();
                    CharacterPartCache characterPart = selectedSprite.GetCharacterPart();

                    Debug.Assert(skeleton != null);
                    Debug.Assert(characterPart != null);
                    Debug.Assert(bone.skeleton == skeleton);
                    Debug.Assert(skeleton.boneCount == characterPart.boneCount);

                    int index = skeleton.IndexOf(bone);

                    if (index == -1)
                        bone = null;
                    else
                        bone = characterPart.GetBone(index);
                }
            }

            return bone;
        }

        public static BoneCache ToSpriteSheetIfNeeded(this BoneCache bone)
        {
            if (bone == null)
                return null;

            SkinningCache skinningCache = bone.skinningCache;

            if (skinningCache.hasCharacter && skinningCache.mode == SkinningMode.SpriteSheet)
            {
                SpriteCache selectedSprite = skinningCache.selectedSprite;

                if (selectedSprite == null)
                    return null;

                SkeletonCache characterSkeleton = skinningCache.character.skeleton;

                Debug.Assert(bone.skeleton == characterSkeleton);

                SkeletonCache skeleton = selectedSprite.GetSkeleton();
                CharacterPartCache characterPart = selectedSprite.GetCharacterPart();

                Debug.Assert(skeleton != null);
                Debug.Assert(characterPart != null);
                Debug.Assert(skeleton.boneCount == characterPart.boneCount);

                int index = characterPart.IndexOf(bone);

                if (index == -1)
                    bone = null;
                else
                    bone = skeleton.GetBone(index);
            }

            return bone;
        }

        public static UnityEngine.U2D.SpriteBone ToSpriteBone(this BoneCache bone, Matrix4x4 rootTransform, int parentId)
        {
            Vector3 position = bone.localPosition;
            Quaternion rotation = bone.localRotation;

            if (parentId == -1)
            {
                rotation = bone.rotation;
                position = rootTransform.inverse.MultiplyPoint3x4(bone.position);
            }

            return new UnityEngine.U2D.SpriteBone()
            {
                name = bone.name,
                guid = bone.guid,
                position = new Vector3(position.x, position.y, bone.depth),
                rotation = rotation,
                length = bone.localLength,
                parentId = parentId,
                color = bone.bindPoseColor
            };
        }

        public static UnityEngine.U2D.SpriteBone[] ToSpriteBone(this BoneCache[] bones, Matrix4x4 rootTransform)
        {
            List<UnityEngine.U2D.SpriteBone> spriteBones = new List<UnityEngine.U2D.SpriteBone>();

            foreach (BoneCache bone in bones)
            {
                int parentId = -1;

                if (ArrayUtility.Contains(bones, bone.parentBone))
                    parentId = Array.IndexOf(bones, bone.parentBone);

                spriteBones.Add(bone.ToSpriteBone(rootTransform, parentId));
            }

            return spriteBones.ToArray();
        }
    }
}
