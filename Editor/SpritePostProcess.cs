using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.U2D;
using UnityEngine.U2D.Animation;

namespace UnityEditor.U2D.Animation
{
    internal class SpritePostProcess : AssetPostprocessor
    {
        void OnPreprocessAsset()
        {
            ISpriteEditorDataProvider dataProvider = GetSpriteEditorDataProvider(assetPath);
            if (dataProvider != null)
                InjectMainSkeletonBones(dataProvider);
        }

#pragma warning disable UNT0033 // suppress warning incorrect method case
        void OnPostprocessSprites(Texture2D texture, Sprite[] sprites)
#pragma warning restore UNT0033
        {
            ISpriteEditorDataProvider ai = GetSpriteEditorDataProvider(assetPath);
            if (ai != null)
            {
                // Injecting these bones a second time, because the Sprite Rect positions
                // might have updated between OnPreprocessAsset and OnPostprocessSprites.
                InjectMainSkeletonBones(ai);

                float definitionScale = CalculateDefinitionScale(texture, ai.GetDataProvider<ITextureDataProvider>());
                ai.InitSpriteEditorDataProvider();
                PostProcessBoneData(ai, definitionScale, sprites);
                PostProcessSpriteMeshData(ai, definitionScale, sprites, assetImporter);
                BoneGizmo.instance.ClearSpriteBoneCache();
            }

            // Get all SpriteSkin in scene and inform them to refresh their cache
            RefreshSpriteSkinCache();
        }

        static void InjectMainSkeletonBones(ISpriteEditorDataProvider dataProvider)
        {
            ICharacterDataProvider characterDataProvider = dataProvider.GetDataProvider<ICharacterDataProvider>();
            IMainSkeletonDataProvider mainSkeletonBonesDataProvider = dataProvider.GetDataProvider<IMainSkeletonDataProvider>();
            if (characterDataProvider == null || mainSkeletonBonesDataProvider == null)
                return;

            SkinningCache skinningCache = Cache.Create<SkinningCache>();
            skinningCache.Create(dataProvider, new SkinningCachePersistentStateTemp());

            SpriteBone[] skeletonBones = mainSkeletonBonesDataProvider.GetMainSkeletonData().bones ?? new SpriteBone[0];
            RemapCharacterPartsToNewBones(skinningCache, skeletonBones);

            SkinningModule.ApplyChanges(skinningCache, dataProvider);
        }

        static void RemapCharacterPartsToNewBones(SkinningCache skinningCache, SpriteBone[] newBones)
        {
            SkeletonCache skeleton = skinningCache.character.skeleton;
            BoneCache[] previousStateBones = skeleton.bones;
            BoneCache[] skeletonBones = skinningCache.CreateBoneCacheFromSpriteBones(newBones, 1.0f);
            skeleton.SetBones(skeletonBones);

            for (int i = 0; i < skinningCache.character.parts.Length; i++)
            {
                CharacterPartCache characterPart = skinningCache.character.parts[i];
                bool useGuids = !skeletonBones.All(newBone => previousStateBones.All(oldBone => newBone.guid != oldBone.guid));
                characterPart.bones = useGuids ? characterPart.bones.Select(partBone => Array.Find(skeletonBones, skeletonBone => partBone.guid == skeletonBone.guid)).ToArray() : characterPart.bones.Select(partBone => skeletonBones.ElementAtOrDefault(Array.FindIndex(previousStateBones, oldBone => partBone.guid == oldBone.guid))).ToArray();

                MeshCache mesh = skinningCache.GetMesh(characterPart.sprite);
                if (mesh != null)
                    mesh.SetCompatibleBoneSet(characterPart.bones);

                skinningCache.character.parts[i] = characterPart;
            }
        }

        static void RefreshSpriteSkinCache()
        {
            SpriteSkin[] spriteSkins = GameObject.FindObjectsByType<SpriteSkin>(FindObjectsSortMode.None);
            foreach (SpriteSkin ss in spriteSkins)
            {
                ss.ResetSprite();
            }
        }

