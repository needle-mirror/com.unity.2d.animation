using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace UnityEditor.U2D.Animation
{
    [Serializable]
    internal class SkeletonController
    {
        static readonly string k_DefaultRootName = "root";
        static readonly string k_DefaultBoneName = "bone";
        static Regex s_Regex = new Regex(@"\w+_\d+$", RegexOptions.IgnoreCase);

        [SerializeField]
        Vector3 m_CreateBoneStartPosition;
        [SerializeField]
        BoneCache m_PrevCreatedBone;

        SkeletonCache m_Skeleton;
        bool m_Moved = false;

        ISkeletonStyle style
        {
            get
            {
                if (styleOverride != null)
                    return styleOverride;

                return SkeletonStyles.Default;
            }
        }

        SkinningCache skinningCache => m_Skeleton.skinningCache;

        BoneCache selectedBone
        {
            get => selection.activeElement.ToSpriteSheetIfNeeded();
            set => selection.activeElement = value.ToCharacterIfNeeded();
        }

        BoneCache[] selectedBones
        {
            get => selection.elements.ToSpriteSheetIfNeeded();
            set => selection.elements = value.ToCharacterIfNeeded();
        }

        BoneCache rootBone => selection.root.ToSpriteSheetIfNeeded();
        BoneCache[] rootBones => selection.roots.ToSpriteSheetIfNeeded();

        public ISkeletonView view { get; set; }
        public ISkeletonStyle styleOverride { get; set; }
        public IBoneSelection selection { get; set; }
        public bool editBindPose { get; set; }

        public SkeletonCache skeleton
        {
            get => m_Skeleton;
            set => SetSkeleton(value);
        }

        public BoneCache hoveredBone => GetBone(view.hoveredBoneID);
        public BoneCache hoveredTail => GetBone(view.hoveredTailID);
        public BoneCache hoveredBody => GetBone(view.hoveredBodyID);
        public BoneCache hoveredJoint => GetBone(view.hoveredJointID);
        public BoneCache hotBone => GetBone(view.hotBoneID);

        BoneCache GetBone(int instanceID)
        {
            return BaseObject.InstanceIDToObject(instanceID) as BoneCache;
        }

        void SetSkeleton(SkeletonCache newSkeleton)
        {
            if (skeleton != newSkeleton)
            {
                m_Skeleton = newSkeleton;
                Reset();
            }
        }

        public void Reset()
        {
            view.DoCancelMultistepAction(true);
        }

        public void OnGUI()
        {
            if (skeleton == null)
                return;

            view.BeginLayout();

            if (view.CanLayout())
                LayoutBones();

            view.EndLayout();

            HandleSelectBone();
            HandleRotateBone();
            HandleMoveBone();
            HandleFreeMoveBone();
            HandleMoveJoint();
            HandleMoveEndPosition();
            HandleChangeLength();
            HandleCreateBone();
            HandleSplitBone();
            HandleRemoveBone();
            HandleCancelMultiStepAction();
            DrawSkeleton();
            DrawSplitBonePreview();
            DrawCreateBonePreview();
            DrawCursors();

            BatchedDrawing.Draw();
        }

        void LayoutBones()
        {
            for (int i = 0; i < skeleton.boneCount; ++i)
            {
                BoneCache bone = skeleton.GetBone(i);

                if (bone.isVisible && bone != hotBone)
                    view.LayoutBone(bone.GetInstanceID(), bone.position, bone.endPosition, bone.forward, bone.up, bone.right, bone.chainedChild == null);
            }
        }

        void HandleSelectBone()
        {
            if (view.DoSelectBone(out int instanceID, out bool additive))
            {
                BoneCache bone = GetBone(instanceID).ToCharacterIfNeeded();

                using (skinningCache.UndoScope(TextContent.boneSelection, true))
                {
                    if (!additive)
                    {
                        if (!selection.Contains(bone))
                            selectedBone = bone;
                    }
                    else
                        selection.Select(bone, !selection.Contains(bone));

                    skinningCache.events.boneSelectionChanged.Invoke();
                }
            }
        }

        void HandleRotateBone()
        {
            if (view.IsActionTriggering(SkeletonAction.RotateBone))
                m_Moved = false;

            BoneCache pivot = hoveredBone;

            if (view.IsActionHot(SkeletonAction.RotateBone))
                pivot = hotBone;

            if (pivot == null)
                return;

            BoneCache[] selectedRootBones = selection.roots.ToSpriteSheetIfNeeded();
            pivot = pivot.FindRoot<BoneCache>(selectedRootBones);

            if (pivot == null)
                return;

            if (view.DoRotateBone(pivot.position, pivot.forward, out float deltaAngle))
            {
                if (!m_Moved)
                {
                    skinningCache.BeginUndoOperation(TextContent.rotateBone);
                    m_Moved = true;
                }

                m_Skeleton.RotateBones(selectedBones, deltaAngle);
                InvokePoseChanged();
            }
        }

        void HandleMoveBone()
        {
            if (view.IsActionTriggering(SkeletonAction.MoveBone))
                m_Moved = false;

            if (view.DoMoveBone(out Vector3 deltaPosition))
            {
                if (!m_Moved)
                {
                    skinningCache.BeginUndoOperation(TextContent.moveBone);
                    m_Moved = true;
                }

                m_Skeleton.MoveBones(rootBones, deltaPosition);
                InvokePoseChanged();
            }
        }

        void HandleFreeMoveBone()
        {
            if (view.IsActionTriggering(SkeletonAction.FreeMoveBone))
                m_Moved = false;

            if (view.DoFreeMoveBone(out Vector3 deltaPosition))
            {
                if (!m_Moved)
                {
                    skinningCache.BeginUndoOperation(TextContent.freeMoveBone);
                    m_Moved = true;
                }

                m_Skeleton.FreeMoveBones(selectedBones, deltaPosition);
                InvokePoseChanged();
            }
        }

        void HandleMoveJoint()
        {
            if (view.IsActionTriggering(SkeletonAction.MoveJoint))
                m_Moved = false;

            if (view.IsActionFinishing(SkeletonAction.MoveJoint))
            {
                if (hoveredTail != null && hoveredTail.chainedChild == null && hotBone.parent == hoveredTail)
                    hoveredTail.chainedChild = hotBone;
            }

            if (view.DoMoveJoint(out Vector3 deltaPosition))
            {
                if (!m_Moved)
                {
                    skinningCache.BeginUndoOperation(TextContent.moveJoint);
                    m_Moved = true;
                }

                //Snap to parent endPosition
                if (hoveredTail != null && hoveredTail.chainedChild == null && hotBone.parent == hoveredTail)
                    deltaPosition = hoveredTail.endPosition - hotBone.position;

                m_Skeleton.MoveJoints(selectedBones, deltaPosition);
                InvokePoseChanged();
            }
        }

        void HandleMoveEndPosition()
        {
            if (view.IsActionTriggering(SkeletonAction.MoveEndPosition))
                m_Moved = false;

            if (view.IsActionFinishing(SkeletonAction.MoveEndPosition))
            {
                if (hoveredJoint != null && hoveredJoint.parent == hotBone)
                    hotBone.chainedChild = hoveredJoint;
            }

            if (view.DoMoveEndPosition(out Vector3 endPosition))
            {
                if (!m_Moved)
                {
                    skinningCache.BeginUndoOperation(TextContent.moveEndPoint);
                    m_Moved = true;
                }

                Debug.Assert(hotBone != null);
                Debug.Assert(hotBone.chainedChild == null);

                if (hoveredJoint != null && hoveredJoint.parent == hotBone)
                    endPosition = hoveredJoint.position;

                m_Skeleton.SetEndPosition(hotBone, endPosition);
                InvokePoseChanged();
            }
        }

        void HandleChangeLength()
        {
            if (view.IsActionTriggering(SkeletonAction.ChangeLength))
                m_Moved = false;

            if (view.DoChangeLength(out Vector3 endPosition))
            {
                if (!m_Moved)
                {
                    skinningCache.BeginUndoOperation(TextContent.boneLength);
                    m_Moved = true;
                }

                Debug.Assert(hotBone != null);

                Vector3 direction = (Vector3)endPosition - hotBone.position;
                hotBone.length = Vector3.Dot(direction, hotBone.right);

                InvokePoseChanged();
            }
        }

        void HandleCreateBone()
        {
            if (view.DoCreateBoneStart(out Vector3 position))
            {
                m_PrevCreatedBone = null;

                if (hoveredTail != null)
                {
                    m_PrevCreatedBone = hoveredTail;
                    m_CreateBoneStartPosition = hoveredTail.endPosition;
                }
                else
                {
                    m_CreateBoneStartPosition = position;
                }
            }

            if (view.DoCreateBone(out position))
            {
                using (skinningCache.UndoScope(TextContent.createBone))
                {
                    bool isChained = m_PrevCreatedBone != null;
                    BoneCache parentBone = isChained ? m_PrevCreatedBone : rootBone;

                    if (isChained)
                        m_CreateBoneStartPosition = m_PrevCreatedBone.endPosition;

                    string name = AutoBoneName(parentBone, skeleton.bones);
                    BoneCache bone = m_Skeleton.CreateBone(parentBone, m_CreateBoneStartPosition, position, isChained, name);

                    m_PrevCreatedBone = bone;
                    m_CreateBoneStartPosition = bone.endPosition;

                    InvokeTopologyChanged();
                    InvokePoseChanged();
                }
            }
        }

        void HandleSplitBone()
        {
            if (view.DoSplitBone(out int instanceID, out Vector3 position))
            {
                using (skinningCache.UndoScope(TextContent.splitBone))
                {
                    BoneCache boneToSplit = GetBone(instanceID);

                    Debug.Assert(boneToSplit != null);

                    float splitLength = Vector3.Dot(hoveredBone.right, position - boneToSplit.position);
                    string name = AutoBoneName(boneToSplit, skeleton.bones);

                    m_Skeleton.SplitBone(boneToSplit, splitLength, name);

                    InvokeTopologyChanged();
                    InvokePoseChanged();
                }
            }
        }

        void HandleRemoveBone()
        {
            if (view.DoRemoveBone())
            {
                using (skinningCache.UndoScope(TextContent.removeBone))
                {
                    m_Skeleton.DestroyBones(selectedBones);

                    selection.Clear();
                    skinningCache.events.boneSelectionChanged.Invoke();
                    InvokeTopologyChanged();
                    InvokePoseChanged();
                }
            }
        }

        void HandleCancelMultiStepAction()
        {
            if (view.DoCancelMultistepAction(false))
                m_PrevCreatedBone = null;
        }

        void DrawSkeleton()
        {
            if (!view.IsRepainting())
                return;

            bool isNotOnVisualElement = !skinningCache.IsOnVisualElement();
            if (view.IsActionActive(SkeletonAction.CreateBone) || view.IsActionHot(SkeletonAction.CreateBone))
            {
                if (isNotOnVisualElement)
                {
                    Vector3 endPoint = view.GetMouseWorldPosition(Vector3.forward, Vector3.zero);

                    if (view.IsActionHot(SkeletonAction.CreateBone))
                        endPoint = m_CreateBoneStartPosition;

                    if (m_PrevCreatedBone == null && hoveredTail == null)
                    {
                        BoneCache root = rootBone;
                        if (root != null)
                            view.DrawBoneParentLink(root.position, endPoint, Vector3.forward, style.GetParentLinkPreviewColor(skeleton.boneCount));
                    }
                }
            }

            for (int i = 0; i < skeleton.boneCount; ++i)
            {
                BoneCache bone = skeleton.GetBone(i);

                if (bone.isVisible == false || bone.parentBone == null || bone.parentBone.chainedChild == bone)
                    continue;

                view.DrawBoneParentLink(bone.parent.position, bone.position, Vector3.forward, style.GetParentLinkColor(bone));
            }

            for (int i = 0; i < skeleton.boneCount; ++i)
            {
                BoneCache bone = skeleton.GetBone(i);

                if ((view.IsActionActive(SkeletonAction.SplitBone) && hoveredBone == bone && isNotOnVisualElement) || bone.isVisible == false)
                    continue;

                bool isSelected = selection.Contains(bone.ToCharacterIfNeeded());
                bool isHovered = hoveredBody == bone && view.IsActionHot(SkeletonAction.None) && isNotOnVisualElement;

                DrawBoneOutline(bone, style.GetOutlineColor(bone, isSelected, isHovered), style.GetOutlineScale(isSelected));
            }

            for (int i = 0; i < skeleton.boneCount; ++i)
            {
                BoneCache bone = skeleton.GetBone(i);

                if ((view.IsActionActive(SkeletonAction.SplitBone) && hoveredBone == bone && isNotOnVisualElement) || bone.isVisible == false)
                    continue;

                DrawBone(bone, style.GetColor(bone));
            }
        }

        void DrawBone(BoneCache bone, Color color)
        {
            bool isSelected = selection.Contains(bone.ToCharacterIfNeeded());
            bool isNotOnVisualElement = !skinningCache.IsOnVisualElement();
            bool isJointHovered = view.IsActionHot(SkeletonAction.None) && hoveredJoint == bone && isNotOnVisualElement;
            bool isTailHovered = view.IsActionHot(SkeletonAction.None) && hoveredTail == bone && isNotOnVisualElement;

            view.DrawBone(bone.position, bone.right, Vector3.forward, bone.length, color, bone.chainedChild != null, isSelected, isJointHovered, isTailHovered, bone == hotBone);
        }

        void DrawBoneOutline(BoneCache bone, Color color, float outlineScale)
        {
            view.DrawBoneOutline(bone.position, bone.right, Vector3.forward, bone.length, color, outlineScale);
        }

        void DrawSplitBonePreview()
        {
            if (!view.IsRepainting())
                return;

            if (skinningCache.IsOnVisualElement())
                return;

            if (view.IsActionActive(SkeletonAction.SplitBone) && hoveredBone != null)
            {
                float splitLength = Vector3.Dot(hoveredBone.right, view.GetMouseWorldPosition(hoveredBone.forward, hoveredBody.position) - hoveredBone.position);
                Vector3 position = hoveredBone.position + hoveredBone.right * splitLength;
                float length = hoveredBone.length - splitLength;
                bool isSelected = selection.Contains(hoveredBone.ToCharacterIfNeeded());

                {
                    Color color = style.GetOutlineColor(hoveredBone, false, false);
                    if (color.a > 0f)
                        view.DrawBoneOutline(hoveredBone.position, hoveredBone.right, Vector3.forward, splitLength, style.GetOutlineColor(hoveredBone, isSelected, true), style.GetOutlineScale(false));

                }
                {
                    Color color = style.GetPreviewOutlineColor(skeleton.boneCount);
                    if (color.a > 0f)
                        view.DrawBoneOutline(position, hoveredBone.right, Vector3.forward, length, style.GetPreviewOutlineColor(skeleton.boneCount), style.GetOutlineScale(false));

                }

                view.DrawBone(hoveredBone.position,
                    hoveredBone.right,
                    Vector3.forward,
                    splitLength,
                    style.GetColor(hoveredBone),
                    hoveredBone.chainedChild != null,
                    false, false, false, false);
                view.DrawBone(position,
                    hoveredBone.right,
                    Vector3.forward,
                    length,
                    style.GetPreviewColor(skeleton.boneCount),
                    hoveredBone.chainedChild != null,
                    false, false, false, false);
            }
        }

        void DrawCreateBonePreview()
        {
            if (!view.IsRepainting())
                return;

            if (skinningCache.IsOnVisualElement())
                return;

            Color color = style.GetPreviewColor(skeleton.boneCount);
            Color outlineColor = style.GetPreviewOutlineColor(skeleton.boneCount);

            Vector3 startPosition = m_CreateBoneStartPosition;
            Vector3 mousePosition = view.GetMouseWorldPosition(Vector3.forward, Vector3.zero);

            if (view.IsActionActive(SkeletonAction.CreateBone))
            {
                startPosition = mousePosition;

                if (hoveredTail != null)
                    startPosition = hoveredTail.endPosition;

                if (outlineColor.a > 0f)
                    view.DrawBoneOutline(startPosition, Vector3.right, Vector3.forward, 0f, outlineColor, style.GetOutlineScale(false));

                view.DrawBone(startPosition, Vector3.right, Vector3.forward, 0f, color, false, false, false, false, false);
            }

            if (view.IsActionHot(SkeletonAction.CreateBone))
            {
                Vector3 direction = (mousePosition - startPosition);

                if (outlineColor.a > 0f)
                    view.DrawBoneOutline(startPosition, direction.normalized, Vector3.forward, direction.magnitude, outlineColor, style.GetOutlineScale(false));

                view.DrawBone(startPosition, direction.normalized, Vector3.forward, direction.magnitude, color, false, false, false, false, false);
            }
        }

        void DrawCursors()
        {
            if (!view.IsRepainting())
                return;

            view.DrawCursors(!skinningCache.IsOnVisualElement());
        }

        public static string AutoBoneName(BoneCache parent, IEnumerable<BoneCache> bones)
        {
            string parentName = "root";

            if (parent != null)
                parentName = parent.name;

            DissectBoneName(parentName, out string inheritedName, out _);
            int nameCounter = FindBiggestNameCounter(bones);

            if (inheritedName == k_DefaultRootName)
                inheritedName = k_DefaultBoneName;

            return $"{inheritedName}_{++nameCounter}";
        }

        public static string AutoNameBoneCopy(string originalBoneName, IEnumerable<BoneCache> bones)
        {
            DissectBoneName(originalBoneName, out string inheritedName, out _);
            int nameCounter = FindBiggestNameCounterForBone(inheritedName, bones);

            if (inheritedName == k_DefaultRootName)
                inheritedName = k_DefaultBoneName;

            return $"{inheritedName}_{++nameCounter}";
        }

        static int FindBiggestNameCounter(IEnumerable<BoneCache> bones)
        {
            int autoNameCounter = 0;
            foreach (BoneCache bone in bones)
            {
                DissectBoneName(bone.name, out _, out int counter);
                if (counter > autoNameCounter)
                    autoNameCounter = counter;
            }

            return autoNameCounter;
        }

        static int FindBiggestNameCounterForBone(string boneName, IEnumerable<BoneCache> bones)
        {
            int autoNameCounter = 0;
            foreach (BoneCache bone in bones)
            {
                DissectBoneName(bone.name, out string inheritedName, out int counter);
                {
                    if (inheritedName == boneName)
                    {
                        if (counter > autoNameCounter)
                            autoNameCounter = counter;
                    }
                }
            }

            return autoNameCounter;
        }

        static void DissectBoneName(string boneName, out string inheritedName, out int counter)
        {
            if (IsBoneNameMatchAutoFormat(boneName))
            {
                string[] tokens = boneName.Split('_');
                int lastTokenIndex = tokens.Length - 1;

                string[] tokensWithoutLast = new string[lastTokenIndex];
                Array.Copy(tokens, tokensWithoutLast, lastTokenIndex);
                inheritedName = string.Join("_", tokensWithoutLast);
                counter = int.Parse(tokens[lastTokenIndex]);
            }
            else
            {
                inheritedName = boneName;
                counter = -1;
            }
        }

        static bool IsBoneNameMatchAutoFormat(string boneName)
        {
            return s_Regex.IsMatch(boneName);
        }

        void InvokeTopologyChanged()
        {
            skinningCache.events.skeletonTopologyChanged.Invoke(skeleton);
        }

        internal void InvokePoseChanged()
        {
            skeleton.SetPosePreview();

            if (editBindPose)
            {
                skeleton.SetDefaultPose();
                skinningCache.events.skeletonBindPoseChanged.Invoke(skeleton);
            }
            else
                skinningCache.events.skeletonPreviewPoseChanged.Invoke(skeleton);
        }
    }
}
