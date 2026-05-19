using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEngine.U2D.Animation
{
    /// <summary>
    /// Per-GameObject storage for bone hierarchy overlay settings in the Editor (bone colors).
    /// In the Editor, colors live in a dictionary and are synced with a serialized list through ISerializationCallbackReceiver.
    /// In Player builds, editor-only serialized fields are not compiled in, so this component carries no hierarchy color data at runtime.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    internal class SpriteBoneHierarchyData : MonoBehaviour, ISerializationCallbackReceiver
    {
#if UNITY_EDITOR
        [Serializable]
        private struct BoneColorEntry
        {
            public string boneGuid;
            public Color32 color;
        }

        /// <summary>
        /// Serialized bone color list (Editor only).
        /// </summary>
        [Serializable]
        private struct SerializedData
        {
            public List<BoneColorEntry> boneColorEntries;
        }

        [SerializeField]
        [HideInInspector]
        SerializedData m_SerializedData;

        [NonSerialized]
        Dictionary<string, Color32> m_BoneColorDictionary;

        void EnsureEditorColorState()
        {
            // Default(initobj) leaves boneColorEntries null; seed list to match first-run behavior.
            if (m_SerializedData.boneColorEntries == null)
                m_SerializedData.boneColorEntries = new();

            if (m_BoneColorDictionary == null)
                m_BoneColorDictionary = new();
        }

        /// <summary>
        /// Replaces the in-memory dictionary from m_SerializedData.boneColorEntries.
        /// </summary>
        public void LoadBoneColors()
        {
            EnsureEditorColorState();
            m_BoneColorDictionary.Clear();
            foreach (BoneColorEntry entry in m_SerializedData.boneColorEntries)
            {
                if (!string.IsNullOrEmpty(entry.boneGuid))
                    m_BoneColorDictionary[entry.boneGuid] = entry.color;
            }
        }

        /// <summary>
        /// Writes the in-memory dictionary into m_SerializedData.boneColorEntries.
        /// Called from OnBeforeSerialize; call explicitly before LoadBoneColors if OnBeforeSerialize has not run yet.
        /// </summary>
        public void StoreBoneColors()
        {
            EnsureEditorColorState();
            m_SerializedData.boneColorEntries.Clear();
            foreach (KeyValuePair<string, Color32> kvp in m_BoneColorDictionary)
            {
                if (string.IsNullOrEmpty(kvp.Key))
                    continue;
                m_SerializedData.boneColorEntries.Add(new() { boneGuid = kvp.Key, color = kvp.Value });
            }
        }

        /// <summary>
        /// Returns the live bone GUID to color map (same instance until deserialize). Copy the dictionary if you need a snapshot.
        /// </summary>
        public Dictionary<string, Color32> GetBoneColors()
        {
            EnsureEditorColorState();
            return m_BoneColorDictionary;
        }

        /// <summary>
        /// Overwrites entries in the in-memory map from boneColors. Serialized data updates on the next OnBeforeSerialize.
        /// </summary>
        public void MergeBoneColorsFrom(Dictionary<string, Color32> boneColors)
        {
            EnsureEditorColorState();
            if (boneColors == null)
                return;

            foreach (KeyValuePair<string, Color32> kvp in boneColors)
            {
                if (string.IsNullOrEmpty(kvp.Key))
                    continue;
                m_BoneColorDictionary[kvp.Key] = kvp.Value;
            }
        }

        void OnValidate()
        {
            EnsureEditorColorState();
        }
#endif

        public void OnBeforeSerialize()
        {
#if UNITY_EDITOR
            StoreBoneColors();
#endif
        }

        public void OnAfterDeserialize()
        {
#if UNITY_EDITOR
            LoadBoneColors();
#endif
        }
    }
}
