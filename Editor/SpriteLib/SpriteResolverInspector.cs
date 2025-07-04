using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.U2D.Animation;
using UnityEngine.U2D.Common;

namespace UnityEditor.U2D.Animation
{
    [CustomEditor(typeof(SpriteResolver))]
    [MovedFrom("UnityEditor.Experimental.U2D.Animation")]
    internal class SpriteResolverInspector : Editor
    {
        static class Style
        {
            public static GUIContent categoryLabel = EditorGUIUtility.TrTextContent("Category");
            public static GUIContent labelLabel = EditorGUIUtility.TrTextContent("Label");
            public static GUIContent categoryIsEmptyLabel = EditorGUIUtility.TrTextContent("Category is Empty");
            public static GUIContent noCategory = EditorGUIUtility.TrTextContent("No Category");
            public static string[] emptyCategoryDropDownOption = new[] { Style.categoryIsEmptyLabel.text };
        }

        struct SpriteCategorySelectionList
        {
            public string categoryName;
            public string[] entryNames;
            public Sprite[] sprites;
        }

        SerializedProperty m_SpriteHash;
        SerializedProperty m_SpriteKey;
        SerializedProperty m_LabelHash;
        SerializedProperty m_CategoryHash;
        SpriteSkin m_SpriteSkin;
        Dictionary<string, SpriteCategorySelectionList> m_SpriteLibSelection = new Dictionary<string, SpriteCategorySelectionList>();
        string[] m_CategorySelection;
        int m_CategorySelectionIndex = 0;
        int m_LabelSelectionIndex = 0;
        string m_PreviousCategoryValue;
        string m_PreviousLabelValue;
        bool m_IgnoreNextDeserializeCallback;
        bool m_ReInitOnNextGUI;
        SpriteSelectorWidget m_SpriteSelectorWidget = new SpriteSelectorWidget();

        public void OnEnable()
        {
            m_SpriteSelectorWidget.Initialize(GetInstanceID());

            m_SpriteHash = serializedObject.FindProperty("m_SpriteHash");
            m_SpriteKey = serializedObject.FindProperty("m_SpriteKey");
            m_LabelHash = serializedObject.FindProperty("m_labelHash");
            m_CategoryHash = serializedObject.FindProperty("m_CategoryHash");
            m_SpriteSkin = (target as SpriteResolver).GetComponent<SpriteSkin>();
            UpdateSpriteLibrary();
            spriteResolver.onDeserializedCallback += SpriteResolverDeserializedCallback;

            EditorApplication.focusChanged += OnEditorFocusChanged;
        }

        void OnDisable()
        {
            EditorApplication.focusChanged -= OnEditorFocusChanged;
        }

        void OnDestroy()
        {
            m_SpriteSelectorWidget.Dispose();
        }

        void SpriteResolverDeserializedCallback()
        {
            if (!m_IgnoreNextDeserializeCallback)
            {
                m_ReInitOnNextGUI = true;
            }
        }

        SpriteResolver spriteResolver => target as SpriteResolver;

        bool IsSpriteHashAssigned => m_SpriteHash.intValue != 0;

        void OnEditorFocusChanged(bool focused)
        {
            if (focused)
                m_ReInitOnNextGUI = true;
        }

