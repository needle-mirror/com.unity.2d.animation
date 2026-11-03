using System.Collections.Generic;
using System.Linq;
using Unity.Profiling;
using Unity.Scripting.LifecycleManagement;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.U2D.Animation;
using UnityEngine.U2D.Common;

namespace UnityEngine.U2D.IK
{
    /// <summary>
    /// Component responsible for managing and updating 2D IK Solvers.
    /// </summary>
    [DefaultExecutionOrder(UpdateOrder.ikUpdateOrder)]
    [MovedFrom("UnityEngine.Experimental.U2D.IK")]
    [AddComponentMenu(U2DAnimationConstants.PackageDisplayName + "/IK Manager 2D")]
    [Icon(IconUtility.IconPath + "Animation.IKManager.png")]
    [HelpURL("https://docs.unity3d.com/Packages/com.unity.2d.animation@17.0/manual/2DIK.html")]
    [ExecuteInEditMode]
    public partial class IKManager2D : MonoBehaviour, IPreviewable
    {
        static class Profiling
        {
            internal static readonly ProfilerMarker UpdateManager = new ProfilerMarker("IKManager2D.UpdateManager");
        }

#if UNITY_EDITOR
        [NoAutoStaticsCleanup] // IKEditorManager subscribes once, in OnEnable
        internal static event System.Action<IKManager2D> onEnabledEditor;
        [NoAutoStaticsCleanup]
        internal static event System.Action<IKManager2D> onDisabledEditor;
#endif

        [SerializeField]
        List<Solver2D> m_Solvers = new List<Solver2D>();
        [SerializeField]
        [Range(0f, 1f)]
        float m_Weight = 1f;
        [SerializeField]
        bool m_AlwaysUpdate = true;

        bool m_CullingEnabled;
        BaseCullingStrategy m_CullingStrategy;
        internal BaseCullingStrategy GetCullingStrategy() => m_CullingStrategy;

        /// <summary>
        /// Get and set the weight for solvers.
        /// </summary>
        public float weight
        {
            get => m_Weight;
            set => m_Weight = Mathf.Clamp01(value);
        }

        /// <summary>
        /// Get the Solvers that are managed by this manager.
        /// </summary>
        /// <remarks>
        /// Use <see cref="AddSolver"/> and <see cref="RemoveSolver"/> to change which Solvers the manager updates. Modifying this list directly is not supported.
        /// </remarks>
        public List<Solver2D> solvers => m_Solvers;

        EntityId[] m_TransformIdCache;

        /// <summary>
        /// Solvers are always updated even if the underlying Sprite Skins are not visible.
        /// </summary>
        public bool alwaysUpdate
        {
            get => m_AlwaysUpdate;
            set
            {
                m_AlwaysUpdate = value;
                ToggleCulling(!m_AlwaysUpdate);
            }
        }

        void OnEnable()
        {
            ToggleCulling(!m_AlwaysUpdate);

#if UNITY_EDITOR
            onEnabledEditor?.Invoke(this);
#endif
        }

        void OnDisable()
        {
            ToggleCulling(false);

#if UNITY_EDITOR
            onDisabledEditor?.Invoke(this);
#endif
        }

        void ToggleCulling(bool enableCulling)
        {
            if (m_CullingStrategy != null && m_CullingEnabled == enableCulling)
                return;

            m_CullingEnabled = enableCulling;
            m_CullingStrategy?.RemoveRequestingObject(this);

            if (m_CullingEnabled)
                m_CullingStrategy = CullingManager.instance.GetCullingStrategy<SpriteSkinVisibilityCullingStrategy>();
            else
                m_CullingStrategy = CullingManager.instance.GetCullingStrategy<AlwaysUpdateCullingStrategy>();

            m_CullingStrategy.AddRequestingObject(this);
        }

        void OnValidate()
        {
            m_Weight = Mathf.Clamp01(m_Weight);
            OnEditorDataValidate();

            // The Inspector and Undo change the solver list without going through AddSolver or RemoveSolver.
            m_TransformIdCache = null;
        }