        static void CalculateLocaltoWorldMatrix(int i, SpriteRect spriteRect, float definitionScale, float pixelsPerUnit, List<UnityEngine.U2D.SpriteBone> spriteBone, ref UnityEngine.U2D.SpriteBone?[] outpriteBone, ref NativeArray<Matrix4x4> bindPose)
        {
            if (outpriteBone[i] != null)
                return;
            UnityEngine.U2D.SpriteBone sp = spriteBone[i];
            bool isRoot = sp.parentId == -1;
            Vector3 position = isRoot ? (spriteBone[i].position - Vector3.Scale(spriteRect.rect.size, spriteRect.pivot)) : spriteBone[i].position;
            position.z = 0f;
            sp.position = position * definitionScale / pixelsPerUnit;
            sp.length = spriteBone[i].length * definitionScale / pixelsPerUnit;
            outpriteBone[i] = sp;

            // Calculate bind poses
            Vector3 worldPosition = Vector3.zero;
            Quaternion worldRotation = Quaternion.identity;

            if (sp.parentId == -1)
            {
                worldPosition = sp.position;
                worldRotation = sp.rotation;
            }
            else
            {
                if (outpriteBone[sp.parentId] == null)
                {
                    CalculateLocaltoWorldMatrix(sp.parentId, spriteRect, definitionScale, pixelsPerUnit, spriteBone, ref outpriteBone, ref bindPose);
                }

                Matrix4x4 parentBindPose = bindPose[sp.parentId];
                Matrix4x4 invParentBindPose = Matrix4x4.Inverse(parentBindPose);

                worldPosition = invParentBindPose.MultiplyPoint(sp.position);
                worldRotation = sp.rotation * invParentBindPose.rotation;
            }

            // Practically Matrix4x4.SetTRInverse
            Quaternion rot = Quaternion.Inverse(worldRotation);
            Matrix4x4 mat = Matrix4x4.identity;
            mat = Matrix4x4.Rotate(rot);
            mat = mat * Matrix4x4.Translate(-worldPosition);


            bindPose[i] = mat;
        }

        static bool PostProcessBoneData(ISpriteEditorDataProvider spriteDataProvider, float definitionScale, Sprite[] sprites)
        {
            ISpriteBoneDataProvider boneDataProvider = spriteDataProvider.GetDataProvider<ISpriteBoneDataProvider>();
            ITextureDataProvider textureDataProvider = spriteDataProvider.GetDataProvider<ITextureDataProvider>();

            if (sprites == null || sprites.Length == 0 || boneDataProvider == null || textureDataProvider == null)
                return false;

            bool dataChanged = false;
            SpriteRect[] spriteRects = spriteDataProvider.GetSpriteRects();
            foreach (Sprite sprite in sprites)
            {
                GUID guid = sprite.GetSpriteID();
                {
                    List<SpriteBone> spriteBone = boneDataProvider.GetBones(guid);
                    if (spriteBone == null)
                        continue;

                    int spriteBoneCount = spriteBone.Count;
                    if (spriteBoneCount == 0)
                        continue;

                    SpriteRect spriteRect = spriteRects.First(s => { return s.spriteID == guid; });

                    NativeArray<Matrix4x4> bindPose = new NativeArray<Matrix4x4>(spriteBoneCount, Allocator.Temp);
                    SpriteBone?[] outputSpriteBones = new UnityEngine.U2D.SpriteBone?[spriteBoneCount];
                    for (int i = 0; i < spriteBoneCount; ++i)
                    {
                        CalculateLocaltoWorldMatrix(i, spriteRect, definitionScale, sprite.pixelsPerUnit, spriteBone, ref outputSpriteBones, ref bindPose);
                    }

                    sprite.SetBindPoses(bindPose);
                    sprite.SetBones(outputSpriteBones.Select(x => x.Value).ToArray());
                    bindPose.Dispose();

                    dataChanged = true;
                }
            }

            return dataChanged;
        }

