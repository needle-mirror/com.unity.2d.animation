using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.U2D.Animation;
using UnityEngine.U2D.Common;

namespace UnityEditor.U2D.Animation
{
    //Make sure Bone Gizmo registers callbacks before anyone else
    [InitializeOnLoad]
    internal class BoneGizmoInitializer
    {
        static BoneGizmoInitializer()
        {
            BoneGizmo.instance.Initialize();
        }
    }

    internal class BoneGizmo : ScriptableSingleton<BoneGizmo>
    {
        BoneGizmoController m_BoneGizmoController;

        internal BoneGizmoController boneGizmoController => m_BoneGizmoController;

        internal void Initialize()
        {
            m_BoneGizmoController = new BoneGizmoController(new SkeletonView(new GUIWrapper()), new UnityEngineUndo(), new BoneGizmoToggle());
            RegisterCallbacks();
        }

        internal void ClearSpriteBoneCache()
        {
            boneGizmoController.ClearSpriteBoneCache();
        }

        void RegisterCallbacks()
        {
            Selection.selectionChanged += OnSelectionChanged;
            SceneView.duringSceneGui += OnSceneGUI;
            AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload;
            EditorApplication.playModeStateChanged += PlayModeStateChanged;
        }

        void OnSceneGUI(SceneView sceneView)
        {
            boneGizmoController.OnGUI();
        }

        void OnSelectionChanged()
        {
            boneGizmoController.OnSelectionChanged();
        }

        void OnAfterAssemblyReload()
        {
            boneGizmoController.OnSelectionChanged();
        }

        void PlayModeStateChanged(PlayModeStateChange stateChange)
        {
            if (stateChange == PlayModeStateChange.EnteredPlayMode ||
                stateChange == PlayModeStateChange.EnteredEditMode)
                boneGizmoController.OnSelectionChanged();
        }
    }

    internal class BoneGizmoController
    {
        Dictionary<Sprite, SpriteBone[]> m_SpriteBones = new Dictionary<Sprite, UnityEngine.U2D.SpriteBone[]>();
        Dictionary<Transform, Vector2> m_BoneData = new Dictionary<Transform, Vector2>();
        HashSet<SpriteSkin> m_SkinComponents = new HashSet<SpriteSkin>();
        HashSet<Transform> m_CachedBones = new HashSet<Transform>();
        HashSet<Transform> m_SelectionRoots = new HashSet<Transform>();
        ISkeletonView m_View;
        IUndo m_Undo;
        Tool m_PreviousTool = Tool.None;

        internal IBoneGizmoToggle boneGizmoToggle { get; set; }
        public Transform hoveredBone => GetBone(m_View.hoveredBoneID);
        public Transform hoveredTail => GetBone(m_View.hoveredTailID);
        public Transform hoveredBody => GetBone(m_View.hoveredBodyID);

        public Transform hoveredJoint => GetBone(m_View.hoveredJointID);

        public Transform hotBone => GetBone(m_View.hotBoneID);

        static Transform GetBone(int instanceID)
        {
            return EditorUtility.InstanceIDToObject(instanceID) as Transform;
        }

        public BoneGizmoController(ISkeletonView view, IUndo undo, IBoneGizmoToggle toggle)
        {
            m_View = view;
            m_View.mode = SkeletonMode.EditPose;
            m_View.InvalidID = 0;
            m_Undo = undo;
            boneGizmoToggle = toggle;
        }

        internal void OnSelectionChanged()
        {
            m_SelectionRoots.Clear();

            foreach (Transform selectedTransform in Selection.transforms)
            {
                GameObject prefabRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(selectedTransform.gameObject);
                Animator animator;

                if (prefabRoot != null)
                    m_SelectionRoots.Add(prefabRoot.transform);
                else if ((animator = selectedTransform.GetComponentInParent<Animator>()) != null)
                    m_SelectionRoots.Add(animator.transform);
                else
                    m_SelectionRoots.Add(selectedTransform.root);
            }

            if (m_PreviousTool == Tool.None && Selection.activeTransform != null && m_BoneData.ContainsKey(Selection.activeTransform))
            {
                m_PreviousTool = UnityEditor.Tools.current;
                UnityEditor.Tools.current = Tool.None;
            }

            if (m_PreviousTool != Tool.None && (Selection.activeTransform == null || !m_BoneData.ContainsKey(Selection.activeTransform)))
            {
                if (UnityEditor.Tools.current == Tool.None)
                    UnityEditor.Tools.current = m_PreviousTool;

                m_PreviousTool = Tool.None;
            }

            FindSkinComponents();
        }

