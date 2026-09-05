using System;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Collections;
using UnityEditor;
using UnityEngine.Rendering;

#if ENABLE_URP
using UnityEngine.Rendering.Universal;
#endif
using Unity.Profiling;
using UnityEngine.U2D.Animation.Profiler;

namespace UnityEngine.U2D.Animation
{
    /// <summary>
    /// The available modes for batched Sprite Skin deformation.
    /// </summary>
    public enum DeformationMethods
    {
        /// <summary>
        /// The Sprite Skin is deformed, in batch, on the CPU.
        /// </summary>
        Cpu = 0,
        /// <summary>
        /// The Sprite Skin is deformed, in batch, on the GPU.
        /// </summary>
        Gpu = 1,
        /// <summary>
        /// Used as a default value when no deformation method is chosen.
        /// </summary>
        None = 2
    }

    internal class DeformationManager : ScriptableObject
    {
        static DeformationManager s_Instance;

        public static DeformationManager instance
        {
            get
            {
                if (s_Instance == null)
                {
                    DeformationManager[] managers = Resources.FindObjectsOfTypeAll<DeformationManager>();
                    if (managers.Length > 0)
                        s_Instance = managers[0];
                    else
                        s_Instance = ScriptableObject.CreateInstance<DeformationManager>();
                    s_Instance.hideFlags = HideFlags.HideAndDontSave;
                    s_Instance.Init();
                }

                return s_Instance;
            }
        }

#if ENABLE_URP
        UniversalRenderPipelineAsset urpPipelineAsset
        {
            get
            {
                RenderPipelineAsset pipelineAsset = GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.RenderPipelineAsset;
                if (pipelineAsset != null)
                {
                    UniversalRenderPipelineAsset urpAsset = pipelineAsset as UniversalRenderPipelineAsset;
                    return urpAsset;
                }
                return null;
            }
        }
#endif

        BaseDeformationSystem[] m_DeformationSystems;

        [SerializeField]
        GameObject m_Helper;
        internal GameObject helperGameObject => m_Helper;

        bool canUseGpuDeformation { get; set; }
        bool m_WasUsingSRPBatcherLastFrame;
        bool m_WasUsingGpuDeformationLastFrame;
        bool m_HandleDeformationChange;

        // Sprite Skins added to the CPU system only because their SRP-batcher compatibility was
        // Undetermined at add time (e.g. before the first render). They are promoted to GPU once it
        // resolves to Compatible. (UUM-143532)
        readonly HashSet<SpriteSkin> m_PendingGpuPromotion = new HashSet<SpriteSkin>();
        static readonly List<SpriteSkin> s_PendingPromotionScratch = new List<SpriteSkin>();

        void OnEnable()
        {
            s_Instance = this;
            canUseGpuDeformation = SpriteSkinUtility.CanUseGpuDeformation();
            m_WasUsingGpuDeformationLastFrame = SpriteSkinUtility.IsUsingGpuDeformation();
            m_WasUsingSRPBatcherLastFrame = false;
            m_HandleDeformationChange = false;

#if ENABLE_URP
            m_WasUsingSRPBatcherLastFrame = urpPipelineAsset ? urpPipelineAsset.useSRPBatcher : false;
#endif

            Init();
        }

        void Init()
        {
            CreateBatchSystems();
            CreateHelper();
        }

        // Create a CPU and a GPU deformation system if the platform supports it.
        // Both systems are needed at the same time as a shader on a sprite may not support GPU deformation.
        void CreateBatchSystems()
        {
            if (m_DeformationSystems != null)
                return;

            int noOfSystems = canUseGpuDeformation ? 2 : 1;
            m_DeformationSystems = new BaseDeformationSystem[noOfSystems];
            m_DeformationSystems[(int)DeformationMethods.Cpu] = new CpuDeformationSystem();

            if (canUseGpuDeformation)
                m_DeformationSystems[(int)DeformationMethods.Gpu] = new GpuDeformationSystem();

            for (int i = 0; i < m_DeformationSystems.Length; ++i)
                m_DeformationSystems[i].Initialize((ulong)m_DeformationSystems[i].GetHashCode());
        }

        // Create a helper GameObject, which has a DeformationManagerUpdater component which will update the deformation systems.
        void CreateHelper()
        {
            if (m_Helper != null)
                return;

            m_Helper = new GameObject("DeformationManagerUpdater");
            m_Helper.hideFlags = HideFlags.HideAndDontSave;
            DeformationManagerUpdater helperComponent = m_Helper.AddComponent<DeformationManagerUpdater>();
            helperComponent.onDestroyingComponent += OnHelperDestroyed;

#if !UNITY_EDITOR
            GameObject.DontDestroyOnLoad(m_Helper);
#endif
        }

