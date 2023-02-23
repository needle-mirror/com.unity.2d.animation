using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D.Animation;
using UnityEngine.UIElements;

namespace UnityEditor.U2D.Animation.SceneOverlays
{
    class SpriteResolverSelector : VisualElement
    {
        static class Styles
        {
            public const string spriteResolverNameLabel = SpriteResolverOverlay.rootStyle + "__resolver-name-label";
            public const string categoryAndLabelNameContainer = SpriteResolverOverlay.rootStyle + "__category-and-label-name-container";
            public const string selector = SpriteResolverOverlay.rootStyle + "__selector";
            public const string descriptionLabel = SpriteResolverOverlay.rootStyle + "__label-description";
        }

        Label m_SpriteResolverLabel;

        INavigableElement m_CategoryContainer;
        INavigableElement m_LabelContainer;
        INavigableElement m_CurrentSelection;
        
        Label m_LabelNameLabel;

        string m_Category = string.Empty;
        string m_Label = string.Empty;

        List<string> m_AvailableCategories;
        List<Tuple<string, Sprite>> m_AvailableLabels;

        SpriteResolver m_SpriteResolver;

        public SpriteResolverSelector(INavigableElement categoryContainer, INavigableElement labelContainer)
        {
            focusable = true;
            AddToClassList(Styles.selector);

            m_SpriteResolverLabel = new Label();
            m_SpriteResolverLabel.AddToClassList(Styles.spriteResolverNameLabel);
            Add(m_SpriteResolverLabel);

            var categoryAndLabelNameHolder = new VisualElement();
            categoryAndLabelNameHolder.AddToClassList(Styles.categoryAndLabelNameContainer);
            Add(categoryAndLabelNameHolder);

            m_CategoryContainer = categoryContainer;
            m_CategoryContainer.onSelectionChange += OnCategorySelected;
            var categoryContainerVisual = m_CategoryContainer.visualElement;
            categoryContainerVisual.RegisterCallback<FocusInEvent>(OnFocusIn);
            categoryContainerVisual.RegisterCallback<FocusOutEvent>(OnFocusOut);
            categoryAndLabelNameHolder.Add(categoryContainerVisual);

            m_LabelNameLabel = new Label();
            m_LabelNameLabel.AddToClassList(Styles.descriptionLabel);
            categoryAndLabelNameHolder.Add(m_LabelNameLabel);

            m_LabelContainer = labelContainer;
            m_LabelContainer.onSelectionChange += OnLabelSelected;
            var labelContainerVisual = m_LabelContainer.visualElement;
            labelContainerVisual.RegisterCallback<FocusInEvent>(OnFocusIn);
            labelContainerVisual.RegisterCallback<FocusOutEvent>(OnFocusOut);
            Add(labelContainerVisual);

            RegisterCallback<KeyDownEvent>(OnKeyDown);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
        }

        public void Select()
        {
            m_LabelContainer.visualElement.Focus();
        }

        void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            SetSpriteResolver(null);
        }

        public void SetSpriteResolver(SpriteResolver spriteResolver)
        {
            if (m_SpriteResolver != null)
                m_SpriteResolver.onResolvedSprite -= OnResolvedSprite;

            m_SpriteResolver = spriteResolver;
            if (m_SpriteResolver == null)
                return;

            m_SpriteResolver.onResolvedSprite += OnResolvedSprite;
            ReadCategoryAndLabelFromSelection();
        }

        void OnResolvedSprite(SpriteResolver spriteResolver)
        {
            ReadCategoryAndLabelFromSelection();
        }

        void ReadCategoryAndLabelFromSelection()
        {
            if (m_SpriteResolver == null)
                return;

            m_Category = m_SpriteResolver.GetCategory() ?? string.Empty;
            m_Label = m_SpriteResolver.GetLabel() ?? string.Empty;

            UpdateVisuals();
        }

