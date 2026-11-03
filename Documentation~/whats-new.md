# What's new in the 2D Animation package

Discover new features and performance improvements in the latest updates to 2D Animation.

For more information, refer to the [changelog](../changelog/CHANGELOG.html).

## Version 17.0.0

### Sprite Skin added automatically when you drag a rigged sprite into a scene

When you drag a sprite that has deformation data into the Scene view or the Hierarchy window, Unity now adds a Sprite Skin component to the new GameObject for you. For more information, refer to [Sprite Skin component reference](SpriteSkin.md).

### Root Bone renamed to Root Transform

The Sprite Skin **Root Bone** property is now called **Root Transform**, because it's the Transform that Unity searches from when it maps bones during Auto Rebind, not a deformation bone. Existing scenes and prefabs keep their assigned value. The `SpriteSkin.rootBone` property and the `SpriteSkin.SetRootBone` method are obsolete; use `rootTransform` and `SetRootTransform` instead. For more information, refer to [Map bones to transforms automatically](Rebind.md).

### Consistent bone setup for single-sprite assets

The Skinning Editor now shows the mode toggle for assets that aren't in Character mode, and you can set up bones on a single-sprite asset without selecting the sprite first. For more information, refer to [Rig and weight an actor in the Skinning Editor](SkinningEditor.md).

### Preview Pose moves a bone without stretching its parent

When you move a bone with the Preview Pose tool, the parent bone no longer rotates and stretches to stay connected to it. Bones in the Skinning Editor now behave the same way as bone Transforms in the Scene view. For more information, refer to [Skinning Editor window reference](SkinEdToolsShortcuts.md).

## Version 16.0.0

### Mouseover highlighting in the Skinning Editor

In the Skinning Editor, sprites are now highlighted when you hover over them. For more information, refer to [Rig and weight an actor in the Skinning Editor](SkinningEditor.md).

### Bone colors in the Scene view

You can now use the Bone overlay to give an actor's bones different colors in the Scene view. Different colors make it easier to identify a bone while you animate an actor. For more information, refer to [Customize bone colors in the Scene view](BoneOverlay.md).

## Version 14.0.0

### Bone-based bounds calculation mode for Sprite Skin

You can now calculate the area a sprite occupies, also known as the bounds, based on the positions of the bones. This is faster than per-vertex calculations, but can result in more conservative culling.

Only select this option if you also enable [calculating sprite deformations on the GPU](GPUDeformation.md). CPU deformation uses only vertex-based bounds and ignores other types of bounds calculations.

For more information, refer to [Sprite Skin component reference](SpriteSkin.md).

## Version 13.0.1

### Paste sprite data between sprites with different sprite counts

When you copy and paste sprite data in the Skinning Editor, you can now paste between a source and destination that have different numbers of sprites. For more information, refer to [Rig and weight an actor in the Skinning Editor](SkinningEditor.md).

## Additional resources

- [Changelog](../changelog/CHANGELOG.html)
