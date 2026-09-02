using System;

namespace UnityEditor.U2D.Animation
{
    /// <summary>
    /// Marks an <see cref="UnityEditor.Overlays.Overlay"/> as a Skinning Editor overlay: it shows (toggleable) in the
    /// Sprite Editor window's overlay menu while the Skinning Editor is active, and is removed when it deactivates.
    /// </summary>
    /// <remarks>
    /// The marked type must be a concrete <see cref="UnityEditor.Overlays.Overlay"/> subclass with a parameterless constructor.
    /// The attribute is not inherited, so mark every type that should show up.
    /// One instance is created per type and reused across activations, so read the current sprite asset state when the panel
    /// acts rather than caching per-asset objects (e.g. bones).
    /// That instance is added to and removed from the window's canvas on every activation, so
    /// <see cref="UnityEditor.Overlays.Overlay.OnCreated"/> and <see cref="UnityEditor.Overlays.Overlay.OnWillBeDestroyed"/>
    /// run once per activation rather than once per instance: make their work idempotent.
    /// Each activation also replays a hidden-then-shown pair on <see cref="UnityEditor.Overlays.Overlay.displayedChanged"/>
    /// for a panel the user left open, so treat that as a refresh rather than a user toggle.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    internal sealed class SkinningEditorOverlayAttribute : Attribute
    {
        /// <summary>Name shown in the overlay menu and panel title. Defaults to the type name when null or empty.</summary>
        public string displayName { get; }

        /// <param name="displayName">Name shown in the overlay menu and panel title.</param>
        public SkinningEditorOverlayAttribute(string displayName = null)
        {
            this.displayName = displayName;
        }
    }
}
