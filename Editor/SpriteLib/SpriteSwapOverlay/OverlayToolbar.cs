using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityEditor.U2D.Animation.SceneOverlays
{
    internal class OverlayToggle : BaseBoolField
    {
        public OverlayToggle(Texture2D icon, string styleName) : base(null)
        {
            this.Q<VisualElement>(className: inputUssClassName).style.backgroundImage = icon;

            AddToClassList(styleName);
        }
    }

    internal class OverlayToolbar : VisualElement
    {
        static class Styles
        {
            public const string toolbar = SpriteSwapOverlay.rootStyle + "__toolbar";
            public const string thumbnailSettings = SpriteSwapOverlay.rootStyle + "__thumbnail-settings";
            public const string slider = SpriteSwapOverlay.rootStyle + "__slider";
            public const string toggle = SpriteSwapOverlay.rootStyle + "__toggle";
        }

        static class Icons
        {
            public const string filter = "EditorUI.Filter";
            public const string locked = "Locked";
            public const string zoom = "ViewToolZoom";
        }

        public event Action<bool> onFilterToggled;
        public event Action<bool> onLockToggled;
        public event Action onResetSliderValue;
        public event Action<float> onSliderValueChanged;

        public OverlayToolbar()
        {
            AddToClassList(Styles.toolbar);

            Texture2D filterIcon = EditorIconUtility.LoadIconResource(Icons.filter, EditorIconUtility.LightIconPath, EditorIconUtility.DarkIconPath);
            OverlayToggle filterToggle = new OverlayToggle(filterIcon, Styles.toggle) { tooltip = TextContent.spriteSwapFilterDescription, value = SpriteSwapOverlay.Settings.filter };
            filterToggle.RegisterValueChangedCallback(evt => onFilterToggled?.Invoke(evt.newValue));
            Add(filterToggle);

            Texture2D lockIcon = EditorIconUtility.LoadIconResource(Icons.locked, EditorIconUtility.LightIconPath, EditorIconUtility.DarkIconPath);
            OverlayToggle lockToggle = new OverlayToggle(lockIcon, Styles.toggle) { tooltip = TextContent.spriteSwapLockDescription, value = SpriteSwapOverlay.Settings.locked };
            lockToggle.RegisterValueChangedCallback(evt => onLockToggled?.Invoke(evt.newValue));
            Add(lockToggle);

            VisualElement thumbnailSettings = new VisualElement();
            Slider slider = new Slider { tooltip = TextContent.spriteSwapThumbnailSlider, lowValue = SpriteSwapOverlay.Settings.minThumbnailSize, highValue = SpriteSwapOverlay.Settings.maxThumbnailSize };
            slider.SetValueWithoutNotify(SpriteSwapOverlay.Settings.thumbnailSize);
            slider.RegisterValueChangedCallback(OnSliderValueChanged);
            slider.AddToClassList(Styles.slider);
            Button resetButton = new Button { tooltip = TextContent.spriteSwapResetThumbnailSize, style = { minHeight = 18 } };
            Texture2D zoomIcon = EditorIconUtility.LoadIconResource(Icons.zoom, EditorIconUtility.LightIconPath, EditorIconUtility.DarkIconPath);
            Image resetImage = new Image { image = zoomIcon };
            resetButton.Add(resetImage);
            resetButton.clicked += () => OnResetSliderValue(slider);
            resetButton.AddToClassList(Styles.toggle);
            thumbnailSettings.Add(resetButton);
            thumbnailSettings.Add(slider);
            thumbnailSettings.AddToClassList(Styles.thumbnailSettings);
            Add(thumbnailSettings);
        }

        void OnResetSliderValue(Slider slider)
        {
            onResetSliderValue?.Invoke();
            slider.SetValueWithoutNotify(SpriteSwapOverlay.Settings.thumbnailSize);
        }

        void OnSliderValueChanged(ChangeEvent<float> evt)
        {
            onSliderValueChanged?.Invoke(evt.newValue);
        }
    }
}
