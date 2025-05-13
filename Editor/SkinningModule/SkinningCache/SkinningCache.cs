using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unity.Mathematics;
using UnityEditor.U2D.Layout;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.U2D.Common;
using UnityEngine.UIElements;
using Debug = UnityEngine.Debug;

namespace UnityEditor.U2D.Animation
{
    internal class SkinningObject : CacheObject
    {
        public SkinningCache skinningCache => owner as SkinningCache;
    }

    internal class SkinningCache : Cache
    {
        [Serializable]
        class SpriteMap : SerializableDictionary<string, SpriteCache> { }

        [Serializable]
        class MeshMap : SerializableDictionary<SpriteCache, MeshCache> { }

        [Serializable]
        class SkeletonMap : SerializableDictionary<SpriteCache, SkeletonCache> { }

        [Serializable]
        class ToolMap : SerializableDictionary<Tools, BaseTool> { }

        [Serializable]
        class MeshPreviewMap : SerializableDictionary<SpriteCache, MeshPreviewCache> { }

        [Serializable]
        class CharacterPartMap : SerializableDictionary<SpriteCache, CharacterPartCache> { }

        [SerializeField]
        SkinningEvents m_Events = new SkinningEvents();
        [SerializeField]
        List<BaseTool> m_Tools = new List<BaseTool>();
        [SerializeField]
        SpriteMap m_SpriteMap = new SpriteMap();
        [SerializeField]
        MeshMap m_MeshMap = new MeshMap();
        [SerializeField]
        MeshPreviewMap m_MeshPreviewMap = new MeshPreviewMap();
        [SerializeField]
        SkeletonMap m_SkeletonMap = new SkeletonMap();
        [SerializeField]
        CharacterPartMap m_CharacterPartMap = new CharacterPartMap();
        [SerializeField]
        ToolMap m_ToolMap = new ToolMap();
        [SerializeField]
        SelectionTool m_SelectionTool;
        [SerializeField]
        CharacterCache m_Character;
        [SerializeField]
        bool m_BonesReadOnly;
        [SerializeField]
        SkinningMode m_Mode = SkinningMode.SpriteSheet;
        [SerializeField]
        BaseTool m_SelectedTool;
        [SerializeField]
        SpriteCache m_SelectedSprite;
        [SerializeField]
        SkeletonSelection m_SkeletonSelection = new SkeletonSelection();
        [SerializeField]
        ISkinningCachePersistentState m_State;

        StringBuilder m_StringBuilder = new StringBuilder();

        public BaseTool selectedTool
        {
            get => m_SelectedTool;
            set
            {
                m_SelectedTool = value;
                try
                {
                    m_State.lastUsedTool = m_ToolMap[value];
                }
                catch (KeyNotFoundException)
                {
                    m_State.lastUsedTool = Tools.EditPose;
                }
            }
        }

        public virtual SkinningMode mode
        {
            get => m_Mode;
            set
            {
                m_Mode = CheckModeConsistency(value);
                m_State.lastMode = m_Mode;
            }
        }

        public SpriteCache selectedSprite
        {
            get => m_SelectedSprite;
            set
            {
                m_SelectedSprite = value;
                m_State.lastSpriteId = m_SelectedSprite ? m_SelectedSprite.id : String.Empty;
            }
        }

        public float brushSize
        {
            get => m_State.lastBrushSize;
            set => m_State.lastBrushSize = value;
        }

        public float brushHardness
        {
            get => m_State.lastBrushHardness;
            set => m_State.lastBrushHardness = value;
        }

        public float brushStep
        {
            get => m_State.lastBrushStep;
            set => m_State.lastBrushStep = value;
        }

        public int visibilityToolIndex
        {
            get => m_State.lastVisibilityToolIndex;
            set => m_State.lastVisibilityToolIndex = value;
        }

        public SkeletonSelection skeletonSelection => m_SkeletonSelection;

        public IndexedSelection vertexSelection => m_State.lastVertexSelection;

        public SkinningEvents events => m_Events;

        public SelectionTool selectionTool => m_SelectionTool;

        public SpriteCache[] GetSprites()
        {
            return m_SpriteMap.Values.ToArray();
        }

        public virtual CharacterCache character => m_Character;

        public bool hasCharacter => character != null;

        public bool bonesReadOnly => m_BonesReadOnly;

        public bool applyingChanges { get; set; }

        SkinningMode CheckModeConsistency(SkinningMode skinningMode)
        {
            if (skinningMode == SkinningMode.Character && hasCharacter == false)
                skinningMode = SkinningMode.SpriteSheet;

            return skinningMode;
        }

