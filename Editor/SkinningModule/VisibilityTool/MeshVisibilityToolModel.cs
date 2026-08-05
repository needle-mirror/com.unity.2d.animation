using UnityEngine;

namespace UnityEditor.U2D.Animation
{
    internal class MeshVisibilityToolModel : SkinningObject
    {
        [SerializeField]
        bool m_AllVisibility = true;

        public bool allVisibility
        {
            get => m_AllVisibility;
            set => m_AllVisibility = value;
        }

        public static void SetMeshVisibility(SpriteCache sprite, bool visibility) { }

        public static bool GetMeshVisibility(SpriteCache sprite)
        {
            return false;
        }

        public bool ShouldDisable(SpriteCache sprite)
        {
            MeshCache mesh = sprite.GetMesh();
            return mesh == null || mesh.vertices.Length == 0;
        }

        public bool previousVisibility { get; set; } = true;
    }
}
