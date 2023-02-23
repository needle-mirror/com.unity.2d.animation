using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Overlays;
using UnityEngine;
using UnityEngine.U2D.Animation;
using UnityEngine.UIElements;

namespace UnityEditor.U2D.Animation.SceneOverlays
{
    internal interface INavigableElement
    {
        public event Action<int> onSelectionChange;

        int itemCount { get; }
        int selectedIndex { get; }

        public void Select(int index);
        void SetItems(IList items);
        object GetItem(int index);

        VisualElement visualElement { get; }
    }

    [Overlay(typeof(SceneView), overlayId, k_DefaultName,
        defaultDisplay = k_DefaultVisibility,
        defaultDockZone = DockZone.RightColumn,
        defaultDockPosition = DockPosition.Bottom,
        defaultLayout = Overlays.Layout.Panel,
        defaultWidth = k_DefaultWidth + k_WidthPadding,
        defaultHeight = k_DefaultHeight + k_HeightPadding)]
    [Icon("Packages/com.unity.2d.animation/Editor/Assets/ComponentIcons/Animation.SpriteResolver.png")]
    internal class SpriteResolverOverlay : Overlay
    {
        public static class Settings
        {
            public const float minThumbnailSize = 20.0f + k_ThumbnailPadding;
            public const float maxThumbnailSize = 110.0f + k_ThumbnailPadding;
            public const float defaultThumbnailSize = 50.0f + k_ThumbnailPadding;
            const float k_ThumbnailPadding = 8.0f;

            const string k_FilterKey = UserSettings.kSettingsUniqueKey + "SpriteResolverOverlay.filter";
            const string k_ThumbnailSizeKey = UserSettings.kSettingsUniqueKey + "SpriteResolverOverlay.thumbnailSize";
            const string k_PreferredWidthKey = UserSettings.kSettingsUniqueKey + "SpriteResolverOverlay.preferredWidth";
            const string k_PreferredHeightKey = UserSettings.kSettingsUniqueKey + "SpriteResolverOverlay.preferredHeight";

            public static bool filter
            {
                get => EditorPrefs.GetBool(k_FilterKey, false);
                set => EditorPrefs.SetBool(k_FilterKey, value);
            }

            public static float thumbnailSize
            {
                get => EditorPrefs.GetFloat(k_ThumbnailSizeKey, defaultThumbnailSize);
                set => EditorPrefs.SetFloat(k_ThumbnailSizeKey, Mathf.Clamp(value, minThumbnailSize, maxThumbnailSize));
            }

            public static float preferredWidth
            {
                get => EditorPrefs.GetFloat(k_PreferredWidthKey, k_DefaultWidth);
                set => EditorPrefs.SetFloat(k_PreferredWidthKey, value);
            }

            public static float preferredHeight
            {
                get => EditorPrefs.GetFloat(k_PreferredHeightKey, k_DefaultHeight);
                set => EditorPrefs.SetFloat(k_PreferredHeightKey, value);
            }
        }

        public const string overlayId = "Scene View/Sprite Resolver";
        public const string rootStyle = "sprite-resolver-overlay";

        const float k_DefaultWidth = 230.0f;
        const float k_DefaultHeight = 133.0f;
        const float k_WidthPadding = 6.0f;
        const float k_HeightPadding = 19.0f;

        const bool k_DefaultVisibility = false;

        const string k_DefaultName = "Sprite Resolver";

        public SpriteResolverOverlayVisualElement mainVisualElement => m_MainVisualElement;

        bool isViewInitialized => m_MainVisualElement != null;

        SpriteResolver[] m_Selection;

        SpriteResolverOverlayVisualElement m_MainVisualElement;

        public SpriteResolverOverlay()
        {
            minSize = new Vector2(k_DefaultWidth + k_WidthPadding, k_DefaultHeight + k_HeightPadding);
            maxSize = new Vector2(k_DefaultWidth * 10.0f + k_WidthPadding, k_DefaultHeight * 10.0f + k_HeightPadding);
        }

        public override VisualElement CreatePanelContent()
        {
            var overlayElement = new SpriteResolverOverlayVisualElement { style = { width = Settings.preferredWidth, height = Settings.preferredHeight } };
            var toolbar = overlayElement.Q<OverlayToolbar>();
            toolbar.onFilterToggled += OnFilterToggled;
            toolbar.onResetSliderValue += OnResetThumbnailSize;
            toolbar.onSliderValueChanged += OnChangeThumbnailSize;
            overlayElement.RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            overlayElement.RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
            overlayElement.styleSheets.Add(ResourceLoader.Load<StyleSheet>("SpriteResolver/SpriteResolverOverlay.uss"));
            return overlayElement;
        }

