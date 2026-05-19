using System;
using UnityEngine;

namespace UnityEditor.U2D.Animation
{
    internal class SkeletonToolWrapper : BaseTool
    {
        private SkeletonTool m_SkeletonTool;
        private SkeletonMode m_Mode;

        public SkeletonTool skeletonTool
        {
            get { return m_SkeletonTool; }
            set { m_SkeletonTool = value; }
        }

        public SkeletonMode mode
        {
            get { return m_Mode; }
            set { m_Mode = value; }
        }

        public bool editBindPose { get; set; }

        bool isSpriteSheetModeWithoutCharacter
        {
            get => skinningCache.mode == SkinningMode.SpriteSheet && !skinningCache.hasCharacter;
        }

        public override bool allowsSpriteSelection
        {
            get
            {
                if (skeletonTool == null || skeletonTool.hoveredBone != null)
                    return false;

                // Allow sprite selection in all modes for PNG sprites (Sprite Sheet Mode without Character)
                if (isSpriteSheetModeWithoutCharacter)
                {
                    // In Create Bone mode, sprites are only selectable but not hoverable when there is a selected sprite.
                    if (mode == SkeletonMode.CreateBone && Event.current.type == EventType.MouseMove)
                        return skinningCache.selectedSprite == null;

                    return true;
                }

                // For Character Mode, only allow in EditPose mode
                return mode == SkeletonMode.EditPose;
            }
        }

        public override int defaultControlID
        {
            get
            {
                Debug.Assert(skeletonTool != null);

                return skeletonTool.defaultControlID;
            }
        }

        protected override void OnActivate()
        {
            Debug.Assert(skeletonTool != null);
            skeletonTool.enableBoneInspector = true;
            skeletonTool.Activate();

            if (!isSpriteSheetModeWithoutCharacter && m_Mode != SkeletonMode.EditPose)
                skinningCache.selectionTool.selectedSprite = null;
        }

        protected override void OnDeactivate()
        {
            skeletonTool.enableBoneInspector = false;
            skeletonTool.Deactivate();
        }

        private SkeletonMode OverrideMode()
        {
            SkeletonMode modeOverride = mode;

            //Disable SkeletonManipulation if character exists and we are in SpriteSheet mode
            if (skinningCache.mode == SkinningMode.SpriteSheet && skinningCache.hasCharacter && editBindPose)
                modeOverride = SkeletonMode.Selection;

            return modeOverride;
        }

        protected override void OnGUI()
        {
            Debug.Assert(skeletonTool != null);

            skeletonTool.mode = OverrideMode();
            skeletonTool.editBindPose = editBindPose;
            skeletonTool.DoGUI();
        }
    }
}