        void OnHelperDestroyed(GameObject helperGo)
        {
            if (m_Helper != helperGo)
                return;

            m_Helper = null;
            CreateHelper();
        }

        void OnDisable()
        {
            if (m_Helper != null)
            {
                m_Helper.GetComponent<DeformationManagerUpdater>().onDestroyingComponent -= OnHelperDestroyed;
                GameObject.DestroyImmediate(m_Helper);
            }

            for (int i = 0; i < m_DeformationSystems.Length; ++i)
                m_DeformationSystems[i].Cleanup();

            s_Instance = null;
        }

        internal void Update()
        {
            if (m_HandleDeformationChange)
            {
                MoveSpriteSkinsToActiveSystem();
                m_HandleDeformationChange = false;
            }
            UpdateGpuDeformationConfig();
            DrainPendingGpuPromotion();

            for (int i = 0; i < m_DeformationSystems.Length; ++i)
                m_DeformationSystems[i].Update();
            EmitProfilerData();
        }

        [Conditional("ENABLE_PROFILER")]
        void EmitProfilerData()
        {
            if (!UnityEngine.Profiling.Profiler.enabled)
                return;

            int totalSpriteSkinCount = 0;
            for (int i = 0; i < m_DeformationSystems.Length; ++i)
            {
                totalSpriteSkinCount += m_DeformationSystems[i].GetSpriteSkins().Count;
            }
            SpriteSkinProfilerFrameData[] frameData = new UnityEngine.U2D.Animation.Profiler.SpriteSkinProfilerFrameData[totalSpriteSkinCount];

            int spriteSkinIndex = 0;
            for (int i = 0; i < m_DeformationSystems.Length; ++i)
            {
                SpriteSkinProfilerFrameData.SpriteSkinType deformationType = GetDeformationType(m_DeformationSystems[i].GetType());
                foreach (SpriteSkin spriteSkin in m_DeformationSystems[i].GetSpriteSkins())
                {
                    SpriteSkinProfilerFrameData spriteSkinData = new SpriteSkinProfilerFrameData();
                    spriteSkinData.gameObjectEntityId = spriteSkin.gameObject.GetEntityId();
                    spriteSkinData.rootBoneGameObjectEntityId = spriteSkin.rootTransform != null ? spriteSkin.rootTransform.gameObject.GetEntityId() : EntityId.None;
                    spriteSkinData.boneCount = spriteSkin.boneTransforms.Length;
                    spriteSkinData.type = (int)deformationType;
                    frameData[spriteSkinIndex++] = spriteSkinData;
                }

            }

            UnityEngine.Profiling.Profiler.EmitFrameMetaData(Animation2DProfilerMarkers.k_Animation2DProfilerProjectId, Animation2DProfilerMarkers.k_SpriteSkinProfilerFrameMetaDataTag, frameData);
        }

        SpriteSkinProfilerFrameData.SpriteSkinType GetDeformationType(Type deformationType)
        {
            if (deformationType == typeof(CpuDeformationSystem))
                return SpriteSkinProfilerFrameData.SpriteSkinType.CPU;
            if (deformationType == typeof(GpuDeformationSystem))
                return SpriteSkinProfilerFrameData.SpriteSkinType.GPU;
            return SpriteSkinProfilerFrameData.SpriteSkinType.Unknown;
        }

        bool UpdateGpuDeformationConfig()
        {
            // This handles the following:
            // 1) SRP / Builtin Transition.
            // 2) SRP Batcher Toggle Transition.
            // 3) GPU Skinning Toggle Transition.

            bool isUsingGpuDeformation = SpriteSkinUtility.IsUsingGpuDeformation();
            if (isUsingGpuDeformation != m_WasUsingGpuDeformationLastFrame)
            {
                m_WasUsingGpuDeformationLastFrame = isUsingGpuDeformation;
                m_HandleDeformationChange = true;
            }

            bool isUsingSRPBatcher = false;
#if ENABLE_URP
            isUsingSRPBatcher = urpPipelineAsset ? urpPipelineAsset.useSRPBatcher : false;
#endif
            if (isUsingSRPBatcher != m_WasUsingSRPBatcherLastFrame)
            {
                m_WasUsingSRPBatcherLastFrame = isUsingSRPBatcher;
                m_HandleDeformationChange = true;
            }

            return m_HandleDeformationChange;
        }

