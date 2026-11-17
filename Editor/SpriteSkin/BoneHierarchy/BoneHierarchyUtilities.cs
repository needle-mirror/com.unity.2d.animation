using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.U2D.Animation;

namespace UnityEditor.U2D.Animation
{
    /// <summary>
    /// Resolves where SpriteBoneHierarchyData lives for a selection and computes per-bone display colors from SpriteSkin and serialized hierarchy data.
    /// </summary>
    internal static class BoneHierarchyUtilities
    {
        /// <summary>
        /// For a single selected transform, determines the bone host: the transform whose GameObject should carry <see cref="SpriteBoneHierarchyData"/> and scopes bone hierarchy / gizmo context.
        /// A selected <see cref="SpriteSkin"/> resolves to its own bone host (see <see cref="TryGetSpriteSkinHost"/>); any other selection resolves to the nearest ancestor-or-self that hosts at least one <see cref="SpriteSkin"/>.
        /// Pass <paramref name="boneHostsByRoot"/> to reuse the bone hosts found under each root across several selections.
        /// </summary>
        internal static bool ResolveBoneHost(Transform selected, out Transform boneHost, Dictionary<Transform, HashSet<Transform>> boneHostsByRoot = null)
        {
            boneHost = null;

            if (selected == null)
                return false;

            SpriteSkin spriteSkin = selected.GetComponent<SpriteSkin>();
            if (spriteSkin != null)
                return TryGetSpriteSkinHost(spriteSkin, out boneHost);

            // May be a rig root, a group of sprites or bones, or a bone transform - find the nearest ancestor that hosts a SpriteSkin.
            if (boneHostsByRoot == null || !boneHostsByRoot.TryGetValue(selected.root, out HashSet<Transform> boneHosts))
            {
                boneHosts = CollectBoneHosts(selected.root);
                if (boneHostsByRoot != null)
                    boneHostsByRoot[selected.root] = boneHosts;
            }

            for (Transform candidate = selected; candidate != null; candidate = candidate.parent)
            {
                if (boneHosts.Contains(candidate))
                {
                    boneHost = candidate;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Bone host of a <see cref="SpriteSkin"/>: the nearest common ancestor (inclusive) of the <see cref="SpriteSkin"/> transform and its first non-null bone transform.
        /// This is the SpriteSkin itself for PNG rigs (bones under the skin), the rig root for PSB/PSD rigs (bones beside the skin), and the common parent when sprites and bones are regrouped under separate GameObjects.
        /// A SpriteSkin without bones hosts itself. Returns <see langword="false"/> when the SpriteSkin and its bones do not share an ancestor.
        /// Assumes bone transforms do not mix incompatible rig layouts; classification uses the first non-null entry in <see cref="SpriteSkin.boneTransforms"/> only.
        /// </summary>
        internal static bool TryGetSpriteSkinHost(SpriteSkin spriteSkin, out Transform boneHost)
        {
            boneHost = null;

            if (spriteSkin == null)
                return false;

            Transform spriteSkinTransform = spriteSkin.transform;
            Transform[] boneTransforms = spriteSkin.boneTransforms;
            if (boneTransforms != null)
            {
                foreach (Transform boneTransform in boneTransforms)
                {
                    if (boneTransform == null)
                        continue;

                    for (Transform candidate = spriteSkinTransform; candidate != null; candidate = candidate.parent)
                    {
                        if (boneTransform.IsChildOf(candidate))
                        {
                            boneHost = candidate;
                            return true;
                        }
                    }

                    // Bones are in a different root hierarchy from the SpriteSkin.
                    return false;
                }
            }

            // No bones yet - the SpriteSkin hosts itself.
            boneHost = spriteSkinTransform;
            return true;
        }

        static HashSet<Transform> CollectBoneHosts(Transform root)
        {
            HashSet<Transform> boneHosts = new();
            foreach (SpriteSkin spriteSkin in root.GetComponentsInChildren<SpriteSkin>(true))
            {
                if (TryGetSpriteSkinHost(spriteSkin, out Transform boneHost))
                    boneHosts.Add(boneHost);
            }

            return boneHosts;
        }

        /// <summary>
        /// Resolves the bone colors of each <see cref="SpriteSkin"/> from <see cref="SpriteBoneHierarchyData"/> on its parent and ancestors up to the bone host; higher levels take precedence.
        /// </summary>
        internal static void ResolveBoneColors(IEnumerable<Transform> boneHosts, out Dictionary<Transform, Color32> resolvedColors)
        {
            resolvedColors = new();

            if (boneHosts == null)
                return;

            foreach (Transform boneHost in boneHosts)
            {
                if (boneHost == null || !TryGetSpriteSkins(boneHost.gameObject, out SpriteSkin[] spriteSkins))
                    continue;

                if (!TryGetBoneTransformToGuid(spriteSkins, out Dictionary<Transform, string> boneTransformToGuid))
                    continue;

                Dictionary<Transform, Dictionary<string, Color32>> boneColorsByTransform = new();
                foreach (SpriteSkin spriteSkin in spriteSkins)
                {
                    Transform colorSource = spriteSkin.transform == boneHost ? boneHost : spriteSkin.transform.parent;
                    Dictionary<string, Color32> boneColors = GetBoneColors(colorSource, boneHost, boneColorsByTransform);
                    if (boneColors.Count > 0)
                        ResolveBoneColors(boneColors, spriteSkin, boneTransformToGuid, resolvedColors);
                }
            }
        }

        // Colors stored on transform and its ancestors up to boneHost; ancestors take precedence.
        static Dictionary<string, Color32> GetBoneColors(Transform transform, Transform boneHost, Dictionary<Transform, Dictionary<string, Color32>> boneColorsByTransform)
        {
            if (boneColorsByTransform.TryGetValue(transform, out Dictionary<string, Color32> boneColors))
                return boneColors;

            boneColors = new();
            MergeBoneColors(transform, boneColors);
            if (transform != boneHost)
            {
                foreach (KeyValuePair<string, Color32> kvp in GetBoneColors(transform.parent, boneHost, boneColorsByTransform))
                    boneColors[kvp.Key] = kvp.Value;
            }

            boneColorsByTransform[transform] = boneColors;
            return boneColors;
        }

        static void MergeBoneColors(Transform transform, Dictionary<string, Color32> boneColors)
        {
            SpriteBoneHierarchyData hierarchyData = transform.GetComponent<SpriteBoneHierarchyData>();
            if (hierarchyData == null)
                return;

            hierarchyData.LoadBoneColors();

            Dictionary<string, Color32> colors = hierarchyData.GetBoneColors();
            if (colors == null)
                return;

            foreach (KeyValuePair<string, Color32> kvp in colors)
                boneColors[kvp.Key] = kvp.Value;
        }

        static void ResolveBoneColors(Dictionary<string, Color32> boneColors, SpriteSkin spriteSkin, Dictionary<Transform, string> boneTransformToGuid, Dictionary<Transform, Color32> resolvedColors)
        {
            Transform[] boneTransforms = spriteSkin.boneTransforms;
            if (boneTransforms == null)
                return;

            foreach (Transform boneTransform in boneTransforms)
            {
                if (boneTransform == null)
                    continue;

                if (!boneTransformToGuid.TryGetValue(boneTransform, out string guid))
                    continue;

                if (boneColors.TryGetValue(guid, out Color32 boneColor))
                    resolvedColors[boneTransform] = boneColor;
                else
                    resolvedColors.TryAdd(boneTransform, BoneGizmo.DefaultBoneColor); // Keep a color resolved through another SpriteSkin sharing this bone.
            }
        }

        /// <summary>
        /// Gathers the <see cref="SpriteSkin"/> components whose bone host (see <see cref="TryGetSpriteSkinHost"/>) is <paramref name="go"/>; they can be anywhere under <paramref name="go"/>, including on <paramref name="go"/> itself.
        /// Fills <paramref name="spriteSkins"/> when any exist; when <paramref name="go"/> is null, <paramref name="spriteSkins"/> is null and the result is <see langword="false"/>.
        /// </summary>
        internal static bool TryGetSpriteSkins(GameObject go, out SpriteSkin[] spriteSkins)
        {
            if (go == null)
            {
                spriteSkins = null;
                return false;
            }

            List<SpriteSkin> collectedSpriteSkins = new();
            foreach (SpriteSkin spriteSkin in go.GetComponentsInChildren<SpriteSkin>(true))
            {
                if (TryGetSpriteSkinHost(spriteSkin, out Transform boneHost) && boneHost == go.transform)
                    collectedSpriteSkins.Add(spriteSkin);
            }

            spriteSkins = collectedSpriteSkins.ToArray();
            return spriteSkins.Length > 0;
        }

        /// <summary>
        /// Builds a transform to sprite bone GUID map from all non-null spriteSkins with matching sprite bone arrays.
        /// </summary>
        internal static bool TryGetBoneTransformToGuid(SpriteSkin[] spriteSkins, out Dictionary<Transform, string> boneTransformToGuid)
        {
            boneTransformToGuid = new();

            if (spriteSkins == null)
                return false;

            foreach (SpriteSkin spriteSkin in spriteSkins)
            {
                if (spriteSkin == null || spriteSkin.spriteRenderer == null || spriteSkin.spriteRenderer.sprite == null)
                    continue;

                Transform[] boneTransforms = spriteSkin.boneTransforms;
                if (boneTransforms == null || boneTransforms.Length == 0)
                    continue;

                SpriteBone[] spriteBones = spriteSkin.spriteRenderer.sprite.GetBones();
                if (spriteBones == null || spriteBones.Length != boneTransforms.Length)
                    continue;

                for (int i = 0; i < boneTransforms.Length; i++)
                {
                    Transform boneTransform = boneTransforms[i];
                    string guid = spriteBones[i].guid;
                    if (string.IsNullOrEmpty(guid))
                        guid = spriteBones[i].name;

                    if (boneTransform != null && !string.IsNullOrEmpty(guid) && !boneTransformToGuid.ContainsKey(boneTransform))
                        boneTransformToGuid[boneTransform] = guid;
                }
            }

            return boneTransformToGuid.Count > 0;
        }
    }
}