        void GetCategoryAndLabelStringValue(out string categoryName, out string labelName)
        {
            categoryName = null;
            labelName = null;
            SpriteLibrary spriteLib = spriteResolver.spriteLibrary;
            if (spriteLib != null)
            {
                int entryHash = m_SpriteHash.intValue;
                spriteLib.GetCategoryAndEntryNameFromHash(entryHash, out categoryName, out labelName);

                if (!IsSpriteHashAssigned && (string.IsNullOrEmpty(categoryName) || string.IsNullOrEmpty(labelName)))
                {
                    m_SpriteHash.intValue = InternalEngineBridge.ConvertFloatToInt(m_SpriteKey.floatValue);
                    entryHash = m_SpriteHash.intValue;
                    spriteLib.GetCategoryAndEntryNameFromHash(entryHash, out categoryName, out labelName);
                }

                if (!IsSpriteHashAssigned && (string.IsNullOrEmpty(categoryName) || string.IsNullOrEmpty(labelName)))
                {
                    int labelHash = InternalEngineBridge.ConvertFloatToInt(m_LabelHash.floatValue);
                    int categoryHash = InternalEngineBridge.ConvertFloatToInt(m_CategoryHash.floatValue);
                    m_SpriteHash.intValue = SpriteResolver.ConvertCategoryLabelHashToSpriteKey(spriteLib, categoryHash, labelHash);
                    entryHash = m_SpriteHash.intValue;
                    spriteLib.GetCategoryAndEntryNameFromHash(entryHash, out categoryName, out labelName);
                }
            }
        }

        void UpdateSpriteLibrary()
        {
            m_SpriteLibSelection.Clear();

            SpriteLibrary spriteLib = spriteResolver.spriteLibrary;
            string categoryName = "", labelName = "";
            if (spriteLib != null)
            {
                GetCategoryAndLabelStringValue(out categoryName, out labelName);
                IEnumerable<string> enumerator = spriteLib.categoryNames;
                foreach (string category in enumerator)
                {
                    if (!m_SpriteLibSelection.ContainsKey(category))
                    {
                        IEnumerable<string> entries = spriteLib.GetEntryNames(category);
                        if (entries == null)
                            entries = new string[0];

                        SpriteCategorySelectionList selectionList = new SpriteCategorySelectionList()
                        {
                            entryNames = entries.ToArray(),
                            sprites = entries.Select(x =>
                            {
                                return spriteLib.GetSprite(category, x);
                            }).ToArray(),
                            categoryName = category,
                        };

                        m_SpriteLibSelection.Add(category, selectionList);

                    }
                }
            }

            m_CategorySelection = new string[1 + m_SpriteLibSelection.Keys.Count];
            m_CategorySelection[0] = Style.noCategory.text;
            for (int i = 0; i < m_SpriteLibSelection.Keys.Count; ++i)
            {
                SpriteCategorySelectionList selection = m_SpriteLibSelection[m_SpriteLibSelection.Keys.ElementAt(i)];
                m_CategorySelection[i + 1] = selection.categoryName;
                if (selection.categoryName == categoryName)
                    m_CategorySelectionIndex = i + 1;
            }

            ValidateCategorySelectionIndexValue();
            if (m_CategorySelectionIndex > 0)
            {
                categoryName = m_CategorySelection[m_CategorySelectionIndex];
                m_SpriteSelectorWidget.UpdateContents(
                    m_SpriteLibSelection[m_CategorySelection[m_CategorySelectionIndex]].sprites);
                if (m_SpriteLibSelection.ContainsKey(categoryName))
                {
                    int labelIndex = Array.FindIndex(m_SpriteLibSelection[categoryName].entryNames,
                        x => x == labelName);

                    if (labelIndex >= 0 ||
                        m_SpriteLibSelection[categoryName].entryNames.Length <= m_LabelSelectionIndex)
                    {
                        m_LabelSelectionIndex = labelIndex;
                    }
                }
            }
            else
            {
                m_SpriteSelectorWidget.UpdateContents(new Sprite[0]);
            }

            spriteResolver.spriteLibChanged = false;
        }

