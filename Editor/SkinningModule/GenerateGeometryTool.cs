using System;
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
                var selectedSprite = skinningCache.selectedSprite;
                if (selectedSprite != null)
                    GenerateGeometryForSprites(new[] { selectedSprite }, detail, alpha, subdivide);
            };

            m_GenerateGeometryPanel.onAutoGenerateGeometryAll += (float detail, byte alpha, float subdivide) =>
            {
                var sprites = skinningCache.GetSprites();
                GenerateGeometryForSprites(sprites, detail, alpha, subdivide);
            };
        }

        void GenerateGeometryForSprites(SpriteCache[] sprites, float detail, byte alpha, float subdivide)
        {
            var cancelProgress = false;
                
            using (skinningCache.UndoScope(TextContent.generateGeometry))
            {
                for (var i = 0; i < sprites.Length; ++i)
                {
                    var sprite = sprites[i];
                    if (!sprite.IsVisible())
                        continue;

                    Debug.Assert(sprite != null);
                    var mesh = sprite.GetMesh();
                    Debug.Assert(mesh != null);

                    m_SpriteMeshDataController.spriteMeshData = mesh;
                    
                    cancelProgress = EditorUtility.DisplayCancelableProgressBar(TextContent.generatingOutline, sprite.name,  i / (sprites.Length * 4f));
                    if (cancelProgress)
                        break;
                    m_SpriteMeshDataController.OutlineFromAlpha(m_OutlineGenerator, mesh.textureDataProvider, detail / 100f, alpha);
                    
                    cancelProgress = EditorUtility.DisplayCancelableProgressBar(TextContent.triangulatingGeometry, sprite.name,  (i * 2) / (sprites.Length * 4f));
                    if (cancelProgress)
                        break;
                    m_SpriteMeshDataController.Triangulate(m_Triangulator);
            
                    if (subdivide > 0f)
                    {
                        cancelProgress = EditorUtility.DisplayCancelableProgressBar(TextContent.subdividingGeometry, sprite.name, (i * 3) / (sprites.Length * 4f));
                        if (cancelProgress)
                            break;
                        var largestAreaFactor = subdivide != 0 ? Mathf.Lerp(0.5f, 0.05f, Math.Min(subdivide, 100f) / 100f) : subdivide;
                        m_SpriteMeshDataController.Subdivide(m_Triangulator, largestAreaFactor, 0f);
                    }                    

                    if (m_GenerateGeometryPanel.generateWeights)
                    {
                        cancelProgress = EditorUtility.DisplayCancelableProgressBar(TextContent.generatingWeights, sprite.name, (i * 4) / (sprites.Length * 4f));
                        if (cancelProgress)
                            break;
                            
                        GenerateWeights(sprite);
                    }
                }

                if (!cancelProgress)
                {
                    skinningCache.vertexSelection.Clear();
                    foreach(var sprite in sprites)
                        skinningCache.events.meshChanged.Invoke(sprite.GetMesh());
                }
                    
                EditorUtility.ClearProgressBar();
            }
                
            if(cancelProgress)
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
            var selectedSprite = skinningCache.selectedSprite;

            if (selectedSprite == null)
                m_GenerateGeometryPanel.SetMode(GenerateGeometryPanel.GenerateMode.Multiple);
            else
                m_GenerateGeometryPanel.SetMode(GenerateGeometryPanel.GenerateMode.Single);
        }

        private void OnSelectedSpriteChanged(SpriteCache sprite)
        {
            UpdateButton();
        }

        private void GenerateWeights(SpriteCache sprite)
        {
            Debug.Assert(sprite != null);

            var mesh = sprite.GetMesh();

            Debug.Assert(mesh != null);

            using (new DefaultPoseScope(skinningCache.GetEffectiveSkeleton(sprite)))
            {
                if (NeedsAssociateBones(sprite.GetCharacterPart()))
                {
                    using (new AssociateBonesScope(sprite))
                    {
                        GenerateWeights(mesh);
                    }
                }
                else
                    GenerateWeights(mesh);
            }
        }

        private bool NeedsAssociateBones(CharacterPartCache characterPart)
        {
            if (characterPart == null)
                return false;

            var skeleton = characterPart.skinningCache.character.skeleton;

            return characterPart.boneCount == 0 ||
                    (characterPart.boneCount == 1 && characterPart.GetBone(0) == skeleton.GetBone(0));
        }

        private void GenerateWeights(MeshCache mesh)
        {
            Debug.Assert(mesh != null);

            m_SpriteMeshDataController.spriteMeshData = mesh;
            m_SpriteMeshDataController.CalculateWeights(m_WeightGenerator, null, kWeightTolerance);
            m_SpriteMeshDataController.SortTrianglesByDepth();
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