        public void Create(ISpriteEditorDataProvider spriteEditor, ISkinningCachePersistentState state)
        {
            Clear();

            ISpriteEditorDataProvider dataProvider = spriteEditor.GetDataProvider<ISpriteEditorDataProvider>();
            ISpriteBoneDataProvider boneProvider = spriteEditor.GetDataProvider<ISpriteBoneDataProvider>();
            ISpriteMeshDataProvider meshProvider = spriteEditor.GetDataProvider<ISpriteMeshDataProvider>();
            SpriteRect[] spriteRects = dataProvider.GetSpriteRects();
            ITextureDataProvider textureProvider = spriteEditor.GetDataProvider<ITextureDataProvider>();

            m_State = state;
            m_State.lastTexture = textureProvider.texture;

            for (int i = 0; i < spriteRects.Length; i++)
            {
                SpriteRect spriteRect = spriteRects[i];
                SpriteCache sprite = CreateSpriteCache(spriteRect);
                CreateSkeletonCache(sprite, boneProvider);
                CreateMeshCache(sprite, meshProvider, textureProvider);
                CreateMeshPreviewCache(sprite);
            }

            CreateCharacter(spriteEditor);
        }

        public void CreateToolCache(ISpriteEditor spriteEditor, LayoutOverlay layoutOverlay)
        {
            ISpriteEditorDataProvider spriteEditorDataProvider = spriteEditor.GetDataProvider<ISpriteEditorDataProvider>();
            SkeletonTool skeletonTool = CreateCache<SkeletonTool>();
            MeshTool meshTool = CreateCache<MeshTool>();

            skeletonTool.Initialize(layoutOverlay);
            meshTool.Initialize(layoutOverlay);

            m_ToolMap.Add(Tools.EditPose, CreateSkeletonTool<SkeletonToolWrapper>(skeletonTool, SkeletonMode.EditPose, false, layoutOverlay));
            m_ToolMap.Add(Tools.EditJoints, CreateSkeletonTool<SkeletonToolWrapper>(skeletonTool, SkeletonMode.EditJoints, true, layoutOverlay));
            m_ToolMap.Add(Tools.CreateBone, CreateSkeletonTool<SkeletonToolWrapper>(skeletonTool, SkeletonMode.CreateBone, true, layoutOverlay));
            m_ToolMap.Add(Tools.SplitBone, CreateSkeletonTool<SkeletonToolWrapper>(skeletonTool, SkeletonMode.SplitBone, true, layoutOverlay));
            m_ToolMap.Add(Tools.ReparentBone, CreateSkeletonTool<BoneReparentTool>(skeletonTool, SkeletonMode.EditPose, false, layoutOverlay));
            m_ToolMap.Add(Tools.CharacterPivotTool, CreateSkeletonTool<PivotTool>(skeletonTool, SkeletonMode.Disabled, false, layoutOverlay));

            m_ToolMap.Add(Tools.EditGeometry, CreateMeshTool<MeshToolWrapper>(skeletonTool, meshTool, SpriteMeshViewMode.EditGeometry, SkeletonMode.Disabled, layoutOverlay));
            m_ToolMap.Add(Tools.CreateVertex, CreateMeshTool<MeshToolWrapper>(skeletonTool, meshTool, SpriteMeshViewMode.CreateVertex, SkeletonMode.Disabled, layoutOverlay));
            m_ToolMap.Add(Tools.CreateEdge, CreateMeshTool<MeshToolWrapper>(skeletonTool, meshTool, SpriteMeshViewMode.CreateEdge, SkeletonMode.Disabled, layoutOverlay));
            m_ToolMap.Add(Tools.SplitEdge, CreateMeshTool<MeshToolWrapper>(skeletonTool, meshTool, SpriteMeshViewMode.SplitEdge, SkeletonMode.Disabled, layoutOverlay));
            m_ToolMap.Add(Tools.GenerateGeometry, CreateMeshTool<GenerateGeometryTool>(skeletonTool, meshTool, SpriteMeshViewMode.EditGeometry, SkeletonMode.EditPose, layoutOverlay));
            CopyTool copyTool = CreateTool<CopyTool>();
            copyTool.Initialize(layoutOverlay);
            copyTool.pixelsPerUnit = spriteEditorDataProvider.pixelsPerUnit;
            copyTool.skeletonTool = skeletonTool;
            copyTool.meshTool = meshTool;
            m_ToolMap.Add(Tools.CopyPaste, copyTool);

            CreateWeightTools(skeletonTool, meshTool, layoutOverlay);

            m_SelectionTool = CreateTool<SelectionTool>();
            m_SelectionTool.spriteEditor = spriteEditor;
            m_SelectionTool.Initialize(layoutOverlay);
            m_SelectionTool.Activate();

            VisibilityTool visibilityTool = CreateTool<VisibilityTool>();
            visibilityTool.Initialize(layoutOverlay);
            visibilityTool.skeletonTool = skeletonTool;
            m_ToolMap.Add(Tools.Visibility, visibilityTool);

            SwitchModeTool switchModeTool = CreateTool<SwitchModeTool>();
            m_ToolMap.Add(Tools.SwitchMode, switchModeTool);
        }

