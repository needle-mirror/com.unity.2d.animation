using UnityEditor.U2D.Common;
using UnityEngine;

namespace UnityEditor.U2D.Animation
{
    internal class SkinningModuleSettings
    {
        public const string kCompactToolbarKey = UserSettings.kSettingsUniqueKey + "AnimationEditorSetting.compactToolbar";
        public const string kShowSpriteMeshOverwriteWarningKey = UserSettings.kSettingsUniqueKey + "AnimationEditorSetting.showSpriteMeshOverwriteWarning";
        public static readonly GUIContent kCompactToolbarLabel = EditorGUIUtility.TrTextContent("Hide Tool Text");
        public static readonly GUIContent kShowSpriteMeshOverwriteWarning = new GUIContent(TextContent.showSpriteMeshOverwriteWarning, TextContent.showSpriteMeshOverwriteWarningTip);

        public static bool compactToolBar
        {
            get => EditorPrefs.GetBool(kCompactToolbarKey, false);
            set => EditorPrefs.SetBool(kCompactToolbarKey, value);
        }

        public static bool showSpriteMeshOverwriteWarning
        {
            get => EditorPrefs.GetBool(kShowSpriteMeshOverwriteWarningKey, true);
            set => EditorPrefs.SetBool(kShowSpriteMeshOverwriteWarningKey, value);
        }

        public void OnGUI()
        {
            EditorGUI.BeginChangeCheck();
            bool c = EditorGUILayout.Toggle(kCompactToolbarLabel, compactToolBar);
            if (EditorGUI.EndChangeCheck())
                compactToolBar = c;

            EditorGUI.BeginChangeCheck();
            c = EditorGUILayout.Toggle(kShowSpriteMeshOverwriteWarning, showSpriteMeshOverwriteWarning);
            if (EditorGUI.EndChangeCheck())
                showSpriteMeshOverwriteWarning = c;
        }
    }

    internal class VisibilityToolSettings
    {
        public const string kBoneOpacitykey = UserSettings.kSettingsUniqueKey + "VisibilityToolSettings.boneOpacity";
        public const string kMeshOpacityKey = UserSettings.kSettingsUniqueKey + "VisibilityToolSettings.meshOpacity";

        public static float boneOpacity
        {
            get => EditorPrefs.GetFloat(kBoneOpacitykey, 1.0f);
            set => EditorPrefs.SetFloat(kBoneOpacitykey, value);
        }

        public static float meshOpacity
        {
            get => EditorPrefs.GetFloat(kMeshOpacityKey, 0.5f);
            set => EditorPrefs.SetFloat(kMeshOpacityKey, value);
        }
    }

    internal class GenerateGeomertySettings
    {
        public const int kDefaultOutlineDetail = 10;
        public const int kDefaultAlphaTolerance = 10;
        public const int kDefaultSubdivide = 0;
        public const string kOutlineDetailKey = UserSettings.kSettingsUniqueKey + "GenerateGeomertySetting.outlineDetail";
        public const string kAlphaToleranceKey = UserSettings.kSettingsUniqueKey + "GenerateGeomertySetting.alphaTolerance";
        public const string kSubdivideKey = UserSettings.kSettingsUniqueKey + "GenerateGeomertySetting.subdivide";
        public const string kGenerateWeightsKey = UserSettings.kSettingsUniqueKey + "GenerateGeomertySetting.generateWeights";

        public static int outlineDetail
        {
            get => EditorPrefs.GetInt(kOutlineDetailKey, kDefaultOutlineDetail);
            set => EditorPrefs.SetInt(kOutlineDetailKey, value);
        }

        public static int alphaTolerance
        {
            get => EditorPrefs.GetInt(kAlphaToleranceKey, kDefaultAlphaTolerance);
            set => EditorPrefs.SetInt(kAlphaToleranceKey, value);
        }

        public static int subdivide
        {
            get => EditorPrefs.GetInt(kSubdivideKey, kDefaultSubdivide);
            set => EditorPrefs.SetInt(kSubdivideKey, value);
        }

        public static bool generateWeights
        {
            get => EditorPrefs.GetBool(kGenerateWeightsKey, true);
            set => EditorPrefs.SetBool(kGenerateWeightsKey, value);
        }
    }

