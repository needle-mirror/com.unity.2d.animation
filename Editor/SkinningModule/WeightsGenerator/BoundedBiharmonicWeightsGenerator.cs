using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using ModuleHandle = UnityEngine.U2D.Common.UTess.ModuleHandle;

namespace UnityEditor.U2D.Animation
{
    // Prepares the solve mesh for bounded biharmonic weights: inserts bone samples and control
    // points into the triangulation. The solve itself is BbwSolveJob, scheduled against this job's
    // outputs (see BoundedBiharmonicWeightsGenerator.ScheduleWeightsJob).
    [BurstCompile]
    internal struct BbwPreprocessJob : IJob
    {
        public int numSamples;
        [DeallocateOnJobCompletion]
        public NativeArray<float2> inputVertices;
        [DeallocateOnJobCompletion]
        public NativeArray<int> inputIndices;
        // Shared with the solve job, which releases them on its own completion.
        [ReadOnly] public NativeArray<float2> controlPoints;
        [ReadOnly] public NativeArray<int2> bones;
        public NativeArray<float2> solveVertices;
        public NativeArray<int> solveIndices;
        public NativeArray<int> solveCounts; // [0] vertex count, [1] index count

        public void Execute()
        {
            BoundedBiharmonicWeightsGenerator.Preprocess_(in inputVertices, in inputIndices, in controlPoints, in bones, numSamples, ref solveVertices, ref solveIndices, ref solveCounts);
        }
    }

    [BurstCompile]
    internal class BoundedBiharmonicWeightsGenerator : IWeightsGenerator
    {
        internal static readonly BoneWeight defaultWeight = new BoneWeight() { weight0 = 1 };
        internal static readonly int k_NumIterations = 100;
        internal static readonly int k_NumSamples = 4;

