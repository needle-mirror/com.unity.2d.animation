using Unity.Mathematics;

namespace UnityEditor.U2D.Animation
{
    internal interface ITriangulator
    {
        void Triangulate(ref int2[] edges, ref float2[] vertices, out int[] indices);
        void Tessellate(float minAngle, float maxAngle, float meshAreaFactor, float largestTriangleAreaFactor, float areaThreshold, int smoothIterations, ref float2[] vertices, ref int2[] edges, out int[] indices);
    }
}