        public void RestoreFromPersistentState()
        {
            mode = m_State.lastMode;
            events.skinningModeChanged.Invoke(mode);

            bool hasLastSprite = m_SpriteMap.TryGetValue(m_State.lastSpriteId, out SpriteCache lastSprite);
            if (hasLastSprite)
            {
                selectedSprite = lastSprite;
            }
            else
            {
                vertexSelection.Clear();
            }

            if (m_ToolMap.TryGetValue(m_State.lastUsedTool, out BaseTool baseTool))
            {
                selectedTool = baseTool;
            }
            else if (m_ToolMap.TryGetValue(Tools.EditPose, out baseTool))
            {
                selectedTool = baseTool;
            }

            BaseTool visibilityTool = m_ToolMap[Tools.Visibility];
            if (m_State.lastVisibilityToolActive)
            {
                visibilityTool.Activate();
            }
        }

        public void RestoreToolStateFromPersistentState()
        {
            events.boneSelectionChanged.RemoveListener(BoneSelectionChanged);
            events.skeletonPreviewPoseChanged.RemoveListener(SkeletonPreviewPoseChanged);
            events.toolChanged.RemoveListener(ToolChanged);

            SkeletonCache skeleton = null;
            if (hasCharacter)
                skeleton = character.skeleton;
            else if (selectedSprite != null)
                skeleton = selectedSprite.GetSkeleton();

            skeletonSelection.Clear();
            if (skeleton != null && m_State.lastBoneSelectionIds.Count > 0)
            {
                bool selectionChanged = false;
                foreach (BoneCache bone in skeleton.bones)
                {
                    int id = GetBoneNameHash(m_StringBuilder, bone);
                    if (m_State.lastBoneSelectionIds.Contains(id))
                    {
                        skeletonSelection.Select(bone, true);
                        selectionChanged = true;
                    }
                }

                if (selectionChanged)
                    events.boneSelectionChanged.Invoke();
            }

            if (m_State.lastPreviewPose.Count > 0)
            {
                if (hasCharacter)
                {
                    UpdatePoseFromPersistentState(character.skeleton, null);
                }

                foreach (SpriteCache sprite in m_SkeletonMap.Keys)
                {
                    UpdatePoseFromPersistentState(m_SkeletonMap[sprite], sprite);
                }
            }

            if (m_State.lastBoneVisibility.Count > 0)
            {
                if (hasCharacter)
                {
                    UpdateVisibilityFromPersistentState(character.skeleton, null);
                }

                foreach (SpriteCache sprite in m_SkeletonMap.Keys)
                {
                    UpdateVisibilityFromPersistentState(m_SkeletonMap[sprite], sprite);
                }
            }

            if (m_State.lastSpriteVisibility.Count > 0 && hasCharacter)
            {
                foreach (CharacterPartCache characterPart in character.parts)
                {
                    if (m_State.lastSpriteVisibility.TryGetValue(characterPart.sprite.id, out bool visibility))
                    {
                        characterPart.isVisible = visibility;
                    }
                }

                foreach (CharacterGroupCache characterGroup in character.groups)
                {
                    int groupHash = GetCharacterGroupHash(m_StringBuilder, characterGroup, character);
                    if (m_State.lastGroupVisibility.TryGetValue(groupHash, out bool visibility))
                    {
                        characterGroup.isVisible = visibility;
                    }
                }
            }

            events.boneSelectionChanged.AddListener(BoneSelectionChanged);
            events.skeletonPreviewPoseChanged.AddListener(SkeletonPreviewPoseChanged);
            events.toolChanged.AddListener(ToolChanged);
        }

        void UpdatePoseFromPersistentState(SkeletonCache skeleton, SpriteCache sprite)
        {
            bool poseChanged = false;
            foreach (BoneCache bone in skeleton.bones)
            {
                int id = GetBoneNameHash(m_StringBuilder, bone, sprite);
                if (m_State.lastPreviewPose.TryGetValue(id, out BonePose pose))
                {
                    bone.localPose = pose;
                    poseChanged = true;
                }
            }

            if (poseChanged)
            {
                skeleton.SetPosePreview();
                events.skeletonPreviewPoseChanged.Invoke(skeleton);
            }
        }

