using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UnityEngine.UIElements;
#if UNITY_6000_2_OR_NEWER
using TreeViewItem = UnityEditor.IMGUI.Controls.TreeViewItem<int>;
using TreeViewState = UnityEditor.IMGUI.Controls.TreeViewState<int>;
#else
using TreeViewItem = UnityEditor.IMGUI.Controls.TreeViewItem;
using TreeViewState = UnityEditor.IMGUI.Controls.TreeViewState;
#endif

namespace UnityEditor.U2D.Animation
{
    internal class BoneVisibilityTool : BoneTreeWidgetModel, IVisibilityTool
    {
        private BoneTreeWidgetController m_Controller;

        VisualElement IVisibilityTool.view
        {
            get { return (VisualElement)m_View; }
        }

        public string name => TextContent.bone;
        public string tooltip => TextContent.boneTooltip;

        public bool isAvailable
        {
            get { return true; }
        }

        public BoneVisibilityTool(SkinningCache s)
        {
            m_SkinningCache = s;
        }

        public void Setup()
        {
            m_Data = skinningCache.CreateCache<BoneVisibilityToolData>();
            m_Controller = new BoneReparentToolController(this, skinningCache.events); //new BoneTreeWidgetController(this, skinningCache.events);
            m_View = new BoneReparentToolView()
            {
                GetModel = () => this,
                GetController = () => m_Controller
            };
        }

        public void Dispose() { }

        public void Activate()
        {
            m_Controller.Activate();
            if (m_Data.previousVisiblity != m_Data.allVisibility)
            {
                m_Data.previousVisiblity = m_Data.allVisibility;
            }
        }

        public void Deactivate()
        {
            m_Controller.Deactivate();
        }

        public void SetAvailabilityChangeCallback(Action callback) { }
    }


    internal class BoneVisibilityToolView : VisibilityToolViewBase, IBoneVisibilityToolView
    {
        public Func<BoneTreeWidgetController> GetController = () => null;
        public Func<IBoneTreeViewModel> GetModel = () => null;

        public BoneVisibilityToolView()
        {
            m_TreeView = new BoneTreeView(m_TreeViewState, SetupToolColumnHeader())
            {
                GetController = InternalGetController
            };
            SetupSearchField();
        }

        protected virtual VisibilityToolColumnHeader SetupToolColumnHeader()
        {
            MultiColumnHeaderState.Column[] columns = new MultiColumnHeaderState.Column[2];
            columns[0] = new MultiColumnHeaderState.Column
            {
                headerContent = VisibilityTreeViewBase.VisibilityIconStyle.visibilityOnIcon,
                headerTextAlignment = TextAlignment.Center,
                width = 32,
                minWidth = 32,
                maxWidth = 32,
                autoResize = false,
                allowToggleVisibility = true
            };
            columns[1] = new MultiColumnHeaderState.Column
            {
                headerContent = new GUIContent(TextContent.bone),
                headerTextAlignment = TextAlignment.Center,
                width = 200,
                minWidth = 130,
                autoResize = true,
                allowToggleVisibility = false
            };
            MultiColumnHeaderState multiColumnHeaderState = new MultiColumnHeaderState(columns);
            return new VisibilityToolColumnHeader(multiColumnHeaderState)
            {
                GetAllVisibility = GetAllVisibility,
                SetAllVisibility = SetAllVisibility,
                canSort = false,
                height = 20,
                visibilityColumn = 0
            };
        }

        BoneTreeWidgetController InternalGetController()
        {
            return GetController();
        }

        protected void SetAllVisibility(bool visibility)
        {
            GetController().SetAllVisibility(visibility);
        }

        protected bool GetAllVisibility()
        {
            return GetModel() != null && GetModel().GetAllVisibility();
        }

        public void OnSelectionChange(SkeletonCache skeleton)
        {
            ((BoneTreeView)m_TreeView).SetupHierarchy();
        }

        public void OnBoneSelectionChange(SkeletonSelection bones)
        {
            ((BoneTreeView)m_TreeView).OnBoneSelectionChanged(bones);
        }

        public void OnBoneExpandedChange(BoneCache[] bones)
        {
            ((BoneTreeView)m_TreeView).OnBoneExpandedChanged(bones);
        }

