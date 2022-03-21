using System;
using System.Collections.Generic;
using UnityEditor.AssetImporters;
using UnityEngine;
using UnityEngine.U2D.Animation;
using Object = UnityEngine.Object;

namespace UnityEditor.U2D.Animation
{
    [CustomEditor(typeof(SpriteLibrarySourceAssetImporter))]
    internal class SpriteLibrarySourceAssetImporterInspector : ScriptedImporterEditor
    {
        static class Contents
        {
            public static GUIContent openInSpriteLibraryEditor = new(L10n.Tr("Open in Sprite Library Editor"));
        }

        static class Style
        {
            public static GUIContent mainAssetLabel = new("Main Library");
        }

        SerializedProperty m_PrimaryLibraryGUID;
        SerializedProperty m_Library;
        SpriteLibraryAsset m_MainSpriteLibraryAsset;
        SpriteLibraryDataInspector m_SpriteLibraryDataInspector;

        public override bool showImportedObject => false;
        protected override Type extraDataType => typeof(SpriteLibrarySourceAsset);

        public override void OnEnable()
        {
            base.OnEnable();

            m_PrimaryLibraryGUID = extraDataSerializedObject.FindProperty(SpriteLibrarySourceAssetPropertyString.primaryLibraryGUID);
            if (!m_PrimaryLibraryGUID.hasMultipleDifferentValues && !string.IsNullOrEmpty(m_PrimaryLibraryGUID.stringValue))
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(m_PrimaryLibraryGUID.stringValue);
                m_MainSpriteLibraryAsset = AssetDatabase.LoadAssetAtPath<SpriteLibraryAsset>(assetPath);
            }

            m_Library = extraDataSerializedObject.FindProperty(SpriteLibrarySourceAssetPropertyString.library);
        }

        protected override void InitializeExtraDataInstance(Object extraTarget, int targetIndex)
        {
            var assetPath = ((AssetImporter)targets[targetIndex]).assetPath;
            var savedAsset = SpriteLibrarySourceAssetImporter.LoadSpriteLibrarySourceAsset(assetPath);
            if (savedAsset != null)
            {
                // Add entries from Main Library Asset.
                if (!SpriteLibrarySourceAssetImporter.HasValidMainLibrary(savedAsset, assetPath))
                    savedAsset.SetPrimaryLibraryGUID(string.Empty);
                SpriteLibrarySourceAssetImporter.UpdateSpriteLibrarySourceAssetLibraryWithMainAsset(savedAsset);

                (extraTarget as SpriteLibrarySourceAsset).InitializeWithAsset(savedAsset);
            }
        }

        public override void OnInspectorGUI()
        {
            if (GUILayout.Button(Contents.openInSpriteLibraryEditor))
                SpriteLibraryEditor.SpriteLibraryEditorWindow.OpenWindow();

            GUILayout.Space(10);

            serializedObject.Update();
            extraDataSerializedObject.Update();
            DoMainAssetGUI();
            serializedObject.ApplyModifiedProperties();
            extraDataSerializedObject.ApplyModifiedProperties();

            ApplyRevertGUI();
        }

        protected override void Apply()
        {
            // Make sure that all changes are Saved / Reverted by the user if there is an instance of SpriteLibraryEditorWindow open. 
            SpriteLibraryEditor.SpriteLibraryEditorWindow.HandleUnsavedChangesOnApply();
            base.Apply();

            for (var i = 0; i < targets.Length; i++)
            {
                var path = ((AssetImporter)targets[i]).assetPath;
                var sourceAsset = (SpriteLibrarySourceAsset)extraDataTargets[i];
                var savedAsset = SpriteLibrarySourceAssetImporter.LoadSpriteLibrarySourceAsset(path);
                savedAsset.InitializeWithAsset(sourceAsset);
                for (var j = 0; j < savedAsset.library.Count; ++j)
                    savedAsset.library[j].overrideEntries = new List<SpriteCategoryEntryOverride>(sourceAsset.library[j].overrideEntries);

                // Remove entries that come from Main Library Asset before saving.
                var savedLibrarySerializedObject = new SerializedObject(savedAsset);
                SpriteLibraryDataInspector.UpdateLibraryWithNewMainLibrary(null, savedLibrarySerializedObject.FindProperty(SpriteLibrarySourceAssetPropertyString.library));
                if (savedLibrarySerializedObject.hasModifiedProperties)
                    savedLibrarySerializedObject.ApplyModifiedPropertiesWithoutUndo();

                // Save asset to disk.
                SpriteLibrarySourceAssetImporter.SaveSpriteLibrarySourceAsset(savedAsset, path);
            }

            // Due to https://fogbugz.unity3d.com/f/cases/1418417/ we can't guarantee
            // that changes will be propagated to the SpriteLibraryEditor window.
            // Until fixed, keep this line to ensure that SpriteLibraryEditor window reloads.
            SpriteLibraryEditor.SpriteLibraryEditorWindow.TriggerAssetModifiedOnApply();
        }

