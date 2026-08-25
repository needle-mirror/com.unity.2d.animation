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
        /// Assumes bone transforms do not mix incompatible rig layouts (e.g. root bones disagree on whether they are under the <see cref="SpriteSkin"/> or its parent); classification uses the first non-null entry in <see cref="SpriteSkin.boneTransforms"/> only.
        /// </summary>
        internal static bool ResolveBoneHost(Transform selected, out Transform boneHost)
        {
            boneHost = null;

            if (selected == null)
                return false;

            SpriteSkin spriteSkin = selected.GetComponent<SpriteSkin>();
            if (spriteSkin != null)
            {
                // Selection is the GameObject that has a SpriteSkin.
                Transform[] boneTransforms = spriteSkin.boneTransforms;
                if (boneTransforms == null || boneTransforms.Length == 0)
                {
                    // No bones yet - use this transform as bone host.
                    boneHost = selected;
                    return true;
                }

                // First non-null bone classifies PNG vs PSB/PSD layout; mixed root-bone parenting is unsupported.
                foreach (Transform boneTransform in boneTransforms)
                {
                    if (boneTransform == null)
                        continue;

                    if (boneTransform.IsChildOf(spriteSkin.transform))
                    {
                        // Bones parented under the SpriteSkin (PNG rig).
                        boneHost = selected;
                        return true;
                    }

                    Transform spriteSkinParent = spriteSkin.transform.parent;
                    if (spriteSkinParent == null)
                        return false;

                    if (boneTransform.IsChildOf(spriteSkinParent))
                    {
                        // Bones under the same parent as the SpriteSkin (PSB or PSD rig root).
                        boneHost = spriteSkinParent;
                        return true;
                    }
                    else
                    {
                        // Bones are not under the SpriteSkin or its parent.
                        return false;
                    }
                }

                return false;
            }

            // No SpriteSkin on this object - may be a PSB/PSD rig root or a bone transform.
            if (!TryGetSpriteSkins(selected.gameObject, out SpriteSkin[] spriteSkins))
            {
                // No SpriteSkin on this object or its direct children - traverse parents (may be a bone transform selected).
                return ResolveBoneHostInParents(selected, out boneHost);
            }

            for (int i = 0; i < spriteSkins.Length; i++)
            {
                if (spriteSkins[i] == null)
                    continue;

                if (spriteSkins[i].transform.parent == selected)
                {
                    // Parent of a child SpriteSkin - PSB/PSD rig root candidate (first non-null bone classifies; mixed layouts are unsupported).
                    Transform[] boneTransforms = spriteSkins[i].boneTransforms;
                    if (boneTransforms == null || boneTransforms.Length == 0)
                        continue;

                    foreach (Transform boneTransform in boneTransforms)
                    {
                        if (boneTransform == null)
                            continue;

                        if (boneTransform.IsChildOf(spriteSkins[i].transform))
                        {
                            // Bones hang under the SpriteSkin transform (nested PNG rig) - try next skin or fall back to ancestor search.
                            break;
                        }
                        else
                        {
                            // Bones are parented from this root beside/above the skin (PSB/PSD rig) - host is this transform.
                            boneHost = selected;
                            return true;
                        }
                    }
                }
            }

            // No PSB/PSD rig root match on this selection - fall back to ancestor search.
            return ResolveBoneHostInParents(selected, out boneHost);
        }

        static bool ResolveBoneHostInParents(Transform selected, out Transform boneHost)
        {
            boneHost = null;

            if (selected == null)
                return false;

            Transform parent = selected.parent;
            if (parent == null)
                return false;

            SpriteSkin spriteSkin = parent.GetComponent<SpriteSkin>();
            if (spriteSkin != null)
            {
                // Selection under SpriteSkin on parent (PNG rig) - host is that transform.
                boneHost = parent;
                return true;
            }

            while (parent != null)
            {
                if (TryGetSpriteSkins(parent.gameObject, out SpriteSkin[] parentSpriteSkins))
                {
                    // Validate PNG (skin on candidate) or PSB/PSD (skin on direct child) layout; first non-null bone classifies.
                    foreach (SpriteSkin parentSpriteSkin in parentSpriteSkins)
                    {
                        if (parentSpriteSkin == null)
                            continue;

                        bool isSpriteSkinOnSelf = parentSpriteSkin.transform == parent;
                        bool isSpriteSkinOnDirectChild = !isSpriteSkinOnSelf && parentSpriteSkin.transform.parent == parent;
                        if (!isSpriteSkinOnSelf && !isSpriteSkinOnDirectChild)
                            continue;

                        Transform[] parentBoneTransforms = parentSpriteSkin.boneTransforms;
                        if (parentBoneTransforms == null)
                            continue;

                        foreach (Transform parentBoneTransform in parentBoneTransforms)
                        {
                            if (parentBoneTransform == null)
                                continue;

                            if (parentBoneTransform.IsChildOf(parent) && !(isSpriteSkinOnDirectChild && parentBoneTransform.IsChildOf(parentSpriteSkin.transform)))
                            {
                                // Ancestor owns this bone via PNG or PSB/PSD layout - host is that ancestor.
                                boneHost = parent;
                                return true;
                            }
                            break;
                        }
                    }
                }
                parent = parent.parent;
            }

            return false;
        }

        /// <summary>
        /// For each bone host transform, reads <see cref="SpriteBoneHierarchyData"/> with <c>GetComponent</c> only (no child transform search); bone hosts are not expected to store that component on nested children.
        /// Merges resolved colors from each host via <c>LoadBoneColors</c> then <see cref="ResolveBoneColors(SpriteBoneHierarchyData, System.Collections.Generic.Dictionary{UnityEngine.Transform, UnityEngine.Color32})"/>.
        /// </summary>
        internal static void ResolveBoneColors(IEnumerable<Transform> boneHosts, out Dictionary<Transform, Color32> resolvedColors)
        {
            resolvedColors = new();

            if (boneHosts == null)
                return;

            foreach (Transform boneHost in boneHosts)
            {
                if (boneHost == null)
                    continue;

                SpriteBoneHierarchyData hierarchyData = boneHost.GetComponent<SpriteBoneHierarchyData>();
                if (hierarchyData == null)
                    continue;

                hierarchyData.LoadBoneColors();

                ResolveBoneColors(hierarchyData, out Dictionary<Transform, Color32> resolvedColorsOnHost);
                foreach (KeyValuePair<Transform, Color32> kvp in resolvedColorsOnHost)
                    resolvedColors[kvp.Key] = kvp.Value;
            }
        }

        /// <summary>
        /// When serialized bone colors are non-empty, maps each assigned bone transform on skins under hierarchyData's GameObject to a color from GUID entries; bones without a direct entry use <see cref="BoneGizmo.DefaultBoneColor"/>.
        /// </summary>
        internal static void ResolveBoneColors(SpriteBoneHierarchyData hierarchyData, out Dictionary<Transform, Color32> resolvedColors)
        {
            resolvedColors = new();

            if (hierarchyData == null)
                return;

            if (!TryGetSpriteSkins(hierarchyData.gameObject, out SpriteSkin[] spriteSkins))
                return;

            Dictionary<string, Color32> boneColors = hierarchyData.GetBoneColors();

            if (boneColors == null || boneColors.Count == 0)
                return;

            if (!TryGetBoneTransformToGuid(spriteSkins, out Dictionary<Transform, string> boneTransformToGuid))
                return;

            foreach (SpriteSkin spriteSkin in spriteSkins)
            {
                if (spriteSkin == null)
                    continue;

                Transform[] boneTransforms = spriteSkin.boneTransforms;
                if (boneTransforms == null || boneTransforms.Length == 0)
                    continue;

                foreach (Transform boneTransform in boneTransforms)
                {
                    if (boneTransform == null)
                        continue;

                    if (!boneTransformToGuid.TryGetValue(boneTransform, out string guid))
                        continue;

                    if (boneColors.TryGetValue(guid, out Color32 boneColor))
                        resolvedColors[boneTransform] = boneColor;
                    else
                        resolvedColors[boneTransform] = BoneGizmo.DefaultBoneColor;
                }
            }
        }

        /// <summary>
        /// Gathers <see cref="SpriteSkin"/> on <paramref name="go"/> or on immediate children only; deeper descendants are not searched.
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
            SpriteSkin spriteSkin = go.GetComponent<SpriteSkin>();
            if (spriteSkin != null)
            {
                collectedSpriteSkins.Add(spriteSkin);
            }
            else
            {
                for (int i = 0; i < go.transform.childCount; i++)
                {
                    Transform child = go.transform.GetChild(i);
                    if (child == null)
                        continue;

                    SpriteSkin childSpriteSkin = child.GetComponent<SpriteSkin>();
                    if (childSpriteSkin != null)
                        collectedSpriteSkins.Add(childSpriteSkin);
                }
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