        void ValidateCategorySelectionIndexValue()
        {
            if (m_CategorySelectionIndex < 0 || m_CategorySelection.Length <= m_CategorySelectionIndex)
                m_CategorySelectionIndex = 0;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            if (m_ReInitOnNextGUI)
            {
                m_ReInitOnNextGUI = false;
                UpdateSpriteLibrary();
            }

            if (spriteResolver.spriteLibChanged)
                UpdateSpriteLibrary();

            GetCategoryAndLabelStringValue(out string currentCategoryValue, out string currentLabelValue);

            m_CategorySelectionIndex = Array.FindIndex(m_CategorySelection, x => x == currentCategoryValue);
            ValidateCategorySelectionIndexValue();

            EditorGUI.BeginChangeCheck();
            using (new EditorGUI.DisabledScope(m_CategorySelection.Length <= 1))
                m_CategorySelectionIndex = EditorGUILayout.Popup(Style.categoryLabel, m_CategorySelectionIndex, m_CategorySelection);

            SpriteCategorySelectionList selection;
            m_SpriteLibSelection.TryGetValue(m_CategorySelection[m_CategorySelectionIndex], out selection);

            string[] entryNames = Style.emptyCategoryDropDownOption;
            if (selection.entryNames != null)
                entryNames = selection.entryNames;
            if (m_LabelSelectionIndex < 0 || m_LabelSelectionIndex >= entryNames.Length)
                m_LabelSelectionIndex = 0;
            using (new EditorGUI.DisabledScope(m_CategorySelectionIndex == 0 || entryNames.Length == 0))
            {
                if (entryNames.Length == 0)
                {
                    m_LabelSelectionIndex = EditorGUILayout.Popup(Style.labelLabel, 0, new[] { Style.categoryIsEmptyLabel });
                }
                else
                {
                    m_LabelSelectionIndex = EditorGUILayout.Popup(Style.labelLabel, m_LabelSelectionIndex, entryNames);
                }
            }

            m_LabelSelectionIndex = m_SpriteSelectorWidget.ShowGUI(m_LabelSelectionIndex);


            if (EditorGUI.EndChangeCheck())
            {
                currentCategoryValue = m_CategorySelection[m_CategorySelectionIndex];
                if (m_SpriteLibSelection.ContainsKey(currentCategoryValue))
                {
                    string[] hash = m_SpriteLibSelection[currentCategoryValue].entryNames;
                    if (hash.Length > 0)
                    {
                        if (m_LabelSelectionIndex < 0 || m_LabelSelectionIndex >= hash.Length)
                            m_LabelSelectionIndex = 0;
                        currentLabelValue = m_SpriteLibSelection[currentCategoryValue].entryNames[m_LabelSelectionIndex];
                    }
                }

                m_SpriteHash.intValue = SpriteLibrary.GetHashForCategoryAndEntry(currentCategoryValue, currentLabelValue);
                ApplyModifiedProperty();

                SpriteResolver sf = target as SpriteResolver;
                if (m_SpriteSkin != null)
                    m_SpriteSkin.ignoreNextSpriteChange = true;
                sf.ResolveSpriteToSpriteRenderer();
            }

            if (m_PreviousCategoryValue != currentCategoryValue)
            {
                if (!string.IsNullOrEmpty(currentCategoryValue) && m_SpriteLibSelection.ContainsKey(currentCategoryValue))
                    m_SpriteSelectorWidget.UpdateContents(m_SpriteLibSelection[currentCategoryValue].sprites);
                else
                    m_SpriteSelectorWidget.UpdateContents(Array.Empty<Sprite>());

                Repaint();

                m_PreviousCategoryValue = currentCategoryValue;
            }

            if (!string.IsNullOrEmpty(currentLabelValue) && m_PreviousLabelValue != currentLabelValue)
            {
                if (m_SpriteLibSelection.ContainsKey(currentCategoryValue))
                    m_LabelSelectionIndex = Array.FindIndex(m_SpriteLibSelection[currentCategoryValue].entryNames, x => x == currentLabelValue);
                m_PreviousLabelValue = currentLabelValue;
            }

            ApplyModifiedProperty();
            if (m_SpriteSelectorWidget.UpdateSpritePreviews())
                this.Repaint();
        }

        void ApplyModifiedProperty()
        {
            m_IgnoreNextDeserializeCallback = true;
            serializedObject.ApplyModifiedProperties();
            m_IgnoreNextDeserializeCallback = false;
        }
    }
}
