using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.U2D.Animation;

namespace UnityEngine.U2D.IK
{
    /// <summary>
    /// Component responsible for 2D Cyclic Coordinate Descent (CCD) IK.
    /// </summary>
    [MovedFrom("UnityEngine.Experimental.U2D.IK")]
    [Solver2DMenuAttribute("Chain (CCD)")]
    [IconAttribute(IconUtility.IconPath + "Animation.IKCCD.png")]
    [BurstCompile]
    public sealed class CCDSolver2D : Solver2D, ISolverCleanup
    {
        const int k_MinIterations = 1;
        const float k_MinTolerance = 0.001f;
        const float k_MinVelocity = 0.01f;
        const float k_MaxVelocity = 1f;

        [SerializeField]
        IKChain2D m_Chain = new IKChain2D();

        [SerializeField]
        [Range(k_MinIterations, 50)]
        int m_Iterations = 10;

        [SerializeField]
        [Range(k_MinTolerance, 0.1f)]
        float m_Tolerance = 0.01f;

        [SerializeField]
        [Range(0f, 1f)]
        float m_Velocity = 0.5f;
        float m_InterpolatedVelocity = Mathf.Lerp(k_MinVelocity, k_MaxVelocity, 0.5f);

        NativeArray<float2> m_Positions;

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
        /// Get and Set the solver velocity.
        /// </summary>
        public float velocity
        {
            get => m_Velocity;
            set
            {
                m_Velocity = Mathf.Clamp01(value);
                m_InterpolatedVelocity = Mathf.Lerp(k_MinVelocity, k_MaxVelocity, m_Velocity);
            }
        }

        /// <summary>
        /// Returns the number of chains in the solver.
        /// </summary>
        /// <returns>Returns 1, because CCD Solver has only one chain.</returns>
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

            return true;
        }

        /// <summary>
        /// Prepares the data required for updating the solver.
        /// </summary>
        protected override void DoPrepare()
        {
            Transform root = m_Chain.rootTransform;
            int transformCount = m_Chain.transformCount;

            Span<Vector3> positionsSpan = stackalloc Vector3[transformCount];
            for (int i = 0; i < transformCount; ++i)
            {
                positionsSpan[i] = m_Chain.transforms[i].position;
            }
            root.InverseTransformPoints(positionsSpan);

            for (int i = 0; i < transformCount; ++i)
            {
                m_Positions[i] = (Vector2)positionsSpan[i];
            }
        }

        /// <summary>
        /// Updates the IK and sets the chain's transform positions.
        /// </summary>
        /// <param name="targetPositions">Target positions for the chain.</param>
        protected override void DoUpdateIK(List<Vector3> targetPositions)
        {
            Transform root = m_Chain.rootTransform;
            int transformCount = m_Chain.transformCount;
            float2 targetPosition = ((float3)root.InverseTransformPoint(targetPositions[0])).xy;

            if (CCD2D.Solve(targetPosition, iterations, tolerance, m_InterpolatedVelocity, ref m_Positions))
            {
                Span<Vector3> positionsSpan = stackalloc Vector3[transformCount];
                for (int i = 0; i < transformCount; ++i)
                {
                    positionsSpan[i] = new float3(m_Positions[i], 0f);
                }
                root.TransformPoints(positionsSpan);

                for (int i = 0; i < transformCount - 1; ++i)
                {
                    Vector2 startLocalPosition = (Vector2)m_Chain.transforms[i + 1].localPosition;
                    Vector2 endLocalPosition = (Vector2)m_Chain.transforms[i].InverseTransformPoint(positionsSpan[i + 1]);
                    m_Chain.transforms[i].localRotation *= Quaternion.AngleAxis(Vector2.SignedAngle(startLocalPosition, endLocalPosition), Vector3.forward);
                }
            }
        }

        void ISolverCleanup.DoCleanUp()
        {
            m_Positions.DisposeIfCreated();
            m_Positions = default;
        }
    }
}
