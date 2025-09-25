using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.Profiling;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.U2D.Animation;

namespace UnityEngine.U2D.IK
{
    /// <summary>
    /// Component responsible for 2D Forward And Backward Reaching Inverse Kinematics (FABRIK) IK.
    /// </summary>
    [MovedFrom("UnityEngine.Experimental.U2D.IK")]
    [Solver2DMenu("Chain (FABRIK)")]
    [IconAttribute(IconUtility.IconPath + "Animation.IKFabrik.png")]
    [BurstCompile]
    public sealed class FabrikSolver2D : Solver2D, ISolverCleanup
    {
        const float k_MinTolerance = 0.001f;
        const int k_MinIterations = 1;

        [SerializeField]
        IKChain2D m_Chain = new IKChain2D();

        [SerializeField]
        [Range(k_MinIterations, 50)]
        int m_Iterations = 10;

        [SerializeField]
        [Range(k_MinTolerance, 0.1f)]
        float m_Tolerance = 0.01f;

        NativeArray<float> m_Lengths;
        NativeArray<float2> m_Positions;
        NativeArray<float3> m_WorldPositions;

        /// <summary>
        /// Get and set the solver's integration count.
        /// </summary>
        public int iterations
        {
            get => m_Iterations;
            set => m_Iterations = Mathf.Max(value, k_MinIterations);
        }

        /// <summary>
        /// Get and set target distance tolerance.
        /// </summary>
        public float tolerance
        {
            get => m_Tolerance;
            set => m_Tolerance = Mathf.Max(value, k_MinTolerance);
        }

        /// <summary>
        /// Returns the number of chains in the solver.
        /// </summary>
        /// <returns>Returns 1, because FABRIK Solver has only one chain.</returns>
        protected override int GetChainCount() => 1;

        /// <summary>
        /// Gets the chain in the solver at index.
        /// </summary>
        /// <param name="index">Index to query. Not used in this override.</param>
        /// <returns>Returns IKChain2D for the Solver.</returns>
        public override IKChain2D GetChain(int index) => m_Chain;

        protected override bool DoValidate()
        {
            int transformCount = m_Chain.transformCount;

            if (!m_Positions.IsCreated)
                m_Positions = new NativeArray<float2>(transformCount, Allocator.Persistent);
            else if (m_Positions.Length != transformCount)
                NativeArrayHelpers.ResizeIfNeeded(ref m_Positions, transformCount);

            if (!m_Lengths.IsCreated)
                m_Lengths = new NativeArray<float>(transformCount - 1, Allocator.Persistent);
            else if (m_Lengths.Length != transformCount - 1)
                NativeArrayHelpers.ResizeIfNeeded(ref m_Lengths, transformCount - 1);

            if (!m_WorldPositions.IsCreated)
                m_WorldPositions = new NativeArray<float3>(transformCount, Allocator.Persistent);
            else if (m_WorldPositions.Length != transformCount)
                NativeArrayHelpers.ResizeIfNeeded(ref m_WorldPositions, transformCount);

            return true;
        }

        /// <summary>
        /// Prepares the data required for updating the solver.
        /// </summary>
        protected override void DoPrepare()
        {
            int transformCount = m_Chain.transformCount;
            ref Plane plane = ref GetPlane();

            Span<Vector3> positionsSpan = stackalloc Vector3[transformCount];
            for (int i = 0; i < transformCount; ++i)
            {
                positionsSpan[i] = plane.ClosestPointOnPlane(m_Chain.transforms[i].position);
            }
            GetPlaneRootTransform().InverseTransformPoints(positionsSpan);

            for (int i = 0; i < transformCount; ++i)
            {
                m_Positions[i] = (Vector2)positionsSpan[i];
            }

            for (int i = 0; i < transformCount - 1; ++i)
            {
                m_Lengths[i] = math.length(m_Positions[i + 1] - m_Positions[i]);
            }
        }

        /// <summary>
        /// Updates the IK and sets the chain's transform positions.
        /// </summary>
        /// <param name="targetPositions">Target position for the chain.</param>
        protected override void DoUpdateIK(List<Vector3> targetPositions)
        {
            float2 targetPosition = (Vector2)GetPointOnSolverPlane(targetPositions[0]);
            float4x4 rootLocalToWorldMatrix = m_Chain.rootTransform.localToWorldMatrix;
            if (Solve(targetPosition, rootLocalToWorldMatrix, m_Iterations, m_Tolerance, m_Lengths, ref m_Positions, ref m_WorldPositions))
            {
                for (int i = 0; i < m_Chain.transformCount - 1; ++i)
                {
                    Vector2 startLocalPosition = (Vector2)m_Chain.transforms[i + 1].localPosition;
                    Vector2 endLocalPosition = (Vector2)m_Chain.transforms[i].InverseTransformPoint(m_WorldPositions[i + 1]);
                    m_Chain.transforms[i].localRotation *= Quaternion.AngleAxis(Vector2.SignedAngle(startLocalPosition, endLocalPosition), Vector3.forward);
                }
            }
        }

        void ISolverCleanup.DoCleanUp()
        {
            m_Positions.DisposeIfCreated();
            m_Positions = default;
            m_Lengths.DisposeIfCreated();
            m_Lengths = default;
            m_WorldPositions.DisposeIfCreated();
            m_WorldPositions = default;
        }

        [BurstCompile]
        static bool Solve(
            in float2 targetPosition,
            in float4x4 rootLocalToWorldMatrix,
            int iterations,
            float tolerance,
            in NativeArray<float> lengths,
            ref NativeArray<float2> positions,
            ref NativeArray<float3> worldPositions
        )
        {
            bool result = FABRIK2D.Solve(targetPosition, iterations, tolerance, lengths, ref positions);
            if (result)
            {
                // Convert all plane positions to world positions
                for (int i = 0; i < positions.Length; i++)
                    worldPositions[i] = math.transform(rootLocalToWorldMatrix, new float3(positions[i], 0f));
            }
            return result;
        }
    }
}
