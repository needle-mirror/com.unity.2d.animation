using System;
using System.Collections.Generic;
using Unity.Properties;
using UnityEngine.UIElements;

namespace UnityEditor.U2D.Animation.Profiler.UI
{
    [UxmlElement]
    partial class SpriteSkinHierarchyView : VisualElement
    {
        const string k_UXML = "Packages/com.unity.2d.animation/Profiler/Editor/UI/SpriteSkinHierarchyView/SpriteSkinHierarchyView.uxml";
        MultiColumnTreeView m_Table;
        List<TreeViewItemData<SpriteSkinHierarchyNodeData>> m_Data = new();
        Label m_NoDataLabel;

        public SpriteSkinHierarchyView()
        {
            VisualTreeAsset visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_UXML);
            visualTree.CloneTree(this);
            m_Table = this.Q<MultiColumnTreeView>();
            m_NoDataLabel = this.Q<Label>("noDataLabel");
            m_Table.selectedIndicesChanged += OnSelectionChanged;
            SetupTable();
            ShowTable();
        }

        void OnSelectionChanged(IEnumerable<int> obj)
        {
            SpriteSkinHierarchyNodeData cellData = m_Table.GetItemDataForIndex<SpriteSkinHierarchyNodeData>(m_Table.selectedIndex);
            if (cellData != null)
            {
                UnityEngine.Object unityObject = EditorUtility.EntityIdToObject(cellData.entityId);
                if (unityObject != null)
                    Selection.activeObject = unityObject;
            }
        }

        void SetupTable()
        {
            if (EditorGUIUtility.isProSkin)
                m_Table.AddToClassList("dark");
            else
                m_Table.AddToClassList("light");

            m_Table.sortingMode = ColumnSortingMode.Custom;
            m_Table.columnSortingChanged += OnColumnSortingChanged;
            for (int i = 0; i < m_Table.columns.Count; ++i)
            {
                Column column = m_Table.columns[i];

                if (column.name == "Name")
                {
                    column.bindCell = (element, i) =>
                    {
                        Label label = element.Q<Label>();
                        SpriteSkinHierarchyNodeData itemData = m_Table.GetItemDataForIndex<SpriteSkinHierarchyNodeData>(i);
                        BindLabelToDataSource(label, column.bindingPath, itemData);
                        SetNameColumnCellIcon(element, itemData);
                    };
                }
                else
                {
                    column.bindCell = (element, i) =>
                    {
                        SpriteSkinHierarchyNodeData itemData = m_Table.GetItemDataForIndex<SpriteSkinHierarchyNodeData>(i);
                        Label label = element.Q<Label>();
                        BindLabelToDataSource(label, column.bindingPath, itemData);
                    };
                }

                column.unbindCell = (element, _) =>
                {
                    Label label = element.Q<Label>();
                    label.SetBinding("text", null);
                };

                column.makeCell = () =>
                {
                    VisualElement ve = new VisualElement();
                    ve.AddToClassList("cell");
                    VisualElement icon = new VisualElement() { name = "Icon" };
                    icon.AddToClassList("cell-icon");
                    Label label = new Label();
                    label.AddToClassList("cell-label");
                    ve.Add(icon);
                    ve.Add(label);
                    return ve;
                };
                column.comparison = (a, b) =>
                {
                    SpriteSkinHierarchyNodeData aData = m_Table.GetItemDataForIndex<SpriteSkinHierarchyNodeData>(a);
                    SpriteSkinHierarchyNodeData bData = m_Table.GetItemDataForIndex<SpriteSkinHierarchyNodeData>(b);
                    return SpriteSkinHierarchyNodeData.Compare(aData, bData, column.bindingPath);
                };
            }
        }