        void UpdateVisibilityFromPersistentState(SkeletonCache skeleton, SpriteCache sprite)
        {
            foreach (BoneCache bone in skeleton.bones)
            {
                int id = GetBoneNameHash(m_StringBuilder, bone, sprite);
                if (m_State.lastBoneVisibility.TryGetValue(id, out bool visibility))
                {
                    bone.isVisible = visibility;
                }
            }
        }

        const string k_NameSeparator = "/";

        int GetBoneNameHash(StringBuilder sb, BoneCache bone, SpriteCache sprite = null)
        {
            sb.Clear();
            BuildBoneName(sb, bone);
            sb.Append(k_NameSeparator);
            if (sprite != null)
            {
                sb.Append(sprite.id);
            }
            else
            {
                sb.Append(0);
            }

            return Animator.StringToHash(sb.ToString());
        }

        static void BuildBoneName(StringBuilder sb, BoneCache bone)
        {
            if (bone.parentBone != null)
            {
                BuildBoneName(sb, bone.parentBone);
                sb.Append(k_NameSeparator);
            }

            sb.Append(bone.name);
        }

        static int GetCharacterGroupHash(StringBuilder sb, CharacterGroupCache characterGroup, CharacterCache characterCache)
        {
            sb.Clear();
            BuildGroupName(sb, characterGroup, characterCache);
            return Animator.StringToHash(sb.ToString());
        }

        static void BuildGroupName(StringBuilder sb, CharacterGroupCache group, CharacterCache characterCache)
        {
            if (group.parentGroup >= 0 && group.parentGroup < characterCache.groups.Length)
            {
                BuildGroupName(sb, characterCache.groups[group.parentGroup], characterCache);
                sb.Append(k_NameSeparator);
            }

            sb.Append(group.order);
        }

        void BoneSelectionChanged()
        {
            m_State.lastBoneSelectionIds.Clear();
            m_State.lastBoneSelectionIds.Capacity = skeletonSelection.elements.Length;
            for (int i = 0; i < skeletonSelection.elements.Length; ++i)
            {
                BoneCache bone = skeletonSelection.elements[i];
                m_State.lastBoneSelectionIds.Add(GetBoneNameHash(m_StringBuilder, bone));
            }
        }

        void SkeletonPreviewPoseChanged(SkeletonCache sc)
        {
            if (applyingChanges)
                return;

            m_State.lastPreviewPose.Clear();
            if (hasCharacter)
            {
                StorePersistentStatePoseForSkeleton(character.skeleton, null);
            }

            foreach (SpriteCache sprite in m_SkeletonMap.Keys)
            {
                StorePersistentStatePoseForSkeleton(m_SkeletonMap[sprite], sprite);
            }
        }

        void StorePersistentStatePoseForSkeleton(SkeletonCache skeleton, SpriteCache sprite)
        {
            foreach (BoneCache bone in skeleton.bones)
            {
                int id = GetBoneNameHash(m_StringBuilder, bone, sprite);
                if (bone.NotInDefaultPose())
                {
                    m_State.lastPreviewPose[id] = bone.localPose;
                }
            }
        }

        internal void Revert()
        {
            m_State.lastVertexSelection.Clear();
        }

        internal void BoneVisibilityChanged()
        {
            if (applyingChanges)
                return;

            m_State.lastBoneVisibility.Clear();
            if (hasCharacter)
            {
                StorePersistentStateVisibilityForSkeleton(character.skeleton, null);
            }

            foreach (SpriteCache sprite in m_SkeletonMap.Keys)
            {
                StorePersistentStateVisibilityForSkeleton(m_SkeletonMap[sprite], sprite);
            }
        }

        void StorePersistentStateVisibilityForSkeleton(SkeletonCache skeleton, SpriteCache sprite)
        {
            foreach (BoneCache bone in skeleton.bones)
            {
                int id = GetBoneNameHash(m_StringBuilder, bone, sprite);
                m_State.lastBoneVisibility[id] = bone.isVisible;
            }
        }

        internal void BoneExpansionChanged(BoneCache[] boneCaches)
        {
            if (applyingChanges)
                return;

            m_State.lastBoneExpansion.Clear();
            if (hasCharacter)
            {
                foreach (BoneCache bone in boneCaches)
                {
                    if (character.skeleton.bones.Contains(bone))
                    {
                        int id = GetBoneNameHash(m_StringBuilder, bone, null);
                        m_State.lastBoneExpansion[id] = true;
                    }
                }
            }

            foreach (SpriteCache sprite in m_SkeletonMap.Keys)
            {
                SkeletonCache skeleton = m_SkeletonMap[sprite];
                foreach (BoneCache bone in boneCaches)
                {
                    if (skeleton.bones.Contains(bone))
                    {
                        int id = GetBoneNameHash(m_StringBuilder, bone, sprite);
                        m_State.lastBoneExpansion[id] = true;
                    }
                }
            }
        }