        public BoneWeight[] Calculate(string name, in float2[] vertices, in int[] indices, in float2[] controlPoints, in int2[] bones, in int[] pins)
        {
            NativeArray<BoneWeight> weights = new NativeArray<BoneWeight>(vertices.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            ScheduleWeightsJob(vertices, indices, controlPoints, bones, pins, weights).Complete();
            BoneWeight[] result = weights.ToArray();
            weights.Dispose();
            return result;
        }

        public JobHandle CalculateJob(string name, in float2[] vertices, in int[] indices, in float2[] controlPoints, in int2[] bones, in int[] pins, SpriteJobData sd)
        {
            return ScheduleWeightsJob(vertices, indices, controlPoints, bones, pins, sd.weights);
        }

        // Preprocessing and solving chained as jobs. Every intermediate container carries a
        // deallocate-on-completion attribute on the job that reads it last, so both the synchronous
        // path and the bulk Auto Geometry path only have to complete the returned handle.
        static JobHandle ScheduleWeightsJob(float2[] vertices, int[] indices, float2[] controlPoints, int2[] bones, int[] pins, NativeArray<BoneWeight> weights)
        {
            // Every sampler vertex introduced by the preprocessing splits a triangle in three.
            int vertexCapacity = 8 * (vertices.Length + (bones.Length * k_NumSamples) + controlPoints.Length);
            int indexCapacity = 8 * (indices.Length + ((controlPoints.Length + bones.Length) * 3));

            NativeArray<float2> nativeControlPoints = new NativeArray<float2>(controlPoints, Allocator.TempJob);
            NativeArray<int2> nativeBones = new NativeArray<int2>(bones, Allocator.TempJob);

            BbwPreprocessJob preprocess = new BbwPreprocessJob
            {
                numSamples = k_NumSamples,
                inputVertices = new NativeArray<float2>(vertices, Allocator.TempJob),
                inputIndices = new NativeArray<int>(indices, Allocator.TempJob),
                controlPoints = nativeControlPoints,
                bones = nativeBones,
                solveVertices = new NativeArray<float2>(vertexCapacity, Allocator.TempJob, NativeArrayOptions.UninitializedMemory),
                solveIndices = new NativeArray<int>(indexCapacity, Allocator.TempJob, NativeArrayOptions.UninitializedMemory),
                solveCounts = new NativeArray<int>(2, Allocator.TempJob),
            };

            BbwSolveJob solve = new BbwSolveJob
            {
                maxIterations = k_NumIterations,
                originalVertexCount = vertices.Length,
                supressCommonWarnings = PlayerSettings.suppressCommonWarnings ? 1 : 0,
                counts = preprocess.solveCounts,
                vertices = preprocess.solveVertices,
                indices = preprocess.solveIndices,
                controlPoints = nativeControlPoints,
                boneEdges = nativeBones,
                pinIndices = new NativeArray<int>(pins, Allocator.TempJob),
                weights = weights,
                result = new NativeArray<int>(1, Allocator.TempJob),
            };

            return solve.Schedule(preprocess.Schedule());
        }

        [BurstCompile]
        internal static void SampleBones_(in NativeArray<float2> points, in NativeArray<int2> edges, int numSamples, ref NativeArray<float2> sampledEdges)
        {
            Debug.Assert(numSamples > 0);
            int j = 0;

            for (int i = 0; i < edges.Length; i++)
            {
                int2 edge = edges[i];
                float2 tip = points[edge.x];
                float2 tail = points[edge.y];

                for (int s = 0; s < numSamples; s++)
                {
                    float f = (s + 1f) / (float)(numSamples + 1f);
                    sampledEdges[j++] = f * tail + (1f - f) * tip;
                }
            }
        }

        [BurstCompile]
        // Triangulate Bone Samplers.
        internal static void TriangulateSamplers_(in NativeArray<float2> samplers, ref NativeArray<float2> triVertices, ref int vtxCount, ref NativeArray<int> triIndices, ref int idxCount)
        {
            foreach (float2 v in samplers)
            {
                int triCount_ = idxCount / 3;

                for (int i = 0; i < triCount_; ++i)
                {
                    int i1 = triIndices[0 + (i * 3)];
                    int i2 = triIndices[1 + (i * 3)];
                    int i3 = triIndices[2 + (i * 3)];
                    float2 v1 = triVertices[i1];
                    float2 v2 = triVertices[i2];
                    float2 v3 = triVertices[i3];
                    bool inside = ModuleHandle.IsInsideTriangle(v, v1, v2, v3);
                    if (inside)
                    {
                        triVertices[vtxCount] = v;
                        // Split the containing triangle by connecting v to its corners. When v lies exactly
                        // on an edge, the sub-triangle spanning that edge is collinear (zero area), which makes
                        // the solver singular (error code 3, UJN-594). Emit only the non-degenerate
                        // sub-triangles; a strictly-interior point keeps all three, matching prior behaviour.
                        if (ModuleHandle.OrientFast(v1, v2, v) != 0f)
                        { triIndices[idxCount++] = i1; triIndices[idxCount++] = i2; triIndices[idxCount++] = vtxCount; }
                        if (ModuleHandle.OrientFast(v2, v3, v) != 0f)
                        { triIndices[idxCount++] = i2; triIndices[idxCount++] = i3; triIndices[idxCount++] = vtxCount; }
                        if (ModuleHandle.OrientFast(v3, v1, v) != 0f)
                        { triIndices[idxCount++] = i3; triIndices[idxCount++] = i1; triIndices[idxCount++] = vtxCount; }
                        vtxCount++;
                        break;
                    }
                }
            }
        }

        [BurstCompile]
        // Triangulate Skipped Original Points. These points are discarded during PlanarGrapg cleanup. But bbw only cares if these are part of any geometry. So just insert them.
        internal static void TriangulateInternal_(in NativeArray<int> internalIndices, in int internalIndexCount, in NativeArray<float2> triVertices, ref NativeArray<int> triIndices, ref int idxCount)
        {
            int triangleCount = idxCount / 3;

            for (int j = 0; j < internalIndexCount; ++j)
            {
                int index = internalIndices[j];
                float2 v = triVertices[index];
                for (int i = 0; i < triangleCount; ++i)
                {
                    int i1 = triIndices[0 + (i * 3)];
                    int i2 = triIndices[1 + (i * 3)];
                    int i3 = triIndices[2 + (i * 3)];
                    float2 v1 = triVertices[i1];
                    float2 v2 = triVertices[i2];
                    float2 v3 = triVertices[i3];
                    float c1 = (float)Math.Round(ModuleHandle.OrientFast(v1, v2, v), 2);
                    if (c1 == 0)
                    {
                        triIndices[0 + (i * 3)] = i1; triIndices[1 + (i * 3)] = index; triIndices[2 + (i * 3)] = i3;
                        triIndices[idxCount++] = index; triIndices[idxCount++] = i2; triIndices[idxCount++] = i3;
                    }
                    else
                    {
                        float c2 = (float)Math.Round(ModuleHandle.OrientFast(v2, v3, v), 2);
                        if (c2 == 0)
                        {
                            triIndices[0 + (i * 3)] = i2; triIndices[1 + (i * 3)] = index; triIndices[2 + (i * 3)] = i1;
                            triIndices[idxCount++] = index; triIndices[idxCount++] = i3; triIndices[idxCount++] = i1;
                        }
                        else
                        {
                            float c3 = (float)Math.Round(ModuleHandle.OrientFast(v3, v1, v), 2);
                            if (c3 == 0)
                            {
                                triIndices[0 + (i * 3)] = i3; triIndices[1 + (i * 3)] = index; triIndices[2 + (i * 3)] = i2;
                                triIndices[idxCount++] = index; triIndices[idxCount++] = i1; triIndices[idxCount++] = i2;
                            }
                        }
                    }
                }
            }
        }

        // The mesh handed to the solver: original triangles minus exactly-degenerate ones, with
        // dropped original points re-attached and bone samples plus control points inserted. This is
        // the front half of the old native pipeline, unchanged; the native call that used to follow
        // it is now BbwSolveJob.
        [BurstCompile]
        static internal void Preprocess_(in NativeArray<float2> inputVertices, in NativeArray<int> inputIndices, in NativeArray<float2> inputControlPoints, in NativeArray<int2> inputBones, int numSamples, ref NativeArray<float2> solveVertices, ref NativeArray<int> solveIndices, ref NativeArray<int> solveCounts)
        {
            solveCounts[0] = 0;
            solveCounts[1] = 0;
            if (inputVertices.Length < 3)
                return;

            NativeArray<int> indices = new NativeArray<int>(inputIndices, Allocator.Temp);
            NativeArray<float2> vertices = new NativeArray<float2>(inputVertices, Allocator.Temp);
            NativeArray<float2> controlPoints = new NativeArray<float2>(inputControlPoints, Allocator.Temp);
            NativeArray<float2> boneSamples = new NativeArray<float2>(inputBones.Length * numSamples, Allocator.Temp);

            SampleBones_(in inputControlPoints, in inputBones, numSamples, ref boneSamples);

            // Copy Original Indices. Every new vertex introduced to the sampler creates 3 triangles.
            int cntIndices = 0;
            for (int i = 0; i < indices.Length / 3; ++i)
            {
                int i1 = indices[0 + (i * 3)];
                int i2 = indices[1 + (i * 3)];
                int i3 = indices[2 + (i * 3)];
                float2 v1 = vertices[i1];
                float2 v2 = vertices[i2];
                float2 v3 = vertices[i3];
                float rt = (float)Math.Round(ModuleHandle.OrientFast(v1, v2, v3), 2);
                if (rt != 0)
                {
                    solveIndices[cntIndices++] = i1;
                    solveIndices[cntIndices++] = i2;
                    solveIndices[cntIndices++] = i3;
                }
            }

            // Insert Samplers.
            NativeArray<int> internalPoints = new NativeArray<int>(vertices.Length, Allocator.Temp);
            int internalPointsCnt = 0;
            for (int i = 0; i < vertices.Length; ++i)
            {
                int counter = 0;
                for (int m = 0; m < cntIndices; ++m)
                {
                    if (solveIndices[m] == i)
                        counter++;
                }
                if (counter == 0)
                    internalPoints[internalPointsCnt++] = i;
            }
            TriangulateInternal_(in internalPoints, in internalPointsCnt, in vertices, ref solveIndices, ref cntIndices);

            int cntVertices = 0;
            for (int i = 0; i < vertices.Length; i++)
                solveVertices[cntVertices++] = vertices[i];

            TriangulateSamplers_(boneSamples, ref solveVertices, ref cntVertices, ref solveIndices, ref cntIndices);
            TriangulateSamplers_(controlPoints, ref solveVertices, ref cntVertices, ref solveIndices, ref cntIndices);

            solveCounts[0] = cntVertices;
            solveCounts[1] = cntIndices;
        }
    }
}
