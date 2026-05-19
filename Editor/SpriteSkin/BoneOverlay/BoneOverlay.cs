using UnityEditor;
using UnityEditor.Overlays;
using UnityEditor.U2D.Animation;
using UnityEngine;
using UnityEngine.U2D.Animation;
using UnityEngine.UIElements;

namespace UnityEditor.U2D.Animation.SceneOverlays
{
    [Overlay(typeof(SceneView), overlayId, "Bone",
        defaultDisplay = k_DefaultVisibility,
        defaultDockZone = DockZone.RightColumn,
        defaultDockPosition = DockPosition.Bottom,
        defaultLayout = Overlays.Layout.Panel,
        defaultWidth = k_DefaultWidth,
        defaultHeight = k_DefaultHeight,
        group = U2DAnimationConstants.PackageDisplayName)]
    [Icon("Packages/com.unity.2d.animation/Editor/Assets/EditorIcons/Edit_Pose.png")]
    internal class BoneOverlay : Overlay
    {
        internal const string overlayId = "Scene View/Bone";
        internal const string rootStyle = "bone-overlay";

        const float k_DefaultWidth = 166.0f;
        const float k_DefaultHeight = 44.0f;
        const bool k_DefaultVisibility = false;

        BoneOverlayVisualElement m_MainVisualElement;

        internal BoneOverlayVisualElement mainVisualElement => m_MainVisualElement;

        bool isViewInitialized => m_MainVisualElement != null;

        public BoneOverlay()
        {
            // Overlay defaults leave min/max at 0; per API, both 0 on an axis disables resizing for that axis.
            minSize = new Vector2(k_DefaultWidth, k_DefaultHeight);
            maxSize = new Vector2(k_DefaultWidth, k_DefaultHeight);
        }

        public override VisualElement CreatePanelContent()
        {
            BoneOverlayVisualElement overlayElement = new(this)
            {
                style = { width = k_DefaultWidth, height = k_DefaultHeight }
            };

            overlayElement.RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            overlayElement.RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
            overlayElement.styleSheets.Add(ResourceLoader.Load<StyleSheet>("BoneOverlay/BoneOverlay.uss"));

            return overlayElement;
        }

        /// <summary>
        /// Sets overlay min/max from measured color row <paramref name="contentSize"/> plus editor chrome padding/header.
        /// </summary>
        internal void ApplyContentSize(Vector2 contentSize)
        {
            float width = contentSize.x + GetHorizontalPadding();
            float height = contentSize.y + GetHeaderHeight() + GetVerticalPadding();
            minSize = new Vector2(width, height);
            maxSize = new Vector2(width, height);

            if (m_MainVisualElement != null)
            {
                m_MainVisualElement.style.width = contentSize.x;
                m_MainVisualElement.style.height = contentSize.y;
            }
        }

        float GetHeaderHeight()
        {
            if (!isViewInitialized)
                return 0f;

            VisualElement parent = m_MainVisualElement.parent;
            VisualElement header = parent?.parent?.Q(className: "overlay-header");
            if (parent == null || header == null)
                return 0f;

            return header.resolvedStyle.height + parent.resolvedStyle.paddingTop;
        }

        float GetVerticalPadding()
        {
            if (!isViewInitialized)
                return 0f;

            VisualElement container = m_MainVisualElement.parent?.parent;
            if (container == null)
                return 0f;

            return container.resolvedStyle.paddingTop + container.resolvedStyle.paddingBottom;
        }

        float GetHorizontalPadding()
        {
            if (!isViewInitialized)
                return 0f;

            VisualElement container = m_MainVisualElement.parent?.parent;
            if (container == null)
                return 0f;

            return container.resolvedStyle.paddingLeft + container.resolvedStyle.paddingRight;
        }

        public override void OnCreated()
        {
            base.OnCreated();

            BoneHierarchyEvent.ContextRebuilt += OnContextRebuilt;
            BoneHierarchyEvent.BoneColorsRefreshed += OnBoneColorsRefreshed;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        public override void OnWillBeDestroyed()
        {
            BoneHierarchyEvent.ContextRebuilt -= OnContextRebuilt;
            BoneHierarchyEvent.BoneColorsRefreshed -= OnBoneColorsRefreshed;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;

            base.OnWillBeDestroyed();
        }

        void OnAttachToPanel(AttachToPanelEvent ev)
        {
            BoneOverlayVisualElement element = (BoneOverlayVisualElement)ev.target;
            if (element != null)
                m_MainVisualElement = element;
        }

        void OnDetachFromPanel(DetachFromPanelEvent ev)
        {
            BoneOverlayVisualElement element = (BoneOverlayVisualElement)ev.currentTarget;
            if (element == m_MainVisualElement)
                m_MainVisualElement = null;
        }

        void OnPlayModeStateChanged(PlayModeStateChange newState)
        {
            if (newState is PlayModeStateChange.EnteredEditMode)
                BoneHierarchyEvent.RaiseHierarchyStructureChanged();
        }

        void OnContextRebuilt()
        {
            UpdateOverlay();
        }

        void OnBoneColorsRefreshed()
        {
            if (!isViewInitialized)
                return;

            m_MainVisualElement.Refresh();
        }

        void UpdateOverlay()
        {
            displayName = TextContent.boneOverlayName;

            if (!isViewInitialized)
                return;

            m_MainVisualElement.Refresh();
        }
    }
}
