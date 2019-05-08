using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.U2D.Animation;

namespace UnityEditor.Experimental.U2D.Animation
{
    [CustomEditor(typeof(SpriteLibraryComponent))]
    public class SpriteLibraryComponentInspector : Editor
    {
        private SerializedProperty m_SpriteLib;

        public void OnEnable()
        {
            m_SpriteLib = serializedObject.FindProperty("m_SpriteLib");
            var go = (target as SpriteLibraryComponent).gameObject;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUI.BeginChangeCheck();
            var obj = EditorGUILayout.ObjectField("Sprite Library", m_SpriteLib.objectReferenceValue, typeof(SpriteLibraryAsset), false);
            if (EditorGUI.EndChangeCheck())
            {
                m_SpriteLib.objectReferenceValue = obj;
                serializedObject.ApplyModifiedProperties();

                var srs = (target as SpriteLibraryComponent).GetComponentsInChildren<SpriteResolver>();
                foreach (var sr in srs)
                {
                    sr.RefreshSpriteFromSpriteKey();
                    sr.spriteLibChanged = true;
                }
            }
        }
    }
}
