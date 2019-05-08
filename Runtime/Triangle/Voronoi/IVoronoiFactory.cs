namespace UnityEngine.Experimental.U2D.Animation.TriangleNet
    .Voronoi
{
    using Animation.TriangleNet.Topology.DCEL;

    public interface IVoronoiFactory
    {
        void Initialize(int vertexCount, int edgeCount, int faceCount);

        void Reset();

        Vertex CreateVertex(double x, double y);

        HalfEdge CreateHalfEdge(Vertex origin, Face face);

        Face CreateFace(Geometry.Vertex vertex);
    }
}
