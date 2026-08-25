# Customize bone colors in the Scene view

Use the Bone overlay to give an actor's bones different colors in the Scene view.

When you animate an actor, its bone gizmos overlap and its limbs often have similar bone chains, which makes it difficult to identify the bone you want to key. Assign each bone or bone chain its own color to tell them apart at a glance while you work in the **Scene** view and the [Animation window](https://docs.unity3d.com/6000.6/Documentation/Manual/AnimationEditorGuide.html).

Unity draws bone gizmos in white until you assign them a color.

> [!NOTE]
> The colors you assign in the **Bone** overlay apply to bone gizmos in the **Scene** view only. They are separate from the **Bone Color** property in the Skinning Editor, and from the outline colors in the [Skinning Editor preferences](ToolPref.md). For more information, refer to [Compare the bone colors of the 2D Animation package](#compare-the-bone-colors-of-the-2d-animation-package).

## Prerequisites

To display and color bone gizmos in the **Scene** view, make sure that all the following are true:

- Your project uses 2D Animation 16.0.0 or later.
- You created bones for your sprite in the [Skinning Editor](SkinningEditor.md) of the Sprite Editor window, then selected **Apply**. For more information, refer to [Actor rigging and weighting workflow](CharacterRig.md).
- The scene contains the rigged actor, and its [Sprite Skin component](SpriteSkin.md) has a GameObject Transform assigned to each entry in the **Bones** list. If the entries have no Transforms, refer to [Sprite Skin component reference](SpriteSkin.md) for the **Create Bones** and **Auto Rebind** properties.
- You enabled Gizmos in the **Scene** view. For more information, refer to [Gizmos menu](https://docs.unity3d.com/6000.6/Documentation/Manual/GizmosMenu.html).

To display bone gizmos, select an actor or one of its bones in the **Hierarchy** window or the **Scene** view. Unity then displays the gizmos of every bone in that actor, not only the bones you selected.

## Open the Bone overlay

Unity hides the **Bone** overlay by default.

To display the **Bone** overlay:

1. In the **Scene** view, press the `` ` `` shortcut key to open the **Overlay Menu**. Alternatively, open the **More** (**⋮**) menu at the upper right of the **Scene** view, then select **Overlays** > **Overlay Menu**.
2. In the **Overlay Menu**, under **2D Animation**, enable **Bone**.

The overlay docks at the bottom of the **Scene** view's right column. It sizes itself to fit its contents, so you can move or dock it but not resize it.

## Change the color of bones

To change the color of bones:

1. In the **Hierarchy** window or the **Scene** view, select the actor, the sprite, or the bones whose color you want to change. The overlay's **Color** property becomes available and displays the current color of the bones you selected. For information about which bones each type of selection includes, refer to [Which bones a selection includes](#which-bones-a-selection-includes).
2. Select **Color**, then choose a new color in the [color picker](https://docs.unity3d.com/6000.6/Documentation/Manual/InspectorColorPicker.html).

Unity applies the color to every bone you selected and updates the bone gizmos in the **Scene** view. Each change adds a single **Change Bone Color** entry to the Undo History window. To restore a bone's default appearance, set its color back to white.

Repeat these steps for each bone or bone chain you want to distinguish. You can change bone colors at any point while you animate an actor, because a bone's color affects only how Unity draws its gizmo. Unity doesn't store bone colors in your animation clips, and they never affect the values that you key.

> [!NOTE]
> Unity discards color changes you make in Play mode when you exit Play mode, as it does for all changes you make to a scene in Play mode.

### Which bones a selection includes

The bones that Unity colors depend on what you select:

| Selection | Bones included |
|-|-|
| An actor's root GameObject that has no Sprite Skin component | Every bone of every sprite that the [Sprite Skin components](SpriteSkin.md) on its immediate children deform. Select the root GameObject to color an entire actor at once. |
| A GameObject with a Sprite Skin component | Only the bones that deform that sprite. Select a single sprite to color the bones of one part of an actor, such as an arm. |
| A bone GameObject | Only that bone. Its child bones keep their own colors. |

> [!NOTE]
> If an actor's root GameObject has its own Sprite Skin component, Unity colors the bones of that Sprite Skin only, even when its immediate children have Sprite Skin components as well.

You can select more than one GameObject at a time. If the bones you selected have different colors, **Color** displays the mixed value state, and choosing a color replaces the color of every bone you selected.

**Color** is unavailable when your selection contains no bones that Unity can resolve, such as when you select a GameObject that's not part of a rigged actor.

## Where Unity stores bone colors

Unity stores an actor's bone colors on the GameObject that it resolves as the root of the rig. 

Where Unity saves that data depends on what you edit:

| What you edit | Where Unity saves the colors |
|-|-|
| A prefab instance in a scene | As an override on that instance, in the scene. Other instances of the prefab keep their own colors. To write the colors to the prefab itself, [apply the override](https://docs.unity3d.com/6000.6/Documentation/Manual/PrefabInstanceOverrides.html). |
| The prefab asset in prefab editing mode | In the prefab asset. Every instance of that prefab uses those colors, unless an instance has its own bone colors. |
| A GameObject that's not part of a prefab | In the scene that contains the actor. |

Save the scene or prefab to keep the colors you assign. Bone colors also behave as follows:

- Bone colors follow the bones of the rig rather than the GameObject hierarchy, so renaming a bone GameObject doesn't reset its color.
- Bone colors are an Editor-only authoring aid. Unity excludes them from Player builds, and they have no effect at runtime.

## Compare the bone colors of the 2D Animation package

The 2D Animation package provides three separate ways to color bones. The following table describes what each of them affects:

| Color | Where you set it | What it affects and where Unity stores it |
|-|-|-|
| Scene view bone color | The **Color** property of the **Bone** overlay. | The bone gizmos of the selected actor in the **Scene** view. For more information, refer to [Where Unity stores bone colors](#where-unity-stores-bone-colors). |
| Skinning Editor bone color | The **Bone Color** property of the [Bone panel](SkinEdToolsShortcuts.md#bone-panel), or the **Color** property of the [Bone tab](SpriteVis.md#bone-tab-and-hierarchy-tree) in the Sprite Visibility panel. | Bones in the Skinning Editor. Unity stores it in the sprite that you rig, so every actor that uses the sprite shares the color. |
| Selected outline color | The **Selected Outline Color** setting in the [Skinning Editor preferences](ToolPref.md). | The outline of a selected bone, in both the Skinning Editor and the **Scene** view. Unity stores it in your Editor preferences, so the setting applies to every project that you open. |

Setting one of these colors doesn't change the others. A bone that has a color in the Skinning Editor still displays in white in the **Scene** view until you assign it a color in the **Bone** overlay.

## Additional resources

- [Actor rigging and weighting workflow](CharacterRig.md)
- [Toggle the visibility of bones and sprites](SpriteVis.md)
- [Skinning Editor window reference](SkinEdToolsShortcuts.md)
- [Skinning Editor preferences](ToolPref.md)
- [Sprite Skin component reference](SpriteSkin.md)
- [Animating an actor](Animating-actor.md)
