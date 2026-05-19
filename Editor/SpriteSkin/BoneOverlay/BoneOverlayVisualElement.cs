using System.Collections.Generic;
using UnityEditor;
using UnityEditor.U2D.Animation;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.U2D.Animation;
using UnityEngine.UIElements;

namespace UnityEditor.U2D.Animation.SceneOverlays
{
    /// <summary>
    /// Bone overlay: bone color field from Hierarchy selection; when context is incomplete the field is disabled (non-pickable) and built-in ColorField tooltips are suppressed.
    /// </summary>
    internal class BoneOverlayVisualElement : VisualElement
    {
        internal static Color MixedBoneColor = Color.lightGray;

        internal static class Styles
        {
            internal const string container = BoneOverlay.rootStyle + "__container";
            internal const string colorFieldContainer = BoneOverlay.rootStyle + "__color-field-container";
            internal const string colorLabel = BoneOverlay.rootStyle + "__color-label";
            internal const string colorField = BoneOverlay.rootStyle + "__color-field";
        }

        readonly BoneOverlay m_Overlay;

        VisualElement m_ColorFieldContainer;
        Label m_ColorLabel;
        ColorField m_ColorField;

        internal BoneOverlayVisualElement(BoneOverlay overlay)
        {
            m_Overlay = overlay;

            AddToClassList(BoneOverlay.rootStyle);
            AddToClassList(Styles.container);

            m_ColorFieldContainer = new();
            m_ColorFieldContainer.AddToClassList(Styles.colorFieldContainer);

            m_ColorLabel = new(TextContent.boneOverlayColorLabel);
            m_ColorLabel.AddToClassList(Styles.colorLabel);
            m_ColorFieldContainer.Add(m_ColorLabel);

            m_ColorField = new()
            {
                showAlpha = true,
                showEyeDropper = false
            };
            m_ColorField.AddToClassList(Styles.colorField);
            m_ColorField.RegisterValueChangedCallback(OnColorFieldValueChanged);
            m_ColorField.RegisterCallback<TooltipEvent>(OnColorFieldTooltip, TrickleDown.TrickleDown);
            m_ColorFieldContainer.Add(m_ColorField);

            Add(m_ColorFieldContainer);

            m_ColorLabel.RegisterCallback<GeometryChangedEvent>(OnColorLabelGeometryChanged);

            ApplyContextToColorField();
        }

        void OnColorLabelGeometryChanged(GeometryChangedEvent ev)
        {
            if (m_Overlay == null)
                return;

            float labelWidth = m_ColorLabel.layout.width;
            if (float.IsNaN(labelWidth) || labelWidth < float.Epsilon)
                return;

            Vector2 contentSize = new Vector2(0.0f, 0.0f);

            contentSize.x = labelWidth + m_ColorLabel.resolvedStyle.marginRight;
            contentSize.x += m_ColorField.resolvedStyle.width;
            contentSize.x += m_ColorFieldContainer.resolvedStyle.paddingLeft + m_ColorFieldContainer.resolvedStyle.paddingRight;

            contentSize.y = m_ColorField.resolvedStyle.height;
            contentSize.y += m_ColorFieldContainer.resolvedStyle.paddingTop + m_ColorFieldContainer.resolvedStyle.paddingBottom;

            m_Overlay.ApplyContentSize(contentSize);
        }

        void OnColorFieldValueChanged(ChangeEvent<Color> ev)
        {
            if (!m_ColorField.enabledSelf)
                return;

            BoneHierarchyContext ctx = BoneHierarchyContext.Current;

            IReadOnlyDictionary<Transform, List<Transform>> targetBones = ctx.TargetBones;
            if (targetBones.Count == 0)
                return;

            IReadOnlyDictionary<Transform, string> boneTransformToGuid = ctx.BoneTransformToGuid;
            Color32 newColor = ev.newValue;

            foreach (KeyValuePair<Transform, List<Transform>> kvp in targetBones)
            {
                Dictionary<string, Color32> colors = new();
                Transform boneHost = kvp.Key;
                foreach (Transform boneTransform in kvp.Value)
                {
                    if (boneTransform == null || !boneTransformToGuid.TryGetValue(boneTransform, out string guid) || string.IsNullOrEmpty(guid))
                        continue;

                    colors[guid] = newColor;
                }

                BoneHierarchyDataBridge.SetBoneColors(boneHost.gameObject, colors);
            }
        }

        /// <summary>
        /// Updates the color field from <see cref="BoneHierarchyContext.Current"/> (full context rebuild or resolved colors only).
        /// </summary>
        internal void Refresh()
        {
            ApplyContextToColorField();
        }

        void ApplyContextToColorField()
        {
            BoneHierarchyContext ctx = BoneHierarchyContext.Current;

            IReadOnlyDictionary<Transform, List<Transform>> targetBones = ctx.TargetBones;
            if (targetBones.Count == 0)
            {
                UpdateColorField(BoneGizmo.DefaultBoneColor, mixed: false, disabled: true);
                return;
            }

            IReadOnlyDictionary<Transform, Color32> resolvedColors = ctx.ResolvedBoneColors;

            Color? color = null;
            bool mixed = false;

            foreach (KeyValuePair<Transform, List<Transform>> kvp in targetBones)
            {
                foreach (Transform boneTransform in kvp.Value)
                {
                    if (boneTransform == null)
                        continue;

                    if (!resolvedColors.TryGetValue(boneTransform, out Color32 c))
                        c = BoneGizmo.DefaultBoneColor;

                    if (color != c)
                    {
                        if (color == null)
                        {
                            color = c;
                        }
                        else
                        {
                            mixed = true;
                            break;
                        }
                    }
                }

                if (mixed)
                    break;
            }

            UpdateColorField(color ?? BoneGizmo.DefaultBoneColor, mixed, disabled: false);
        }

        void UpdateColorField(Color color, bool mixed, bool disabled)
        {
            m_ColorField.SetValueWithoutNotify(mixed ? MixedBoneColor : color);
            m_ColorField.showMixedValue = mixed;
            m_ColorField.SetEnabled(!disabled);
            m_ColorField.pickingMode = disabled ? PickingMode.Ignore : PickingMode.Position;
        }

        void OnColorFieldTooltip(TooltipEvent evt)
        {
            if (m_ColorField.enabledSelf)
                return;

            evt.tooltip = string.Empty;
            evt.StopImmediatePropagation();
        }
    }
}