        static bool PostProcessSpriteMeshData(ISpriteEditorDataProvider spriteDataProvider, float definitionScale, Sprite[] sprites, AssetImporter assetImporter)
        {
            ISpriteMeshDataProvider spriteMeshDataProvider = spriteDataProvider.GetDataProvider<ISpriteMeshDataProvider>();
            ISpriteBoneDataProvider boneDataProvider = spriteDataProvider.GetDataProvider<ISpriteBoneDataProvider>();
            ITextureDataProvider textureDataProvider = spriteDataProvider.GetDataProvider<ITextureDataProvider>();
            ISpriteOutlineDataProvider outlineDataProvider = spriteDataProvider.GetDataProvider<ISpriteOutlineDataProvider>();
            if (sprites == null || sprites.Length == 0 || spriteMeshDataProvider == null || textureDataProvider == null)
                return false;

            bool dataChanged = false;
            SpriteRect[] spriteRects = spriteDataProvider.GetSpriteRects();
            bool showMeshOverwriteWarning = SkinningModuleSettings.showSpriteMeshOverwriteWarning;
            foreach (Sprite sprite in sprites)
            {
                GUID guid = sprite.GetSpriteID();
                Vertex2DMetaData[] vertices = spriteMeshDataProvider.GetVertices(guid);
                int[] indices = null;
                if (vertices.Length > 2)
                    indices = spriteMeshDataProvider.GetIndices(guid);

                List<SpriteBone> spriteBone = boneDataProvider.GetBones(guid);
                bool hasBones = spriteBone is { Count: > 0 };

                if (indices != null && indices.Length > 2 && vertices.Length > 2)
                {
                    SpriteRect spriteRect = spriteRects.First(s => { return s.spriteID == guid; });
                    bool hasInvalidWeights = false;

                    NativeArray<Vector3> vertexArray = new NativeArray<Vector3>(vertices.Length, Allocator.Temp);
                    NativeArray<BoneWeight> boneWeightArray = new NativeArray<BoneWeight>(vertices.Length, Allocator.Temp);

                    for (int i = 0; i < vertices.Length; ++i)
                    {
                        BoneWeight boneWeight = vertices[i].boneWeight;

                        vertexArray[i] = (Vector3)(vertices[i].position - Vector2.Scale(spriteRect.rect.size, spriteRect.pivot)) * definitionScale / sprite.pixelsPerUnit;
                        boneWeightArray[i] = boneWeight;

                        if (hasBones && !hasInvalidWeights)
                        {
                            float sum = boneWeight.weight0 + boneWeight.weight1 + boneWeight.weight2 + boneWeight.weight3;
                            hasInvalidWeights = sum < 0.999f;
                        }
                    }

                    NativeArray<ushort> indicesArray = new NativeArray<ushort>(indices.Length, Allocator.Temp);
                    for (int i = 0; i < indices.Length; ++i)
                        indicesArray[i] = (ushort)indices[i];

                    if (showMeshOverwriteWarning && outlineDataProvider?.GetOutlines(guid)?.Count > 0)
                        Debug.LogWarning(string.Format(TextContent.spriteMeshOverwriteWarning, sprite.name), assetImporter);
                    sprite.SetVertexCount(vertices.Length);
                    sprite.SetVertexAttribute<Vector3>(VertexAttribute.Position, vertexArray);
                    sprite.SetIndices(indicesArray);
                    if (hasBones)
                        sprite.SetVertexAttribute<BoneWeight>(VertexAttribute.BlendWeight, boneWeightArray);
                    vertexArray.Dispose();
                    boneWeightArray.Dispose();
                    indicesArray.Dispose();

                    // Deformed Sprites require proper Tangent Channels if Lit. Enable Tangent channels.
                    if (hasBones)
                    {
                        NativeArray<Vector4> tangentArray = new NativeArray<Vector4>(vertices.Length, Allocator.Temp);
                        for (int i = 0; i < vertices.Length; ++i)
                            tangentArray[i] = new Vector4(1.0f, 0.0f, 0, -1.0f);
                        sprite.SetVertexAttribute<Vector4>(VertexAttribute.Tangent, tangentArray);
                        tangentArray.Dispose();
                    }

                    dataChanged = true;

                    if (hasBones && hasInvalidWeights)
                        Debug.LogWarning(string.Format(TextContent.boneWeightsNotSumZeroWarning, spriteRect.name), assetImporter);
                }
                else
                {
                    if (hasBones)
                    {
                        NativeArray<BoneWeight> boneWeightArray = new NativeArray<BoneWeight>(sprite.GetVertexCount(), Allocator.Temp);
                        BoneWeight defaultBoneWeight = new BoneWeight() { weight0 = 1f };

                        for (int i = 0; i < boneWeightArray.Length; ++i)
                            boneWeightArray[i] = defaultBoneWeight;

                        sprite.SetVertexAttribute<BoneWeight>(VertexAttribute.BlendWeight, boneWeightArray);
                    }
                }
            }

            return dataChanged;
        }