        public void OnBoneNameChanged(BoneCache bone)
        {
            ((BoneTreeView)m_TreeView).OnBoneNameChanged(bone);
        }

        public void Deactivate()
        {
            if (m_TreeView.HasSelection())
                m_TreeView.EndRename();
        }
    }

    class BoneTreeView : VisibilityTreeViewBase
    {
        public Func<BoneTreeWidgetController> GetController = () => null;

        public BoneTreeView(TreeViewState treeViewState, MultiColumnHeader columnHeader)
            : base(treeViewState, columnHeader)
        {
            columnIndexForTreeFoldouts = 1;
            ReloadView();
        }

        public void SetupHierarchy()
        {
            ReloadView();
        }

        private void ReloadView()
        {
            Reload();
        }

        public void OnBoneSelectionChanged(SkeletonSelection boneSelection)
        {
            BoneCache[] bones = boneSelection.elements.ToSpriteSheetIfNeeded();
            int[] ids = GetController().GetIDsToSelect(bones);

            SetSelection(ids, TreeViewSelectionOptions.RevealAndFrame);
        }

        public void OnBoneExpandedChanged(BoneCache[] bones)
        {
            int[] expandIds = GetController().GetIDsToSelect(bones);
            if (expandIds.Length == 0)
                return;

            SetExpanded(expandIds.Union(GetExpanded()).ToList());
        }

        public void OnBoneNameChanged(BoneCache bone)
        {
            GetController().SetTreeViewBoneName(GetRows(), bone);
        }

        protected override void SelectionChanged(IList<int> selectedIds)
        {
            GetController().SelectBones(selectedIds, GetRows());
        }

        protected override void ExpandedStateChanged()
        {
            GetController().ExpandBones(GetExpanded(), GetRows());
        }

        protected override float GetCustomRowHeight(int row, TreeViewItem item)
        {
            return EditorGUIUtility.singleLineHeight * 1.1f;
        }

        void CellGUI(Rect cellRect, TreeViewItem item, int column, ref RowGUIArgs args)
        {
            CenterRectUsingSingleLineHeight(ref cellRect);
            switch (column)
            {
                case 0:
                    DrawVisibilityCell(cellRect, item);
                    break;
                case 1:
                    DrawNameCell(cellRect, item, ref args);
                    break;
                case 2:
                    DrawDepthCell(cellRect, item);
                    break;
                case 3:
                    DrawColorCell(cellRect, item);
                    break;
            }
        }

        void DrawDepthCell(Rect cellRect, TreeViewItem item)
        {
            TreeViewItemBase<BoneCache> boneItemView = DrawCell(cellRect, item);

            EditorGUI.BeginChangeCheck();
            int depth = GetController().GetTreeItemDepthValue(boneItemView);
            depth = EditorGUI.IntField(cellRect, depth);
            if (EditorGUI.EndChangeCheck())
                GetController().SetTreeItemDepthValue(boneItemView, depth);
        }

        void DrawColorCell(Rect cellRect, TreeViewItem item)
        {
            TreeViewItemBase<BoneCache> boneItemView = DrawCell(cellRect, item);

            EditorGUI.BeginChangeCheck();
            Color color = GetController().GetTreeItemColorValue(boneItemView);
            color = EditorGUI.ColorField(cellRect, color);
            if (EditorGUI.EndChangeCheck())
                GetController().SetTreeItemColorValue(boneItemView, color);
        }

        static TreeViewItemBase<BoneCache> DrawCell(Rect cellRect, TreeViewItem item)
        {
            const int width = 30;

            TreeViewItemBase<BoneCache> boneItemView = item as TreeViewItemBase<BoneCache>;
            cellRect.height = EditorGUIUtility.singleLineHeight;
            cellRect.x += (cellRect.width - width) * 0.5f;
            cellRect.width = width;

            return boneItemView;
        }