        void Reset()
        {
            FindChildSolvers();
            OnEditorDataValidate();
        }

        void FindChildSolvers()
        {
            m_Solvers.Clear();

            List<Solver2D> solvers = new List<Solver2D>();
            transform.GetComponentsInChildren<Solver2D>(true, solvers);

            foreach (Solver2D solver in solvers)
            {
                if (solver.GetComponentInParent<IKManager2D>() == this)
                    AddSolver(solver);
            }
        }

        /// <summary>
        /// Add Solver to the manager.
        /// </summary>
        /// <param name="solver">Solver to add.</param>
        public void AddSolver(Solver2D solver)
        {
            if (!m_Solvers.Contains(solver))
            {
                m_Solvers.Add(solver);
                AddSolverEditorData();
                m_TransformIdCache = null;
            }
        }

        /// <summary>
        /// Remove Solver from the manager.
        /// </summary>
        /// <param name="solver">Solver to remove.</param>
        public void RemoveSolver(Solver2D solver)
        {
            RemoveSolverEditorData(solver);
            m_Solvers.Remove(solver);
            m_TransformIdCache = null;
        }

        /// <summary>
        /// Updates the Solvers in this manager.
        /// </summary>
        public void UpdateManager()
        {
            if (m_Solvers.Count == 0)
                return;

            Profiling.UpdateManager.Begin();

            ToggleCulling(!m_AlwaysUpdate);

            bool solverInitialized = false;
            for (int i = 0; i < m_Solvers.Count; i++)
            {
                Solver2D solver = m_Solvers[i];
                if (solver == null || !solver.isActiveAndEnabled)
                    continue;

                if (!solver.isValid)
                {
                    solver.Initialize();
                    solverInitialized = true;
                }

                if (!m_CullingEnabled)
                    solver.UpdateIK(m_Weight);
            }

            if (m_CullingEnabled)
            {
                if (solverInitialized || m_TransformIdCache == null)
                    CacheSolversTransformIds();

                bool canUpdate = m_AlwaysUpdate || m_CullingStrategy.AreBonesVisible(m_TransformIdCache);
                if (canUpdate)
                {
                    for (int i = 0; i < m_Solvers.Count; i++)
                    {
                        Solver2D solver = m_Solvers[i];
                        if (solver == null || !solver.isActiveAndEnabled)
                            continue;

                        solver.UpdateIK(weight);
                    }
                }
            }

            Profiling.UpdateManager.End();
        }

        void CacheSolversTransformIds()
        {
            HashSet<EntityId> transformCache = new HashSet<EntityId>();
            for (int s = 0; s < solvers.Count; s++)
            {
                Solver2D solver = solvers[s];
                if (solver == null)
                    continue;

                for (int c = 0; c < solver.chainCount; c++)
                {
                    IKChain2D chain = solver.GetChain(c);
                    Transform[] chainTransforms = chain?.transforms;
                    if (chainTransforms == null)
                        continue;

                    // Initialize leaves the previous transforms in place when the chain can no longer be built.
                    int boneCount = Mathf.Min(chain.transformCount, chainTransforms.Length);
                    for (int b = 0; b < boneCount; b++)
                    {
                        Transform boneTransform = chainTransforms[b];
                        if (boneTransform != null)
                            transformCache.Add(boneTransform.GetEntityId());
                    }
                }
            }

            m_TransformIdCache = transformCache.ToArray();
        }

        /// <summary>
        /// Used by the animation clip preview window. Recommended to not use outside of this purpose.
        /// </summary>
        public void OnPreviewUpdate()
        {
#if UNITY_EDITOR
            if (IsInGUIUpdateLoop())
                UpdateManager();
#endif
        }

        static bool IsInGUIUpdateLoop() => Event.current != null;

        void LateUpdate()
        {
            UpdateManager();
        }

#if UNITY_EDITOR
        [NoAutoStaticsCleanup]
        internal static Events.UnityEvent onDrawGizmos = new Events.UnityEvent();
        void OnDrawGizmos()
        {
            onDrawGizmos.Invoke();
        }
#endif
    }
}
