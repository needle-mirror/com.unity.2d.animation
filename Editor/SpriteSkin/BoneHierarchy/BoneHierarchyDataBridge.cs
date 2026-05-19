using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.U2D.Animation;

namespace UnityEditor.U2D.Animation
{
    /// <summary>
    /// Reads and writes SpriteBoneHierarchyData on a bone host GameObject; records Undo and marks objects and scenes dirty.
    /// BoneHierarchyEvent.BoneColorsPersisted is raised elsewhere (BoneHierarchyObjectChangeListener on ObjectChangeEvents) when serialized hierarchy color data may have changed.
    /// </summary>
    internal static class BoneHierarchyDataBridge
    {
        const string k_ChangeBoneColorUndoName = "Change Bone Color";

        static SpriteBoneHierarchyData GetOrCreateHierarchyDataOn(GameObject boneHost)
        {
            if (boneHost == null)
                return null;

            try
            {
                SpriteBoneHierarchyData hierarchyData = boneHost.GetComponent<SpriteBoneHierarchyData>();
                if (hierarchyData == null)
                {
                    // Snapshot boneHost first so undo restores its pre-AddComponent state and the entry uses k_ChangeBoneColorUndoName.
                    Undo.RegisterCompleteObjectUndo(boneHost, k_ChangeBoneColorUndoName);
                    hierarchyData = boneHost.AddComponent<SpriteBoneHierarchyData>();
                    Debug.Assert(hierarchyData != null, "SpriteBoneHierarchyData could not be added to bone host.");
                    if (hierarchyData != null)
                    {
                        Undo.RegisterCreatedObjectUndo(hierarchyData, k_ChangeBoneColorUndoName);
                        EditorUtility.SetDirty(boneHost);
                    }
                }

                return hierarchyData;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[2D Animation] Bone hierarchy colors: could not access or add SpriteBoneHierarchyData. " + ex);
                return null;
            }
        }

        static void MarkBoneColorsDirtyAndNotify(GameObject boneHost)
        {
            if (boneHost == null)
                return;

            EditorUtility.SetDirty(boneHost);

            SpriteBoneHierarchyData hierarchyData = boneHost.GetComponent<SpriteBoneHierarchyData>();
            if (hierarchyData != null)
            {
                // Persist dictionary into m_SerializedData before any code path calls LoadBoneColors; otherwise an empty serialized list can clear colors on reload.
                hierarchyData.StoreBoneColors();
                EditorUtility.SetDirty(hierarchyData);
            }

            if (PrefabUtility.IsPartOfPrefabInstance(boneHost))
            {
                if (hierarchyData != null)
                    PrefabUtility.RecordPrefabInstancePropertyModifications(hierarchyData);
            }

            if (!EditorApplication.isPlaying && boneHost.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(boneHost.scene);
        }

        internal static void SetBoneColors(GameObject boneHost, Dictionary<string, Color32> boneColors)
        {
            if (boneHost == null || boneColors == null)
                return;

            SpriteBoneHierarchyData hierarchyData = GetOrCreateHierarchyDataOn(boneHost);
            if (hierarchyData == null)
            {
                Debug.LogWarning("[2D Animation] Bone hierarchy colors: could not add SpriteBoneHierarchyData; bone colors were not saved.");
                return;
            }

            Undo.RegisterCompleteObjectUndo(hierarchyData, k_ChangeBoneColorUndoName);
            hierarchyData.MergeBoneColorsFrom(boneColors);
            MarkBoneColorsDirtyAndNotify(boneHost);
        }
    }
}
