using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace UnityEditor.U2D.Animation
{
    internal class BoneTreeWidgetController
    {
        protected IBoneTreeViewModel m_Model;
        protected SkinningEvents m_SkinningEvents;

        public BoneTreeWidgetController(IBoneTreeViewModel model, SkinningEvents eventSystem)
        {
            m_Model = model;
            m_SkinningEvents = eventSystem;
        }

        public void Activate()
        {
            SetupSkeleton();

            m_SkinningEvents.selectedSpriteChanged.AddListener(OnSelectionChange);
            m_SkinningEvents.skinningModeChanged.AddListener(OnSkinningModuleModeChanged);
            m_SkinningEvents.boneSelectionChanged.AddListener(OnBoneSelectionChanged);
            m_SkinningEvents.boneNameChanged.AddListener(OnBoneNameChanged);
            m_SkinningEvents.skeletonTopologyChanged.AddListener(SkeletonTopologyChanged);
        }

        public void Deactivate()
        {
            m_SkinningEvents.selectedSpriteChanged.RemoveListener(OnSelectionChange);
            m_SkinningEvents.skinningModeChanged.RemoveListener(OnSkinningModuleModeChanged);
            m_SkinningEvents.boneSelectionChanged.RemoveListener(OnBoneSelectionChanged);
            m_SkinningEvents.boneNameChanged.RemoveListener(OnBoneNameChanged);
            m_SkinningEvents.skeletonTopologyChanged.RemoveListener(SkeletonTopologyChanged);
            if (m_Model.view != null)
                m_Model.view.Deactivate();
        }

        private void OnSelectionChange(SpriteCache sprite)
        {
            SetupSkeleton();
            m_Model.SetAllVisibility(null, true);
        }

        private void OnBoneSelectionChanged()
        {
            m_Model.view.OnBoneSelectionChange(m_Model.GetBoneSelection());
        }

        private void OnBoneNameChanged(BoneCache bone)
        {
            m_Model.view.OnBoneNameChanged(bone);
        }

        private void OnSkinningModuleModeChanged(SkinningMode mode)
        {
            SetupSkeleton();
            m_Model.SetAllVisibility(null, true);
        }

        private void SetupSkeleton()
        {
            m_Model.view.OnSelectionChange(m_Model.GetSelectedSkeleton());
            m_Model.view.OnBoneExpandedChange(m_Model.GetExpandedBones());
            m_Model.view.OnBoneSelectionChange(m_Model.GetBoneSelection());
        }

        public void SetAllVisibility(bool visibility)
        {
            SkeletonCache skeleton = m_Model.GetSelectedSkeleton();
            if (skeleton != null)
            {
                using (m_Model.UndoScope(TextContent.boneVisibility))
                {
                    m_Model.SetAllVisibility(skeleton, visibility);
                }
            }
        }

        private void SkeletonTopologyChanged(SkeletonCache skeleton)
        {
            m_Model.view.OnSelectionChange(skeleton);
        }

        public List<TreeViewItem> BuildTreeView()
        {
            List<TreeViewItem> rows = new List<TreeViewItem>();
            SkeletonCache skeleton = m_Model.GetSelectedSkeleton();
            if (skeleton != null)
            {
                BoneCache[] bones = skeleton.bones;
                BoneCache[] children = bones.Where(x => x.parentBone == null).ToArray();
                Array.Sort(children, (a, b) => a.siblingIndex.CompareTo(b.siblingIndex));

                foreach (BoneCache bone in children)
                    AddTreeViewItem(rows, bone, bones, 0);
            }
            return rows;
        }

        private static void AddTreeViewItem(IList<TreeViewItem> rows, BoneCache bone, BoneCache[] bones, int depth)
        {
            TreeViewItemBase<BoneCache> item = new TreeViewItemBase<BoneCache>(bone.GetInstanceID(), depth, bone.name, bone);
            rows.Add(item);

            BoneCache[] children = bones.Where(x => x.parentBone == bone).ToArray();
            Array.Sort(children, (a, b) => a.siblingIndex.CompareTo(b.siblingIndex));

            foreach (BoneCache childBone in children)
                AddTreeViewItem(rows, childBone, bones, depth + 1);
        }

        public List<int> GetIDsToExpand(BoneCache[] bones)
        {
            List<int> result = new List<int>();
            if (bones != null)
            {
                foreach (BoneCache bone in bones)
                {
                    if (bone != null)
                    {
                        BoneCache parent = bone.parentBone;
                        while (parent != null)
                        {
                            int parentId = parent.GetInstanceID();
                            result.Add(parentId);
                            parent = parent.parentBone;
                        }
                    }
                }
            }
            return result;
        }

        public int[] GetIDsToSelect(BoneCache[] bones)
        {
            return bones == null ? new int[0] : Array.ConvertAll(bones, x => x != null ? x.GetInstanceID() : 0);
        }

        public void SelectBones(IList<int> selectedIds, IList<TreeViewItem> items)
        {
            BoneCache[] selectedBones = items.Where(x => selectedIds.Contains(x.id)).Select(y => ((TreeViewItemBase<BoneCache>)y).customData).ToArray();
            using (m_Model.UndoScope(TextContent.boneSelection))
            {
                m_Model.SelectBones(selectedBones);
                m_SkinningEvents.boneSelectionChanged.Invoke();
            }
        }

        public void ExpandBones(IList<int> expandedIds, IList<TreeViewItem> items)
        {
            BoneCache[] expandedBones = items.Where(x => expandedIds.Contains(x.id)).Select(y => ((TreeViewItemBase<BoneCache>)y).customData).ToArray();
            using (m_Model.UndoScope(TextContent.expandBones))
            {
                m_Model.SetExpandedBones(expandedBones);
            }
        }

        public bool GetTreeItemVisibility(TreeViewItemBase<BoneCache> item)
        {
            return m_Model.GetVisibility(item.customData);
        }

        public void SetTreeItemVisibility(TreeViewItemBase<BoneCache> item, bool visible, bool includeChildren)
        {
            BoneCache bone = item.customData;
            if (bone != null && bone.isVisible != visible)
            {
                using (m_Model.UndoScope(TextContent.visibilityChange))
                {
                    m_Model.SetVisibility(item.customData, visible);
                    if (includeChildren)
                    {
                        // toggle all children as well
                        SetChildrenVisibility(item, visible);
                    }
                }
            }
        }

        void SetChildrenVisibility(TreeViewItemBase<BoneCache> bone, bool visible)
        {
            if (bone.children == null)
                return;
            foreach (TreeViewItem childBone in bone.children)
            {
                TreeViewItemBase<BoneCache> cb = childBone as TreeViewItemBase<BoneCache>;
                SetChildrenVisibility(cb, visible);
                m_Model.SetVisibility(cb.customData, visible);
            }
        }

        public int GetTreeItemDepthValue(TreeViewItemBase<BoneCache> bone)
        {
            return m_Model.GetDepth(bone.customData);
        }

        public void SetTreeItemDepthValue(TreeViewItemBase<BoneCache> bone, int value)
        {
            if (bone != null && bone.customData != null)
            {
                using (m_Model.UndoScope(TextContent.boneDepth))
                {
                    m_Model.SetDepth(bone.customData, value);
                    m_SkinningEvents.boneDepthChanged.Invoke(bone.customData);
                }
            }
        }

        public Color GetTreeItemColorValue(TreeViewItemBase<BoneCache> bone)
        {
            return m_Model.GetBoneColor(bone.customData);
        }

        public void SetTreeItemColorValue(TreeViewItemBase<BoneCache> bone, Color color)
        {
            if (bone != null && bone.customData != null)
            {
                using (m_Model.UndoScope(TextContent.boneColor))
                {
                    m_Model.SetBoneColor(bone.customData, color);
                    m_SkinningEvents.boneColorChanged.Invoke(bone.customData);
                }
            }
        }

        public void SetTreeViewBoneName(IList<TreeViewItem> items, BoneCache bone)
        {
            TreeViewItem treeBone = items.FirstOrDefault(x => ((TreeViewItemBase<BoneCache>)x).customData == bone);
            if (treeBone != null)
                treeBone.displayName = bone.name;
        }

        public void TreeViewItemRename(IList<TreeViewItem> rows, int itemID, string newName)
        {
            TreeViewItemBase<BoneCache> item = rows.FirstOrDefault(x => x.id == itemID) as TreeViewItemBase<BoneCache>;

            if (item == null)
                return;

            if (item.customData != null && item.customData.name != newName && !string.IsNullOrEmpty(newName)
                && !string.IsNullOrWhiteSpace(newName))
            {
                item.displayName = newName;
                using (m_Model.UndoScope(TextContent.boneName))
                {
                    m_Model.SetName(item.customData, newName);
                    m_SkinningEvents.boneNameChanged.Invoke(item.customData);
                }
            }
        }

        public bool CanReparent(TreeViewItemBase<BoneCache> parent, List<TreeViewItem> draggedItems)
        {
            TreeViewItemBase<BoneCache> currentParent = parent;
            while (currentParent != null)
            {
                if (draggedItems.Contains(currentParent))
                    return false;
                currentParent = currentParent.parent as TreeViewItemBase<BoneCache>;
            }
            return true;
        }

        public void ReparentItems(TreeViewItemBase<BoneCache> newParent, List<TreeViewItem> draggedItems, int insertAtIndex)
        {
            if ((m_Model.hasCharacter && m_Model.mode != SkinningMode.Character) ||
                (!m_Model.hasCharacter && m_Model.mode == SkinningMode.Character))
                return;

            BoneCache parent = newParent != null ? newParent.customData : null;
            using (m_Model.UndoScope(TextContent.setParentBone))
            {
                for (int i = draggedItems.Count - 1; i >= 0; --i)
                {
                    BoneCache bone = ((TreeViewItemBase<BoneCache>)draggedItems[i]).customData;

                    // Do not reparent if the parent is unchanged.
                    // This prevents unnecessary operations and avoids creating redundant undo steps.
                    if (bone.parent == parent)
                        continue;

                    m_Model.SetBoneParent(parent, bone, insertAtIndex);
                    m_SkinningEvents.skeletonTopologyChanged.Invoke(bone.skeleton);
                }
            }
        }

        public virtual bool CanDrag()
        {
            m_SkinningEvents.boneVisibility.Invoke("drag");
            return false;
        }

        public virtual bool CanRename()
        {
            m_SkinningEvents.boneVisibility.Invoke("rename");
            return false;
        }
    }
}
