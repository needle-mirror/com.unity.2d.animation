using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Scripting.LifecycleManagement;
using UnityEditor.Overlays;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace UnityEditor.U2D.Animation
{
    // Adds [SkinningEditorOverlay]-marked overlays to the Sprite Editor's OverlayCanvas while the Skinning module is active.
    internal static class SkinningEditorOverlayManager
    {
        // One instance per marked type, created on first use and reused, so panel state persists across activations.
        [NoAutoStaticsCleanup] // Tied to Skinning Editor activation, not to Play
        static readonly List<Overlay> s_Overlays = new();
        // The canvas the overlays are currently added to; null while the Skinning module is inactive.
        [NoAutoStaticsCleanup]
        static OverlayCanvas s_Canvas;
        [NoAutoStaticsCleanup]
        static EditorWindow s_Window;

        // Test seam: pre-created overlays skip discovery, so the attach path can run without marking a type in the editor.
        internal static List<Overlay> overlays => s_Overlays;

        public static void Attach(ISpriteEditor spriteEditor)
        {
            Detach();

            // Only an ISupportsOverlays window has an initialised canvas; Add on any other throws from RestoreOverlay.
            EditorWindow window = spriteEditor as EditorWindow;
            if (window == null || !(window is ISupportsOverlays))
                return;

            s_Window = window;

            // Deferred a frame because the module switch rebuilds the overlay canvas, dropping anything added before it.
            EditorApplication.delayCall += AddOverlays;
        }

        public static void Detach()
        {
            // Drops a pending add when the module deactivates before the deferred call runs; a no-op otherwise.
            EditorApplication.delayCall -= AddOverlays;

            if (s_Canvas != null)
            {
                foreach (Overlay overlay in s_Overlays)
                {
                    try
                    {
                        s_Canvas.Remove(overlay);
                    }
                    catch (Exception e)
                    {
                        // Remove runs the overlay's OnWillBeDestroyed. A broken one must not strand the rest on
                        // the canvas, where the next Add skips them as already present and leaves them hidden.
                        Debug.LogException(e);
                    }
                }
            }

            s_Canvas = null;
            s_Window = null;
        }

        // Reached one tick after Attach; internal so tests can drive it without depending on when delayCall dispatches.
        internal static void AddOverlays()
        {
            if (s_Window == null)
                return;

            s_Canvas = s_Window.overlayCanvas;
            if (s_Canvas == null)
                return;

            if (s_Overlays.Count == 0)
                CreateOverlays(TypeCache.GetTypesWithAttribute<SkinningEditorOverlayAttribute>(), s_Overlays);

            foreach (Overlay overlay in s_Overlays)
            {
                // Add restores the visibility the canvas saved on Remove, but only when displayed changes: start hidden.
                overlay.displayed = false;
                try
                {
                    s_Canvas.Add(overlay);
                }
                catch (Exception e)
                {
                    // Add runs the overlay's OnCreated. A broken one must not keep the rest off the canvas.
                    Debug.LogException(e);
                }
            }
        }

        // Split from the TypeCache lookup so filtering and creation stay testable without marking a type in the editor.
        internal static void CreateOverlays(IEnumerable<Type> types, List<Overlay> results)
        {
            foreach (Type type in types)
            {
                if (!IsOverlayType(type))
                    continue;

                Overlay overlay = CreateOverlay(type, type.GetCustomAttribute<SkinningEditorOverlayAttribute>());
                if (overlay != null)
                    results.Add(overlay);
            }
        }

        // A size stated on the attribute wins over one the constructor set, as Overlay.Initialize does for [Overlay].
        // Both axes have to be stated: one alone says nothing about the other.
        static void ApplyAttributeSizes(Overlay overlay, SkinningEditorOverlayAttribute attribute)
        {
            if (overlay == null || attribute == null)
                return;

            if (IsSizeStated(attribute.minSize))
                overlay.minSize = attribute.minSize;
            if (IsSizeStated(attribute.maxSize))
                overlay.maxSize = attribute.maxSize;
            if (IsSizeStated(attribute.defaultSize))
                overlay.defaultSize = attribute.defaultSize;
        }

        static bool IsSizeStated(Vector2 size)
        {
            return !float.IsNegativeInfinity(size.x) && !float.IsNegativeInfinity(size.y);
        }

        // With no [Overlay] attribute the canvas applies no defaults, and a zero defaultSize makes
        // the frame size itself from its content, unbounded by the window.
        static readonly Vector2 k_FallbackDefaultSize = new Vector2(240f, 160f);

        internal static void EnsureDefaultSize(Overlay overlay)
        {
            if (overlay == null)
                return;

            Vector2 size = overlay.defaultSize;
            Vector2 min = overlay.minSize;
            Vector2 max = overlay.maxSize;

            if (size.x <= 0f)
                size.x = DeriveDefaultSize(min.x, max.x, k_FallbackDefaultSize.x);
            if (size.y <= 0f)
                size.y = DeriveDefaultSize(min.y, max.y, k_FallbackDefaultSize.y);

            overlay.defaultSize = size;
        }

        // Only ever used for an axis the type left unstated, so it has to land inside the bounds it did state.
        static float DeriveDefaultSize(float min, float max, float fallback)
        {
            float size = min > 0f ? min : fallback;
            return max > 0f ? Mathf.Min(size, max) : size;
        }

        // Concrete, closed Overlay subclasses only; anything else carrying the attribute is ignored.
        internal static bool IsOverlayType(Type type)
        {
            return type != null && !type.IsAbstract && !type.ContainsGenericParameters && typeof(Overlay).IsAssignableFrom(type);
        }

        // Returns null after logging when the constructor throws, so one broken extension does not hide the rest.
        internal static Overlay CreateOverlay(Type type, SkinningEditorOverlayAttribute attribute)
        {
            try
            {
                if (!(Activator.CreateInstance(type) is Overlay overlay))
                    return null;

                if (!string.IsNullOrEmpty(attribute?.displayName))
                    overlay.displayName = attribute.displayName;

                ApplyAttributeSizes(overlay, attribute);
                EnsureDefaultSize(overlay);

                return overlay;
            }
            catch (Exception e)
            {
                // One entry naming the type, unwrapped so the trace ends in the extension's constructor.
                Exception cause = (e as TargetInvocationException)?.InnerException ?? e;
                Debug.LogError($"Could not create the Skinning Editor overlay '{type.FullName}': {cause}");
                return null;
            }
        }
    }
}
