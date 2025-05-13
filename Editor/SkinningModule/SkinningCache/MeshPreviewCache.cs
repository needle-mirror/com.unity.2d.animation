using System.Collections.Generic;
using UnityEngine;

namespace UnityEditor.U2D.Animation
{
    internal class MeshPreviewCache : SkinningObject
    {
        [SerializeField]
        SpriteCache m_Sprite;
        [SerializeField]
        Mesh m_Mesh;
        [SerializeField]
        Mesh m_DefaultMesh;
        List<Vector3> m_SkinnedVertices = new List<Vector3>();
        List<Vector3> m_Vertices = new List<Vector3>();
        List<BoneWeight> m_Weights = new List<BoneWeight>();
        List<Vector2> m_TexCoords = new List<Vector2>();
        List<Color> m_Colors = new List<Color>();
        List<Matrix4x4> m_SkinningMatrices = new List<Matrix4x4>();
        bool m_MeshDirty;
        bool m_VerticesDirty;
        bool m_SkinningDirty;
        bool m_WeightsDirty;
        bool m_IndicesDirty;
        bool m_ColorsDirty;
        bool m_EnableSkinning;

        public SpriteCache sprite
        {
            get => m_Sprite;
            set
            {
                m_Sprite = value;
                InitializeDefaultMesh();
                SetMeshDirty();
            }
        }

        public Mesh mesh => m_Mesh;
        public Mesh defaultMesh => m_DefaultMesh;

        public bool enableSkinning
        {
            get => m_EnableSkinning;

            set
            {
                if (m_EnableSkinning != value)
                {
                    m_EnableSkinning = value;
                    SetSkinningDirty();
                }
            }
        }

        public bool canSkin => CanSkin();

        public List<Vector3> vertices
        {
            get
            {
                if (enableSkinning && canSkin)
                    return m_SkinnedVertices;

                return m_Vertices;
            }
        }

        bool CanSkin()
        {
            if (m_Vertices.Count == 0 || m_Vertices.Count != m_Weights.Count)
                return false;

            BoneCache[] bones = sprite.GetBonesFromMode();

            Debug.Assert(bones != null);

            if (bones.Length == 0)
                return false;

            foreach (BoneWeight weight in m_Weights)
            {
                if (weight.boneIndex0 < 0 || weight.boneIndex0 >= bones.Length ||
                    weight.boneIndex1 < 0 || weight.boneIndex1 >= bones.Length ||
                    weight.boneIndex2 < 0 || weight.boneIndex2 >= bones.Length ||
                    weight.boneIndex3 < 0 || weight.boneIndex3 >= bones.Length)
                    return false;
            }

            return true;
        }

        internal override void OnCreate()
        {
            m_Mesh = CreateMesh();
            m_DefaultMesh = CreateMesh();
        }

        internal override void OnDestroy()
        {
            DestroyImmediate(m_Mesh);
            DestroyImmediate(m_DefaultMesh);
        }

        static Mesh CreateMesh()
        {
            Mesh mesh = new Mesh();
            mesh.MarkDynamic();
            mesh.hideFlags = HideFlags.DontSave;

            return mesh;
        }

        void InitializeDefaultMesh()
        {
            Debug.Assert(sprite != null);
            Debug.Assert(m_DefaultMesh != null);

            MeshCache meshCache = sprite.GetMesh();

            Debug.Assert(meshCache != null);

            meshCache.textureDataProvider.GetTextureActualWidthAndHeight(out int width, out int height);

            Vector2 uvScale = new Vector2(1f / width, 1f / height);
            Vector3 position = sprite.textureRect.position;
            Vector2 size = sprite.textureRect.size;

            List<Vector3> defaultVerts = new List<Vector3>()
            {
                Vector3.zero,
                new Vector3(0f, size.y, 0f),
                new Vector3(size.x, 0f, 0f),
                size,
            };

            List<Vector2> uvs = new List<Vector2>()
            {
                Vector3.Scale(defaultVerts[0] + position, uvScale),
                Vector3.Scale(defaultVerts[1] + position, uvScale),
                Vector3.Scale(defaultVerts[2] + position, uvScale),
                Vector3.Scale(defaultVerts[3] + position, uvScale),
            };

            m_DefaultMesh.SetVertices(defaultVerts);
            m_DefaultMesh.SetUVs(0, uvs);
            m_DefaultMesh.SetColors(new List<Color>
            {
                Color.black,
                Color.black,
                Color.black,
                Color.black
            });
            m_DefaultMesh.SetIndices(new int[]
            {
                0, 1, 3, 0, 3, 2
            },
                MeshTopology.Triangles, 0);

            m_DefaultMesh.UploadMeshData(false);
        }

        public void SetMeshDirty()
        {
            m_MeshDirty = true;
        }

        public void SetSkinningDirty()
        {
            m_SkinningDirty = true;
        }

        public void SetWeightsDirty()
        {
            m_WeightsDirty = true;
        }

        public void SetColorsDirty()
        {
            m_ColorsDirty = true;
        }

