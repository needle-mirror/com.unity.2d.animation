using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace UnityEditor.Experimental.U2D.Animation.Test.SkinningModuleTests
{
    [TestFixture]
    public class CopyCharacterTest : SkinningModuleFullFakeCharacterTestBase
    {
        private CopyTool m_CopyTool;
        private ICopyToolStringStore m_CopyStringStore;

        private string kDefaultSpriteCopyString =
            @"{""pixelsPerUnit"":100.0,""copyData"":[{""spriteName"":"""",""spriteBones"":[{""m_Name"":""Bone 1"",""m_Position"":{""x"":15.0,""y"":0.0,""z"":0.0},""m_Rotation"":{""x"":0.0,""y"":0.0,""z"":0.0,""w"":1.0},""m_Length"":10.0,""m_ParentId"":-1},{""m_Name"":""Bone 2"",""m_Position"":{""x"":0.0,""y"":0.0,""z"":0.0},""m_Rotation"":{""x"":0.0,""y"":0.0,""z"":0.38268348574638369,""w"":-0.9238795042037964},""m_Length"":30.0,""m_ParentId"":0},{""m_Name"":""Bone 3"",""m_Position"":{""x"":0.0,""y"":0.0,""z"":0.0},""m_Rotation"":{""x"":0.0,""y"":0.0,""z"":0.38268348574638369,""w"":0.9238795638084412},""m_Length"":20.0,""m_ParentId"":1}],""vertices"":[{""m_Position"":{""x"":0.0,""y"":0.0},""m_EditableBoneWeight"":{""m_Channels"":[{""m_Enabled"":true,""m_BoneIndex"":0,""m_Weight"":1.0},{""m_Enabled"":false,""m_BoneIndex"":0,""m_Weight"":0.0},{""m_Enabled"":false,""m_BoneIndex"":0,""m_Weight"":0.0},{""m_Enabled"":false,""m_BoneIndex"":0,""m_Weight"":0.0}]}},{""m_Position"":{""x"":0.0,""y"":100.0},""m_EditableBoneWeight"":{""m_Channels"":[{""m_Enabled"":true,""m_BoneIndex"":0,""m_Weight"":0.25},{""m_Enabled"":true,""m_BoneIndex"":1,""m_Weight"":0.75},{""m_Enabled"":false,""m_BoneIndex"":0,""m_Weight"":0.0},{""m_Enabled"":false,""m_BoneIndex"":0,""m_Weight"":0.0}]}},{""m_Position"":{""x"":100.0,""y"":100.0},""m_EditableBoneWeight"":{""m_Channels"":[{""m_Enabled"":true,""m_BoneIndex"":1,""m_Weight"":1.0},{""m_Enabled"":false,""m_BoneIndex"":0,""m_Weight"":0.0},{""m_Enabled"":false,""m_BoneIndex"":0,""m_Weight"":0.0},{""m_Enabled"":false,""m_BoneIndex"":0,""m_Weight"":0.0}]}},{""m_Position"":{""x"":100.0,""y"":0.0},""m_EditableBoneWeight"":{""m_Channels"":[{""m_Enabled"":true,""m_BoneIndex"":1,""m_Weight"":0.3999999761581421},{""m_Enabled"":true,""m_BoneIndex"":0,""m_Weight"":0.6000000238418579},{""m_Enabled"":false,""m_BoneIndex"":0,""m_Weight"":0.0},{""m_Enabled"":false,""m_BoneIndex"":0,""m_Weight"":0.0}]}}],""indices"":[0,1,2,0,2,3],""edges"":[{""m_Index1"":0,""m_Index2"":1},{""m_Index1"":1,""m_Index2"":2},{""m_Index1"":2,""m_Index2"":3},{""m_Index1"":3,""m_Index2"":0}],""boneWeightNames"":[""Bone 1"",""Bone 2""]}]}";
        private string kAllSpriteCopyString =
            @"{""pixelsPerUnit"":100.0,""copyData"":[{""spriteName"":""Sprite1"",""spriteBones"":[{""m_Name"":""Bone 1"",""m_Position"":{""x"":15.0,""y"":0.0,""z"":0.0},""m_Rotation"":{""x"":0.0,""y"":0.0,""z"":0.0,""w"":1.0},""m_Length"":10.0,""m_ParentId"":-1},{""m_Name"":""Bone 2"",""m_Position"":{""x"":0.0,""y"":0.0,""z"":0.0},""m_Rotation"":{""x"":0.0,""y"":0.0,""z"":0.38268348574638369,""w"":-0.9238795042037964},""m_Length"":30.0,""m_ParentId"":0},{""m_Name"":""Bone 3"",""m_Position"":{""x"":0.0,""y"":0.0,""z"":0.0},""m_Rotation"":{""x"":0.0,""y"":0.0,""z"":0.38268348574638369,""w"":0.9238795638084412},""m_Length"":20.0,""m_ParentId"":1}],""vertices"":[{""m_Position"":{""x"":0.0,""y"":0.0},""m_EditableBoneWeight"":{""m_Channels"":[{""m_Enabled"":true,""m_BoneIndex"":0,""m_Weight"":1.0},{""m_Enabled"":false,""m_BoneIndex"":0,""m_Weight"":0.0},{""m_Enabled"":false,""m_BoneIndex"":0,""m_Weight"":0.0},{""m_Enabled"":false,""m_BoneIndex"":0,""m_Weight"":0.0}]}},{""m_Position"":{""x"":0.0,""y"":100.0},""m_EditableBoneWeight"":{""m_Channels"":[{""m_Enabled"":true,""m_BoneIndex"":0,""m_Weight"":0.25},{""m_Enabled"":true,""m_BoneIndex"":1,""m_Weight"":0.75},{""m_Enabled"":false,""m_BoneIndex"":0,""m_Weight"":0.0},{""m_Enabled"":false,""m_BoneIndex"":0,""m_Weight"":0.0}]}},{""m_Position"":{""x"":100.0,""y"":100.0},""m_EditableBoneWeight"":{""m_Channels"":[{""m_Enabled"":true,""m_BoneIndex"":1,""m_Weight"":1.0},{""m_Enabled"":false,""m_BoneIndex"":0,""m_Weight"":0.0},{""m_Enabled"":false,""m_BoneIndex"":0,""m_Weight"":0.0},{""m_Enabled"":false,""m_BoneIndex"":0,""m_Weight"":0.0}]}},{""m_Position"":{""x"":100.0,""y"":0.0},""m_EditableBoneWeight"":{""m_Channels"":[{""m_Enabled"":true,""m_BoneIndex"":1,""m_Weight"":0.3999999761581421},{""m_Enabled"":true,""m_BoneIndex"":0,""m_Weight"":0.6000000238418579},{""m_Enabled"":false,""m_BoneIndex"":0,""m_Weight"":0.0},{""m_Enabled"":false,""m_BoneIndex"":0,""m_Weight"":0.0}]}}],""indices"":[0,1,2,0,2,3],""edges"":[{""m_Index1"":0,""m_Index2"":1},{""m_Index1"":1,""m_Index2"":2},{""m_Index1"":2,""m_Index2"":3},{""m_Index1"":3,""m_Index2"":0}],""boneWeightNames"":[""Bone 1"",""Bone 2""]},{""spriteName"":""Sprite2"",""spriteBones"":[{""m_Name"":""Bone 1"",""m_Position"":{""x"":15.0,""y"":0.0,""z"":0.0},""m_Rotation"":{""x"":0.0,""y"":0.0,""z"":0.0,""w"":1.0},""m_Length"":10.0,""m_ParentId"":-1},{""m_Name"":""Bone 2"",""m_Position"":{""x"":0.0,""y"":0.0,""z"":0.0},""m_Rotation"":{""x"":0.0,""y"":0.0,""z"":0.38268348574638369,""w"":-0.9238795042037964},""m_Length"":30.0,""m_ParentId"":0},{""m_Name"":""Bone 3"",""m_Position"":{""x"":0.0,""y"":0.0,""z"":0.0},""m_Rotation"":{""x"":0.0,""y"":0.0,""z"":0.38268348574638369,""w"":0.9238795638084412},""m_Length"":20.0,""m_ParentId"":1}],""vertices"":[{""m_Position"":{""x"":100.0,""y"":0.0},""m_EditableBoneWeight"":{""m_Channels"":[{""m_Enabled"":true,""m_BoneIndex"":0,""m_Weight"":1.0},{""m_Enabled"":false,""m_BoneIndex"":0,""m_Weight"":0.0},{""m_Enabled"":false,""m_BoneIndex"":0,""m_Weight"":0.0},{""m_Enabled"":false,""m_BoneIndex"":0,""m_Weight"":0.0}]}},{""m_Position"":{""x"":100.0,""y"":100.0},""m_EditableBoneWeight"":{""m_Channels"":[{""m_Enabled"":true,""m_BoneIndex"":1,""m_Weight"":0.5},{""m_Enabled"":false,""m_BoneIndex"":0,""m_Weight"":0.0},{""m_Enabled"":false,""m_BoneIndex"":0,""m_Weight"":0.0},{""m_Enabled"":false,""m_BoneIndex"":0,""m_Weight"":0.0}]}},{""m_Position"":{""x"":190.0,""y"":50.0},""m_EditableBoneWeight"":{""m_Channels"":[{""m_Enabled"":true,""m_BoneIndex"":0,""m_Weight"":0.5},{""m_Enabled"":true,""m_BoneIndex"":1,""m_Weight"":0.5},{""m_Enabled"":false,""m_BoneIndex"":0,""m_Weight"":0.0},{""m_Enabled"":false,""m_BoneIndex"":0,""m_Weight"":0.0}]}}],""indices"":[0,1,2],""edges"":[{""m_Index1"":0,""m_Index2"":1},{""m_Index1"":1,""m_Index2"":2},{""m_Index1"":2,""m_Index2"":0}],""boneWeightNames"":[""Bone 2"",""Bone 3""]},{""spriteName"":""Sprite3"",""spriteBones"":[{""m_Name"":""Bone 1"",""m_Position"":{""x"":15.0,""y"":0.0,""z"":0.0},""m_Rotation"":{""x"":0.0,""y"":0.0,""z"":0.0,""w"":1.0},""m_Length"":10.0,""m_ParentId"":-1},{""m_Name"":""Bone 2"",""m_Position"":{""x"":0.0,""y"":0.0,""z"":0.0},""m_Rotation"":{""x"":0.0,""y"":0.0,""z"":0.38268348574638369,""w"":-0.9238795042037964},""m_Length"":30.0,""m_ParentId"":0},{""m_Name"":""Bone 3"",""m_Position"":{""x"":0.0,""y"":0.0,""z"":0.0},""m_Rotation"":{""x"":0.0,""y"":0.0,""z"":0.38268348574638369,""w"":0.9238795638084412},""m_Length"":20.0,""m_ParentId"":1}],""vertices"":[],""indices"":[],""edges"":[],""boneWeightNames"":[]},{""spriteName"":""Sprite4"",""spriteBones"":[{""m_Name"":""Bone 1"",""m_Position"":{""x"":15.0,""y"":0.0,""z"":0.0},""m_Rotation"":{""x"":0.0,""y"":0.0,""z"":0.0,""w"":1.0},""m_Length"":10.0,""m_ParentId"":-1},{""m_Name"":""Bone 2"",""m_Position"":{""x"":0.0,""y"":0.0,""z"":0.0},""m_Rotation"":{""x"":0.0,""y"":0.0,""z"":0.38268348574638369,""w"":-0.9238795042037964},""m_Length"":30.0,""m_ParentId"":0},{""m_Name"":""Bone 3"",""m_Position"":{""x"":0.0,""y"":0.0,""z"":0.0},""m_Rotation"":{""x"":0.0,""y"":0.0,""z"":0.38268348574638369,""w"":0.9238795638084412},""m_Length"":20.0,""m_ParentId"":1}],""vertices"":[],""indices"":[],""edges"":[],""boneWeightNames"":[]}]}";

        public override void DoOtherSetup()
        {
            var sprite = skinningCache.GetSprites()[0];
            skinningCache.events.selectedSpriteChanged.Invoke(sprite);
            skinningCache.events.boneSelectionChanged.Invoke();

            m_CopyTool = skinningCache.GetTool(Tools.CopyPaste) as CopyTool;
            m_CopyStringStore = new StringCopyToolStringStore();
            m_CopyStringStore.stringStore = "";
            m_CopyTool.copyToolStringStore = m_CopyStringStore;
        }

        public override void DoOtherTeardown()
        {
            m_CopyStringStore.stringStore = "";
        }
        
        [Test]
        public void SelectedSprite_DoCopy_CopiesToSystemCopyBuffer()
        {
            m_CopyTool.OnCopyActivated();
            Assert.IsFalse(String.IsNullOrEmpty(m_CopyStringStore.stringStore));
            Assert.AreEqual(kDefaultSpriteCopyString, m_CopyStringStore.stringStore);
        }

        [Test]
        public void NoSelectedSprite_DoCopy_CopiesAllToSystemCopyBuffer()
        {
            skinningCache.events.selectedSpriteChanged.Invoke(null);

            m_CopyTool.OnCopyActivated();
            Assert.IsFalse(String.IsNullOrEmpty(m_CopyStringStore.stringStore));
            Assert.AreEqual(kAllSpriteCopyString, m_CopyStringStore.stringStore);
        }

        [Test]
        public void SelectedSprite_DoCopyAndPasteBones_PastesToSprite()
        {
            m_CopyTool.OnCopyActivated();

            var sprite = skinningCache.GetSprites()[2];
            skinningCache.events.selectedSpriteChanged.Invoke(sprite);

            m_CopyTool.OnPasteActivated(true, false, false, false);

            var skeleton = skinningCache.character.skeleton;
            Assert.AreEqual(6, skeleton.BoneCount);
            Assert.AreEqual("Bone 1", skeleton.bones[0].name);
            Assert.AreEqual(0, (new Vector3(15f, 0f, 0f) - skeleton.bones[0].position).magnitude, 0.0001f);
            Assert.AreEqual(0, (new Vector3(0f, 0f, 0f) - skeleton.bones[0].rotation.eulerAngles).magnitude, 0.0001f);
            Assert.AreEqual(10.0f, skeleton.bones[0].length);
            Assert.AreEqual("Bone 2", skeleton.bones[1].name);
            Assert.AreEqual(0, (new Vector3(0f, 0f, 0f) - skeleton.bones[1].localPosition).magnitude, 0.0001f);
            Assert.AreEqual(0, (new Vector3(0f, 0f, 315f) - skeleton.bones[1].localRotation.eulerAngles).magnitude, 0.0001f);
            Assert.AreEqual(30.0f, skeleton.bones[1].length);
            Assert.AreEqual("Bone 3", skeleton.bones[2].name);
            Assert.AreEqual(0, (new Vector3(0f, 0f, 0f) - skeleton.bones[2].localPosition).magnitude, 0.0001f);
            Assert.AreEqual(0, (new Vector3(0f, 0f, 45f) - skeleton.bones[2].localRotation.eulerAngles).magnitude, 0.0001f);
            Assert.AreEqual(20.0f, skeleton.bones[2].length);
            Assert.AreEqual("bone_1", skeleton.bones[3].name);
            Assert.AreEqual(0, (new Vector3(15f, 100f, 0f) - skeleton.bones[3].position).magnitude, 0.0001f);
            Assert.AreEqual(0, (new Vector3(0f, 0f, 0f) - skeleton.bones[3].rotation.eulerAngles).magnitude, 0.0001f);
            Assert.AreEqual(10.0f, skeleton.bones[0].length);
            Assert.AreEqual("bone_2", skeleton.bones[4].name);
            Assert.AreEqual(0, (new Vector3(0f, 0f, 0f) - skeleton.bones[4].localPosition).magnitude, 0.0001f);
            Assert.AreEqual(0, (new Vector3(0f, 0f, 315f) - skeleton.bones[4].localRotation.eulerAngles).magnitude, 0.0001f);
            Assert.AreEqual(30.0f, skeleton.bones[4].length);
            Assert.AreEqual("bone_3", skeleton.bones[5].name);
            Assert.AreEqual(0, (new Vector3(0f, 0f, 0f) - skeleton.bones[5].localPosition).magnitude, 0.0001f);
            Assert.AreEqual(0, (new Vector3(0f, 0f, 45f) - skeleton.bones[5].localRotation.eulerAngles).magnitude, 0.0001f);
            Assert.AreEqual(20.0f, skeleton.bones[5].length);

            var characterPartBones = skinningCache.GetCharacterPart(sprite).bones;
            Assert.AreEqual(0, characterPartBones.Length);

            var meshPasteCache = sprite.GetMesh();
            Assert.AreEqual(0, meshPasteCache.vertexCount);
        }

        [Test]
        public void SelectedSprite_DoCopyAndPasteMesh_PastesToSprite()
        {
            m_CopyTool.OnCopyActivated();

            var sprite = skinningCache.GetSprites()[2];
            skinningCache.events.selectedSpriteChanged.Invoke(sprite);

            m_CopyTool.OnPasteActivated(false, true, false, false);

            var skeleton = skinningCache.character.skeleton;
            Assert.AreEqual(3, skeleton.BoneCount);
            Assert.AreEqual("Bone 1", skeleton.bones[0].name);
            Assert.AreEqual(0, (new Vector3(15f, 0f, 0f) - skeleton.bones[0].position).magnitude, 0.0001f);
            Assert.AreEqual(0, (new Vector3(0f, 0f, 0f) - skeleton.bones[0].rotation.eulerAngles).magnitude, 0.0001f);
            Assert.AreEqual(10.0f, skeleton.bones[0].length);
            Assert.AreEqual("Bone 2", skeleton.bones[1].name);
            Assert.AreEqual(0, (new Vector3(0f, 0f, 0f) - skeleton.bones[1].localPosition).magnitude, 0.0001f);
            Assert.AreEqual(0, (new Vector3(0f, 0f, 315f) - skeleton.bones[1].localRotation.eulerAngles).magnitude, 0.0001f);
            Assert.AreEqual(30.0f, skeleton.bones[1].length);
            Assert.AreEqual("Bone 3", skeleton.bones[2].name);
            Assert.AreEqual(0, (new Vector3(0f, 0f, 0f) - skeleton.bones[2].localPosition).magnitude, 0.0001f);
            Assert.AreEqual(0, (new Vector3(0f, 0f, 45f) - skeleton.bones[2].localRotation.eulerAngles).magnitude, 0.0001f);
            Assert.AreEqual(20.0f, skeleton.bones[2].length);

            var characterPartBones = skinningCache.GetCharacterPart(sprite).bones;
            Assert.AreEqual(2, characterPartBones.Length);
            Assert.IsTrue(characterPartBones.Contains(skeleton.bones[0]));
            Assert.IsTrue(characterPartBones.Contains(skeleton.bones[1]));
            Assert.IsFalse(characterPartBones.Contains(skeleton.bones[2]));

            var meshPasteCache = sprite.GetMesh();
            Assert.AreEqual(4, meshPasteCache.vertexCount);
            Assert.AreEqual(0, (new Vector2(0, 0) - meshPasteCache.vertices[0].position).magnitude, 0.0001f);
            Assert.IsTrue(meshPasteCache.vertices[0].editableBoneWeight.ContainsBoneIndex(0));
            Assert.IsFalse(meshPasteCache.vertices[0].editableBoneWeight.ContainsBoneIndex(1));
            Assert.AreEqual(1.0f, meshPasteCache.vertices[0].editableBoneWeight[meshPasteCache.vertices[0].editableBoneWeight.GetChannelFromBoneIndex(0)].weight);
            Assert.AreEqual(0, (new Vector2(0, 100f) - meshPasteCache.vertices[1].position).magnitude, 0.0001f);
            Assert.IsTrue(meshPasteCache.vertices[1].editableBoneWeight.ContainsBoneIndex(0));
            Assert.IsTrue(meshPasteCache.vertices[1].editableBoneWeight.ContainsBoneIndex(1));
            Assert.AreEqual(0.25f, meshPasteCache.vertices[1].editableBoneWeight[meshPasteCache.vertices[1].editableBoneWeight.GetChannelFromBoneIndex(0)].weight);
            Assert.AreEqual(0.75f, meshPasteCache.vertices[1].editableBoneWeight[meshPasteCache.vertices[1].editableBoneWeight.GetChannelFromBoneIndex(1)].weight);
            Assert.AreEqual(0, (new Vector2(100f, 100f) - meshPasteCache.vertices[2].position).magnitude, 0.0001f);
            Assert.IsFalse(meshPasteCache.vertices[2].editableBoneWeight.ContainsBoneIndex(0));
            Assert.IsTrue(meshPasteCache.vertices[2].editableBoneWeight.ContainsBoneIndex(1));
            Assert.AreEqual(1.0f, meshPasteCache.vertices[2].editableBoneWeight[meshPasteCache.vertices[2].editableBoneWeight.GetChannelFromBoneIndex(1)].weight);
            Assert.AreEqual(0, (new Vector2(100f, 0) - meshPasteCache.vertices[3].position).magnitude, 0.0001f);
            Assert.IsTrue(meshPasteCache.vertices[3].editableBoneWeight.ContainsBoneIndex(0));
            Assert.IsTrue(meshPasteCache.vertices[3].editableBoneWeight.ContainsBoneIndex(1));
            Assert.AreEqual(0.4f, meshPasteCache.vertices[3].editableBoneWeight[meshPasteCache.vertices[3].editableBoneWeight.GetChannelFromBoneIndex(1)].weight, 0.0001f);
            Assert.AreEqual(0.6f, meshPasteCache.vertices[3].editableBoneWeight[meshPasteCache.vertices[3].editableBoneWeight.GetChannelFromBoneIndex(0)].weight, 0.0001f);
            Assert.AreEqual(6, meshPasteCache.indices.Count);
            Assert.AreEqual(4, meshPasteCache.edges.Count);
        }

        [Test]
        public void SelectedSprite_DoCopyAndPasteBonesAndMesh_PastesToSprite()
        {
            m_CopyTool.OnCopyActivated();

            var sprite = skinningCache.GetSprites()[2];
            skinningCache.events.selectedSpriteChanged.Invoke(sprite);

            m_CopyTool.OnPasteActivated(true, true, false, false);

            var skeleton = skinningCache.character.skeleton;
            Assert.AreEqual(6, skeleton.BoneCount);
            Assert.AreEqual("Bone 1", skeleton.bones[0].name);
            Assert.AreEqual(0, (new Vector3(15f, 0f, 0f) - skeleton.bones[0].position).magnitude, 0.0001f);
            Assert.AreEqual(0, (new Vector3(0f, 0f, 0f) - skeleton.bones[0].rotation.eulerAngles).magnitude, 0.0001f);
            Assert.AreEqual(10.0f, skeleton.bones[0].length);
            Assert.AreEqual("Bone 2", skeleton.bones[1].name);
            Assert.AreEqual(0, (new Vector3(0f, 0f, 0f) - skeleton.bones[1].localPosition).magnitude, 0.0001f);
            Assert.AreEqual(0, (new Vector3(0f, 0f, 315f) - skeleton.bones[1].localRotation.eulerAngles).magnitude, 0.0001f);
            Assert.AreEqual(30.0f, skeleton.bones[1].length);
            Assert.AreEqual("Bone 3", skeleton.bones[2].name);
            Assert.AreEqual(0, (new Vector3(0f, 0f, 0f) - skeleton.bones[2].localPosition).magnitude, 0.0001f);
            Assert.AreEqual(0, (new Vector3(0f, 0f, 45f) - skeleton.bones[2].localRotation.eulerAngles).magnitude, 0.0001f);
            Assert.AreEqual(20.0f, skeleton.bones[2].length);
            Assert.AreEqual("bone_1", skeleton.bones[3].name);
            Assert.AreEqual(0, (new Vector3(15f, 100f, 0f) - skeleton.bones[3].position).magnitude, 0.0001f);
            Assert.AreEqual(0, (new Vector3(0f, 0f, 0f) - skeleton.bones[3].rotation.eulerAngles).magnitude, 0.0001f);
            Assert.AreEqual(10.0f, skeleton.bones[0].length);
            Assert.AreEqual("bone_2", skeleton.bones[4].name);
            Assert.AreEqual(0, (new Vector3(0f, 0f, 0f) - skeleton.bones[4].localPosition).magnitude, 0.0001f);
            Assert.AreEqual(0, (new Vector3(0f, 0f, 315f) - skeleton.bones[4].localRotation.eulerAngles).magnitude, 0.0001f);
            Assert.AreEqual(30.0f, skeleton.bones[4].length);
            Assert.AreEqual("bone_3", skeleton.bones[5].name);
            Assert.AreEqual(0, (new Vector3(0f, 0f, 0f) - skeleton.bones[5].localPosition).magnitude, 0.0001f);
            Assert.AreEqual(0, (new Vector3(0f, 0f, 45f) - skeleton.bones[5].localRotation.eulerAngles).magnitude, 0.0001f);
            Assert.AreEqual(20.0f, skeleton.bones[5].length);

            var characterPartBones = skinningCache.GetCharacterPart(sprite).bones;
            Assert.AreEqual(2, characterPartBones.Length);
            Assert.IsFalse(characterPartBones.Contains(skeleton.bones[0]));
            Assert.IsFalse(characterPartBones.Contains(skeleton.bones[1]));
            Assert.IsFalse(characterPartBones.Contains(skeleton.bones[2]));
            Assert.IsTrue(characterPartBones.Contains(skeleton.bones[3]));
            Assert.IsTrue(characterPartBones.Contains(skeleton.bones[4]));
            Assert.IsFalse(characterPartBones.Contains(skeleton.bones[5]));

            var meshPasteCache = sprite.GetMesh();
            Assert.AreEqual(4, meshPasteCache.vertexCount);
            Assert.AreEqual(0, (new Vector2(0, 0) - meshPasteCache.vertices[0].position).magnitude, 0.0001f);
            Assert.IsTrue(meshPasteCache.vertices[0].editableBoneWeight.ContainsBoneIndex(0));
            Assert.IsFalse(meshPasteCache.vertices[0].editableBoneWeight.ContainsBoneIndex(1));
            Assert.AreEqual(1.0f, meshPasteCache.vertices[0].editableBoneWeight[meshPasteCache.vertices[0].editableBoneWeight.GetChannelFromBoneIndex(0)].weight);
            Assert.AreEqual(0, (new Vector2(0, 100f) - meshPasteCache.vertices[1].position).magnitude, 0.0001f);
            Assert.IsTrue(meshPasteCache.vertices[1].editableBoneWeight.ContainsBoneIndex(0));
            Assert.IsTrue(meshPasteCache.vertices[1].editableBoneWeight.ContainsBoneIndex(1));
            Assert.AreEqual(0.25f, meshPasteCache.vertices[1].editableBoneWeight[meshPasteCache.vertices[1].editableBoneWeight.GetChannelFromBoneIndex(0)].weight);
            Assert.AreEqual(0.75f, meshPasteCache.vertices[1].editableBoneWeight[meshPasteCache.vertices[1].editableBoneWeight.GetChannelFromBoneIndex(1)].weight);
            Assert.AreEqual(0, (new Vector2(100f, 100f) - meshPasteCache.vertices[2].position).magnitude, 0.0001f);
            Assert.IsFalse(meshPasteCache.vertices[2].editableBoneWeight.ContainsBoneIndex(0));
            Assert.IsTrue(meshPasteCache.vertices[2].editableBoneWeight.ContainsBoneIndex(1));
            Assert.AreEqual(1.0f, meshPasteCache.vertices[2].editableBoneWeight[meshPasteCache.vertices[2].editableBoneWeight.GetChannelFromBoneIndex(1)].weight);
            Assert.AreEqual(0, (new Vector2(100f, 0) - meshPasteCache.vertices[3].position).magnitude, 0.0001f);
            Assert.IsTrue(meshPasteCache.vertices[3].editableBoneWeight.ContainsBoneIndex(0));
            Assert.IsTrue(meshPasteCache.vertices[3].editableBoneWeight.ContainsBoneIndex(1));
            Assert.AreEqual(0.4f, meshPasteCache.vertices[3].editableBoneWeight[meshPasteCache.vertices[3].editableBoneWeight.GetChannelFromBoneIndex(1)].weight, 0.0001f);
            Assert.AreEqual(0.6f, meshPasteCache.vertices[3].editableBoneWeight[meshPasteCache.vertices[3].editableBoneWeight.GetChannelFromBoneIndex(0)].weight, 0.0001f);
            Assert.AreEqual(6, meshPasteCache.indices.Count);
            Assert.AreEqual(4, meshPasteCache.edges.Count);
        }
    }
}
