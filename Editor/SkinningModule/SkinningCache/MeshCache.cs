using System.Collections.Generic;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace UnityEditor.U2D.Animation
{
    internal class MeshCache : SkinningObject, ISpriteMeshData
    {
        [SerializeField]
        SpriteCache m_Sprite;
        [SerializeField] 
        Vector2[] m_Vertices = new Vector2[0];
        [SerializeField] 
        EditableBoneWeight[] m_VertexWeights = new EditableBoneWeight[0];
        [SerializeField] 
        int[] m_Indices = new int[0];
        [SerializeField] 
        Vector2Int[] m_Edges = new Vector2Int[0];
        [SerializeField]
        List<BoneCache> m_Bones = new List<BoneCache>();
        
        public ITextureDataProvider textureDataProvider { get; set; }

        public SpriteCache sprite
        {
            get => m_Sprite;
            set => m_Sprite = value;
        }

        public string spriteName => sprite.name;
        public Vector2[] vertices => m_Vertices;
        public EditableBoneWeight[] vertexWeights => m_VertexWeights;

        public Vector2Int[] edges
        {
            get => m_Edges;
            set => m_Edges = value;
        }

        public int[] indices
        {
            get => m_Indices;
            set => m_Indices = value;
        }

        public BoneCache[] bones
        {
            get => m_Bones.ToArray();
            set => SetBones(value);
        }

        Rect ISpriteMeshData.frame => sprite.textureRect;
        public int vertexCount => m_Vertices.Length;
        public int boneCount => m_Bones.Count;

        public void SetVertices(Vector2[] newVertices, EditableBoneWeight[] newWeights)
        {
            m_Vertices = newVertices;
            m_VertexWeights = newWeights;
        }

        public void AddVertex(Vector2 position, BoneWeight weight)
        {
            var listOfVertices = new List<Vector2>(m_Vertices);
            listOfVertices.Add(position);
            m_Vertices = listOfVertices.ToArray();

            var listOfWeights = new List<EditableBoneWeight>(m_VertexWeights);
            listOfWeights.Add(EditableBoneWeightUtility.CreateFromBoneWeight(weight));
            m_VertexWeights = listOfWeights.ToArray();
        }

        public void RemoveVertex(int index)
        {
            var listOfVertices = new List<Vector2>(m_Vertices);
            listOfVertices.RemoveAt(index);
            m_Vertices = listOfVertices.ToArray();

            var listOfWeights = new List<EditableBoneWeight>(m_VertexWeights);
            listOfWeights.RemoveAt(index);
            m_VertexWeights = listOfWeights.ToArray();
        }

        SpriteBoneData ISpriteMeshData.GetBoneData(int index)
        {
            var worldToLocalMatrix = sprite.worldToLocalMatrix;

            //We expect m_Bones to contain character's bones references if character exists. Sprite's skeleton bones otherwise.
            if (skinningCache.hasCharacter)
                worldToLocalMatrix = sprite.GetCharacterPart().worldToLocalMatrix;

            SpriteBoneData spriteBoneData;
            var bone = m_Bones[index];

            if (bone == null)
                spriteBoneData = new SpriteBoneData();
            else
            {
                spriteBoneData = new SpriteBoneData()
                {
                    parentId = bone.parentBone == null ? -1 : m_Bones.IndexOf(bone.parentBone),
                    localPosition = bone.localPosition,
                    localRotation = bone.localRotation,
                    position = worldToLocalMatrix.MultiplyPoint3x4(bone.position),
                    endPosition = worldToLocalMatrix.MultiplyPoint3x4(bone.endPosition),
                    depth = bone.depth,
                    length = bone.localLength
                };
            }

            return spriteBoneData;
        }

        float ISpriteMeshData.GetBoneDepth(int index)
        {
            return m_Bones[index].depth;
        }

        public void Clear()
        {
            m_Indices = new int[0];
            m_Vertices = new Vector2[0];
            m_VertexWeights = new EditableBoneWeight[0];
            m_Edges = new Vector2Int[0];
        }

        public bool ContainsBone(BoneCache bone)
        {
            return m_Bones.Contains(bone);
        }

        public void SetCompatibleBoneSet(BoneCache[] boneCache)
        {
            m_Bones = new List<BoneCache>(boneCache);
        }

        void SetBones(BoneCache[] boneCache)
        {
            FixWeights(boneCache);
            SetCompatibleBoneSet(boneCache);
        }

        void FixWeights(BoneCache[] newBones)
        {
            var newBonesList = new List<BoneCache>(newBones);
            var indexMap = new Dictionary<int, int>();

            for (var i = 0; i < m_Bones.Count; ++i)
            {
                var bone = m_Bones[i];
                var newIndex = newBonesList.IndexOf(bone);

                if (newIndex != -1)
                    indexMap.Add(i, newIndex);
            }

            for (var i = 0; i < vertexWeights.Length; ++i)
            {
                var boneWeight = vertexWeights[i];
                for (var m = 0; m < boneWeight.Count; ++m)
                {
                    var boneRemoved = indexMap.TryGetValue(boneWeight[m].boneIndex, out var newIndex) == false;

                    if (boneRemoved)
                    {
                        boneWeight[m].weight = 0f;
                        boneWeight[m].enabled = false;
                    }

                    boneWeight[m].boneIndex = newIndex;

                    if (boneRemoved)
                        boneWeight.CompensateOtherChannels(m);
                }

                boneWeight.UnifyChannelsWithSameBoneIndex();
                vertexWeights[i] = boneWeight;
            }
        }
    }
}
