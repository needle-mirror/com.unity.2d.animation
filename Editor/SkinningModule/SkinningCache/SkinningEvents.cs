using UnityEngine.Events;

namespace UnityEditor.U2D.Animation
{
    internal class SkinningEvents
    {
        public class SpriteEvent : UnityEvent<SpriteCache> {}
        public class SkeletonEvent : UnityEvent<SkeletonCache> {}
        public class MeshEvent : UnityEvent<MeshCache> {}
        public class MeshPreviewEvent : UnityEvent<MeshPreviewCache> {}
        public class SkinningModuleModeEvent : UnityEvent<SkinningMode> {}
        public class BoneSelectionEvent : UnityEvent {}
        public class BoneEvent : UnityEvent<BoneCache> {}
        public class CharacterPartEvent : UnityEvent<CharacterPartCache> {}
        public class ToolChangeEvent : UnityEvent<ITool> {}
        public class RestoreBindPoseEvent : UnityEvent {}
        public class CopyEvent : UnityEvent {}
        public class PasteEvent : UnityEvent<bool, bool, bool, bool> {}
        public class ShortcutEvent : UnityEvent<string> {}
        public class BoneVisibilityEvent : UnityEvent<string> {}
        public class MeshPreviewBehaviourChangeEvent : UnityEvent<IMeshPreviewBehaviour> {}

        SpriteEvent m_SelectedSpriteChanged = new SpriteEvent();
        SkeletonEvent m_SkeletonPreviewPoseChanged = new SkeletonEvent();
        SkeletonEvent m_SkeletonBindPoseChanged = new SkeletonEvent();
        SkeletonEvent m_SkeletonTopologyChanged = new SkeletonEvent();
        MeshEvent m_MeshChanged = new MeshEvent();
        MeshPreviewEvent m_MeshPreviewChanged = new MeshPreviewEvent();
        SkinningModuleModeEvent m_SkinningModuleModeChanged = new SkinningModuleModeEvent();
        BoneSelectionEvent m_BoneSelectionChangedEvent = new BoneSelectionEvent();
        BoneEvent m_BoneNameChangedEvent = new BoneEvent();
        BoneEvent m_BoneDepthChangedEvent = new BoneEvent();
        BoneEvent m_BoneColorChangedEvent = new BoneEvent();
        CharacterPartEvent m_CharacterPartChanged = new CharacterPartEvent();
        ToolChangeEvent m_ToolChanged = new ToolChangeEvent();
        RestoreBindPoseEvent m_RestoreBindPose = new RestoreBindPoseEvent();
        CopyEvent m_CopyEvent = new CopyEvent();
        PasteEvent m_PasteEvent = new PasteEvent();
        ShortcutEvent m_ShortcutEvent = new ShortcutEvent();
        BoneVisibilityEvent m_BoneVisibilityEvent = new BoneVisibilityEvent();
        MeshPreviewBehaviourChangeEvent m_MeshPreviewBehaviourChange = new MeshPreviewBehaviourChangeEvent();
        UnityEvent m_PivotChanged = new UnityEvent();

        //Setting them as virtual so that we can create mock them
        public virtual SpriteEvent selectedSpriteChanged => m_SelectedSpriteChanged;
        public virtual SkeletonEvent skeletonPreviewPoseChanged => m_SkeletonPreviewPoseChanged;
        public virtual SkeletonEvent skeletonBindPoseChanged => m_SkeletonBindPoseChanged;
        public virtual SkeletonEvent skeletonTopologyChanged => m_SkeletonTopologyChanged;
        public virtual MeshEvent meshChanged => m_MeshChanged;
        public virtual MeshPreviewEvent meshPreviewChanged => m_MeshPreviewChanged;
        public virtual SkinningModuleModeEvent skinningModeChanged => m_SkinningModuleModeChanged;
        public virtual BoneSelectionEvent boneSelectionChanged => m_BoneSelectionChangedEvent;
        public virtual BoneEvent boneNameChanged => m_BoneNameChangedEvent;
        public virtual BoneEvent boneDepthChanged => m_BoneDepthChangedEvent;
        public virtual BoneEvent boneColorChanged => m_BoneColorChangedEvent;
        public virtual CharacterPartEvent characterPartChanged => m_CharacterPartChanged;
        public virtual ToolChangeEvent toolChanged => m_ToolChanged;
        public virtual RestoreBindPoseEvent restoreBindPose => m_RestoreBindPose;
        public virtual CopyEvent copy => m_CopyEvent;
        public virtual PasteEvent paste => m_PasteEvent;
        public virtual ShortcutEvent shortcut => m_ShortcutEvent;
        public virtual BoneVisibilityEvent boneVisibility => m_BoneVisibilityEvent;
        public virtual MeshPreviewBehaviourChangeEvent meshPreviewBehaviourChange => m_MeshPreviewBehaviourChange;
        public virtual UnityEvent pivotChange => m_PivotChanged;
    }
}
