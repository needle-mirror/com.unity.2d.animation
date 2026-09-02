using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityEditor.U2D.Animation
{
    /// <summary>
    /// Draws UIToolkit elements over the Skinning Editor's character canvas, shown only while the Skinning
    /// Editor is active. Elements are either pinned to world positions — following the canvas as it is zoomed
    /// and panned — or anchored in canvas space, laid out by their own styles over the visible canvas area.
    /// </summary>
    /// <remarks>
    /// World positions are in the Skinning Editor's world space (the same space the bones are drawn in).
    /// Elements are pickable by default; while the pointer is over one the Skinning Editor treats it as UI
    /// and suspends the canvas tools underneath. Set <see cref="PickingMode.Ignore"/> on display-only
    /// elements to keep them click-through.
    /// Registrations persist until <see cref="Remove"/> is called, including across Skinning Editor
    /// activations and sprite asset changes: remove elements when the owning feature is torn down, and read
    /// current state inside the position callback rather than capturing per-asset objects (e.g. bones).
    /// </remarks>
    internal static class SkinningEditorCanvas
    {
        /// <summary>
        /// Adds an element to the canvas overlay, positioned at <paramref name="worldPosition"/> each frame.
        /// </summary>
        /// <param name="element">The element to overlay. It is hosted in a positioning wrapper, so its own styles stay untouched.</param>
        /// <param name="worldPosition">Returns the element's world-space position; evaluated every repaint.</param>
        public static void Add(VisualElement element, Func<Vector2> worldPosition)
        {
            SkinningEditorCanvasManager.Add(element, worldPosition);
        }

        /// <summary>
        /// Adds an element to the canvas overlay at a fixed world position. Use this when the position never
        /// changes; the element still follows the canvas as it is zoomed and panned.
        /// </summary>
        /// <param name="element">The element to overlay. It is hosted in a positioning wrapper, so its own styles stay untouched.</param>
        /// <param name="worldPosition">The element's world-space position.</param>
        public static void Add(VisualElement element, Vector2 worldPosition)
        {
            SkinningEditorCanvasManager.Add(element, () => worldPosition);
        }

        /// <summary>
        /// Adds an element laid out in canvas space. The hosting layer stretches over the whole character
        /// canvas, so the element can anchor and size itself with regular layout styles (left/top/right/bottom,
        /// percentages, flex) relative to the visible canvas area. It does not follow zoom or pan.
        /// </summary>
        /// <param name="element">The element to overlay. Position and size it with its own styles.</param>
        public static void Add(VisualElement element)
        {
            SkinningEditorCanvasManager.Add(element);
        }

        /// <summary>
        /// Removes a previously added element from the canvas overlay.
        /// </summary>
        public static void Remove(VisualElement element)
        {
            SkinningEditorCanvasManager.Remove(element);
        }

        /// <summary>
        /// Immediately repositions world-pinned elements. The canvas does this automatically on every Sprite
        /// Editor repaint; call it when a world position changes without one — e.g. from a pointer handler that
        /// captured the pointer, so the drag never reaches the canvas.
        /// </summary>
        public static void Reposition()
        {
            SkinningEditorCanvasManager.Reposition();
        }

        /// <summary>
        /// Converts a world-space position to a canvas (overlay container local) position.
        /// </summary>
        /// <returns>False when no Skinning Editor canvas transform is currently available.</returns>
        public static bool TryWorldToCanvas(Vector2 worldPosition, out Vector2 canvasPosition)
        {
            return SkinningEditorCanvasManager.TryWorldToCanvas(worldPosition, out canvasPosition);
        }

        /// <summary>
        /// Converts a canvas (overlay container local) position to a world-space position. Useful when handling
        /// pointer events on an interactive overlay element.
        /// </summary>
        /// <returns>False when no Skinning Editor canvas transform is currently available.</returns>
        public static bool TryCanvasToWorld(Vector2 canvasPosition, out Vector2 worldPosition)
        {
            return SkinningEditorCanvasManager.TryCanvasToWorld(canvasPosition, out worldPosition);
        }
    }
}
