using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityEditor.U2D.Animation.SceneOverlays
{
    internal class LabelContainer : VisualElement, INavigableElement
    {
        static class Styles
        {
            public const string labelVisual = SpriteSwapOverlay.rootStyle + "__label-visual";
            public const string labelSelected = SpriteSwapOverlay.rootStyle + "__label-selected";
        }

        public event Action<int> onSelectionChange;

        public int itemCount => m_LabelImagesInternalContainer.childCount;
        public int selectedIndex { get; private set; } = -1;

        public VisualElement visualElement => this;

        List<Tuple<string, Sprite>> m_Labels;

        ScrollView m_LabelImagesContainer;
        private VisualElement m_LabelImagesInternalContainer;
        PropertyAnimationState m_AnimationState;

        public LabelContainer()
        {

            m_LabelImagesContainer = new ScrollView
            {
                horizontalScrollerVisibility = ScrollerVisibility.Hidden,
                verticalScrollerVisibility = ScrollerVisibility.Auto,

                nestedInteractionKind = ScrollView.NestedInteractionKind.StopScrolling,
                mode = ScrollViewMode.Vertical
            };
            m_LabelImagesContainer.style.flexDirection = FlexDirection.Column;
            m_LabelImagesContainer.style.flexWrap = Wrap.Wrap;

            m_LabelImagesInternalContainer = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    flexWrap = Wrap.Wrap
                }
            };
            m_LabelImagesContainer.Add(m_LabelImagesInternalContainer);
            Add(m_LabelImagesContainer);

            RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        }

        public void SetItems(IList labels)
        {
            if (labels is not List<Tuple<string, Sprite>> list)
            {
                throw new ArgumentException("labels must be of type List<Tuple<string, Sprite>>");
            }
            m_Labels = list;
            if (m_LabelImagesInternalContainer.childCount > 0)
                m_LabelImagesInternalContainer.Clear();
            foreach ((string labelName, Sprite labelSprite) in m_Labels)
                m_LabelImagesInternalContainer.Add(GetVisualForLabel(labelName, labelSprite));
        }

        public object GetItem(int index)
        {
            if (m_Labels == null || index < 0 || index >= m_Labels.Count)
                return null;

            return m_Labels[index];
        }

        public void Select(int index)
        {
            if (index < 0 || index >= itemCount)
                return;

            selectedIndex = index;
            UpdateSelectionVisuals();
            onSelectionChange?.Invoke(selectedIndex);
        }

        void OnGeometryChanged(GeometryChangedEvent evt)
        {
            m_LabelImagesInternalContainer.style.width = evt.newRect.width;
        }

        void OnLabelPointerDown(PointerDownEvent evt)
        {
            if (evt.currentTarget is Image image)
                Select(m_LabelImagesInternalContainer.IndexOf(image));
        }

        void UpdateSelectionVisuals()
        {
            int count = m_LabelImagesInternalContainer.childCount;
            for (int i = 0; i < count; ++i)
            {
                VisualElement child = m_LabelImagesInternalContainer[i];
                if (i == selectedIndex)
                {
                    child.AddToClassList(Styles.labelSelected);
                    child.style.backgroundColor = GetColorForAnimationState(m_AnimationState);
                }
                else
                {
                    child.RemoveFromClassList(Styles.labelSelected);
                }
            }
        }

        VisualElement GetVisualForLabel(string labelName, Sprite labelSprite)
        {
            Image image = new Image { name = labelName, tooltip = labelName, sprite = labelSprite };
            image.style.height = image.style.width = SpriteSwapOverlay.Settings.thumbnailSize;
            image.AddToClassList(Styles.labelVisual);
            image.RegisterCallback<PointerDownEvent>(OnLabelPointerDown);
            return image;
        }

        public void SetAnimationState(PropertyAnimationState animationState)
        {
            if (animationState != m_AnimationState)
            {
                m_AnimationState = animationState;

                UpdateSelectionVisuals();
            }
        }

        static Color GetColorForAnimationState(PropertyAnimationState animationState)
        {
            Color color = new Color(88.0f / 255.0f, 88.0f / 255.0f, 88.0f / 255.5f, 1.0f);
            if (animationState == PropertyAnimationState.Animated)
                color *= AnimationMode.animatedPropertyColor;
            else if (animationState == PropertyAnimationState.Candidate)
                color *= AnimationMode.candidatePropertyColor;
            else if (animationState == PropertyAnimationState.Recording)
                color *= AnimationMode.recordedPropertyColor;

            return color;
        }
    }
}
