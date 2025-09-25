using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine.U2D.Common.UTess;

namespace UnityEngine.U2D.Animation
{
    [BurstCompile]
    internal static class MeshUtilities
    {

        static readonly ProfilerMarker k_OldOutline = new ProfilerMarker("MeshUtilities.OldOutline");
        static readonly ProfilerMarker k_newOutline = new ProfilerMarker("MeshUtilities.NewOutline");
        /// <summary>
        /// Get the outline edges from a set of indices.
        /// This method expects the index array to be laid out with one triangle for every 3 indices.
        /// E.g. triangle 0: index 0 - 2, triangle 1: index 3 - 5, etc.
        /// </summary>
        /// <returns>Returns a NativeArray of sorted edges. It is up to the caller to dispose this array.</returns>
        public static NativeArray<int2> GetOutlineEdges(in NativeArray<ushort> indices)
        {
            k_OldOutline.Begin();
            NativeArray<int2> sortedEdges;
            GetOutlineEdgesFallback(indices, out sortedEdges);
            k_OldOutline.End();
            return sortedEdges;
        }

        public static NativeArray<int2> GetOutlineEdgesUTess(in NativeArray<ushort> indices)
        {
            k_newOutline.Begin();

            NativeArray<int2> uTessOutput = new NativeArray<int2>(indices.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            int uTessLength = GenerateUTessOutline(indices, ref uTessOutput);
            NativeArray<int2> output = new NativeArray<int2>(uTessLength, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            unsafe
            {
                UnsafeUtility.MemCpy(output.GetUnsafePtr(), uTessOutput.GetUnsafePtr(), uTessLength * UnsafeUtility.SizeOf<int2>());
            }

            k_newOutline.End();
            return output;
        }

        [BurstCompile]
        static int GenerateUTessOutline(in NativeArray<ushort> indices, ref NativeArray<int2> outline)
        {
            // To ensure this function is Burst compiled GenerateOutlineFromTriangleIndices is wrapped within GenerateUTessOutline
            return ModuleHandle.GenerateOutlineFromTriangleIndices(indices, ref outline);
        }

        [BurstCompile]
        public static void GetOutlineEdgesFallback(in NativeArray<ushort> indices, out NativeArray<int2> output)
        {
            UnsafeHashMap<ulong, int2> edges = new UnsafeHashMap<ulong, int2>(indices.Length, Allocator.Temp);

            for (int i = 0; i < indices.Length; i += 3)
            {
                ushort i0 = indices[i];
                ushort i1 = indices[i + 1];
                ushort i2 = indices[i + 2];

                AddToEdgeMap(i0, i1, ref edges);
                AddToEdgeMap(i1, i2, ref edges);
                AddToEdgeMap(i2, i0, ref edges);
            }

            NativeArray<int2> values = edges.GetValueArray(Allocator.Temp);
            SortEdges(values, out output);
            values.Dispose();
            edges.Dispose();
        }

        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void AddToEdgeMap(int x, int y, ref UnsafeHashMap<ulong, int2> edgeMap)
        {
            // Use ulong as edge key for hash map (min,max vertex) to avoid struct hash overhead and redundant hash calculations.
            int minV = math.min(x, y);
            int maxV = math.max(x, y);
            ulong key = ((ulong)minV << 32) | (uint)maxV;
            if (!edgeMap.Remove(key))
                edgeMap[key] = new int2(x, y);
        }

        [BurstCompile]
        static void SortEdges(in NativeArray<int2> unsortedEdges, out NativeArray<int2> sortedEdges)
        {
            NativeArray<int2> tmpEdges = new NativeArray<int2>(unsortedEdges.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            NativeList<int> shapeStartingEdge = new NativeList<int>(1, Allocator.Temp);

            UnsafeHashMap<int, int> edgeMap = new UnsafeHashMap<int, int>(unsortedEdges.Length, Allocator.Temp);
            NativeBitArray usedEdges = new NativeBitArray(unsortedEdges.Length, Allocator.Temp);

            int searchStartPosition = 0;

            for (int i = 0; i < unsortedEdges.Length; i++)
                edgeMap[unsortedEdges[i].x] = i;

            bool findStartingEdge = true;
            int edgeIndex = -1;
            int startingEdge = 0;
            for (int i = 0; i < unsortedEdges.Length; i++)
            {
                if (findStartingEdge)
                {
                    for (int pos = searchStartPosition; pos < unsortedEdges.Length; pos += 64)
                    {
                        ulong bits = ~usedEdges.GetBits(pos, math.min(64, unsortedEdges.Length - pos));
                        if (bits != 0)
                        {
                            int bitPosition = math.tzcnt(bits);
                            edgeIndex = pos + bitPosition;
                            searchStartPosition = edgeIndex;

                            break;
                        }
                    }

                    startingEdge = edgeIndex;
                    findStartingEdge = false;
                    shapeStartingEdge.Add(i);
                }

                usedEdges.Set(edgeIndex, true);
                tmpEdges[i] = unsortedEdges[edgeIndex];
                int nextVertex = unsortedEdges[edgeIndex].y;
                edgeIndex = edgeMap[nextVertex];

                if (edgeIndex == startingEdge)
                    findStartingEdge = true;
            }

            int finalEdgeArrLength = unsortedEdges.Length;
            sortedEdges = new NativeArray<int2>(finalEdgeArrLength, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            int count = 0;
            for (int i = 0; i < shapeStartingEdge.Length; ++i)
            {
                int edgeStart = shapeStartingEdge[i];
                int edgeEnd = (i + 1) == shapeStartingEdge.Length ? tmpEdges.Length : shapeStartingEdge[i + 1];

                for (int m = edgeStart; m < edgeEnd; ++m)
                    sortedEdges[count++] = tmpEdges[m];
            }

            usedEdges.Dispose();
            edgeMap.Dispose();
            shapeStartingEdge.Dispose();
            tmpEdges.Dispose();
        }
    }
}
