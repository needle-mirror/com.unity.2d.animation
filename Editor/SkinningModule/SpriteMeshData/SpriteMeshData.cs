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

    [Serializable]
    internal abstract class BaseSpriteMeshData
    {
        [SerializeField] 
        Vector2[] m_Vertices = new Vector2[0];
        [SerializeField] 
        EditableBoneWeight[] m_VertexWeights = new EditableBoneWeight[0];
        [SerializeField] 
        int[] m_Indices = new int[0];
        [SerializeField] 
        Vector2Int[] m_Edges = new Vector2Int[0];

        public abstract Rect frame { get; }
        
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

        public int vertexCount => m_Vertices.Length;
        public virtual int boneCount => 0;
        public virtual string spriteName => "";

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

        public abstract SpriteBoneData GetBoneData(int index);

        public abstract float GetBoneDepth(int index);

        public void Clear()
        {
            m_Indices = new int[0];
            m_Vertices = new Vector2[0];
            m_VertexWeights = new EditableBoneWeight[0];
            m_Edges = new Vector2Int[0];
        }
    }

    [Serializable]
    internal class SpriteMeshData : BaseSpriteMeshData
    {
        [SerializeField]
        List<SpriteBoneData> m_Bones = new List<SpriteBoneData>();
        
        [SerializeField]
        Rect m_Frame;

        public override Rect frame => m_Frame;
        public override int boneCount => m_Bones.Count;

        public List<SpriteBoneData> bones
        {
            get => m_Bones;
            set => m_Bones = value;
        }

        public override SpriteBoneData GetBoneData(int index)
        {
            return m_Bones[index];
        }

        public override float GetBoneDepth(int index)
        {
            return m_Bones[index].depth;
        }

        public void SetFrame(Rect newFrame)
        {
            m_Frame = newFrame;
        }
    }
}