        public void Prepare()
        {
            bool meshChanged = false;
            MeshCache meshCache = sprite.GetMesh();

            Debug.Assert(meshCache != null);

            m_MeshDirty |= m_Vertices.Count != meshCache.vertices.Length;

            if (m_MeshDirty)
            {
                m_Mesh.Clear();
                m_VerticesDirty = true;
                m_WeightsDirty = true;
                m_IndicesDirty = true;
                m_SkinningDirty = true;
                m_MeshDirty = false;
            }

            if (m_VerticesDirty)
            {
                m_Vertices.Clear();
                m_TexCoords.Clear();

                meshCache.textureDataProvider.GetTextureActualWidthAndHeight(out int width, out int height);

                Vector2 uvScale = new Vector2(1f / width, 1f / height);

                foreach (Vector2 vertex in meshCache.vertices)
                {
                    m_Vertices.Add(vertex);
                    m_TexCoords.Add(Vector2.Scale(vertex + sprite.textureRect.position, uvScale));
                }

                m_Mesh.SetVertices(m_Vertices);
                m_Mesh.SetUVs(0, m_TexCoords);
                meshChanged = true;
                m_VerticesDirty = false;
            }

            if (m_WeightsDirty)
            {
                m_Weights.Clear();

                for (int i = 0; i < meshCache.vertexWeights.Length; ++i)
                {
                    EditableBoneWeight weight = meshCache.vertexWeights[i];
                    m_Weights.Add(weight.ToBoneWeight(true));
                }

                SetColorsDirty();
                meshChanged = true;
                m_WeightsDirty = false;
            }

            if (m_ColorsDirty)
            {
                PrepareColors();

                m_Mesh.SetColors(m_Colors);
                meshChanged = true;
                m_ColorsDirty = false;
            }

            if (m_IndicesDirty)
            {
                m_Mesh.SetTriangles(meshCache.indices, 0);
                meshChanged = true;
                m_IndicesDirty = false;
            }

            if (m_SkinningDirty)
            {
                if (enableSkinning && canSkin)
                {
                    SkinVertices();
                    m_Mesh.SetVertices(m_SkinnedVertices);
                    meshChanged = true;
                }

                m_SkinningDirty = false;
            }

            if (meshChanged)
            {
                m_Mesh.UploadMeshData(false);
                m_Mesh.RecalculateBounds();
                skinningCache.events.meshPreviewChanged.Invoke(this);
            }
        }

        void PrepareColors()
        {
            BoneCache[] bones = sprite.GetBonesFromMode();

            Debug.Assert(bones != null);

            m_Colors.Clear();

            for (int i = 0; i < m_Weights.Count; ++i)
            {
                BoneWeight boneWeight = m_Weights[i];
                float weightSum = 0f;
                Color color = Color.black;

                for (int j = 0; j < 4; ++j)
                {
                    int boneIndex = boneWeight.GetBoneIndex(j);
                    float weight = boneWeight.GetWeight(j);

                    if (boneIndex >= 0 && boneIndex < bones.Length)
                        color += bones[boneIndex].bindPoseColor * weight;

                    weightSum += weight;
                }

                color.a = 1f;

                m_Colors.Add(Color.Lerp(Color.black, color, weightSum));
            }
        }

        void SkinVertices()
        {
            Debug.Assert(canSkin);
            Debug.Assert(sprite != null);

            BoneCache[] bones = sprite.GetBonesFromMode();

            Matrix4x4 originMatrix = Matrix4x4.TRS(sprite.pivotRectSpace, Quaternion.identity, Vector3.one);
            Matrix4x4 originInverseMatrix = originMatrix.inverse;
            Matrix4x4 spriteMatrix = sprite.GetLocalToWorldMatrixFromMode();
            Matrix4x4 spriteMatrixInv = spriteMatrix.inverse;

            m_SkinnedVertices.Clear();
            m_SkinningMatrices.Clear();

            for (int i = 0; i < bones.Length; ++i)
                m_SkinningMatrices.Add(spriteMatrixInv * originInverseMatrix * bones[i].localToWorldMatrix * bones[i].bindPose.matrix.inverse * spriteMatrix);

            for (int i = 0; i < m_Vertices.Count; ++i)
            {
                Vector3 position = m_Vertices[i];
                BoneWeight boneWeight = m_Weights[i];
                float weightSum = boneWeight.weight0 + boneWeight.weight1 + boneWeight.weight2 + boneWeight.weight3;

                if (weightSum > 0f)
                {
                    float weightSumInv = 1f / weightSum;
                    Vector3 skinnedPosition = m_SkinningMatrices[boneWeight.boneIndex0].MultiplyPoint3x4(position) * boneWeight.weight0 * weightSumInv +
                        m_SkinningMatrices[boneWeight.boneIndex1].MultiplyPoint3x4(position) * boneWeight.weight1 * weightSumInv +
                        m_SkinningMatrices[boneWeight.boneIndex2].MultiplyPoint3x4(position) * boneWeight.weight2 * weightSumInv +
                        m_SkinningMatrices[boneWeight.boneIndex3].MultiplyPoint3x4(position) * boneWeight.weight3 * weightSumInv;

                    position = Vector3.Lerp(position, skinnedPosition, weightSum);
                }

                m_SkinnedVertices.Add(position);
            }
        }

        protected override void OnAfterDeserialize()
        {
            SetMeshDirty();
        }
    }
}