        internal BoneCache[] GetExpandedBones()
        {
            HashSet<BoneCache> expandedBones = new HashSet<BoneCache>();
            if (m_State.lastBoneExpansion.Count > 0)
            {
                if (hasCharacter)
                {
                    foreach (BoneCache bone in character.skeleton.bones)
                    {
                        int id = GetBoneNameHash(m_StringBuilder, bone, null);
                        if (m_State.lastBoneExpansion.TryGetValue(id, out bool expanded))
                        {
                            expandedBones.Add(bone);
                        }
                    }
                }

                foreach (SpriteCache sprite in m_SkeletonMap.Keys)
                {
                    SkeletonCache skeleton = m_SkeletonMap[sprite];
                    foreach (BoneCache bone in skeleton.bones)
                    {
                        int id = GetBoneNameHash(m_StringBuilder, bone, sprite);
                        if (m_State.lastBoneExpansion.TryGetValue(id, out bool expanded))
                        {
                            expandedBones.Add(bone);
                        }
                    }
                }
            }

            return expandedBones.ToArray();
        }

        internal void SpriteVisibilityChanged(CharacterPartCache cc)
        {
            m_State.lastSpriteVisibility[cc.sprite.id] = cc.isVisible;
        }

        internal void GroupVisibilityChanged(CharacterGroupCache gc)
        {
            if (!hasCharacter)
                return;

            int groupHash = GetCharacterGroupHash(m_StringBuilder, gc, character);
            m_State.lastGroupVisibility[groupHash] = gc.isVisible;
        }

        void Clear()
        {
            Destroy();
            m_Tools.Clear();
            m_SpriteMap.Clear();
            m_MeshMap.Clear();
            m_MeshPreviewMap.Clear();
            m_SkeletonMap.Clear();
            m_ToolMap.Clear();
            m_CharacterPartMap.Clear();
        }

        public SpriteCache GetSprite(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;

            m_SpriteMap.TryGetValue(id, out SpriteCache sprite);
            return sprite;
        }

        public virtual MeshCache GetMesh(SpriteCache sprite)
        {
            if (sprite == null)
                return null;

            m_MeshMap.TryGetValue(sprite, out MeshCache mesh);
            return mesh;
        }

        public virtual MeshPreviewCache GetMeshPreview(SpriteCache sprite)
        {
            if (sprite == null)
                return null;

            m_MeshPreviewMap.TryGetValue(sprite, out MeshPreviewCache meshPreview);
            return meshPreview;
        }

        public SkeletonCache GetSkeleton(SpriteCache sprite)
        {
            if (sprite == null)
                return null;

            m_SkeletonMap.TryGetValue(sprite, out SkeletonCache skeleton);
            return skeleton;
        }

        public virtual CharacterPartCache GetCharacterPart(SpriteCache sprite)
        {
            if (sprite == null)
                return null;

            m_CharacterPartMap.TryGetValue(sprite, out CharacterPartCache part);
            return part;
        }

        public SkeletonCache GetEffectiveSkeleton(SpriteCache sprite)
        {
            if (mode == SkinningMode.SpriteSheet)
                return GetSkeleton(sprite);

            if (hasCharacter)
                return character.skeleton;

            return null;
        }

        public BaseTool GetTool(Tools tool)
        {
            m_ToolMap.TryGetValue(tool, out BaseTool t);
            return t;
        }

        public override void BeginUndoOperation(string operationName)
        {
            if (isUndoOperationSet == false)
            {
                base.BeginUndoOperation(operationName);
                undo.RegisterCompleteObjectUndo(m_State, operationName);
            }
        }

        public UndoScope UndoScope(string operationName, bool incrementGroup = false)
        {
            return new UndoScope(this, operationName, incrementGroup);
        }

        public DisableUndoScope DisableUndoScope()
        {
            return new DisableUndoScope(this);
        }

        public T CreateTool<T>() where T : BaseTool
        {
            T tool = CreateCache<T>();
            m_Tools.Add(tool);
            return tool;
        }

        void UpdateCharacterPart(CharacterPartCache characterPart)
        {
            SpriteCache sprite = characterPart.sprite;
            BoneCache[] characterPartBones = characterPart.bones;
            List<BoneCache> newBones = new List<BoneCache>(characterPartBones);
            newBones.RemoveAll(b => b == null || IsRemoved(b) || b.skeleton != character.skeleton);
            int removedBonesCount = characterPartBones.Length - newBones.Count;

            characterPartBones = newBones.ToArray();
            characterPart.bones = characterPartBones;
            sprite.UpdateMesh(characterPartBones);

            if (removedBonesCount > 0)
                sprite.SmoothFill();
        }

