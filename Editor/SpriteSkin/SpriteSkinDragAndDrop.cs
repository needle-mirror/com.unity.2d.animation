using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.U2D;
using UnityEngine.U2D.Animation;

namespace UnityEditor.U2D.Animation
{
    /// <summary>
    /// Automatically adds a <see cref="SpriteSkin"/> when a Sprite that
    /// carries deformation data is dragged into the Scene view or Hierarchy and a GameObject is created for it.
    /// </summary>
    /// <remarks>
    /// The Editor has no direct "a Sprite was dropped" notification, so the drop is detected from the drag
    /// events themselves: <see cref="SceneView.duringSceneGui"/> runs on the Scene view's DragPerform before
    /// SpriteUtility consumes it, and a passive <see cref="DragAndDrop.HierarchyDropHandlerV2"/> observes the
    /// Hierarchy drop while returning <see cref="DragAndDropVisualMode.None"/> so the built-in creation still
    /// runs. Both paths replace the selection with the newly created GameObject(s); the work is deferred to
    /// <see cref="EditorApplication.delayCall"/> and applied to whatever became newly selected. All hooks are
    /// event driven, so an idle Editor or a drag with no skinned Sprite costs nothing.
    /// </remarks>
    [InitializeOnLoad]
    internal static class SpriteSkinDragAndDrop
    {
        // Selection captured at drop time so delayCall only touches GameObjects the drop newly selected.
        static readonly HashSet<EntityId> s_SelectionBeforeDrop = new HashSet<EntityId>();
        static int s_UndoGroup;
        static bool s_Scheduled;

        static SpriteSkinDragAndDrop()
        {
            SceneView.duringSceneGui += OnDuringSceneGui;
            DragAndDrop.AddDropHandlerV2((DragAndDrop.HierarchyDropHandlerV2)OnHierarchyDrop);
        }

        static void OnDuringSceneGui(SceneView sceneView)
        {
            if (Event.current.type == EventType.DragPerform && DragHasDeformationSprite())
                ScheduleProcessNewSelection();
        }

        static DragAndDropVisualMode OnHierarchyDrop(EntityId dropTargetEntityId, HierarchyDropFlags dropMode, Transform parentForDraggedObjects, bool perform)
        {
            // Observe only: returning None leaves the built-in native Sprite handling to create the GameObject.
            // Inspect the drag only on the actual drop, not on every hover event.
            if (perform && DragHasDeformationSprite())
                ScheduleProcessNewSelection();

            return DragAndDropVisualMode.None;
        }

        static void ScheduleProcessNewSelection()
        {
            // Keep the first capture if the same drop is notified more than once before the deferred work runs;
            // a later capture could already contain the created GameObject and would make the pass skip it.
            if (s_Scheduled)
                return;

            s_Scheduled = true;

            s_SelectionBeforeDrop.Clear();
            foreach (Object selected in Selection.objects)
                s_SelectionBeforeDrop.Add(selected.GetEntityId());

            // The GameObject is created after this event, so remember where Undo is now and collapse everything
            // (creation + Sprite Skin) into one step once the deferred work runs.
            s_UndoGroup = Undo.GetCurrentGroup();

            EditorApplication.delayCall += ProcessNewSelection;
        }

        static void ProcessNewSelection()
        {
            s_Scheduled = false;

            bool addedAny = false;
            foreach (GameObject go in Selection.gameObjects)
            {
                if (s_SelectionBeforeDrop.Contains(go.GetEntityId()))
                    continue; // Not created by this drop.

                addedAny |= TryAddSpriteSkin(go);
            }

            if (addedAny)
                Undo.CollapseUndoOperations(s_UndoGroup);

            s_SelectionBeforeDrop.Clear();
        }

        // --- Core. Kept free of drag state so it can be unit tested directly. ---

        internal static bool TryAddSpriteSkin(GameObject go)
        {
            if (go == null)
                return false;
            if (!go.TryGetComponent(out SpriteRenderer spriteRenderer))
                return false;
            if (go.GetComponent<SpriteSkin>() != null || go.GetComponent<Animator>() != null)
                return false;

            Sprite sprite = spriteRenderer.sprite;
            if (sprite == null || !HasDeformationData(sprite))
                return false;

            SpriteSkin spriteSkin = Undo.AddComponent<SpriteSkin>(go);
            EditorUtility.SetDirty(spriteSkin);
            return true;
        }

        static bool DragHasDeformationSprite()
        {
            Object[] draggedObjects = DragAndDrop.objectReferences;
            for (int i = 0; i < draggedObjects.Length; i++)
            {
                if (ResolveSpriteWithDeformation(draggedObjects[i]) != null)
                    return true;
            }

            return false;
        }

        static Sprite ResolveSpriteWithDeformation(Object draggedObject)
        {
            Sprite sprite = draggedObject as Sprite;
            if (sprite == null && draggedObject is Texture2D texture)
                sprite = GetFirstSprite(texture);

            return sprite != null && HasDeformationData(sprite) ? sprite : null;
        }

        static bool HasDeformationData(Sprite sprite)
        {
            return sprite.GetBindPoses().Length > 0
                && sprite.GetBones().Length > 0
                && sprite.HasVertexAttribute(VertexAttribute.BlendWeight);
        }

        static Sprite GetFirstSprite(Texture2D texture)
        {
            string assetPath = AssetDatabase.GetAssetPath(texture);
            if (string.IsNullOrEmpty(assetPath))
                return null;

            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is Sprite sprite)
                    return sprite;
            }

            return null;
        }
    }
}
