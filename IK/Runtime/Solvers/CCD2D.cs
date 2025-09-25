using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine.Scripting.APIUpdating;

namespace UnityEngine.U2D.IK
{
    /// <summary>
    /// Utility for 2D based Cyclic Coordinate Descent (CCD) IK Solver.
    /// </summary>
    [MovedFrom("UnityEngine.Experimental.U2D.IK")]
    [BurstCompile]
    public static class CCD2D
    {
        static class Profiling
        {
            internal static readonly ProfilerMarker Solve = new ProfilerMarker("CCD2D.Solve");
        }

        /// <summary>
        /// Solve IK Chain based on CCD.
        /// </summary>
        /// <param name="targetPosition">Target position.</param>
        /// <param name="forward">Forward vector for solver.</param>
        /// <param name="solverLimit">Solver iteration count.</param>
        /// <param name="tolerance">Target position's tolerance.</param>
        /// <param name="velocity">Velocity towards target position.</param>
        /// <param name="positions">Chain positions.</param>
        /// <returns>Returns true if solver successfully completes within iteration limit. False otherwise.</returns>
        public static bool Solve(Vector3 targetPosition, Vector3 forward, int solverLimit, float tolerance, float velocity, ref Vector3[] positions)
        {
            NativeArray<float2> nativePositions = new NativeArray<float2>(positions.Length, Allocator.Temp);
            for (int i = 0; i < positions.Length; ++i)
                nativePositions[i] = new float2(positions[i].x, positions[i].y);

            bool result = Solve((Vector2)targetPosition, solverLimit, tolerance, velocity, ref nativePositions);

            for (int i = 0; i < positions.Length; ++i)
                positions[i] = (Vector2)nativePositions[i];

            nativePositions.Dispose();

            return result;
        }

        /// <summary>
        /// Solve IK Chain based on CCD for 2D positions.
        /// </summary>
        /// <param name="targetPosition">Target position in 2D.</param>
        /// <param name="solverLimit">Solver iteration count.</param>
        /// <param name="tolerance">Target position's tolerance.</param>
        /// <param name="velocity">Velocity towards target position.</param>
        /// <param name="positions">Chain positions in 2D.</param>
        /// <returns>Returns true if solver successfully completes within iteration limit. False otherwise.</returns>
        [BurstCompile]
        internal static bool Solve(in float2 targetPosition, int solverLimit, float tolerance, float velocity, ref NativeArray<float2> positions)
        {
            Profiling.Solve.Begin();

            int last = positions.Length - 1;
            int iterations = 0;
            float sqrTolerance = tolerance * tolerance;
            float sqrDistanceToTarget = math.lengthsq(targetPosition - positions[last]);
            while (sqrDistanceToTarget > sqrTolerance)
            {
                DoIteration(targetPosition, last, velocity, ref positions);
                sqrDistanceToTarget = math.lengthsq(targetPosition - positions[last]);
                if (++iterations >= solverLimit)
                    break;
            }

            Profiling.Solve.End();

            return iterations != 0;
        }

        [BurstCompile]
        static void DoIteration([NoAlias] in float2 targetPosition, [AssumeRange(1, int.MaxValue)] int last, float velocity, [NoAlias] ref NativeArray<float2> positions)
        {
            for (int i = last - 1; i >= 0; --i)
            {
                float2 pivot = positions[i];
                float2 toTarget = targetPosition - pivot;
                float2 toLast = positions[last] - pivot;

                float angle = IKMathUtility.SignedAngle(toLast, toTarget);
                angle *= velocity;
                math.sincos(angle, out float s, out float c);

                for (int j = last; j > i; --j)
                {
                    RotatePositionFrom(positions[j], pivot, s, c, out float2 rotated);
                    positions[j] = rotated;
                }
            }
        }

        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void RotatePositionFrom([NoAlias] in float2 position, [NoAlias] in float2 pivot, float s, float c, [NoAlias] out float2 result)
        {
            float2 v = position - pivot;
            float2 rotated;
            rotated.x = c * v.x - s * v.y;
            rotated.y = s * v.x + c * v.y;
            result = pivot + rotated;
        }
    }
}
