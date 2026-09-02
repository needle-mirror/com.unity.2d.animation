using System;
using System.Collections.Generic;
using UnityEditor.U2D.Layout;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityEditor.U2D.Animation
{
    // Hosts UIToolkit elements over the Skinning Editor canvas in two modes: world-pinned elements follow the
    // canvas as it zooms/pans, canvas-anchored elements are laid out by their own styles over the canvas area.
    // Everything hangs off a single layer that Attach/Detach moves in and out of the layout overlay, so
    // registrations persist across module activations.
    internal static class SkinningEditorCanvasManager
    {
        class Entry
        {
            // The entry's root in the layer: a positioning wrapper for world-pinned entries, or the caller's
            // element itself for canvas-anchored ones (worldPosition null).
            public VisualElement root;
            public Func<Vector2> worldPosition;
            public Vector2 lastCanvas = new(float.NaN, float.NaN);
        }

        // Keyed by the caller's element; z-order lives in the layer's child order.
        static readonly Dictionary<VisualElement, Entry> s_Entries = new();
        // Stretched, click-through layer holding all registered elements.
        static readonly VisualElement s_Container = CreateContainer();
        static ISpriteEditor s_SpriteEditor;
        // World-pinned entry count; gates the per-tick update driver so it idles when nothing follows.
        static int s_WorldPinnedCount;

        // World <-> canvas transform captured during the last DoMainGUI (see SkinningModule).
        static bool s_HasTransform;
        static Matrix4x4 s_WorldToGui;
        static Matrix4x4 s_GuiToWorld;
        static Vector2 s_ClipOffset;

        static VisualElement CreateContainer()
        {
            VisualElement container = new() { name = "skinning-editor-canvas-overlay", pickingMode = PickingMode.Ignore };
            container.StretchToParentSize();
            return container;
        }

        // Positioning the wrapper instead of the element leaves the caller's own styles untouched. The
        // wrapper stays click-through; the element's own picking mode decides interactivity.
        static VisualElement CreateWrapper(VisualElement element)
        {
            VisualElement wrapper = new() { name = "canvas-overlay-position", pickingMode = PickingMode.Ignore };
            wrapper.style.position = Position.Absolute;
            wrapper.style.left = 0;
            wrapper.style.top = 0;
            wrapper.Add(element);
            return wrapper;
        }

        public static void Add(VisualElement element, Func<Vector2> worldPosition)
        {
            if (element == null || worldPosition == null)
                return;
            if (s_Entries.ContainsKey(element))
                return;

            Entry entry = new() { root = CreateWrapper(element), worldPosition = worldPosition };
            s_Entries.Add(element, entry);
            s_WorldPinnedCount++;
            s_Container.Add(entry.root);

            // Position now so the element does not flash at (0,0) until the next repaint.
            if (s_HasTransform)
                PositionEntry(entry);
        }

        // Canvas-anchored registration: hosted directly in the layer, laid out by its own styles.
        public static void Add(VisualElement element)
        {
            if (element == null)
                return;
            if (s_Entries.ContainsKey(element))
                return;

            s_Entries.Add(element, new Entry { root = element });
            s_Container.Add(element);
        }

        public static void Remove(VisualElement element)
        {
            if (!s_Entries.TryGetValue(element, out Entry entry))
                return;
            s_Entries.Remove(element);
            if (entry.worldPosition != null)
                s_WorldPinnedCount--;

            // Remove the wrapper from the layer, then the element from the wrapper (second call is a no-op
            // when they are the same). The element comes back with its own styles untouched.
            entry.root.RemoveFromHierarchy();
            element.RemoveFromHierarchy();
        }

        // Hosting under the LayoutOverlay keeps canvas coordinates and makes
        // SkinningCache.IsOnVisualElement() treat the hosted elements as UI.
        public static void Attach(ISpriteEditor spriteEditor, LayoutOverlay host)
        {
            Detach();

            if (spriteEditor == null || host == null)
                return;
            s_SpriteEditor = spriteEditor;

            // Insert behind the host's existing children so canvas overlays render under the side panels.
            host.Insert(0, s_Container);

            // Reposition from the editor loop, not just the IMGUI Repaint: translate set during the IMGUI
            // pass lands a frame late, so world-pinned overlays trail the bones. Detach() above already
            // unsubscribed, so this stays a single subscription.
            EditorApplication.update += OnEditorUpdate;
        }

        public static void Detach()
        {
            EditorApplication.update -= OnEditorUpdate;
            s_Container.RemoveFromHierarchy();
            s_SpriteEditor = null;
            s_HasTransform = false;
        }

        static void OnEditorUpdate()
        {
            // Idle when nothing follows or the overlay isn't in a live panel (window hidden). Both are O(1).
            if (s_WorldPinnedCount == 0 || s_Container.panel == null)
                return;

            Reposition();
        }

        // Repositions world-pinned elements from the last captured transform. Call after a world position
        // changes without a window repaint, e.g. from a pointer handler that captured the pointer.
        public static void Reposition()
        {
            if (!s_HasTransform)
                return;

            foreach (Entry entry in s_Entries.Values)
            {
                if (entry.worldPosition != null)
                    PositionEntry(entry);
            }
        }

        static void PositionEntry(Entry entry)
        {
            Vector2 worldPosition;
            try
            {
                worldPosition = entry.worldPosition();
            }
            catch (Exception e)
            {
                // A broken callback must not abort positioning of the remaining entries.
                Debug.LogException(e);
                return;
            }

            Vector2 canvas = WorldToCanvas(worldPosition, s_WorldToGui, s_ClipOffset);
            // A non-finite result never equals lastCanvas, so it would re-set translate every tick. Skip it.
            if (!float.IsFinite(canvas.x) || !float.IsFinite(canvas.y))
                return;
            if (canvas == entry.lastCanvas)
                return;

            entry.root.style.translate = new Translate(canvas.x, canvas.y);
            entry.lastCanvas = canvas;
        }

        public static bool TryWorldToCanvas(Vector2 worldPosition, out Vector2 canvasPosition)
        {
            canvasPosition = s_HasTransform ? WorldToCanvas(worldPosition, s_WorldToGui, s_ClipOffset) : default;
            return s_HasTransform;
        }

        public static bool TryCanvasToWorld(Vector2 canvasPosition, out Vector2 worldPosition)
        {
            worldPosition = s_HasTransform ? CanvasToWorld(canvasPosition, s_GuiToWorld, s_ClipOffset) : default;
            return s_HasTransform;
        }

        // Call from DoMainGUI() on Repaint (where Handles.matrix is valid), then Reposition() to apply it.
        public static void CaptureTransform()
        {
            if (s_SpriteEditor == null)
            {
                s_HasTransform = false;
                return;
            }

            s_WorldToGui = Handles.matrix;
            s_GuiToWorld = Handles.inverseMatrix;
            // Handles.matrix excludes the viewport offset. GUIClip is internal, so mirror the clip
            // SpriteUtilityWindow.DoTextureGUI pushes: GUIClip.Push(textureViewRect, -scrollPosition).
            // Must follow any change to that clip setup.
            s_ClipOffset = s_SpriteEditor.windowDimension.position - s_SpriteEditor.scrollPosition;
            s_HasTransform = true;
        }

        // Pure, testable conversions: canvas = Handles-matrix point + GUIClip offset (inverse undoes both).
        internal static Vector2 WorldToCanvas(Vector3 worldPosition, Matrix4x4 worldToGui, Vector2 clipOffset)
        {
            Vector2 gui = worldToGui.MultiplyPoint(worldPosition);
            return gui + clipOffset;
        }

        internal static Vector2 CanvasToWorld(Vector2 canvasPosition, Matrix4x4 guiToWorld, Vector2 clipOffset)
        {
            Vector3 gui = canvasPosition - clipOffset;
            return guiToWorld.MultiplyPoint(gui);
        }
    }
}
