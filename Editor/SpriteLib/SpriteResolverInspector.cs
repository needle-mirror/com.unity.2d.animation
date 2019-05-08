using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Experimental.U2D.Animation;

namespace UnityEditor.Experimental.U2D.Animation
{
    [CustomEditor(typeof(SpriteResolver))]
    internal class SpriteResolverInspector : Editor
    {
        static class Style
        {
            public static GUIContent noSpriteLibContainer = EditorGUIUtility.TrTextContent("No Sprite Library Container Component found");
            public static GUIContent categoryLabel = EditorGUIUtility.TrTextContent("Category");
            public static GUIContent indexLabel = EditorGUIUtility.TrTextContent("Index");
            public static GUIContent categoryIsEmptyLabel = EditorGUIUtility.TrTextContent("Category is Empty");
        }

        struct SpriteCategorySelectionList
        {
            public string[] names;
            public Sprite[] sprites;
        }

        private SerializedProperty m_SpriteKeyCategory;
        private SerializedProperty m_SpriteKeyVariant;
        private SerializedProperty m_CategoryHash;

        Dictionary<string, SpriteCategorySelectionList> m_SpriteLibSelection = new Dictionary<string, SpriteCategorySelectionList>();
        string[] m_CategorySelection;
        int m_CategorySelectionIndex = 0;
        SpriteSelectorWidget m_SpriteSelectorWidget = new SpriteSelectorWidget();

        public void OnEnable()
        {
            m_SpriteKeyCategory = serializedObject.FindProperty("m_Category");
            m_CategoryHash = serializedObject.FindProperty("m_CategoryHash");
            m_SpriteKeyVariant = serializedObject.FindProperty("m_Index");

            UpdateSpriteLibrary();
        }

        SpriteResolver spriteResolver { get {return target as SpriteResolver; } }

        void UpdateSpriteLibrary()
        {
            var spriteLibs = spriteResolver.GetComponentsInParent<SpriteLibraryComponent>().Select(x => x).ToArray();
            foreach (var spriteLib in spriteLibs)
            {
                foreach (var entries in spriteLib.entries)
                {
                    if (!m_SpriteLibSelection.ContainsKey(entries.category))
                    {
                        var selectionList = new SpriteCategorySelectionList()
                        {
                            names = new string[entries.spriteList.Count],
                            sprites = new Sprite[entries.spriteList.Count]
                        };
                        for (int i = 0; i < entries.spriteList.Count; ++i)
                        {
                            var spriteName = entries.spriteList[i] != null ? entries.spriteList[i].name : "Missing Sprite";
                            selectionList.names[i] = string.Format("{0} - {1}", i, spriteName);
                            selectionList.sprites[i] = entries.spriteList[i];
                        }
                        m_SpriteLibSelection.Add(entries.category, selectionList);
                    }
                }
            }
            m_CategorySelection = new string[1 + m_SpriteLibSelection.Keys.Count];
            m_CategorySelection[0] = "None";
            for (int i = 0; i < m_SpriteLibSelection.Keys.Count; ++i)
            {
                m_CategorySelection[i + 1] = m_SpriteLibSelection.Keys.ElementAt(i);
            }
            m_CategorySelectionIndex = Array.FindIndex(m_CategorySelection, x => x == m_SpriteKeyCategory.stringValue);
            if (m_CategorySelectionIndex == -1)
                m_CategorySelectionIndex = 0;
            if (m_CategorySelectionIndex > 0)
                m_SpriteSelectorWidget.UpdateContents(m_SpriteLibSelection[m_CategorySelection[m_CategorySelectionIndex]].sprites);

            spriteResolver.spriteLibChanged = false;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            if (spriteResolver.spriteLibChanged)
                UpdateSpriteLibrary();

            m_CategorySelectionIndex = Array.FindIndex(m_CategorySelection, x => x == m_SpriteKeyCategory.stringValue);
            if (m_CategorySelectionIndex == -1)
                m_CategorySelectionIndex = 0;

            if (m_CategorySelection.Length == 1)
            {
                EditorGUILayout.LabelField(Style.noSpriteLibContainer);
            }
            else
            {
                EditorGUI.BeginChangeCheck();
                var selectionIndex = m_SpriteKeyVariant.intValue;
                m_CategorySelectionIndex = EditorGUILayout.Popup(Style.categoryLabel, m_CategorySelectionIndex, m_CategorySelection);
                if (m_CategorySelectionIndex != 0)
                {
                    var selection = m_SpriteLibSelection[m_CategorySelection[m_CategorySelectionIndex]];
                    if (selection.names.Length <= 0)
                    {
                        EditorGUILayout.LabelField(Style.categoryIsEmptyLabel);
                    }
                    else
                    {
                        if (selectionIndex < 0 || selectionIndex >= selection.names.Length)
                            selectionIndex = 0;
                        selectionIndex = EditorGUILayout.Popup(Style.indexLabel, selectionIndex, selection.names);
                        selectionIndex = m_SpriteSelectorWidget.ShowGUI(selectionIndex);
                    }
                }

                if (EditorGUI.EndChangeCheck())
                {
                    if (m_CategorySelectionIndex > 0)
                        m_SpriteSelectorWidget.UpdateContents(m_SpriteLibSelection[m_CategorySelection[m_CategorySelectionIndex]].sprites);
                    m_SpriteKeyCategory.stringValue = m_CategorySelectionIndex > 0 ? m_CategorySelection[m_CategorySelectionIndex] : "";
                    m_SpriteKeyVariant.intValue = selectionIndex;
                    m_CategoryHash.intValue = SpriteLibraryAsset.GetCategoryHash(m_SpriteKeyCategory.stringValue);
                    serializedObject.ApplyModifiedProperties();

                    var sf = target as SpriteResolver;
                    sf.RefreshSpriteFromSpriteKey();
                }
                serializedObject.ApplyModifiedProperties();
            }
        }
    }
}