        public void CreateSpriteSheetSkeletons()
        {
            Debug.Assert(character != null);

            using (new DefaultPoseScope(character.skeleton))
            {
                CharacterPartCache[] characterParts = character.parts;

                foreach (CharacterPartCache characterPart in characterParts)
                    CreateSpriteSheetSkeleton(characterPart);
            }

            SyncSpriteSheetSkeletons();
        }

        public void SyncSpriteSheetSkeletons()
        {
            Debug.Assert(character != null);

            CharacterPartCache[] characterParts = character.parts;

            foreach (CharacterPartCache characterPart in characterParts)
                characterPart.SyncSpriteSheetSkeleton();
        }

        public void CreateSpriteSheetSkeleton(CharacterPartCache characterPart)
        {
            UpdateCharacterPart(characterPart);

            Debug.Assert(character != null);
            Debug.Assert(character.skeleton != null);
            Debug.Assert(character.skeleton.isPosePreview == false);

            SpriteCache sprite = characterPart.sprite;
            BoneCache[] characterPartBones = characterPart.bones;
            SkeletonCache skeleton = sprite.GetSkeleton();

            Debug.Assert(skeleton != null);

            UnityEngine.U2D.SpriteBone[] spriteBones = characterPartBones.ToSpriteBone(characterPart.localToWorldMatrix);
            skeleton.SetBones(CreateBoneCacheFromSpriteBones(spriteBones, 1.0f), false);

            events.skeletonTopologyChanged.Invoke(skeleton);
        }

        SpriteCache CreateSpriteCache(SpriteRect spriteRect)
        {
            SpriteCache sprite = CreateCache<SpriteCache>();
            sprite.name = spriteRect.name;
            sprite.id = spriteRect.spriteID.ToString();
            sprite.textureRect = spriteRect.rect;
            sprite.position = spriteRect.rect.position;
            m_SpriteMap[sprite.id] = sprite;
            return sprite;
        }

        void CreateSkeletonCache(SpriteCache sprite, ISpriteBoneDataProvider boneProvider)
        {
            GUID guid = new GUID(sprite.id);
            SkeletonCache skeleton = CreateCache<SkeletonCache>();

            skeleton.position = sprite.textureRect.position;
            skeleton.SetBones(CreateBoneCacheFromSpriteBones(boneProvider.GetBones(guid).ToArray(), 1.0f), false);

            m_SkeletonMap[sprite] = skeleton;
        }

        void CreateMeshCache(SpriteCache sprite, ISpriteMeshDataProvider meshProvider, ITextureDataProvider textureDataProvider)
        {
            Debug.Assert(m_SkeletonMap.ContainsKey(sprite));

            GUID guid = new GUID(sprite.id);
            MeshCache mesh = new MeshCache();
            SkeletonCache skeleton = m_SkeletonMap[sprite] as SkeletonCache;

            mesh.sprite = sprite;
            mesh.SetCompatibleBoneSet(skeleton.bones);

            Vertex2DMetaData[] metaVertices = meshProvider.GetVertices(guid);
            if (metaVertices.Length > 0)
            {
                Vector2[] vertices = new Vector2[metaVertices.Length];
                EditableBoneWeight[] weights = new EditableBoneWeight[metaVertices.Length];
                for (int i = 0; i < metaVertices.Length; ++i)
                {
                    vertices[i] = metaVertices[i].position;
                    weights[i] = EditableBoneWeightUtility.CreateFromBoneWeight(metaVertices[i].boneWeight);
                }

                mesh.SetVertices(vertices, weights);
                mesh.SetIndices(meshProvider.GetIndices(guid));
                mesh.SetEdges(EditorUtilities.ToInt2(meshProvider.GetEdges(guid)));
            }
            else
            {
                GenerateOutline(sprite, textureDataProvider, out Vector2[] vertices, out int[] indices, out int2[] edges);

                EditableBoneWeight[] vertexWeights = new EditableBoneWeight[vertices.Length];
                for (int i = 0; i < vertexWeights.Length; ++i)
                    vertexWeights[i] = new EditableBoneWeight();

                mesh.SetVertices(vertices, vertexWeights);
                mesh.SetIndices(indices);
                mesh.SetEdges(edges);
            }

            mesh.textureDataProvider = textureDataProvider;

            m_MeshMap[sprite] = mesh;
        }

