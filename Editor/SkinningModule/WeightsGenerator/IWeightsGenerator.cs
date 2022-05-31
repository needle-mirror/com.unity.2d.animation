using UnityEngine;

namespace UnityEditor.U2D.Animation
{
    internal interface IWeightsGenerator
    {
        BoneWeight[] Calculate(string name, Vector2[] vertices, int[] indices, Vector2Int[] edges, Vector2[] controlPoints, Vector2Int[] bones, int[] pins);
    }
}