        void OnColumnSortingChanged()
        {
            if (m_Table.sortedColumns != null)
            {
                List<TreeViewItemData<SpriteSkinHierarchyNodeData>> sortedData = new();
                foreach (TreeViewItemData<SpriteSkinHierarchyNodeData> child in m_Data)
                {
                    List<TreeViewItemData<SpriteSkinHierarchyNodeData>> children = new();
                    if (child.children != null)
                    {
                        foreach (TreeViewItemData<SpriteSkinHierarchyNodeData> c in child.children)
                        {
                            children.Add(c);
                        }
                        children.Sort(SortData);
                    }

                    sortedData.Add(new TreeViewItemData<SpriteSkinHierarchyNodeData>(child.id, child.data, children));
                }

                sortedData.Sort(SortData);
                m_Data.Clear();
                m_Data = sortedData;

                m_Table.Clear();
                HashSet<int> expandedIds = new();
                foreach (TreeViewItemData<SpriteSkinHierarchyNodeData> d in m_Data)
                {
                    if (m_Table.IsExpanded(d.id))
                        expandedIds.Add(d.id);
                }

                m_Table.SetRootItems(m_Data);
                m_Table.Rebuild();
                foreach (int id in expandedIds)
                {
                    m_Table.ExpandItem(id);
                }

                ShowTable();
            }
        }

        int SortData(TreeViewItemData<SpriteSkinHierarchyNodeData> a, TreeViewItemData<SpriteSkinHierarchyNodeData> b)
        {
            using (IEnumerator<SortColumnDescription> enumerator = m_Table.sortedColumns.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    int result = SpriteSkinHierarchyNodeData.Compare(a.data, b.data, enumerator.Current.column.bindingPath);
                    if (result != 0)
                        return result * (enumerator.Current.direction == SortDirection.Ascending ? 1 : -1);
                }
            }

            return SpriteSkinHierarchyNodeData.Compare(a.data, b.data, null);
        }

        void BindLabelToDataSource(Label label, string path, SpriteSkinHierarchyNodeData cellData)
        {
            label.SetBinding("text", new DataBinding { dataSourcePath = new PropertyPath(path), bindingMode = BindingMode.ToTarget, dataSource = cellData });
        }

        void SetNameColumnCellIcon(VisualElement ele, SpriteSkinHierarchyNodeData data)
        {
            VisualElement icon = ele.Q("Icon");
            icon.RemoveFromClassList("gameObject-icon");
            icon.RemoveFromClassList("spriteSkin-icon");
            if (data.icon?.Length > 0)
                icon.AddToClassList(data.icon);
        }

        public void SetData(IEnumerable<SpriteSkinHierarchyNodeData> values)
        {
            m_Table.Clear();
            HashSet<int> expandedIds = new();
            foreach (TreeViewItemData<SpriteSkinHierarchyNodeData> d in m_Data)
            {
                if (m_Table.IsExpanded(d.id))
                    expandedIds.Add(d.id);
            }

            m_Data.Clear();
            foreach (SpriteSkinHierarchyNodeData node in values)
            {
                List<TreeViewItemData<SpriteSkinHierarchyNodeData>> children = null;
                if (node.children != null)
                {
                    children = new();
                    foreach (SpriteSkinHierarchyNodeData child in node.children)
                    {
                        child.icon = "spriteSkin-icon";
                        children.Add(new TreeViewItemData<SpriteSkinHierarchyNodeData>(child.id, child));
                    }

                    if (m_Table.sortedColumns != null)
                    {
                        children.Sort(SortData);
                    }
                }

                node.icon = "gameObject-icon";
                m_Data.Add(new TreeViewItemData<SpriteSkinHierarchyNodeData>(node.id, node, children));
            }

            if (m_Table.sortedColumns != null)
            {
                m_Data.Sort(SortData);
            }

            m_Table.SetRootItems(m_Data);
            m_Table.Rebuild();
            foreach (int id in expandedIds)
            {
                m_Table.ExpandItem(id);
            }

            ShowTable();
        }

        void ShowTable()
        {
            if (m_Data.Count == 0)
            {
                m_Table.style.display = DisplayStyle.None;
                m_NoDataLabel.style.display = DisplayStyle.Flex;
            }
            else
            {
                m_Table.style.display = DisplayStyle.Flex;
                m_NoDataLabel.style.display = DisplayStyle.None;
            }
        }
    }
}
