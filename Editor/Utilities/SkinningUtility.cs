using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D;

namespace UnityEditor.U2D.Animation
{
    /// <summary>
    /// Utility methods for scaling Sprite skinning data (bones, meshes).
    /// </summary>
    internal static class SkinningUtility
    {
        private struct BoneWorldTransform
        {
            public Vector2 oldPos;
            public Quaternion oldRot;
            public Vector2 newPos;
            public Quaternion newRot;
        }

        /// <summary>
        /// Uniformly scales a hierarchy of SpriteBones around a pivot.
        /// </summary>
        /// <remarks>The input is not modified; the scaled bones are returned in a new list.</remarks>
        /// <param name="bones">The bones to scale, ordered so that a bone's parent precedes it.</param>
        /// <param name="scale">The uniform scale factor to apply.</param>
        /// <param name="pivot">The pivot to scale around, in the Sprite rect's pixel space.</param>
        /// <returns>A new list containing the scaled bones.</returns>
        public static List<SpriteBone> ScaleBones(IReadOnlyList<SpriteBone> bones, float scale, Vector2 pivot)
        {
            // The fast path below only holds for a positive scale; a negative one flips orientation, so defer it.
            if (scale < 0f)
                return ScaleBones(bones, new Vector2(scale, scale), pivot);

            // Positive uniform scale commutes with rotation: roots scale around the pivot, children scale their
            // parent-local position, and rotations are left unchanged.
            List<SpriteBone> result = new(bones.Count);
            for (int i = 0; i < bones.Count; ++i)
            {
                SpriteBone bone = bones[i];
                Vector2 xy = bone.position;

                if (bone.parentId < 0)
                    xy = (xy - pivot) * scale + pivot;
                else
                    xy *= scale;

                bone.position = new Vector3(xy.x, xy.y, bone.position.z);
                bone.length *= scale;
                result.Add(bone);
            }

            return result;
        }

        /// <summary>
        /// Scales a hierarchy of SpriteBones around a pivot. Negative components mirror along that axis.
        /// </summary>
        /// <remarks>
        /// The input is not modified; the scaled bones are returned in a new list. For a non-uniform scale,
        /// joint positions are placed exactly and each bone's rotation is fit to the scaled direction, as a
        /// SpriteBone cannot represent the resulting shear.
        /// </remarks>
        /// <param name="bones">The bones to scale, ordered so that a bone's parent precedes it.</param>
        /// <param name="scale">The per-axis scale factor to apply, in the Sprite rect's space.</param>
        /// <param name="pivot">The pivot to scale around, in the Sprite rect's pixel space.</param>
        /// <returns>A new list containing the scaled bones.</returns>
        public static List<SpriteBone> ScaleBones(IReadOnlyList<SpriteBone> bones, Vector2 scale, Vector2 pivot)
        {
            // A uniform positive scale preserves orientation exactly, so defer to the uniform overload, which
            // leaves rotation untouched. This avoids the atan2/Euler round-trip below (and its drift) for the
            // most common case, making an identity scale a true no-op.
            if (scale.x == scale.y && scale.x > 0f)
                return ScaleBones(bones, scale.x, pivot);

            int count = bones.Count;

            // World transforms before and after scaling (unit-scale TRS, so position + Z rotation only).
            BoneWorldTransform[] boneWorldTransforms = new BoneWorldTransform[count];

            List<SpriteBone> result = new(count);
            for (int i = 0; i < count; ++i)
            {
                SpriteBone bone = bones[i];
                int parent = bone.parentId;

                // Accumulate the bone's old world transform from its parent (roots are already in rect space).
                if (parent < 0)
                {
                    boneWorldTransforms[i].oldRot = bone.rotation;
                    boneWorldTransforms[i].oldPos = (Vector2)bone.position;
                }
                else
                {
                    boneWorldTransforms[i].oldRot = boneWorldTransforms[parent].oldRot * bone.rotation;
                    boneWorldTransforms[i].oldPos = boneWorldTransforms[parent].oldPos + (Vector2)(boneWorldTransforms[parent].oldRot * bone.position);
                }

                // Apply the scale in rect space: joints are placed exactly, the direction is scaled.
                boneWorldTransforms[i].newPos = Vector2.Scale(boneWorldTransforms[i].oldPos - pivot, scale) + pivot;
                Vector2 scaledDir = Vector2.Scale(boneWorldTransforms[i].oldRot * Vector3.right, scale);
                if (scaledDir.sqrMagnitude > Mathf.Epsilon)
                    boneWorldTransforms[i].newRot = Quaternion.Euler(0f, 0f, Mathf.Atan2(scaledDir.y, scaledDir.x) * Mathf.Rad2Deg);
                else
                    boneWorldTransforms[i].newRot = boneWorldTransforms[i].oldRot; // Collapsed (e.g. zero scale): keep orientation, length goes to 0.
                bone.length *= scaledDir.magnitude;

                // Convert the new world transform back to local, relative to the parent's new world transform.
                Vector2 localPos;
                if (parent < 0)
                {
                    localPos = boneWorldTransforms[i].newPos;
                    bone.rotation = boneWorldTransforms[i].newRot;
                }
                else
                {
                    Quaternion inverseParent = Quaternion.Inverse(boneWorldTransforms[parent].newRot);
                    localPos = (Vector2)(inverseParent * (Vector3)(boneWorldTransforms[i].newPos - boneWorldTransforms[parent].newPos));
                    bone.rotation = inverseParent * boneWorldTransforms[i].newRot;
                }

                bone.position = new Vector3(localPos.x, localPos.y, bone.position.z);
                result.Add(bone);
            }

            return result;
        }

        /// <summary>
        /// Uniformly scales mesh vertex positions around a pivot.
        /// </summary>
        /// <remarks>The input is not modified; the scaled vertices are returned in a new array.</remarks>
        /// <param name="vertices">The vertex positions to scale, in the Sprite rect's pixel space.</param>
        /// <param name="scale">The uniform scale factor to apply.</param>
        /// <param name="pivot">The pivot to scale around, in the Sprite rect's pixel space.</param>
        /// <returns>A new array containing the scaled vertex positions.</returns>
        public static Vector2[] ScaleVertices(IReadOnlyList<Vector2> vertices, float scale, Vector2 pivot)
        {
            return ScaleVertices(vertices, new Vector2(scale, scale), pivot);
        }

        /// <summary>
        /// Scales mesh vertex positions around a pivot. Negative components mirror along that axis.
        /// </summary>
        /// <remarks>
        /// The input is not modified; the scaled vertices are returned in a new array. Vertex weights and
        /// triangle indices are unaffected, so a mirroring scale does not reverse the triangle winding.
        /// </remarks>
        /// <param name="vertices">The vertex positions to scale, in the Sprite rect's pixel space.</param>
        /// <param name="scale">The per-axis scale factor to apply, in the Sprite rect's space.</param>
        /// <param name="pivot">The pivot to scale around, in the Sprite rect's pixel space.</param>
        /// <returns>A new array containing the scaled vertex positions.</returns>
        public static Vector2[] ScaleVertices(IReadOnlyList<Vector2> vertices, Vector2 scale, Vector2 pivot)
        {
            Vector2[] result = new Vector2[vertices.Count];
            for (int i = 0; i < result.Length; ++i)
                result[i] = Vector2.Scale(vertices[i] - pivot, scale) + pivot;

            return result;
        }
    }
}