        static float CalculateDefinitionScale(Texture2D texture, ITextureDataProvider dataProvider)
        {
            float definitionScale = 1;
            if (texture != null && dataProvider != null)
            {
                int actualWidth = 0, actualHeight = 0;
                dataProvider.GetTextureActualWidthAndHeight(out actualWidth, out actualHeight);
                float definitionScaleW = texture.width / (float)actualWidth;
                float definitionScaleH = texture.height / (float)actualHeight;
                definitionScale = Mathf.Min(definitionScaleW, definitionScaleH);
            }

            return definitionScale;
        }

        static ISpriteEditorDataProvider GetSpriteEditorDataProvider(string assetPath)
        {
            SpriteDataProviderFactories dataProviderFactories = new SpriteDataProviderFactories();
            dataProviderFactories.Init();
            return dataProviderFactories.GetSpriteEditorDataProviderFromObject(AssetImporter.GetAtPath(assetPath));
        }

        internal class SkinningCachePersistentStateTemp : ISkinningCachePersistentState
        {
            private string _lastSpriteId;
            private Tools _lastUsedTool;
            private List<int> _lastBoneSelectionIds = null;
            private Texture2D _lastTexture = null;
            private SerializableDictionary<int, BonePose> _lastPreviewPose = null;
            private SerializableDictionary<int, bool> _lastBoneVisibility = null;
            private SerializableDictionary<int, bool> _lastBoneExpansion = null;
            private SerializableDictionary<string, bool> _lastSpriteVisibility = null;
            private SerializableDictionary<int, bool> _lastGroupVisibility = null;
            private SkinningMode _lastMode;
            private bool _lastVisibilityToolActive;
            private int _lastVisibilityToolIndex;
            private IndexedSelection _lastVertexSelection = null;
            private float _lastBrushSize;
            private float _lastBrushHardness;
            private float _lastBrushStep;

            string ISkinningCachePersistentState.lastSpriteId
            {
                get => _lastSpriteId;
                set => _lastSpriteId = value;
            }

            Tools ISkinningCachePersistentState.lastUsedTool
            {
                get => _lastUsedTool;
                set => _lastUsedTool = value;
            }

            List<int> ISkinningCachePersistentState.lastBoneSelectionIds => _lastBoneSelectionIds;

            Texture2D ISkinningCachePersistentState.lastTexture
            {
                get => _lastTexture;
                set => _lastTexture = value;
            }

            SerializableDictionary<int, BonePose> ISkinningCachePersistentState.lastPreviewPose => _lastPreviewPose;

            SerializableDictionary<int, bool> ISkinningCachePersistentState.lastBoneVisibility => _lastBoneVisibility;

            SerializableDictionary<int, bool> ISkinningCachePersistentState.lastBoneExpansion => _lastBoneExpansion;

            SerializableDictionary<string, bool> ISkinningCachePersistentState.lastSpriteVisibility => _lastSpriteVisibility;

            SerializableDictionary<int, bool> ISkinningCachePersistentState.lastGroupVisibility => _lastGroupVisibility;

            SkinningMode ISkinningCachePersistentState.lastMode
            {
                get => _lastMode;
                set => _lastMode = value;
            }

            bool ISkinningCachePersistentState.lastVisibilityToolActive
            {
                get => _lastVisibilityToolActive;
                set => _lastVisibilityToolActive = value;
            }

            int ISkinningCachePersistentState.lastVisibilityToolIndex
            {
                get => _lastVisibilityToolIndex;
                set => _lastVisibilityToolIndex = value;
            }

            IndexedSelection ISkinningCachePersistentState.lastVertexSelection => _lastVertexSelection;

            float ISkinningCachePersistentState.lastBrushSize
            {
                get => _lastBrushSize;
                set => _lastBrushSize = value;
            }

            float ISkinningCachePersistentState.lastBrushHardness
            {
                get => _lastBrushHardness;
                set => _lastBrushHardness = value;
            }

            float ISkinningCachePersistentState.lastBrushStep
            {
                get => _lastBrushStep;
                set => _lastBrushStep = value;
            }
        }
    }
}
