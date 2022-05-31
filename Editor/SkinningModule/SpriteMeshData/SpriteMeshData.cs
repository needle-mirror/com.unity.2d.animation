using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEditor.U2D.Animation
{
    [Serializable]
    internal class SpriteBoneData
    {
        public int parentId = -1;
        public Vector2 localPosition;
        public Quaternion localRotation = Quaternion.identity;
        public Vector2 position;
        public Vector2 endPosition;
        public float depth;
        public float length;
    }

    internal interface ISpriteMeshData
    {
        Rect frame { get; }
        Vector2[] vertices { get; }
        EditableBoneWeight[] vertexWeights { get; }
        int[] indices { get; set; }
        Vector2Int[] edges { get; set; }
        int vertexCount { get; }
        int boneCount { get; }
        string spriteName { get; }
        void SetVertices(Vector2[] newVertices, EditableBoneWeight[] newWeights);
        void AddVertex(Vector2 position, BoneWeight weight);
        void RemoveVertex(int index);
        SpriteBoneData GetBoneData(int index);
        float GetBoneDepth(int index);
        void Clear();
    }

    [Serializable]
    internal class SpriteMeshData : ISpriteMeshData
    {
        [SerializeField]
        List<SpriteBoneData> m_Bones = new List<SpriteBoneData>();
        [SerializeField]
        Rect m_Frame;
        [SerializeField] 
        Vector2[] m_Vertices = new Vector2[0];
        [SerializeField] 
        EditableBoneWeight[] m_VertexWeights = new EditableBoneWeight[0];
        [SerializeField] 
        int[] m_Indices = new int[0];
        [SerializeField] 
        Vector2Int[] m_Edges = new Vector2Int[0];

        public Rect frame
        {
            get => m_Frame;
            set => m_Frame = value;
        }

        public Vector2[] vertices => m_Vertices;
        public EditableBoneWeight[] vertexWeights => m_VertexWeights;

        public int[] indices
        {
            get => m_Indices;
            set => m_Indices = value;
        }

        public Vector2Int[] edges
        {
            get => m_Edges;
            set => m_Edges = value;
        }

        public List<SpriteBoneData> bones
        {
            get => m_Bones;
            set => m_Bones = value;
        }

        public string spriteName => "";
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

        public SpriteBoneData GetBoneData(int index)
        {
            return m_Bones[index];
        }

        public float GetBoneDepth(int index)
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
    }
}
