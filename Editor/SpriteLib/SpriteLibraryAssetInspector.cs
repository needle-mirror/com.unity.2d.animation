using UnityEditor.Callbacks;
using UnityEditor.U2D.Animation.Upgrading;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.U2D.Animation;

namespace UnityEditor.U2D.Animation
{
    [CustomEditor(typeof(SpriteLibraryAsset))]
    [MovedFrom("UnityEditor.Experimental.U2D.Animation")]
    internal class SpriteLibraryAssetInspector : Editor
    {
        [OnOpenAssetAttribute(OnOpenAssetAttributeMode.Execute)]
        public static bool ExecuteOpenSpriteLibraryAsset(int instanceID)
        {
            SpriteLibraryAsset spriteLibraryAsset = EditorUtility.InstanceIDToObject(instanceID) as SpriteLibraryAsset;
            if (spriteLibraryAsset != null)
            {
                SpriteLibraryEditor.SpriteLibraryEditorWindow.OpenWindow();

                return true;
            }

            return false;
        }

        static class Style
        {
            public static GUIContent duplicateWarningText = EditorGUIUtility.TrTextContent("Duplicate name found or name hash clashes. Please use a different name");
            public static GUIContent duplicateWarning = EditorGUIUtility.TrIconContent("console.warnicon.sml", duplicateWarningText.text);
            public static GUIContent nameLabel = new GUIContent(TextContent.label);
            public static string categoryListLabel = TextContent.categoryList;
            public static readonly string UpgradeHelpBox = L10n.Tr("This is the runtime version of the Sprite Library Source Asset. You may choose to convert this asset into a Sprite Library Source Asset for increased tooling support.");
            public static readonly string UpgradeButton = L10n.Tr("Open Sprite Library Asset Upgrader");
            public static int lineSpacing = 3;
        }

        private SerializedProperty m_Labels;
        private ReorderableList m_LabelReorderableList;

        private bool m_UpdateHash = false;

        private readonly float kElementHeight = EditorGUIUtility.singleLineHeight * 3;

        public void OnEnable()
        {
            m_Labels = serializedObject.FindProperty("m_Labels");

            m_LabelReorderableList = new ReorderableList(serializedObject, m_Labels, true, false, true, true);
            SetupOrderList();
        }

        public void OnDisable()
        {
            SpriteLibraryAsset sla = target as SpriteLibraryAsset;
            if (sla != null)
                sla.UpdateHashes();
        }

        float GetElementHeight(int index)
        {
            SerializedProperty property = m_Labels.GetArrayElementAtIndex(index);
            SerializedProperty spriteListProp = property.FindPropertyRelative("m_CategoryList");
            if (spriteListProp.isExpanded)
                return (spriteListProp.arraySize + 1) * (EditorGUIUtility.singleLineHeight + Style.lineSpacing) + kElementHeight;

            return kElementHeight;
        }

        void DrawElement(Rect rect, int index, bool selected, bool focused)
        {
            SerializedProperty property = m_Labels.GetArrayElementAtIndex(index);

            Rect catRect = new Rect(rect.x, rect.y, rect.width - kElementHeight, EditorGUIUtility.singleLineHeight);
            Rect vaRect = new Rect(rect.x, rect.y + EditorGUIUtility.singleLineHeight, rect.width - kElementHeight, EditorGUIUtility.singleLineHeight);

            SerializedProperty categoryProp = property.FindPropertyRelative("m_Name");

            SerializedProperty spriteListProp = property.FindPropertyRelative("m_CategoryList");

            EditorGUI.BeginChangeCheck();
            string newCatName = EditorGUI.DelayedTextField(catRect, categoryProp.stringValue);
            if (EditorGUI.EndChangeCheck())
            {
                newCatName = newCatName.Trim();
                m_UpdateHash = true;
                if (categoryProp.stringValue != newCatName)
                {
                    // Check if this nameLabel is already taken
                    if (!IsNameInUsed(newCatName, m_Labels, "m_Name", 0))
                        categoryProp.stringValue = newCatName;
                    else
                        Debug.LogWarning(Style.duplicateWarningText.text);
                }
            }

            spriteListProp.isExpanded = EditorGUI.Foldout(vaRect, spriteListProp.isExpanded, Style.categoryListLabel);
            if (spriteListProp.isExpanded)
            {
                EditorGUI.indentLevel++;
                Rect indentedRect = EditorGUI.IndentedRect(vaRect);
                float labelWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 40 + indentedRect.x - vaRect.x;
                indentedRect.y += EditorGUIUtility.singleLineHeight + Style.lineSpacing;
                Rect sizeRect = indentedRect;
                int size = EditorGUI.IntField(sizeRect, TextContent.size, spriteListProp.arraySize);
                if (size != spriteListProp.arraySize && size >= 0)
                    spriteListProp.arraySize = size;
                indentedRect.y += EditorGUIUtility.singleLineHeight + Style.lineSpacing;
                DrawSpriteListProperty(indentedRect, spriteListProp);
                EditorGUIUtility.labelWidth = labelWidth;
                EditorGUI.indentLevel--;
            }
        }

