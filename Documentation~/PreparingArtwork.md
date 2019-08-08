# Preparing And Importing Artwork

It’s best practice to arrange and separate the individual parts of your character onto different Layers (see Example 1) when you design your character for animation. The [PSD Importer](https://docs.unity3d.com/Packages/com.unity.2d.psdimporter@latest/index.html?preview=1) automatically arranges the individual Layers into a Sprite Sheet layout (see Example 2).

When you import the graphic data from each Photoshop Layer, the Importer does the following:

1. Arranges/Mosaics the Layers into a Sprite Sheet layout.
2. Generates a Sprite from each Layer’s graphic data.

When an artist designs a character for animation (see Example 1), they usually manually separate and arrange the different parts of the character (see Example 2). The PSD Importer can generate a Prefab that reassembles the Sprites in their original positions as arranged in the PSB source file automatically (see Example 3), which makes it easier for you to begin animating the character.

Example 1: Layered character artwork in Adobe Photoshop.![Example 1: Layered character artwork in Adobe Photoshop](images/2DAnimationV2_PSDLayers.png)



Example 2: The arranged layers and the generated Prefab of the character.![Example 2: The arranged layers and the generated Prefab of the character.](images/2DAnimationV2_Mosaic_Prefab.png)

## Importer Settings

Prepare your character by separating the character's limbs and parts into separate layers, and arrange them in a default pose.

1. Save your artwork as a PSB file in Adobe Photoshop by selecting the __Large Document Format__ under the __Save As__ menu. You can convert an existing PSD file to PSB in the same way.

2. Import the PSB file into Unity as an Asset.

3. Select the Asset to bring up the [PSD Importer](https://docs.unity3d.com/Packages/com.unity.2d.psdimporter@latest/index.html?preview=1) Inspector window.

4. In the Inspector window, ensure the following settings are set:

   - Set Texture Type to __Sprite(2D and UI)__
   - Set Sprite Mode to __Multiple__
   - Check the __Mosaic __checkbox
   - Check the __Character Rig __checkbox
   - Check the __Use Layer Grouping__ checkbox if you want to preserve any Layer Groups in the PSB file 

   ![Importer Window settings](images/ImporterWindow.png)
   Importer Window settings

Click __Apply__ to apply the settings. Refer to the [PSD Importer](https://docs.unity3d.com/Packages/com.unity.2d.psdimporter@latest/index.html?preview=1) for more information about these settings.

