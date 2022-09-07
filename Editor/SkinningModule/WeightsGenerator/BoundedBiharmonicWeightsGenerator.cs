using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using ModuleHandle = UnityEngine.U2D.Common.UTess.ModuleHandle;

namespace UnityEditor.U2D.Animation
{
    internal class BoundedBiharmonicWeightsGenerator : IWeightsGenerator
    {
        internal static readonly BoneWeight defaultWeight = new BoneWeight() { weight0 = 1 };
        const int k_NumIterations = 100;
        const int k_NumSamples = 4;
        const float k_LargestTriangleAreaFactor = 0.4f;
        const float k_MeshAreaFactor = 0.004f;

        [DllImport("BoundedBiharmonicWeightsModule")]
        static extern int Bbw(int iterations,
            [In, Out] IntPtr vertices, int vertexCount, int originalVertexCount,
            [In, Out] IntPtr indices, int indexCount,
            [In, Out] IntPtr controlPoints, int controlPointsCount,
            [In, Out] IntPtr boneEdges, int boneEdgesCount,    
            [In, Out] IntPtr pinIndices, int pinIndexCount,
            [In, Out] IntPtr weights
            );

        public BoneWeight[] Calculate(string name, in float2[] vertices, in int[] indices, in int2[] edges, in float2[] controlPoints, in int2[] bones, in int[] pins)
        {
            var sanitizedEdges = SanitizeEdges(edges, vertices.Length);
            
            // In almost all cases subdivided mesh weights fine. Non-subdivide is only a fail-safe.
            bool success = false;
            var weights = CalculateInternal(vertices, indices, sanitizedEdges, controlPoints, bones, pins, k_NumSamples, true, ref success);
            if (!success)
                weights = CalculateInternal(vertices, indices, sanitizedEdges, controlPoints, bones, pins, k_NumSamples, false, ref success);
            return weights;
        }

        static int2[] SanitizeEdges(in int2[] edges, int noOfVertices)
        {
            var tmpEdges = new List<int2>(edges);
            for (var i = tmpEdges.Count - 1; i >= 0; i--)
            {
                if (tmpEdges[i].x >= noOfVertices || tmpEdges[i].y >= noOfVertices)
                    tmpEdges.RemoveAt(i);
            }

            return tmpEdges.ToArray();
        }
        
        static void Round(ref float2[] data)
        {
            for (var i = 0; i < data.Length; ++i)
            {
                var x = data[i].x;
                var y = data[i].y;
                x = (float) Math.Round(x, 8);
                y = (float) Math.Round(y, 8);
                data[i] = new float2(x, y);
            }        
        }

