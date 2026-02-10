using System.Collections.Generic;
using UnityEditor;
using UnityEngine.Rendering;

#if ENABLE_URP
using UnityEngine.Rendering.Universal;
#endif

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

            for (int i = 0; i < m_DeformationSystems.Length; ++i)
                m_DeformationSystems[i].Update();
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
                else if (!SpriteSkinUtility.CanSpriteSkinUseGpuDeformation(spriteSkin))
                {
                    deformationMethod = DeformationMethods.Cpu;

                    Material material = spriteSkin.GetComponent<SpriteRenderer>()?.sharedMaterial;
                    string shaderName = material?.shader?.name ?? "Unknown";
                    Debug.LogWarning($"{spriteSkin.name} is using a shader '{shaderName}' without GPU deformation support. Switching the renderer over to CPU deformation.", spriteSkin);
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
