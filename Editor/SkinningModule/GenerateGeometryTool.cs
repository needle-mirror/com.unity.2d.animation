using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor.U2D.Common;
using UnityEditor.U2D.Layout;
using UnityEngine;

namespace UnityEditor.U2D.Animation
{
    internal class GenerateGeometryTool : MeshToolWrapper
    {
        private const float kWeightTolerance = 0.1f;
        private SpriteMeshDataController m_SpriteMeshDataController = new SpriteMeshDataController();
        private ITriangulator m_Triangulator;
        private IOutlineGenerator m_OutlineGenerator;
        private IWeightsGenerator m_WeightGenerator;
        private GenerateGeometryPanel m_GenerateGeometryPanel;

        internal override void OnCreate()
        {
            m_Triangulator = new Triangulator();
            m_OutlineGenerator = new OutlineGenerator();
            m_WeightGenerator = new BoundedBiharmonicWeightsGenerator();
        }

        public override void Initialize(LayoutOverlay layout)
        {
            base.Initialize(layout);

            m_GenerateGeometryPanel = GenerateGeometryPanel.GenerateFromUXML();
            m_GenerateGeometryPanel.skinningCache = skinningCache;

            layout.rightOverlay.Add(m_GenerateGeometryPanel);

            BindElements();
            Hide();
        }

        private void BindElements()
        {
            Debug.Assert(m_GenerateGeometryPanel != null);

            m_GenerateGeometryPanel.onAutoGenerateGeometry += (float detail, byte alpha, float subdivide) =>
            {
                SpriteCache selectedSprite = skinningCache.selectedSprite;
                if (selectedSprite != null)
                    GenerateGeometryForSprites(new[] { selectedSprite }, detail, alpha, subdivide);
            };

            m_GenerateGeometryPanel.onAutoGenerateGeometryAll += (float detail, byte alpha, float subdivide) =>
            {
                SpriteCache[] sprites = skinningCache.GetSprites();
                GenerateGeometryForSprites(sprites, detail, alpha, subdivide);
            };
        }

