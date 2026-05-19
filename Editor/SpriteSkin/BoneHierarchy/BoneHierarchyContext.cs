using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.U2D.Animation;

namespace UnityEditor.U2D.Animation
{
    /// <summary>
    /// Editor session state for the Bone overlay and bone gizmo: selection metadata and per-bone display colors.
    /// <see cref="BuildFromSelections"/> updates this instance and raises <see cref="BoneHierarchyEvent.ContextRebuilt"/> when the rebuild completes.
    /// <see cref="Reset"/> clears state and also raises <see cref="BoneHierarchyEvent.ContextRebuilt"/>.
    /// </summary>
    internal sealed class BoneHierarchyContext
    {
        static BoneHierarchyContext s_Current;

        /// <summary>
        /// Lazily allocated session singleton.
        /// </summary>
        internal static BoneHierarchyContext Current => s_Current ??= new();

        private HashSet<Transform> m_BoneHosts = new();
        private HashSet<SpriteSkin> m_SpriteSkins = new();
        private Dictionary<Transform, List<Transform>> m_TargetBones = new();
        private Dictionary<Transform, string> m_BoneTransformToGuid = new();

        private Dictionary<Transform, Color32> m_ResolvedBoneColors = new();

        /// <summary>
        /// Bone hosts resolved from each selection root (deduplicated). Empty when no root resolves to a supported layout.
        /// </summary>
        internal IReadOnlyCollection<Transform> BoneHosts => m_BoneHosts;

        internal IReadOnlyCollection<SpriteSkin> SpriteSkins => m_SpriteSkins;

        /// <summary>
        /// Returns true if <paramref name="spriteSkin"/> is currently tracked by this context. Uses the underlying <see cref="HashSet{T}"/> for O(1) lookup since <see cref="IReadOnlyCollection{T}"/> exposes no Contains overload.
        /// </summary>
        internal bool ContainsSpriteSkin(SpriteSkin spriteSkin) => spriteSkin != null && m_SpriteSkins.Contains(spriteSkin);

        internal IReadOnlyDictionary<Transform, List<Transform>> TargetBones => m_TargetBones;

        internal IReadOnlyDictionary<Transform, string> BoneTransformToGuid => m_BoneTransformToGuid;

        /// <summary>
        /// Bone transform to display color for the current gizmo scope.
        /// </summary>
        internal IReadOnlyDictionary<Transform, Color32> ResolvedBoneColors => m_ResolvedBoneColors;

        /// <summary>
        /// Clears editor session state (bone hosts, layout, resolved colors). Raises <see cref="BoneHierarchyEvent.ContextRebuilt"/> so the Bone overlay and gizmo subscribers stay in sync.
        /// Intended for test fixtures and any editor path that must discard bone-hierarchy scope without a natural <see cref="BuildFromSelections"/> transition.
        /// </summary>
        internal void Reset()
        {
            m_BoneHosts.Clear();
            m_SpriteSkins.Clear();
            m_TargetBones.Clear();
            m_BoneTransformToGuid.Clear();
            m_ResolvedBoneColors.Clear();
            BoneHierarchyEvent.RaiseContextRebuilt();
        }

        /// <summary>
        /// Recomputes bone hosts and bone layout from selection roots, fills ResolvedBoneColors, then raises ContextRebuilt.
        /// </summary>
        internal void BuildFromSelections(IEnumerable<GameObject> selections)
        {
            Debug.Assert(selections != null);

            m_BoneHosts.Clear();
            m_SpriteSkins.Clear();
            m_TargetBones.Clear();
            m_BoneTransformToGuid.Clear();

            // Resolve each bone host's skins and GUID map only once; CollectTargetBones still runs per selection.
            Dictionary<Transform, (SpriteSkin[] spriteSkins, Dictionary<Transform, string> boneTransformToGuid)> perBoneHostData = new();

            foreach (GameObject selection in selections)
            {
                if (selection == null)
                    continue;

                if (!BoneHierarchyUtilities.ResolveBoneHost(selection.transform, out Transform boneHost))
                    continue;

                if (!perBoneHostData.TryGetValue(boneHost, out (SpriteSkin[] spriteSkins, Dictionary<Transform, string> boneTransformToGuid) data))
                {
                    if (!BoneHierarchyUtilities.TryGetSpriteSkins(boneHost.gameObject, out SpriteSkin[] spriteSkins))
                        continue;

                    if (!BoneHierarchyUtilities.TryGetBoneTransformToGuid(spriteSkins, out Dictionary<Transform, string> boneTransformToGuid))
                        continue;

                    m_BoneHosts.Add(boneHost);
                    m_SpriteSkins.UnionWith(spriteSkins);

                    foreach (KeyValuePair<Transform, string> kvp in boneTransformToGuid)
                        m_BoneTransformToGuid[kvp.Key] = kvp.Value;

                    data = (spriteSkins, boneTransformToGuid);
                    perBoneHostData[boneHost] = data;
                }

                CollectTargetBones(selection, boneHost, data.spriteSkins, data.boneTransformToGuid);
            }

            BoneHierarchyUtilities.ResolveBoneColors(m_BoneHosts, out m_ResolvedBoneColors);
            BoneHierarchyEvent.RaiseContextRebuilt();
        }

        void CollectTargetBones(GameObject selection, Transform boneHost, IEnumerable<SpriteSkin> spriteSkins, Dictionary<Transform, string> boneTransformToGuid)
        {
            Transform selectionTransform = selection.transform;
            if (selectionTransform == boneHost)
            {
                foreach (SpriteSkin spriteSkin in spriteSkins)
                {
                    if (spriteSkin != null)
                        AddTargetBones(boneHost, spriteSkin.boneTransforms);
                }
            }
            else
            {
                SpriteSkin spriteSkin = selection.GetComponent<SpriteSkin>();
                if (spriteSkin != null)
                {
                    // Selection is a SpriteSkin
                    Transform[] boneTransforms = spriteSkin.boneTransforms;
                    if (boneTransforms != null && boneTransforms.Length > 0 && boneTransformToGuid.ContainsKey(boneTransforms[0]))
                        AddTargetBones(boneHost, spriteSkin.boneTransforms);
                }
                else if (boneTransformToGuid.ContainsKey(selectionTransform))
                {
                    // Selection is a bone transform
                    AddTargetBones(boneHost, new[] { selectionTransform });
                }
            }
        }

        void AddTargetBones(Transform boneHost, IEnumerable<Transform> boneTransforms)
        {
            if (!m_TargetBones.ContainsKey(boneHost))
                m_TargetBones[boneHost] = new();

            m_TargetBones[boneHost].AddRange(boneTransforms);
        }

        /// <summary>
        /// Recomputes only <see cref="ResolvedBoneColors"/> from current <see cref="BoneHosts"/>; does not change which hosts are tracked or bone layout, and raises <see cref="BoneHierarchyEvent.BoneColorsRefreshed"/> instead of <see cref="BoneHierarchyEvent.ContextRebuilt"/>.
        /// </summary>
        internal void RefreshResolvedBoneColors()
        {
            BoneHierarchyUtilities.ResolveBoneColors(m_BoneHosts, out m_ResolvedBoneColors);
            BoneHierarchyEvent.RaiseBoneColorsRefreshed();
        }
    }
}