        static void GenerateOutline(SpriteCache sprite, ITextureDataProvider textureDataProvider,
            out Vector2[] vertices, out int[] indices, out int2[] edges)
        {
            if (textureDataProvider == null ||
                textureDataProvider.texture == null)
            {
                vertices = new Vector2[0];
                indices = new int[0];
                edges = new int2[0];
                return;
            }

            const float detail = 0.05f;
            const byte alphaTolerance = 200;

            SpriteMeshData smd = new SpriteMeshData();
            smd.SetFrame(sprite.textureRect);

            SpriteMeshDataController meshDataController = new SpriteMeshDataController
            {
                spriteMeshData = smd
            };

            meshDataController.OutlineFromAlpha(new OutlineGenerator(), textureDataProvider, detail, alphaTolerance);
            meshDataController.Triangulate(new Triangulator());

            vertices = smd.vertices;
            indices = smd.indices;
            edges = smd.edges;
        }

        void CreateMeshPreviewCache(SpriteCache sprite)
        {
            Debug.Assert(sprite != null);
            Debug.Assert(m_MeshPreviewMap.ContainsKey(sprite) == false);

            MeshPreviewCache meshPreview = CreateCache<MeshPreviewCache>();

            meshPreview.sprite = sprite;
            meshPreview.SetMeshDirty();

            m_MeshPreviewMap.Add(sprite, meshPreview);
        }

        void CreateCharacter(ISpriteEditorDataProvider spriteEditor)
        {
            ICharacterDataProvider characterProvider = spriteEditor.GetDataProvider<ICharacterDataProvider>();

            if (characterProvider != null)
            {
                CharacterData characterData = characterProvider.GetCharacterData();
                List<CharacterPartCache> characterParts = new List<CharacterPartCache>();

                m_Character = CreateCache<CharacterCache>();
                m_BonesReadOnly = spriteEditor.GetDataProvider<IMainSkeletonDataProvider>() != null;

                SkeletonCache skeleton = CreateCache<SkeletonCache>();

                UnityEngine.U2D.SpriteBone[] characterBones = characterData.bones;

                skeleton.SetBones(CreateBoneCacheFromSpriteBones(characterBones, 1.0f));
                skeleton.position = Vector3.zero;

                BoneCache[] bones = skeleton.bones;
                foreach (CharacterPart p in characterData.parts)
                {
                    List<int> spriteBones = p.bones != null ? p.bones.ToList() : new List<int>();
                    BoneCache[] characterPartBones = spriteBones.ConvertAll(i => bones.ElementAtOrDefault(i)).ToArray();
                    CharacterPartCache characterPart = CreateCache<CharacterPartCache>();

                    Vector2Int positionInt = p.spritePosition.position;
                    characterPart.position = new Vector2(positionInt.x, positionInt.y);
                    characterPart.sprite = GetSprite(p.spriteId);
                    characterPart.bones = characterPartBones;
                    characterPart.parentGroup = p.parentGroup;
                    characterPart.order = p.order;

                    MeshCache mesh = characterPart.sprite.GetMesh();
                    if (mesh != null)
                        mesh.SetCompatibleBoneSet(characterPartBones);

                    characterParts.Add(characterPart);

                    m_CharacterPartMap.Add(characterPart.sprite, characterPart);
                }

                if (characterData.characterGroups != null)
                {
                    m_Character.groups = characterData.characterGroups.Select(x =>
                    {
                        CharacterGroupCache group = CreateCache<CharacterGroupCache>();
                        group.name = x.name;
                        group.parentGroup = x.parentGroup;
                        group.order = x.order;
                        return group;
                    }).ToArray();
                }
                else
                {
                    m_Character.groups = new CharacterGroupCache[0];
                }

                m_Character.parts = characterParts.ToArray();
                m_Character.skeleton = skeleton;
                m_Character.dimension = characterData.dimension;
                m_Character.pivot = characterData.pivot;
                CreateSpriteSheetSkeletons();
            }
        }

        T CreateSkeletonTool<T>(SkeletonTool skeletonTool, SkeletonMode skeletonMode, bool editBindPose, LayoutOverlay layoutOverlay) where T : SkeletonToolWrapper
        {
            T tool = CreateTool<T>();
            tool.skeletonTool = skeletonTool;
            tool.mode = skeletonMode;
            tool.editBindPose = editBindPose;
            tool.Initialize(layoutOverlay);
            return tool;
        }