        void MoveSpriteSkinsToActiveSystem()
        {
            // No GPU system was allocated (platform can't use GPU deformation), so only the CPU
            // system exists at index 0. All skins already live there — nothing to migrate, and
            // indexing DeformationMethods.Gpu (1) would be out of bounds. (DANB-1079)
            if (!canUseGpuDeformation || m_DeformationSystems.Length <= 1)
                return;

            BaseDeformationSystem prevSystem = SpriteSkinUtility.IsUsingGpuDeformation() ? m_DeformationSystems[(int)DeformationMethods.Cpu] : m_DeformationSystems[(int)DeformationMethods.Gpu];

            HashSet<SpriteSkin> skins = prevSystem.GetSpriteSkins();
            foreach (SpriteSkin spriteSkin in skins)
                prevSystem.RemoveSpriteSkin(spriteSkin);

            foreach (SpriteSkin spriteSkin in skins)
                AddSpriteSkin(spriteSkin);
#if UNITY_EDITOR
            SceneView.RepaintAll();
#endif
        }

        // Promote Sprite Skins whose SRP-batcher compatibility was Undetermined at add time to GPU once it
        // resolves to Compatible. Empty pending set => O(1). Promote-only, so a transient state never bounces
        // a GPU skin back to CPU. (UUM-143532)
        void DrainPendingGpuPromotion()
        {
            if (m_PendingGpuPromotion.Count == 0)
                return;

            // GPU deformation globally unavailable/off: these belong on CPU. A global toggle change re-adds
            // everyone via MoveSpriteSkinsToActiveSystem, so just stop tracking them here.
            if (!canUseGpuDeformation || !SpriteSkinUtility.IsUsingGpuDeformation())
            {
                m_PendingGpuPromotion.Clear();
                return;
            }

            s_PendingPromotionScratch.Clear();
            s_PendingPromotionScratch.AddRange(m_PendingGpuPromotion);
            foreach (SpriteSkin spriteSkin in s_PendingPromotionScratch)
            {
                // Disabled/destroyed skins detach from their deformation system; drop them.
                if (spriteSkin == null || spriteSkin.DeformationSystem == null)
                {
                    m_PendingGpuPromotion.Remove(spriteSkin);
                    continue;
                }

                switch (SpriteSkinUtility.GetGpuDeformEligibility(spriteSkin))
                {
                    case SpriteSkinUtility.GpuDeformEligibility.Eligible:
                        MoveSpriteSkinToGpu(spriteSkin);
                        m_PendingGpuPromotion.Remove(spriteSkin);
                        break;
                    case SpriteSkinUtility.GpuDeformEligibility.Undetermined:
                        // Not computed yet; keep and re-check next Update.
                        break;
                    case SpriteSkinUtility.GpuDeformEligibility.IneligibleUnsupportedShader:
                        // A pending skin's shader was supported when queued but now resolves to unsupported
                        // (e.g. its material was swapped while waiting). Emit the same actionable warning
                        // AddSpriteSkin would, then stop tracking. (UUM-143532)
                        WarnUnsupportedShader(spriteSkin);
                        m_PendingGpuPromotion.Remove(spriteSkin);
                        break;
                    default:
                        // Determined ineligible (SRP-batcher incompatible); stays on CPU silently, stop tracking.
                        m_PendingGpuPromotion.Remove(spriteSkin);
                        break;
                }
            }
        }

        void MoveSpriteSkinToGpu(SpriteSkin spriteSkin)
        {
            BaseDeformationSystem gpuSystem = m_DeformationSystems[(int)DeformationMethods.Gpu];
            if (spriteSkin.DeformationSystem == gpuSystem)
                return;

            // Deferred remove from the current (CPU) system + add to GPU. SetDeformationSystem updates the
            // owner immediately so the CPU system's queued BatchRemoveSpriteSkins does not clear the dataIndex
            // the GPU system reassigns (UUM-137003). Re-cache for the GPU (outline) deformation path.
            spriteSkin.spriteRenderer.DeactivateDeformableBuffer();
            spriteSkin.DeformationSystem?.RemoveSpriteSkin(spriteSkin);
            if (gpuSystem.AddSpriteSkin(spriteSkin))
            {
                spriteSkin.SetDeformationSystem(gpuSystem);
                spriteSkin.UpdateSpriteDeformationData();
            }
        }

