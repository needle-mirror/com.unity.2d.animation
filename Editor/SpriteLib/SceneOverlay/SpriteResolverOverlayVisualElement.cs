using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D.Animation;
using UnityEngine.UIElements;

namespace UnityEditor.U2D.Animation.SceneOverlays
{
    internal class SpriteResolverOverlayVisualElement : VisualElement
    {
        static class Styles
        {
            public const string selectorList = SpriteResolverOverlay.rootStyle + "__selector-list";
            public static string infoLabelHolder = SpriteResolverOverlay.rootStyle + "__info-label-holder";
            public static string infoLabel = SpriteResolverOverlay.rootStyle + "__info-label";
        }

        VisualElement m_InfoLabelHolder;
        Label m_InfoLabel;
        ListView m_ListView;
        OverlayToolbar m_OverlayToolbar;

        public SpriteResolverOverlayVisualElement()
        {
            AddToClassList(SpriteResolverOverlay.rootStyle);

            m_InfoLabelHolder = new VisualElement();
            m_InfoLabelHolder.AddToClassList(Styles.infoLabelHolder);
            m_InfoLabel = new Label { text = TextContent.selectSpriteResolver };
            m_InfoLabel.AddToClassList(Styles.infoLabel);
            m_InfoLabelHolder.Add(m_InfoLabel);
            Add(m_InfoLabelHolder);

            m_ListView = new ListView { virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight };
            m_ListView.makeItem += MakeItem;
            m_ListView.bindItem += BindItem;
            m_ListView.unbindItem += UnbindItem;
            m_ListView.selectionChanged += OnSelectionChanged;
            m_ListView.AddToClassList(Styles.selectorList);
            Add(m_ListView);

            m_OverlayToolbar = new OverlayToolbar();
            Add(m_OverlayToolbar);
        }

        static VisualElement MakeItem() => new SpriteResolverSelector(new CategoryContainer(), new LabelContainer());

        void BindItem(VisualElement visualElement, int i)
        {
            if (m_ListView.itemsSource == null || m_ListView.itemsSource.Count <= i)
                return;

            var resolverSelector = (SpriteResolverSelector)visualElement;
            resolverSelector.SetSpriteResolver((SpriteResolver)m_ListView.itemsSource[i]);
        }

        void UnbindItem(VisualElement visualElement, int i)
        {
            if (m_ListView.itemsSource == null || m_ListView.itemsSource.Count <= i)
                return;

            var resolverSelector = (SpriteResolverSelector)visualElement;
            resolverSelector.SetSpriteResolver(null);
        }

        void OnSelectionChanged(IEnumerable<object> obj)
        {
            var index = m_ListView.selectedIndex;
            if (index == -1)
                return;

            var selector = (SpriteResolverSelector)m_ListView.GetRootElementForIndex(index);
            selector?.Select();
        }

        public void SetSpriteResolvers(SpriteResolver[] selection)
        {
            var isListVisible = selection is { Length: > 0 };

            m_InfoLabelHolder.style.display = isListVisible ? DisplayStyle.None : DisplayStyle.Flex;
            m_ListView.style.display = isListVisible ? DisplayStyle.Flex : DisplayStyle.None;

            if (isListVisible)
            {
                m_ListView.selectedIndex = -1;
                m_ListView.itemsSource = selection;
                m_ListView.Rebuild();
            }
        }
    }
}