        void CreateWeightTools(SkeletonTool skeletonTool, MeshTool meshTool, LayoutOverlay layoutOverlay)
        {
            WeightPainterTool weightPainterTool = CreateCache<WeightPainterTool>();
            weightPainterTool.Initialize(layoutOverlay);
            weightPainterTool.skeletonTool = skeletonTool;
            weightPainterTool.meshTool = meshTool;

            {
                SpriteBoneInfluenceTool tool = CreateTool<SpriteBoneInfluenceTool>();
                tool.Initialize(layoutOverlay);
                tool.skeletonTool = skeletonTool;
                m_ToolMap.Add(Tools.BoneInfluence, tool);
            }

            {
                BoneSpriteInfluenceTool tool = CreateTool<BoneSpriteInfluenceTool>();
                tool.Initialize(layoutOverlay);
                tool.skeletonTool = skeletonTool;
                m_ToolMap.Add(Tools.SpriteInfluence, tool);
            }

            {
                WeightPainterToolWrapper tool = CreateTool<WeightPainterToolWrapper>();

                tool.weightPainterTool = weightPainterTool;
                tool.paintMode = WeightPainterMode.Slider;
                tool.title = TextContent.weightSlider;
                tool.Initialize(layoutOverlay);
                m_ToolMap.Add(Tools.WeightSlider, tool);
            }

            {
                WeightPainterToolWrapper tool = CreateTool<WeightPainterToolWrapper>();

                tool.weightPainterTool = weightPainterTool;
                tool.paintMode = WeightPainterMode.Brush;
                tool.title = TextContent.weightBrush;
                tool.Initialize(layoutOverlay);
                m_ToolMap.Add(Tools.WeightBrush, tool);
            }

            {
                GenerateWeightsTool tool = CreateTool<GenerateWeightsTool>();
                tool.Initialize(layoutOverlay);
                tool.meshTool = meshTool;
                tool.skeletonTool = skeletonTool;
                m_ToolMap.Add(Tools.GenerateWeights, tool);
            }
        }

        T CreateMeshTool<T>(SkeletonTool skeletonTool, MeshTool meshTool, SpriteMeshViewMode meshViewMode, SkeletonMode skeletonMode, LayoutOverlay layoutOverlay) where T : MeshToolWrapper
        {
            T tool = CreateTool<T>();
            tool.skeletonTool = skeletonTool;
            tool.meshTool = meshTool;
            tool.meshMode = meshViewMode;
            tool.skeletonMode = skeletonMode;
            tool.Initialize(layoutOverlay);
            return tool;
        }

        public void RestoreBindPose()
        {
            SpriteCache[] sprites = GetSprites();

            foreach (SpriteCache sprite in sprites)
                sprite.RestoreBindPose();

            if (character != null)
                character.skeleton.RestoreDefaultPose();
        }

        public void UndoRedoPerformed()
        {
            foreach (BaseTool tool in m_Tools)
            {
                if (tool == null)
                    continue;

                if (!tool.isActive)
                    tool.Deactivate();
            }

            foreach (BaseTool tool in m_Tools)
            {
                if (tool == null)
                    continue;

                if (tool.isActive)
                    tool.Activate();
            }
        }

        public BoneCache[] CreateBoneCacheFromSpriteBones(UnityEngine.U2D.SpriteBone[] spriteBones, float scale)
        {
            BoneCache[] bones = Array.ConvertAll(spriteBones, b => CreateCache<BoneCache>());

            for (int i = 0; i < spriteBones.Length; ++i)
            {
                UnityEngine.U2D.SpriteBone spriteBone = spriteBones[i];
                BoneCache bone = bones[i];

                if (spriteBone.parentId >= 0)
                    bone.SetParent(bones[spriteBone.parentId]);

                bone.name = spriteBone.name;
                bone.guid = spriteBone.guid?.Length == 0 ? GUID.Generate().ToString() : spriteBone.guid;
                bone.localLength = spriteBone.length * scale;
                bone.depth = spriteBone.position.z;
                bone.localPosition = (Vector2)spriteBone.position * scale;
                bone.localRotation = spriteBone.rotation;
                if (spriteBone.color.a == 0)
                    bone.bindPoseColor = ModuleUtility.CalculateNiceColor(i, 6);
                else
                    bone.bindPoseColor = spriteBone.color;
            }

            foreach (BoneCache bone in bones)
            {
                if (bone.parentBone != null && bone.parentBone.localLength > 0f && (bone.position - bone.parentBone.endPosition).sqrMagnitude < 0.005f)
                    bone.parentBone.chainedChild = bone;
            }

            return bones;
        }

        public bool IsOnVisualElement()
        {
            if (selectedTool == null || selectedTool.layoutOverlay == null)
                return false;

            LayoutOverlay overlay = selectedTool.layoutOverlay;
            Vector2 point = InternalEngineBridge.GUIUnclip(Event.current.mousePosition);
            point = overlay.parent.parent.LocalToWorld(point);

            VisualElement selectedElement = selectedTool.layoutOverlay.panel.Pick(point);
            return selectedElement != null
                && selectedElement.pickingMode != PickingMode.Ignore
                && selectedElement.FindCommonAncestor(overlay) == overlay;
        }

        void ToolChanged(ITool tool)
        {
            BaseTool visibilityTool = GetTool(Tools.Visibility);
            if ((ITool)visibilityTool == tool)
            {
                m_State.lastVisibilityToolActive = visibilityTool.isActive;
            }
        }
    }
}