        void GenerateGeometryForSprites(SpriteCache[] sprites, float detail, byte alpha, float subdivide)
        {
            bool cancelProgress = false;

            using (skinningCache.UndoScope(TextContent.generateGeometry))
            {

                float progressMax = sprites.Length * 4; // for ProgressBar
                int validSpriteCount = 0;

                //
                // Generate Outline
                //
                for (int i = 0; i < sprites.Length; ++i)
                {
                    SpriteCache sprite = sprites[i];
                    if (!sprite.IsVisible())
                        continue;

                    Debug.Assert(sprite != null);
                    MeshCache mesh = sprite.GetMesh();
                    Debug.Assert(mesh != null);

                    m_SpriteMeshDataController.spriteMeshData = mesh;
                    validSpriteCount++;

                    cancelProgress = EditorUtility.DisplayCancelableProgressBar(TextContent.generatingOutline, sprite.name, i / progressMax);
                    if (cancelProgress)
                        break;
                    m_SpriteMeshDataController.OutlineFromAlpha(m_OutlineGenerator, mesh.textureDataProvider, detail / 100f, alpha);
                }

                //
                // Generate Base Mesh Threaded.
                //
                const int maxDataCount = 65536;
                Dictionary<SpriteCache, SpriteJobData> dictSpriteJobData = new Dictionary<SpriteCache, SpriteJobData>();
                NativeArray<JobHandle> jobHandles = new NativeArray<JobHandle>(validSpriteCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                int jobCount = 0;

                for (int i = 0; i < sprites.Length; ++i)
                {
                    SpriteCache sprite = sprites[i];
                    if (!sprite.IsVisible())
                        continue;

                    cancelProgress = EditorUtility.DisplayCancelableProgressBar(TextContent.triangulatingGeometry, sprite.name, 0.25f + (i / progressMax));
                    if (cancelProgress)
                        break;

                    MeshCache mesh = sprite.GetMesh();
                    m_SpriteMeshDataController.spriteMeshData = mesh;

                    SpriteJobData sd = new SpriteJobData();
                    sd.spriteMesh = mesh;
                    sd.vertices = new NativeArray<float2>(maxDataCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                    sd.edges = new NativeArray<int2>(maxDataCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                    sd.indices = new NativeArray<int>(maxDataCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                    sd.weights = new NativeArray<BoneWeight>(maxDataCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                    sd.result = new NativeArray<int4>(1, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                    sd.result[0] = int4.zero;
                    dictSpriteJobData.Add(sprite, sd);
                    if (m_GenerateGeometryPanel.generateWeights)
                    {
                        jobHandles[jobCount] = m_SpriteMeshDataController.TriangulateJob(m_Triangulator, sd);
                    }
                    else
                    {
                        jobHandles[jobCount] = default(JobHandle);
                        m_SpriteMeshDataController.Triangulate(m_Triangulator);
                    }
                    jobCount++;
                }
                JobHandle.CombineDependencies(jobHandles).Complete();

                //
                // Generate Base Mesh Fallback.
                //
                foreach (SpriteJobData sd in dictSpriteJobData.Values)
                {
                    if (math.all(sd.result[0].xy))
                    {
                        sd.spriteMesh.Clear();

                        int2[] edges = new int2[sd.result[0].z];
                        int[] indices = new int[sd.result[0].y];

                        for (int j = 0; j < sd.result[0].x; ++j)
                            sd.spriteMesh.AddVertex(sd.vertices[j], default(BoneWeight));
                        for (int j = 0; j < sd.result[0].y; ++j)
                            indices[j] = sd.indices[j];
                        for (int j = 0; j < sd.result[0].z; ++j)
                            edges[j] = sd.edges[j];

                        sd.spriteMesh.SetEdges(edges);
                        sd.spriteMesh.SetIndices(indices);
                    }
                    else
                    {
                        m_SpriteMeshDataController.spriteMeshData = sd.spriteMesh;
                        m_SpriteMeshDataController.Triangulate(m_Triangulator);
                    }
                }

                //
                // Subdivide.
                //
                jobCount = 0;
                if (subdivide > 0f)
                {
                    float largestAreaFactor = subdivide != 0 ? Mathf.Lerp(0.5f, 0.05f, Math.Min(subdivide, 100f) / 100f) : subdivide;

                    for (int i = 0; i < sprites.Length; ++i)
                    {
                        SpriteCache sprite = sprites[i];
                        if (!sprite.IsVisible())
                            continue;

                        cancelProgress = EditorUtility.DisplayCancelableProgressBar(TextContent.subdividingGeometry, sprite.name, 0.5f + (i / progressMax));
                        if (cancelProgress)
                            break;

                        MeshCache mesh = sprite.GetMesh();
                        m_SpriteMeshDataController.spriteMeshData = mesh;

                        SpriteJobData sd = dictSpriteJobData[sprite];
                        sd.spriteMesh = mesh;
                        sd.result[0] = int4.zero;
                        m_SpriteMeshDataController.Subdivide(m_Triangulator, sd, largestAreaFactor, 0f);
                    }

                }

                //
                // Weight.
                //
                jobCount = 0;
                if (m_GenerateGeometryPanel.generateWeights)
                {

                    for (int i = 0; i < sprites.Length; i++)
                    {
                        SpriteCache sprite = sprites[i];
                        if (!sprite.IsVisible())
                            continue;

                        MeshCache mesh = sprite.GetMesh();
                        m_SpriteMeshDataController.spriteMeshData = mesh;

                        cancelProgress = EditorUtility.DisplayCancelableProgressBar(TextContent.generatingWeights, sprite.name, 0.75f + (i / progressMax));
                        if (cancelProgress)
                            break;

                        SpriteJobData sd = dictSpriteJobData[sprite];
                        jobHandles[jobCount] = GenerateWeights(sprite, sd);
                        jobCount++;
                    }

                    // Weight
                    JobHandle.CombineDependencies(jobHandles).Complete();

                    for (int i = 0; i < sprites.Length; i++)
                    {
                        SpriteCache sprite = sprites[i];
                        if (!sprite.IsVisible())
                            continue;

                        MeshCache mesh = sprite.GetMesh();
                        m_SpriteMeshDataController.spriteMeshData = mesh;
                        SpriteJobData sd = dictSpriteJobData[sprite];

                        for (int j = 0; j < mesh.vertexCount; ++j)
                        {
                            EditableBoneWeight editableBoneWeight = EditableBoneWeightUtility.CreateFromBoneWeight(sd.weights[j]);

                            if (kWeightTolerance > 0f)
                            {
                                editableBoneWeight.FilterChannels(kWeightTolerance);
                                editableBoneWeight.Normalize();
                            }

                            mesh.vertexWeights[j] = editableBoneWeight;
                        }
                        if (null != sprite.GetCharacterPart())
                            sprite.DeassociateUnusedBones();
                        m_SpriteMeshDataController.SortTrianglesByDepth();
                    }

                }

                foreach (SpriteJobData sd in dictSpriteJobData.Values)
                {
                    sd.result.Dispose();
                    sd.indices.Dispose();
                    sd.edges.Dispose();
                    sd.vertices.Dispose();
                    sd.weights.Dispose();
                }

                if (!cancelProgress)
                {
                    skinningCache.vertexSelection.Clear();
                    foreach (SpriteCache sprite in sprites)
                        skinningCache.events.meshChanged.Invoke(sprite.GetMesh());
                }

                EditorUtility.ClearProgressBar();
            }

            if (cancelProgress)
                Undo.PerformUndo();
        }

        protected override void OnActivate()
        {
            base.OnActivate();
            UpdateButton();
            Show();
            skinningCache.events.selectedSpriteChanged.AddListener(OnSelectedSpriteChanged);
        }

        protected override void OnDeactivate()
        {
            base.OnDeactivate();
            Hide();
            skinningCache.events.selectedSpriteChanged.RemoveListener(OnSelectedSpriteChanged);
        }

        private void Show()
        {
            m_GenerateGeometryPanel.SetHiddenFromLayout(false);
        }

        private void Hide()
        {
            m_GenerateGeometryPanel.SetHiddenFromLayout(true);
        }

        private void UpdateButton()
        {
            SpriteCache selectedSprite = skinningCache.selectedSprite;

            if (selectedSprite == null)
                m_GenerateGeometryPanel.SetMode(GenerateGeometryPanel.GenerateMode.Multiple);
            else
                m_GenerateGeometryPanel.SetMode(GenerateGeometryPanel.GenerateMode.Single);
        }

        private void OnSelectedSpriteChanged(SpriteCache sprite)
        {
            UpdateButton();
        }

        private JobHandle GenerateWeights(SpriteCache sprite, SpriteJobData sd)
        {
            Debug.Assert(sprite != null);

            MeshCache mesh = sprite.GetMesh();

            Debug.Assert(mesh != null);

            using (new DefaultPoseScope(skinningCache.GetEffectiveSkeleton(sprite)))
            {
                sprite.AssociatePossibleBones();
                return GenerateWeights(mesh, sd);
            }
        }

        // todo: Remove. This function seems dubious. Only associate if boneCount is 0 or if boneCount 1 and first bone matches ?
        private bool NeedsAssociateBones(CharacterPartCache characterPart)
        {
            if (characterPart == null)
                return false;

            SkeletonCache skeleton = characterPart.skinningCache.character.skeleton;

            return characterPart.boneCount == 0 ||
                    (characterPart.boneCount == 1 && characterPart.GetBone(0) == skeleton.GetBone(0));
        }

        private JobHandle GenerateWeights(MeshCache mesh, SpriteJobData sd)
        {
            Debug.Assert(mesh != null);

            m_SpriteMeshDataController.spriteMeshData = mesh;
            JobHandle JobHandle = m_SpriteMeshDataController.CalculateWeightsJob(m_WeightGenerator, null, kWeightTolerance, sd);

            return JobHandle;
        }

        protected override void OnGUI()
        {
            m_MeshPreviewBehaviour.showWeightMap = m_GenerateGeometryPanel.generateWeights;
            m_MeshPreviewBehaviour.overlaySelected = m_GenerateGeometryPanel.generateWeights;

            skeletonTool.skeletonStyle = SkeletonStyles.Default;

            if (m_GenerateGeometryPanel.generateWeights)
                skeletonTool.skeletonStyle = SkeletonStyles.WeightMap;

            DoSkeletonGUI();
        }
    }
}