        void UpdateVisuals()
        {
            if (m_SpriteResolver == null)
                return;

            m_SpriteResolverLabel.text = m_SpriteResolverLabel.tooltip = m_SpriteResolver.name;

            m_AvailableCategories = GetAvailableCategories(m_SpriteResolver) ?? new List<string>();
            m_AvailableLabels = new List<Tuple<string, Sprite>>();
            if (m_SpriteResolver.spriteLibrary != null)
            {
                foreach (var labelName in GetAvailableLabels(m_SpriteResolver, m_Category))
                    m_AvailableLabels.Add(new Tuple<string, Sprite>(labelName, m_SpriteResolver.spriteLibrary.GetSprite(m_Category, labelName)));
            }

            m_CategoryContainer.SetItems(m_AvailableCategories);
            m_CategoryContainer.Select(m_AvailableCategories.IndexOf(m_Category));

            m_LabelContainer.SetItems(m_AvailableLabels);
            m_LabelContainer.Select(m_AvailableLabels.FindIndex(label => label.Item1 == m_Label));

            m_Label = !string.IsNullOrWhiteSpace(m_Label) ? m_Label : TextContent.emptyCategory;
            m_LabelNameLabel.text = m_LabelNameLabel.tooltip = m_Label;
            m_LabelNameLabel.SetEnabled(m_AvailableLabels.Count > 0);

            if (m_LabelContainer.itemCount == 0)
                m_CurrentSelection = m_CategoryContainer;
        }

        internal void OnCategorySelected(int newSelection)
        {
            if (m_SpriteResolver == null)
                return;

            var categoryName = (string)m_CategoryContainer.GetItem(newSelection);
            if (categoryName == null || categoryName == m_Category)
                return;

            var availableLabels = m_SpriteResolver.spriteLibrary != null ? m_SpriteResolver.spriteLibrary.GetEntryNames(categoryName) : null;
            var labelList = availableLabels != null ? new List<string>(availableLabels) : new List<string>();
            var labelName = string.Empty;
            if (labelList.Count > 0)
                labelName = labelList.Contains(m_Label) ? m_Label : labelList[0];

            m_SpriteResolver.SetCategoryAndLabelEditor(categoryName, labelName);
        }

        internal void OnLabelSelected(int newSelection)
        {
            if (m_SpriteResolver == null)
                return;

            var (labelName, _) = (Tuple<string, Sprite>)m_LabelContainer.GetItem(newSelection);
            if (string.IsNullOrWhiteSpace(labelName) || labelName == m_Label)
                return;

            m_SpriteResolver.SetCategoryAndLabelEditor(m_Category, labelName);
            m_LabelNameLabel.text = m_LabelNameLabel.tooltip = labelName;
        }

        void OnKeyDown(KeyDownEvent evt)
        {
            if (m_CurrentSelection == null)
                return;

            switch (evt.keyCode)
            {
                case KeyCode.LeftArrow:
                    var previousIndex = m_CurrentSelection.selectedIndex - 1;
                    if (previousIndex < 0)
                        previousIndex += m_CurrentSelection.itemCount;
                    m_CurrentSelection.Select(previousIndex);
                    evt.StopPropagation();
                    break;
                case KeyCode.RightArrow:
                    var nextIndex = m_CurrentSelection.selectedIndex + 1;
                    if (nextIndex >= m_CurrentSelection.itemCount)
                        nextIndex = 0;
                    m_CurrentSelection?.Select(nextIndex);
                    evt.StopPropagation();
                    break;
                case KeyCode.DownArrow:
                case KeyCode.UpArrow:
                    evt.StopPropagation();
                    break;
            }
        }

        void OnFocusIn(FocusInEvent evt)
        {
            var navigable = (INavigableElement)evt.currentTarget;
            if (navigable != null)
                m_CurrentSelection = navigable;
        }

        void OnFocusOut(FocusOutEvent evt)
        {
            var navigable = (INavigableElement)evt.currentTarget;
            if (navigable != null && m_CurrentSelection == navigable)
                m_CurrentSelection = null;
        }

        static List<string> GetAvailableCategories(SpriteResolver spriteResolver)
        {
            if (spriteResolver == null || spriteResolver.spriteLibrary == null)
                return new List<string>();

            var availableCategories = spriteResolver.spriteLibrary.categoryNames;
            return availableCategories != null ? new List<string>(availableCategories) : new List<string>();
        }

        static List<string> GetAvailableLabels(SpriteResolver spriteResolver, string categoryName)
        {
            if (spriteResolver == null || spriteResolver.spriteLibrary == null || string.IsNullOrEmpty(categoryName))
                return new List<string>();

            var availableLabels = spriteResolver.spriteLibrary.GetEntryNames(categoryName);
            return availableLabels != null ? new List<string>(availableLabels) : new List<string>();
        }
    }
}