        internal void OnGUI()
        {
            boneGizmoToggle.OnGUI();

            if (!boneGizmoToggle.enableGizmos)
                return;

            PrepareBones();
            DoBoneGUI();
        }

        internal void FindSkinComponents()
        {
            m_SkinComponents.Clear();

            foreach (Transform root in m_SelectionRoots)
            {
                SpriteSkin[] components = root.GetComponentsInChildren<SpriteSkin>(false);

                foreach (SpriteSkin component in components)
                    m_SkinComponents.Add(component);
            }

            SceneView.RepaintAll();
        }

        internal void ClearSpriteBoneCache()
        {
            m_SpriteBones.Clear();
        }

        void PrepareBones()
        {
            if (!m_View.CanLayout())
                return;

            if (m_View.IsActionHot(SkeletonAction.None))
                m_CachedBones.Clear();

            m_BoneData.Clear();

            foreach (SpriteSkin skinComponent in m_SkinComponents)
            {
                if (skinComponent == null)
                    continue;

                PrepareBones(skinComponent);
            }
        }

        SpriteBone[] GetSpriteBones(SpriteSkin spriteSkin)
        {
            Debug.Assert(spriteSkin.isValid);

            Sprite sprite = spriteSkin.spriteRenderer.sprite;
            if (!m_SpriteBones.TryGetValue(sprite, out SpriteBone[] spriteBones))
            {
                spriteBones = sprite.GetBones();
                m_SpriteBones[sprite] = sprite.GetBones();
            }

            return spriteBones;
        }

        void PrepareBones(SpriteSkin spriteSkin)
        {
            Debug.Assert(spriteSkin != null);
            Debug.Assert(m_View.CanLayout());

            if (!spriteSkin.isActiveAndEnabled || !spriteSkin.isValid || !spriteSkin.spriteRenderer.enabled)
                return;

            Transform[] boneTransforms = spriteSkin.boneTransforms;
            SpriteBone[] spriteBones = GetSpriteBones(spriteSkin);
            const float alpha = 1f;

            if (spriteBones == null)
                return;

            for (int i = 0; i < boneTransforms.Length; ++i)
            {
                Transform boneTransform = boneTransforms[i];

                if (boneTransform == null || m_BoneData.ContainsKey(boneTransform))
                    continue;

                SpriteBone bone = spriteBones[i];

                if (m_View.IsActionHot(SkeletonAction.None))
                    m_CachedBones.Add(boneTransform);

                m_BoneData.Add(boneTransform, new Vector2(bone.length, alpha));
            }
        }

        void DoBoneGUI()
        {
            m_View.BeginLayout();

            if (m_View.CanLayout())
                LayoutBones();

            m_View.EndLayout();

            HandleSelectBone();
            HandleRotateBone();
            HandleMoveBone();
            DrawBoneAndOutlines();
            DrawCursors();
        }

        void LayoutBones()
        {
            foreach (Transform bone in m_CachedBones)
            {
                if (bone == null)
                    continue;

                if (!m_BoneData.TryGetValue(bone, out Vector2 value))
                    continue;

                float length = value.x;

                if (bone != hotBone)
                {
                    Vector3 bonePosition = bone.position;
                    m_View.LayoutBone(bone.GetInstanceID(), bonePosition, bonePosition + bone.GetScaledRight() * length, bone.forward, bone.up, bone.right, false);
                }
            }
        }

        void HandleSelectBone()
        {
            if (m_View.DoSelectBone(out int instanceID, out bool additive))
            {
                Transform bone = GetBone(instanceID);

                if (!additive)
                {
                    if (!Selection.Contains(bone.gameObject))
                        Selection.activeTransform = bone;
                }
                else
                {
                    List<Object> objectList = new List<Object>(Selection.objects);

                    if (objectList.Contains(bone.gameObject))
                        objectList.Remove(bone.gameObject);
                    else
                        objectList.Add(bone.gameObject);

                    Selection.objects = objectList.ToArray();
                }
            }
        }