        static BoneWeight[] CalculateInternal(in float2[] inputVertices, in int[] inputIndices, in int2[] inputEdges, in float2[] inputControlPoints, in int2[] inputBones, in int[] inputPins, int numSamples, bool subdivide, ref bool done)
        {
            done = false;            
            var weights = new BoneWeight[inputVertices.Length];
            for (var i = 0; i < weights.Length; ++i)
                weights[i] = defaultWeight;
            if (inputVertices.Length < 3)
                return weights;

            var edges = EditorUtilities.CreateCopy(inputEdges);
            var controlPoints = EditorUtilities.CreateCopy(inputControlPoints);
            var vertices = EditorUtilities.CreateCopy(inputVertices);

            var boneSamples = SampleBones(in inputControlPoints, in inputBones, numSamples);
            Round(ref vertices);
            Round(ref controlPoints);
            Round(ref boneSamples);

            // Input Vertices are well refined and smoothed, just triangulate with bones and cages.
            var ok = TriangulationUtility.TriangulateSafe(ref vertices, ref edges, out var indices);
            if (!ok || indices.Length == 0)
            {
                indices = EditorUtilities.CreateCopy(inputIndices);
                vertices = EditorUtilities.CreateCopy(inputVertices);
                Round(ref vertices);
            }
            else if(subdivide)
            {
                var targetArea = TriangulationUtility.FindTargetAreaForWeightMesh(in vertices, in indices, k_MeshAreaFactor, k_LargestTriangleAreaFactor);
                TriangulationUtility.TessellateSafe(0, targetArea, 1, 0, ref vertices, ref edges, out indices);
            }
            if (indices.Length == 0)
                return weights;

            // Copy Original Indices.
            var coIndices = EditorUtilities.CreateCopy(indices);
            var tmpIndices = new List<int>(indices.Length);
            for (var i = 0; i < coIndices.Length / 3; ++i)
            {
                var i1 = coIndices[0 + (i * 3)];
                var i2 = coIndices[1 + (i * 3)];
                var i3 = coIndices[2 + (i * 3)];
                var v1 = vertices[i1];
                var v2 = vertices[i2];
                var v3 = vertices[i3];
                var rt = (float)Math.Round(ModuleHandle.OrientFast(v1, v2, v3), 2);
                if (rt != 0)
                {
                    tmpIndices.Add(i1);
                    tmpIndices.Add(i2);
                    tmpIndices.Add(i3);
                }
            }
            indices = tmpIndices.ToArray();
            
            // Insert Samplers.
            var internalPoints = new List<int>();
            for (var i = 0; i < vertices.Length; ++i)
            {
                var counter = 0;
                for (var m = 0; m < indices.Length; ++m)
                {
                    if (indices[m] == i)
                        counter++;
                }
                if (counter == 0)
                    internalPoints.Add(i);
            }

            tmpIndices = new List<int>(indices);
            TriangulationUtility.TriangulateInternal(internalPoints.ToArray(), in vertices, ref tmpIndices);
            
            var tmpVertices = new List<float2>(vertices);
            TriangulationUtility.TriangulateSamplers(boneSamples, ref tmpVertices, ref tmpIndices);
            TriangulationUtility.TriangulateSamplers(controlPoints, ref tmpVertices, ref tmpIndices);
            vertices = tmpVertices.ToArray();
            indices = tmpIndices.ToArray();
            
            var verticesHandle = GCHandle.Alloc(vertices, GCHandleType.Pinned);
            var indicesHandle = GCHandle.Alloc(indices, GCHandleType.Pinned);
            var controlPointsHandle = GCHandle.Alloc(controlPoints, GCHandleType.Pinned);
            var bonesHandle = GCHandle.Alloc(inputBones, GCHandleType.Pinned);
            var pinsHandle = GCHandle.Alloc(inputPins, GCHandleType.Pinned);
            var weightsHandle = GCHandle.Alloc(weights, GCHandleType.Pinned);

            var result = Bbw(k_NumIterations,
                verticesHandle.AddrOfPinnedObject(), vertices.Length, inputVertices.Length,
                indicesHandle.AddrOfPinnedObject(), indices.Length,
                controlPointsHandle.AddrOfPinnedObject(), controlPoints.Length,
                bonesHandle.AddrOfPinnedObject(), inputBones.Length,
                pinsHandle.AddrOfPinnedObject(), inputPins.Length,
                weightsHandle.AddrOfPinnedObject());
            
            switch (result)
            {
                case 1:
                case 2:
                    Debug.LogWarning($"Weight generation failure due to unexpected mesh input. Re-generate the mesh with a different Outline Detail value to resolve the issue. Error Code: {result}");
                    break;
                case 3:
                    if(!PlayerSettings.suppressCommonWarnings)
                        Debug.LogWarning($"Internal weight generation error. Error Code: {result}");
                    break;
            }

            verticesHandle.Free();
            indicesHandle.Free();
            controlPointsHandle.Free();
            bonesHandle.Free();
            pinsHandle.Free();
            weightsHandle.Free();

            for (var i = 0; i < weights.Length; ++i)
            {
                var weight = weights[i];

                if (weight.Sum() == 0f)
                    weights[i] = defaultWeight;
                else if (!done)
                    done = (weight.boneIndex0 != 0 || weight.boneIndex1 != 0 || weight.boneIndex2 != 0 || weight.boneIndex3 != 0);
            }

            return weights;
        }

        public void DebugMesh(BaseSpriteMeshData spriteMeshData, float2[] vertices, int2[] edges, float2[] controlPoints, int2[] bones, int[] pins)
        {
            var boneSamples = SampleBones(controlPoints, bones, k_NumSamples);
            var testVertices = new float2[vertices.Length + controlPoints.Length + boneSamples.Length];

            var headIndex = 0;
            Array.Copy(vertices, 0, testVertices, headIndex, vertices.Length);
            headIndex = vertices.Length;
            Array.Copy(controlPoints, 0, testVertices, headIndex, controlPoints.Length);
            headIndex += controlPoints.Length;
            Array.Copy(boneSamples, 0, testVertices, headIndex, boneSamples.Length);
            
            TriangulationUtility.Triangulate(ref edges, ref testVertices, out var indices, Allocator.Temp);


            spriteMeshData.Clear();

            for (var i = 0; i < testVertices.Length; ++i)
                spriteMeshData.AddVertex(testVertices[i], new BoneWeight());

            spriteMeshData.SetIndices(indices);
            
            var convertedEdges = new int2[edges.Length];
            Array.Copy(edges, convertedEdges, edges.Length);
            spriteMeshData.SetEdges(convertedEdges);
        }

        static float2[] SampleBones(in float2[] points, in int2[] edges, int numSamples)
        {
            Debug.Assert(numSamples > 0);

            var sampledEdges = new List<float2>(edges.Length * numSamples);
            for (var i = 0; i < edges.Length; i++)
            {
                var edge = edges[i];
                var tip = points[edge.x];
                var tail = points[edge.y];

                for (var s = 0; s < numSamples; s++)
                {
                    var f = (s + 1f) / (float)(numSamples + 1f);
                    sampledEdges.Add(f * tail + (1f - f) * tip);
                }
            }

            return sampledEdges.ToArray();
        }
    }
}
