using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityEditor.U2D.Animation.SceneOverlays
{
    internal class OverlayToggle : BaseBoolField
    {
        const string k_StyleName = SpriteResolverOverlay.rootStyle + "__filter-toggle";

        public OverlayToggle() : base(null)
        {
            var filterIcon = EditorIconUtility.LoadIconResource("EditorUI.Filter", EditorIconUtility.LightIconPath, EditorIconUtility.DarkIconPath);
            this.Q<VisualElement>(className: inputUssClassName).style.backgroundImage = filterIcon;

            AddToClassList(k_StyleName);
        }
    }

    internal class OverlayToolbar : VisualElement
    {
        static class Styles
        {
            public const string toolbar = SpriteResolverOverlay.rootStyle + "__toolbar";
            public const string thumbnailSettings = SpriteResolverOverlay.rootStyle + "__thumbnail-settings";
            public const string resetButton = SpriteResolverOverlay.rootStyle + "__reset-button";
            public const string slider = SpriteResolverOverlay.rootStyle + "__slider";
        }

        public event Action<bool> onFilterToggled;
        public event Action onResetSliderValue;
        public event Action<float> onSliderValueChanged;

        public bool isFilterOn => m_FilterToggle.value;

        OverlayToggle m_FilterToggle;
        Slider m_Slider;

        public OverlayToolbar()
        {
            AddToClassList(Styles.toolbar);

            m_FilterToggle = new OverlayToggle { tooltip = TextContent.resolverOverlayFilterDescription };
            m_FilterToggle.RegisterValueChangedCallback(OnToggleValueChanged);
            Add(m_FilterToggle);

            var thumbnailSettings = new VisualElement();
            thumbnailSettings.AddToClassList(Styles.thumbnailSettings);
            Add(thumbnailSettings);
            var resetButton = new Button { tooltip = TextContent.resolverOverlayResetThumbnailSize, style = { minHeight = 18 } };
            var resetImage = new Image { image = (Texture2D)EditorGUIUtility.IconContent("ViewToolZoom").image };
            resetButton.Add(resetImage);
            resetButton.clicked += OnResetSliderValue;
            resetButton.SetEnabled(true);
            resetButton.AddToClassList(Styles.resetButton);
            thumbnailSettings.Add(resetButton);

            m_Slider = new Slider { tooltip = TextContent.resolverOverlayThumbnailSlider };
            m_Slider.RegisterValueChangedCallback(OnSliderValueChanged);
            m_Slider.AddToClassList(Styles.slider);
            thumbnailSettings.Add(m_Slider);

            m_FilterToggle.SetValueWithoutNotify(SpriteResolverOverlay.Settings.filter);

            m_Slider.lowValue = SpriteResolverOverlay.Settings.minThumbnailSize;
            m_Slider.highValue = SpriteResolverOverlay.Settings.maxThumbnailSize;
            m_Slider.SetValueWithoutNotify(SpriteResolverOverlay.Settings.thumbnailSize);
        }

        void OnToggleValueChanged(ChangeEvent<bool> evt)
        {
            onFilterToggled?.Invoke(evt.newValue);
        }

        void OnResetSliderValue()
        {
            onResetSliderValue?.Invoke();
            m_Slider.SetValueWithoutNotify(SpriteResolverOverlay.Settings.thumbnailSize);
        }

        void OnSliderValueChanged(ChangeEvent<float> evt)
        {
            onSliderValueChanged?.Invoke(evt.newValue);
        }
    }
}