        public override void OnCreated()
        {
            base.OnCreated();
            m_Selection = GetSelection();
            Selection.selectionChanged += OnSelectionChanged;
        }

        public override void OnWillBeDestroyed()
        {
            Selection.selectionChanged -= OnSelectionChanged;
            base.OnWillBeDestroyed();
        }

        void OnAttachToPanel(AttachToPanelEvent evt)
        {
            var element = (SpriteResolverOverlayVisualElement)evt.target;
            if (element != null)
            {
                m_MainVisualElement = element;
                m_MainVisualElement.parent.RegisterCallback<GeometryChangedEvent>(OnParentGeometryChanged);
                if (collapsed || isInToolbar)
                {
                    m_MainVisualElement.style.width = m_MainVisualElement.style.maxWidth = Settings.preferredWidth;
                    m_MainVisualElement.style.height = m_MainVisualElement.style.maxHeight = Settings.preferredHeight;
                }
                UpdateVisuals();
            }
        }

        void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            var element = (SpriteResolverOverlayVisualElement)evt.currentTarget;
            if (element == m_MainVisualElement)
                m_MainVisualElement = null;
        }

        void OnParentGeometryChanged(GeometryChangedEvent evt)
        {
            if(m_MainVisualElement == null)
                return;

            if (!isInToolbar && !collapsed)
            {
                Settings.preferredWidth = evt.newRect.width;
                Settings.preferredHeight = evt.newRect.height;
            }

            m_MainVisualElement.style.width = evt.newRect.width;
        }

        void OnFilterToggled(bool filter)
        {
            if (Settings.filter == filter)
                return;

            Settings.filter = filter;

            UpdateVisuals();
        }

        void OnChangeThumbnailSize(float newSize)
        {
            if (Math.Abs(Settings.thumbnailSize - newSize) < 0.01f)
                return;

            Settings.thumbnailSize = newSize;

            UpdateVisuals();
        }
        
        void OnResetThumbnailSize()
        {
            if (Math.Abs(Settings.thumbnailSize - Settings.defaultThumbnailSize) < 0.01f)
                return;

            Settings.thumbnailSize = Settings.defaultThumbnailSize;

            UpdateVisuals();
        }

        void SetSelection(SpriteResolver[] newSelection)
        {
            if (m_Selection == newSelection)
                return;

            m_Selection = newSelection;

            if (!isViewInitialized)
                return;

            UpdateVisuals();
        }

        void UpdateVisuals()
        {
            var selection = Settings.filter ? FilterSelection() : m_Selection;
            m_MainVisualElement.SetSpriteResolvers(selection);
        }

        void OnSelectionChanged()
        {
            SetSelection(GetSelection());
        }

        SpriteResolver[] FilterSelection()
        {
            var filteredSelection = new List<SpriteResolver>();
            if (m_Selection != null)
            {
                for (var i = 0; i < m_Selection.Length; i++)
                {
                    var spriteResolver = m_Selection[i];
                    var spriteLibrary = spriteResolver.spriteLibrary;
                    if (spriteLibrary == null)
                        continue;

                    var selectedCategory = spriteResolver.GetCategory();
                    if (string.IsNullOrEmpty(selectedCategory))
                        continue;

                    var labelNames = spriteLibrary.GetEntryNames(selectedCategory);
                    if (labelNames != null && labelNames.Count() > 1)
                        filteredSelection.Add(spriteResolver);
                }
            }

            return filteredSelection.ToArray();
        }

        internal static SpriteResolver[] GetSelection()
        {
            var spriteResolvers = new HashSet<SpriteResolver>();
            for (var o = 0; o < Selection.gameObjects.Length; o++)
            {
                var gameObject = Selection.gameObjects[o];
                var children = gameObject.GetComponentsInChildren<SpriteResolver>();
                for (var c = 0; c < children.Length; c++)
                {
                    var spriteResolver = children[c];
                    spriteResolvers.Add(spriteResolver);
                }
            }

            return spriteResolvers.ToArray();
        }
    }
}
