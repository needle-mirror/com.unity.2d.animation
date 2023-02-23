#if UNITY_EDITOR
using System;
using UnityEditor;

namespace UnityEngine.U2D.Animation
{
    public partial class SpriteResolver : ISerializationCallbackReceiver
    {
        bool m_SpriteLibChanged;

        /// <summary>
        /// Raised when object is deserialized in the Editor.
        /// </summary>
        public event Action onDeserializedCallback = () => { };

        void OnDidApplyAnimationProperties()
        {
            if (IsInGUIUpdateLoop())
                ResolveUpdatedValue();
        }

        internal bool spriteLibChanged
        {
            get => m_SpriteLibChanged;
            set => m_SpriteLibChanged = value;
        }

        internal void SetCategoryAndLabelEditor(string category, string label)
        {
            var so = new SerializedObject(this);
            var newHash = SpriteLibrary.GetHashForCategoryAndEntry(category, label);
            so.FindProperty(nameof(m_SpriteHash)).intValue = newHash;
            so.ApplyModifiedProperties();
        }

        /// <summary>
        /// Called before object is serialized.
        /// </summary>
        void ISerializationCallbackReceiver.OnBeforeSerialize() { }

        /// <summary>
        /// Called after object is deserialized.
        /// </summary>
        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            onDeserializedCallback();
        }
    }
}
#endif
