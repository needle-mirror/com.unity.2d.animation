using UnityEditor;
using UnityEditorInternal;

namespace UnityEngine.Experimental.U2D.Animation
{
    [CustomEditor(typeof(SpriteLibraryAsset))]
    public class SpriteLibraryAssetInspector : Editor
    {
        private SerializedProperty m_Entries;
        private ReorderableList m_EntriesList;

        private bool m_UpdateHash = false;

        private readonly float kElementHeight = EditorGUIUtility.singleLineHeight * 3;
        private readonly float kLabelWidth = 60.0f;

        public void OnEnable()
        {
            m_Entries = serializedObject.FindProperty("m_Entries");

            m_EntriesList = new ReorderableList(serializedObject, m_Entries, true, false, true, true);
            SetupOrderList();
        }

        float GetElementHeight(int index)
        {
            var property = m_Entries.GetArrayElementAtIndex(index);
            var spriteListProp = property.FindPropertyRelative("spriteList");
            if (spriteListProp.isExpanded)
                return (spriteListProp.arraySize + 1) * (EditorGUIUtility.singleLineHeight +2)+ kElementHeight;

            return kElementHeight;
        }

        void DrawElement(Rect rect, int index, bool selected, bool focused)
        {
            var property = m_Entries.GetArrayElementAtIndex(index);

            var catRect = new Rect(rect.x, rect.y, rect.width - kElementHeight, EditorGUIUtility.singleLineHeight);
            var vaRect = new Rect(rect.x, rect.y + EditorGUIUtility.singleLineHeight, rect.width - kElementHeight, rect.height - EditorGUIUtility.singleLineHeight);

            var categoryProp = property.FindPropertyRelative("category");

            var spriteListProp = property.FindPropertyRelative("spriteList");

            var oldWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = kLabelWidth;
            EditorGUI.BeginChangeCheck();
            var newCatName = EditorGUI.DelayedTextField(catRect, categoryProp.stringValue);
            EditorGUI.PropertyField(vaRect, spriteListProp, true);
            if (EditorGUI.EndChangeCheck())
            {
                m_UpdateHash = true;
                if (categoryProp.stringValue != newCatName)
                {
                    // Check if this name is already taken
                    if (!IsCategoryNameInUsed(newCatName))
                        categoryProp.stringValue = newCatName;
                }
            }

            EditorGUIUtility.labelWidth = oldWidth;
        }

        void DrawElementMulti(Rect rect, int index, bool selected, bool focused)
        {
            var property = m_Entries.GetArrayElementAtIndex(index);

            var catRect = new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight);
            var vaRect = new Rect(rect.x, rect.y + EditorGUIUtility.singleLineHeight, rect.width, EditorGUIUtility.singleLineHeight);

            var categoryProp = property.FindPropertyRelative("category");
            var spriteListProp = property.FindPropertyRelative("spriteList");
            if (spriteListProp.arraySize == 0)
            {
                for (var i = 0; i < 1; ++i)
                    spriteListProp.InsertArrayElementAtIndex(0);
                for (var i = 0; i < 1; ++i)
                {
                    var spriteEntryProp = spriteListProp.GetArrayElementAtIndex(i);
                    var spritePropertyNameProp = spriteEntryProp.FindPropertyRelative("propertyName");
                    spritePropertyNameProp.stringValue = i == 0 ? "_MainTex" : string.Format("_Tex{0}", i);
                }
            }

            EditorGUI.BeginChangeCheck();
            EditorGUI.DelayedTextField(catRect, categoryProp);
            if (EditorGUI.EndChangeCheck())
                m_UpdateHash = true;

            for (var i = 0; i < 1; ++i)
            {
                var nameRect = new Rect(rect.x + i * kElementHeight, rect.y + EditorGUIUtility.singleLineHeight * 2, kElementHeight, EditorGUIUtility.singleLineHeight);
                var spriteRect = new Rect(rect.x + i * kElementHeight, rect.y + EditorGUIUtility.singleLineHeight * 3, kElementHeight, kElementHeight);

                EditorGUIUtility.labelWidth = 1;

                var spriteEntryProp = spriteListProp.GetArrayElementAtIndex(i);
                var spriteProp = spriteEntryProp.FindPropertyRelative("sprite");
                var sprite = spriteProp.objectReferenceValue;

                EditorGUI.BeginChangeCheck();
                sprite = EditorGUI.ObjectField(spriteRect, sprite, typeof(Sprite), false);
                if (EditorGUI.EndChangeCheck())
                    spriteProp.objectReferenceValue = sprite;
            }

            EditorGUIUtility.labelWidth = 0.0f;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUI.BeginChangeCheck();
            if (EditorGUI.EndChangeCheck())
                SetupOrderList();

            m_UpdateHash = false;
            m_EntriesList.DoLayoutList();

            serializedObject.ApplyModifiedProperties();

            if (m_UpdateHash)
                (target as SpriteLibraryAsset).UpdateHashes();
        }

        bool IsCategoryNameInUsed(string name)
        {
            for (int i = 0; i < m_Entries.arraySize; ++i)
            {
                var sp = m_Entries.GetArrayElementAtIndex(i);
                if (sp.FindPropertyRelative("category").stringValue == name)
                    return true;
            }

            return false;
        }

        void OnAddCallback(ReorderableList list)
        {
            var oldSize = m_Entries.arraySize;
            m_Entries.arraySize += 1;
            const string kNewCatName = "New Category";
            string newCatName = kNewCatName;
            int catNameIncrement = 1;
            while (true)
            {
                if (IsCategoryNameInUsed(newCatName))
                    newCatName = string.Format("{0} {1}", kNewCatName, catNameIncrement++);
                else
                    break;
            }

            var sp = m_Entries.GetArrayElementAtIndex(oldSize);
            sp.FindPropertyRelative("category").stringValue = newCatName;
        }
        
        private void SetupOrderList()
        {
            m_EntriesList.drawElementCallback = DrawElement;
            m_EntriesList.elementHeight = kElementHeight;
            m_EntriesList.elementHeightCallback = GetElementHeight;
            m_EntriesList.onAddCallback = OnAddCallback;
        }
    }
}