        void DrawSpriteListProperty(Rect rect, SerializedProperty spriteListProp)
        {
            for (int i = 0; i < spriteListProp.arraySize; ++i)
            {
                SerializedProperty element = spriteListProp.GetArrayElementAtIndex(i);
                EditorGUI.BeginChangeCheck();
                string oldName = element.FindPropertyRelative("m_Name").stringValue;
                Rect nameRect = new Rect(rect.x, rect.y, rect.width / 2, EditorGUIUtility.singleLineHeight);
                bool nameDuplicate = IsNameInUsed(oldName, spriteListProp, "m_Name", 1);
                if (nameDuplicate)
                {
                    nameRect.width -= 20;
                }

                string newName = EditorGUI.DelayedTextField(
                    nameRect,
                    Style.nameLabel,
                    oldName);
                if (nameDuplicate)
                {
                    nameRect.x += nameRect.width;
                    nameRect.width = 20;
                    GUI.Label(nameRect, Style.duplicateWarning);
                }

                if (EditorGUI.EndChangeCheck())
                {
                    newName = newName.Trim();
                    element.FindPropertyRelative("m_Name").stringValue = newName;
                }

                EditorGUI.PropertyField(new Rect(rect.x + rect.width / 2 + 5, rect.y, rect.width / 2, EditorGUIUtility.singleLineHeight),
                    element.FindPropertyRelative("m_Sprite"));
                rect.y += EditorGUIUtility.singleLineHeight + Style.lineSpacing;
            }
        }

        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(Style.UpgradeHelpBox, MessageType.Info);
            if (GUILayout.Button(Style.UpgradeButton))
                AssetUpgraderWindow.OpenWindow();

            EditorGUILayout.Space(10);

            serializedObject.Update();
            EditorGUI.BeginChangeCheck();
            if (EditorGUI.EndChangeCheck())
                SetupOrderList();

            m_UpdateHash = false;
            m_LabelReorderableList.DoLayoutList();

            serializedObject.ApplyModifiedProperties();

            if (m_UpdateHash)
                (target as SpriteLibraryAsset).UpdateHashes();
        }

        bool IsNameInUsed(string name, SerializedProperty property, string propertyField, int threshold)
        {
            int count = 0;
            int nameHash = SpriteLibraryUtility.GetStringHash(name);
            for (int i = 0; i < property.arraySize; ++i)
            {
                SerializedProperty sp = property.GetArrayElementAtIndex(i);
                string otherName = sp.FindPropertyRelative(propertyField).stringValue;
                int otherNameHash = SpriteLibraryUtility.GetStringHash(otherName);
                if (otherName == name || nameHash == otherNameHash)
                {
                    count++;
                    if (count > threshold)
                        return true;
                }
            }

            return false;
        }

        void OnAddCallback(ReorderableList list)
        {
            int oldSize = m_Labels.arraySize;
            m_Labels.arraySize += 1;
            const string kNewCatName = "New Category";
            string newCatName = kNewCatName;
            int catNameIncrement = 1;
            while (true)
            {
                if (IsNameInUsed(newCatName, m_Labels, "m_Name", 0))
                    newCatName = string.Format("{0} {1}", kNewCatName, catNameIncrement++);
                else
                    break;
            }

            SerializedProperty sp = m_Labels.GetArrayElementAtIndex(oldSize);
            sp.FindPropertyRelative("m_Name").stringValue = newCatName;
            sp.FindPropertyRelative("m_Hash").intValue = SpriteLibraryUtility.GetStringHash(newCatName);
        }

        private void SetupOrderList()
        {
            m_LabelReorderableList.drawElementCallback = DrawElement;
            m_LabelReorderableList.elementHeight = kElementHeight;
            m_LabelReorderableList.elementHeightCallback = GetElementHeight;
            m_LabelReorderableList.onAddCallback = OnAddCallback;
        }
    }
}
