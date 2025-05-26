using System.Collections.Generic;
using UnityEngine;

namespace UnityEditor.U2D.Animation
{
    internal static class CharacterPartCacheExtensions
    {
        public static void SyncSpriteSheetSkeleton(this CharacterPartCache characterPart)
        {
            SkinningCache skinningCache = characterPart.skinningCache;
            CharacterCache character = skinningCache.character;
            SkeletonCache characterSkeleton = character.skeleton;
            SkeletonCache spriteSkeleton = characterPart.sprite.GetSkeleton();
            BoneCache[] spriteSkeletonBones = spriteSkeleton.bones;
            BoneCache[] characterPartBones = characterPart.bones;

            if (spriteSkeletonBones.Length != characterPartBones.Length)
                return;

            for (int i = 0; i < characterPartBones.Length; ++i)
            {
                BoneCache spriteBone = spriteSkeletonBones[i];
                BoneCache characterBone = characterPartBones[i];
                Pose[] childWorldPose = spriteBone.GetChildrenWoldPose();

                spriteBone.position = spriteSkeleton.localToWorldMatrix.MultiplyPoint3x4(
                    characterPart.worldToLocalMatrix.MultiplyPoint3x4(characterBone.position));
                spriteBone.rotation = characterBone.rotation;
                spriteBone.length = characterBone.length;
                spriteBone.guid = characterBone.guid;
                spriteBone.name = characterBone.name;
                spriteBone.depth = characterBone.depth;
                spriteBone.bindPoseColor = characterBone.bindPoseColor;

                spriteBone.SetChildrenWorldPose(childWorldPose);
            }

            if (characterSkeleton.isPosePreview)
                spriteSkeleton.SetPosePreview();
            else
                spriteSkeleton.SetDefaultPose();
        }

        public static void DissociateUnusedBones(this CharacterPartCache characterPart)
        {
            SkinningCache skinningCache = characterPart.skinningCache;
            BoneCache[] bones = characterPart.bones;

            if (bones.Length == 0)
                return;

            Debug.Assert(characterPart.sprite != null);

            MeshCache mesh = characterPart.sprite.GetMesh();

            Debug.Assert(mesh != null);

            EditableBoneWeight[] weights = mesh.vertexWeights;
            HashSet<BoneCache> newBonesSet = new HashSet<BoneCache>();

            foreach (EditableBoneWeight weight in weights)
            {
                foreach (BoneWeightChannel channel in weight)
                    if (channel.enabled)
                        newBonesSet.Add(bones[channel.boneIndex]);
            }

            bones = new List<BoneCache>(newBonesSet).ToArray();

            characterPart.bones = bones;

            characterPart.sprite.UpdateMesh(bones);

            skinningCache.events.characterPartChanged.Invoke(characterPart);
        }
    }
}
