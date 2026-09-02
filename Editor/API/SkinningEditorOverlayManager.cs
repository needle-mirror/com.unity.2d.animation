using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor.Overlays;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace UnityEditor.U2D.Animation
{
    // Adds [SkinningEditorOverlay]-marked overlays to the Sprite Editor's OverlayCanvas while the Skinning module is active.
    internal static class SkinningEditorOverlayManager
    {
        // One instance per marked type, created on first use and reused, so panel state persists across activations.
        static readonly List<Overlay> s_Overlays = new();
        // The canvas the overlays are currently added to; null while the Skinning module is inactive.
        static OverlayCanvas s_Canvas;
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

                Overlay overlay = CreateOverlay(type, type.GetCustomAttribute<SkinningEditorOverlayAttribute>()?.displayName);
                if (overlay != null)
                    results.Add(overlay);
            }
        }

        // Concrete, closed Overlay subclasses only; anything else carrying the attribute is ignored.
        internal static bool IsOverlayType(Type type)
        {
            return type != null && !type.IsAbstract && !type.ContainsGenericParameters && typeof(Overlay).IsAssignableFrom(type);
        }

        // Returns null after logging when the constructor throws, so one broken extension does not hide the rest.
        internal static Overlay CreateOverlay(Type type, string displayName)
        {
            try
            {
                if (!(Activator.CreateInstance(type) is Overlay overlay))
                    return null;

                if (!string.IsNullOrEmpty(displayName))
                    overlay.displayName = displayName;

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