        void DoMainAssetGUI()
        {
            EditorGUI.BeginChangeCheck();
            if (m_PrimaryLibraryGUID.hasMultipleDifferentValues)
                EditorGUI.showMixedValue = true;

            var currentMainSpriteLibraryAsset = AssetDatabase.LoadAssetAtPath<SpriteLibraryAsset>(AssetDatabase.GUIDToAssetPath(m_PrimaryLibraryGUID.stringValue));
            m_MainSpriteLibraryAsset = EditorGUILayout.ObjectField(Style.mainAssetLabel, currentMainSpriteLibraryAsset, typeof(SpriteLibraryAsset), false) as SpriteLibraryAsset;
            if (EditorGUI.EndChangeCheck())
            {
                var isNewMainLibraryValid = true;
                foreach (var currentTarget in targets)
                {
                    var assetPath = ((AssetImporter)currentTarget).assetPath;
                    var spriteLibraryAsset = AssetDatabase.LoadAssetAtPath<SpriteLibraryAsset>(assetPath);
                    var mainAssetParents = SpriteLibrarySourceAssetImporter.GetAssetParentChain(m_MainSpriteLibraryAsset);
                    if (spriteLibraryAsset == m_MainSpriteLibraryAsset || mainAssetParents.Contains(spriteLibraryAsset))
                    {
                        Debug.LogWarning(TextContent.spriteLibraryCircularDependency);
                        m_MainSpriteLibraryAsset = currentMainSpriteLibraryAsset;
                        isNewMainLibraryValid = false;
                        break;
                    }
                }

                if (isNewMainLibraryValid)
                {
                    m_PrimaryLibraryGUID.stringValue = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(m_MainSpriteLibraryAsset));
                    SpriteLibraryDataInspector.UpdateLibraryWithNewMainLibrary(m_MainSpriteLibraryAsset, m_Library);
                    serializedObject.ApplyModifiedProperties();
                }
            }

            EditorGUI.showMixedValue = false;
        }
    }

    internal class CreateSpriteLibrarySourceAsset : ProjectWindowCallback.EndNameEditAction
    {
        const int k_SpriteLibraryAssetMenuPriority = 30;
        string m_MainLibrary;

        public override void Action(int instanceId, string pathName, string resourceFile)
        {
            var asset = CreateInstance<SpriteLibrarySourceAsset>();
            UnityEditorInternal.InternalEditorUtility.SaveToSerializedFileAndForget(new Object[] { asset }, pathName, true);
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        }

        [MenuItem("Assets/Create/2D/Sprite Library Asset", priority = k_SpriteLibraryAssetMenuPriority)]
        static void CreateSpriteLibrarySourceAssetMenu()
        {
            var action = CreateInstance<CreateSpriteLibrarySourceAsset>();
            var icon = EditorIconUtility.LoadIconResource("Sprite Library", "Icons/Light", "Icons/Dark");
            ProjectWindowUtil.StartNameEditingIfProjectWindowExists(0, action, SpriteLibrarySourceAsset.defaultName + SpriteLibrarySourceAsset.extension, icon, null);
        }

        [MenuItem("Assets/Create/2D/Sprite Library Asset Variant", true, priority = k_SpriteLibraryAssetMenuPriority + 1)]
        static bool ValidateCanCreateVariant()
        {
            return SpriteLibrarySourceAssetImporter.GetAssetFromSelection() != null;
        }
    }
}