    internal class SelectionOutlineSettings
    {
        public const string kSelectedOutlineRedKey = UserSettings.kSettingsUniqueKey + "OutlineColorRed";
        public const string kSelectedOutlineGreenKey = UserSettings.kSettingsUniqueKey + "OutlineColorGreen";
        public const string kSelectedOutlineBlueKey = UserSettings.kSettingsUniqueKey + "OutlineColorBlue";
        public const string kSelectedOutlineAlphaKey = UserSettings.kSettingsUniqueKey + "OutlineColorAlpha";
        public const string kSelectedSpriteOutlineSize = UserSettings.kSettingsUniqueKey + "OutlineSize";
        public const string kSelectedBoneOutlineSize = UserSettings.kSettingsUniqueKey + "BoneOutlineSize";
        public static readonly GUIContent kSelectedOutlineColorLabel = new GUIContent(TextContent.selectedOutlineColor);
        public static readonly GUIContent kSelectedOutlineSizeLabel = new GUIContent(TextContent.spriteOutlineSize);
        public static readonly GUIContent kSelectedBoneOutlineSizeLabel = new GUIContent(TextContent.boneOutlineSize);


        public static Color outlineColor
        {
            get =>
                new()
                {
                    r = EditorPrefs.GetFloat(kSelectedOutlineRedKey, 1),
                    g = EditorPrefs.GetFloat(kSelectedOutlineGreenKey, 102.0f / 255.0f),
                    b = EditorPrefs.GetFloat(kSelectedOutlineBlueKey, 0),
                    a = EditorPrefs.GetFloat(kSelectedOutlineAlphaKey, 1)
                };
            set
            {
                EditorPrefs.SetFloat(kSelectedOutlineRedKey, value.r);
                EditorPrefs.SetFloat(kSelectedOutlineGreenKey, value.g);
                EditorPrefs.SetFloat(kSelectedOutlineBlueKey, value.b);
                EditorPrefs.SetFloat(kSelectedOutlineAlphaKey, value.a);
            }
        }

        public static int selectedSpriteOutlineSize
        {
            get => EditorPrefs.GetInt(kSelectedSpriteOutlineSize, 1);
            set => EditorPrefs.SetInt(kSelectedSpriteOutlineSize, value);
        }

        public static float selectedBoneOutlineSize
        {
            get => EditorPrefs.GetFloat(kSelectedBoneOutlineSize, 1);
            set => EditorPrefs.SetFloat(kSelectedBoneOutlineSize, value);
        }

        public void OnGUI()
        {
            EditorGUI.BeginChangeCheck();
            Color c = EditorGUILayout.ColorField(kSelectedOutlineColorLabel, outlineColor);
            if (EditorGUI.EndChangeCheck())
                outlineColor = c;

            EditorGUI.BeginChangeCheck();
            int s = EditorGUILayout.IntSlider(kSelectedOutlineSizeLabel, selectedSpriteOutlineSize, 0, 10);
            if (EditorGUI.EndChangeCheck())
                selectedSpriteOutlineSize = s;

            EditorGUI.BeginChangeCheck();
            float o = EditorGUILayout.Slider(kSelectedBoneOutlineSizeLabel, selectedBoneOutlineSize, 0, 3);
            if (EditorGUI.EndChangeCheck())
                selectedBoneOutlineSize = o;
        }
    }

    internal class UserSettings : SettingsProvider
    {
        public const string kSettingsUniqueKey = "UnityEditor.U2D.Animation/";
        private static SelectionOutlineSettings s_SelectionOutlineSettings = new SelectionOutlineSettings();
        private static SkinningModuleSettings s_SkinningModuleSettings = new SkinningModuleSettings();

        public UserSettings()
            : base("Preferences/2D/Animation", SettingsScope.User)
        {
            guiHandler = OnGUI;
        }

        [SettingsProvider]
        private static SettingsProvider CreateSettingsProvider()
        {
            return new UserSettings()
            {
                guiHandler = SettingsGUI
            };
        }

        private static void SettingsGUI(string searchContext)
        {
            using (new SettingsWindowUtils.GUIScope())
            {
                s_SkinningModuleSettings.OnGUI();
                s_SelectionOutlineSettings.OnGUI();
            }
        }
    }
}
