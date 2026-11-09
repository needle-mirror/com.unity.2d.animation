using System;
using UnityEngine;

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
    /// State the size bounds here or in the constructor - the canvas applies no defaults to a marked type.
    /// A size stated here wins; a type stating no default size at all is given one.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    internal sealed class SkinningEditorOverlayAttribute : Attribute
    {
        /// <summary>Name shown in the overlay menu and panel title. Defaults to the type name when null or empty.</summary>
        public string displayName { get; }

        /// <summary>Size the overlay opens at. Both axes must be set to take effect.</summary>
        public float defaultWidth { get; set; } = float.NegativeInfinity;

        /// <inheritdoc cref="defaultWidth"/>
        public float defaultHeight { get; set; } = float.NegativeInfinity;

        /// <summary>Smallest size the resize handles allow. Both axes must be set to take effect.</summary>
        public float minWidth { get; set; } = float.NegativeInfinity;

        /// <inheritdoc cref="minWidth"/>
        public float minHeight { get; set; } = float.NegativeInfinity;

        /// <summary>Largest size the resize handles allow. Both axes must be set to take effect.</summary>
        public float maxWidth { get; set; } = float.NegativeInfinity;

        /// <inheritdoc cref="maxWidth"/>
        public float maxHeight { get; set; } = float.NegativeInfinity;

        internal Vector2 defaultSize => new Vector2(defaultWidth, defaultHeight);
        internal Vector2 minSize => new Vector2(minWidth, minHeight);
        internal Vector2 maxSize => new Vector2(maxWidth, maxHeight);

        /// <param name="displayName">Name shown in the overlay menu and panel title.</param>
        public SkinningEditorOverlayAttribute(string displayName = null)
        {
            this.displayName = displayName;
        }
    }
}
