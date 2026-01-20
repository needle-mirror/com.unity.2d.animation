# Add new sprites to an existing rig
Add new sprites to an existing rig to use character rigs already set up for Sprite Swap.

By adding new sprites to your existing [character rigs](CharacterRig.md), you can update and maintain existing character rigs and sprite libraries with new sprites used for [Sprite Swap](SpriteSwapSetup.md).

## Adding new sprites to an existing rig for Sprite Swap
Follow the steps below to add new sprites to your existing character rigs.

1. Add the new artwork you want to use onto new layers in the .psb files you [imported](https://docs.unity3d.com/Packages/com.unity.2d.psdimporter@latest) for your character rig. Unity automatically updates the generated prefab from the imported .psb file by adding the new art assets as new sprite GameObjects in the **Hierarchy** window. 
2. Hide the newly added sprites in the **Hierarchy** window as they are visible by default and may cause visible clutter.
3. Open the [Sprite Library Asset](SL-Asset.md) used by your character rig in the [Sprite Library Editor](SL-Editor.md) (menu: **Window &gt; 2D &gt; Sprite Library Editor**) and add the newly imported sprites to the appropriate [Categories](SL-Editor.md#categories) and [Labels](SL-Editor.md#labels).
4. Open the **Sprite Editor** window (menu: **Window &gt; 2D &gt; Sprite Editor**) and select the [Skinning Editor](SkinEdToolsShortcuts.md) from the dropdown in the upper left of the editor. Then select the character prefab that you are editing to open the character rig in the **Skinning Editor**.

### Copy mesh data from an existing sprite to the new sprite
To copy the skeleton and mesh data from an existing sprite onto the new sprite you opened in the **Skinning Editor**:

1. Open the [Sprite Visibility panel](SpriteVis.md) in the Skinning Editor, and select the source sprite from which you want to copy the skeleton data.
2. Select **Copy Rig** from the [Rig tools panel](SkinEdToolsShortcuts.md#rig-tools) in the Skinning Editor to copy the mesh data of the selected sprite.
3. Select the new sprite you added and select **Paste Rig**. In the **Paste** panel that appears lower right of the editor window, first deselect **Bones** and then press **Paste**. This is to ensure additional bones are not added.
4. Repeat the [copy and paste](SkinEdToolsShortcuts.md#copy-and-paste-behavior) steps for each sprite as needed.

By following these steps, the newly added sprites will have the same weights and bone rigging as the previous sprites and can be easily used for sprite swapping.

## Additional resources
* [PSD Importer package](https://docs.unity3d.com/Packages/com.unity.2d.psdimporter@latest)
* [Setting up for Sprite Swap](SpriteSwapSetup.md)
* [Sprite Library Editor fundamentals](SL-Editor.md)