        void HandleRotateBone()
        {
            Transform pivot = hoveredBone;

            if (m_View.IsActionHot(SkeletonAction.RotateBone))
                pivot = hotBone;

            if (pivot == null)
                return;

            FindPivotTransform(pivot, out pivot);
            if (pivot == null)
                return;

            if (m_View.DoRotateBone(pivot.position, pivot.forward, out float deltaAngle))
                SetBoneRotation(deltaAngle);
        }

        static bool FindPivotTransform(Transform transform, out Transform selectedTransform)
        {
            selectedTransform = transform;
            Transform[] selectedRoots = Selection.transforms;

            foreach (Transform selectedRoot in selectedRoots)
            {
                if (transform.IsDescendentOf(selectedRoot))
                {
                    selectedTransform = selectedRoot;
                    return true;
                }
            }

            return false;
        }

        void HandleMoveBone()
        {
            if (m_View.DoMoveBone(out Vector3 deltaPosition))
                SetBonePosition(deltaPosition);
        }

        void SetBonePosition(Vector3 deltaPosition)
        {
            foreach (Transform selectedTransform in Selection.transforms)
            {
                if (!m_BoneData.ContainsKey(selectedTransform))
                    continue;

                Transform boneTransform = selectedTransform;

                m_Undo.RecordObject(boneTransform, TextContent.moveBone);
                boneTransform.position += deltaPosition;
            }
        }

        void SetBoneRotation(float deltaAngle)
        {
            foreach (GameObject selectedGameObject in Selection.gameObjects)
            {
                if (!m_BoneData.ContainsKey(selectedGameObject.transform))
                    continue;

                Transform boneTransform = selectedGameObject.transform;

                m_Undo.RecordObject(boneTransform, TextContent.rotateBone);
                boneTransform.Rotate(boneTransform.forward, deltaAngle, Space.World);
                InternalEngineBridge.SetLocalEulerHint(boneTransform);
            }
        }

        void DrawBoneAndOutlines()
        {
            if (!m_View.IsRepainting())
                return;

            DrawBoneOutlines();
            DrawBones();
        }

        void DrawBoneOutlines()
        {
            Color selectedOutlineColor = SelectionOutlineSettings.outlineColor;
            float selectedOutlineSize = SelectionOutlineSettings.selectedBoneOutlineSize;
            Color defaultOutlineColor = Color.black.AlphaMultiplied(0.5f);

            foreach (KeyValuePair<Transform, Vector2> boneData in m_BoneData)
            {
                Transform bone = boneData.Key;

                if (bone == null)
                    continue;

                Vector2 value = boneData.Value;
                float length = value.x;
                float alpha = value.y;

                if (alpha == 0f || !bone.gameObject.activeInHierarchy)
                    continue;

                Color color = defaultOutlineColor;
                float outlineSize = 1.25f;

                bool isSelected = Selection.Contains(bone.gameObject);
                bool isHovered = hoveredBody == bone && m_View.IsActionHot(SkeletonAction.None);

                if (isSelected)
                {
                    color = selectedOutlineColor;
                    outlineSize = selectedOutlineSize * 0.5f + 1f;
                }
                else if (isHovered)
                    color = Handles.preselectionColor;

                m_View.DrawBoneOutline(bone.position, bone.GetScaledRight(), bone.forward, length, color, outlineSize);
            }

            BatchedDrawing.Draw();
        }

        void DrawBones()
        {
            foreach (KeyValuePair<Transform, Vector2> boneData in m_BoneData)
            {
                Transform bone = boneData.Key;
                if (bone == null)
                    continue;

                Vector2 value = boneData.Value;
                float length = value.x;
                float alpha = value.y;

                if (alpha == 0f || !bone.gameObject.activeInHierarchy)
                    continue;

                DrawBone(bone, length, Color.white);
            }

            BatchedDrawing.Draw();
        }

        void DrawBone(Transform bone, float length, Color color)
        {
            bool isSelected = Selection.Contains(bone.gameObject);
            bool isJointHovered = m_View.IsActionHot(SkeletonAction.None) && hoveredJoint == bone;
            bool isTailHovered = m_View.IsActionHot(SkeletonAction.None) && hoveredTail == bone;

            m_View.DrawBone(bone.position, bone.GetScaledRight(), bone.forward, length, color, false, isSelected, isJointHovered, isTailHovered, bone == hotBone);
        }

        void DrawCursors()
        {
            if (!m_View.IsRepainting())
                return;

            m_View.DrawCursors(true);
        }
    }
}