        void DrawVisibilityCell(Rect cellRect, TreeViewItem item)
        {
            GUIStyle style = MultiColumnHeader.DefaultStyles.columnHeaderCenterAligned;
            EditorGUI.BeginChangeCheck();
            TreeViewItemBase<BoneCache> boneItemView = item as TreeViewItemBase<BoneCache>;
            bool visible = GetController().GetTreeItemVisibility(boneItemView);
            visible = GUI.Toggle(cellRect, visible, visible ? VisibilityIconStyle.visibilityOnIcon : VisibilityIconStyle.visibilityOffIcon, style);
            if (EditorGUI.EndChangeCheck())
            {
                GetController().SetTreeItemVisibility(boneItemView, visible, Event.current.alt);
            }
        }

        void DrawNameCell(Rect cellRect, TreeViewItem item, ref RowGUIArgs args)
        {
            args.rowRect = cellRect;
            base.RowGUI(args);
        }

        protected override Rect GetRenameRect(Rect rowRect, int row, TreeViewItem item)
        {
            Rect cellRect = GetCellRectForTreeFoldouts(rowRect);
            CenterRectUsingSingleLineHeight(ref cellRect);
            return base.GetRenameRect(cellRect, row, item);
        }

        protected override void RowGUI(RowGUIArgs args)
        {
            TreeViewItem item = args.item;

            for (int i = 0; i < args.GetNumVisibleColumns(); ++i)
            {
                CellGUI(args.GetCellRect(i), item, args.GetColumn(i), ref args);
            }
        }

        protected override TreeViewItem BuildRoot()
        {
            TreeViewItem root = new TreeViewItem { id = 0, depth = -1, displayName = "Root" };
            List<TreeViewItem> rows = GetController() != null ? GetController().BuildTreeView() : new List<TreeViewItem>();
            SetupParentsAndChildrenFromDepths(root, rows);
            return root;
        }

        protected override bool CanRename(TreeViewItem item)
        {
            return GetController().CanRename();
        }

        protected override void RenameEnded(RenameEndedArgs args)
        {
            IList<TreeViewItem> rows = GetRows();
            GetController().TreeViewItemRename(rows, args.itemID, args.newName);
            base.RenameEnded(args);
        }

        // dragging
        const string k_GenericDragID = "GenericDragColumnDragging";

        protected override bool CanStartDrag(CanStartDragArgs args)
        {
            return true;
        }

        protected override void SetupDragAndDrop(SetupDragAndDropArgs args)
        {
            if (GetController().CanDrag() && !hasSearch)
            {
                DragAndDrop.PrepareStartDrag();
                List<TreeViewItem> draggedRows = GetRows().Where(item => args.draggedItemIDs.Contains(item.id)).ToList();
                DragAndDrop.SetGenericData(k_GenericDragID, draggedRows);
                DragAndDrop.objectReferences = new UnityEngine.Object[] { }; // this IS required for dragging to work
                string title = draggedRows.Count == 1 ? draggedRows[0].displayName : "< Multiple >";
                DragAndDrop.StartDrag(title);
            }
        }

        protected override DragAndDropVisualMode HandleDragAndDrop(DragAndDropArgs args)
        {
            // Check if we can handle the current drag data (could be dragged in from other areas/windows in the editor)
            List<TreeViewItem> draggedRows = DragAndDrop.GetGenericData(k_GenericDragID) as List<TreeViewItem>;
            if (draggedRows == null)
                return DragAndDropVisualMode.None;

            // Parent item is null when dragging outside any tree view items.
            switch (args.dragAndDropPosition)
            {
                case DragAndDropPosition.UponItem:
                case DragAndDropPosition.OutsideItems:
                case DragAndDropPosition.BetweenItems:
                {
                    TreeViewItemBase<BoneCache> newParent = args.parentItem as TreeViewItemBase<BoneCache>;
                    bool validDrag = false;
                    validDrag = GetController().CanReparent(newParent, draggedRows);
                    if (args.performDrop && validDrag)
                    {
                        GetController().ReparentItems(newParent, draggedRows, args.insertAtIndex);
                        Reload();
                        List<int> selectedIDs = draggedRows.ConvertAll(b => b.id);
                        SetSelection(selectedIDs, TreeViewSelectionOptions.RevealAndFrame);
                        SelectionChanged(selectedIDs);
                    }

                    return validDrag ? DragAndDropVisualMode.Move : DragAndDropVisualMode.None;
                }
            }

            return DragAndDropVisualMode.None;
        }
    }
}