        internal void AddSpriteSkin(SpriteSkin spriteSkin, bool isUpdateSpriteDeformationData = true)
        {
            if (spriteSkin == null)
                return;

            // First, find the system which can handle the sprite skin.
            DeformationMethods deformationMethod = SpriteSkinUtility.IsUsingGpuDeformation() ? DeformationMethods.Gpu : DeformationMethods.Cpu;
            if (deformationMethod == DeformationMethods.Gpu && null != spriteSkin.sprite)
            {
                // Deactivate Buffer for GPU.
                spriteSkin.spriteRenderer.DeactivateDeformableBuffer();

                if (!canUseGpuDeformation)
                {
                    deformationMethod = DeformationMethods.Cpu;
                    Debug.LogWarning($"{spriteSkin.name} is trying to use GPU deformation, but the platform does not support it. Switching the renderer over to CPU deformation.", spriteSkin);
                }
                else
                {
                    // Re-evaluate from scratch; only stays pending below if still Undetermined.
                    m_PendingGpuPromotion.Remove(spriteSkin);

                    SpriteSkinUtility.GpuDeformEligibility eligibility = SpriteSkinUtility.GetGpuDeformEligibility(spriteSkin);
                    if (eligibility != SpriteSkinUtility.GpuDeformEligibility.Eligible)
                    {
                        deformationMethod = DeformationMethods.Cpu;

                        if (eligibility == SpriteSkinUtility.GpuDeformEligibility.Undetermined)
                        {
                            // The renderer's SRP-batcher compatibility has not been computed yet (e.g. before
                            // the first render / during asset preview generation). Use CPU for now and promote
                            // to GPU once it resolves to Compatible, instead of warning and staying on CPU. (UUM-143532)
                            m_PendingGpuPromotion.Add(spriteSkin);
                        }
                        else if (eligibility == SpriteSkinUtility.GpuDeformEligibility.IneligibleUnsupportedShader)
                        {
                            // The shader genuinely lacks GPU deformation support (the user-actionable cause).
                            // Other Ineligible reasons (e.g. SRP-batcher incompatibility) fall back to CPU silently.
                            WarnUnsupportedShader(spriteSkin);
                        }
                    }
                }
            }
            // Second, add the sprite skin to the system.
            BaseDeformationSystem deformationSystem = m_DeformationSystems[(int)deformationMethod];
            if (deformationSystem.AddSpriteSkin(spriteSkin))
            {
                spriteSkin.SetDeformationSystem(deformationSystem);
                if (isUpdateSpriteDeformationData)
                    spriteSkin.UpdateSpriteDeformationData();
            }
        }

        static void WarnUnsupportedShader(SpriteSkin spriteSkin)
        {
            Material material = spriteSkin.spriteRenderer.sharedMaterial;
            string shaderName = material?.shader?.name ?? "Unknown";
            Debug.LogWarning($"{spriteSkin.name} is using a shader '{shaderName}' without GPU deformation support. Switching the renderer over to CPU deformation.", spriteSkin);
        }

        internal void RemoveBoneTransforms(SpriteSkin spriteSkin)
        {
            for (int i = 0; i < m_DeformationSystems.Length; ++i)
                m_DeformationSystems[i].RemoveBoneTransforms(spriteSkin);
        }

        internal void AddSpriteSkinBoneTransform(SpriteSkin spriteSkin)
        {
            if (spriteSkin == null)
                return;
            BaseDeformationSystem system = spriteSkin.DeformationSystem;
            if (system == null)
                return;

            system.AddBoneTransforms(spriteSkin);
        }

#if UNITY_INCLUDE_TESTS
        internal SpriteSkin[] GetSpriteSkins()
        {
            List<SpriteSkin> skinList = new List<SpriteSkin>();
            for (int i = 0; i < m_DeformationSystems.Length; ++i)
                skinList.AddRange(m_DeformationSystems[i].GetSpriteSkins());

            return skinList.ToArray();
        }

        internal TransformAccessJob GetWorldToLocalTransformAccessJob(DeformationMethods deformationMethod)
        {
            if (!IsValidDeformationMethod(deformationMethod, out int systemIndex))
                return null;
            return m_DeformationSystems[systemIndex].GetWorldToLocalTransformAccessJob();
        }

        internal TransformAccessJob GetLocalToWorldTransformAccessJob(DeformationMethods deformationMethod)
        {
            if (!IsValidDeformationMethod(deformationMethod, out int systemIndex))
                return null;
            return m_DeformationSystems[systemIndex].GetLocalToWorldTransformAccessJob();
        }

        bool IsValidDeformationMethod(DeformationMethods deformationMethod, out int methodIndex)
        {
            methodIndex = (int)deformationMethod;
            return methodIndex < m_DeformationSystems.Length;
        }
#endif
    }

#if UNITY_EDITOR

    [UnityEditor.InitializeOnLoad]
    internal class DeformationStartup
    {
        static DeformationStartup()
        {
            if (null == DeformationManager.instance.helperGameObject)
                throw new System.InvalidOperationException("SpriteSkinComposite not initialized properly.");
        }
    }

#endif

}
