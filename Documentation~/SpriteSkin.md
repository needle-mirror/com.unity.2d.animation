# Sprite Skin component reference

When the Sprite Skin component is added to a GameObject that also contains the [Sprite Renderer](https://docs.unity3d.com/Manual/class-SpriteRenderer.html) component with a Sprite assigned, the Sprite Skin deforms that Sprite by using the bones that were [rigged](CharacterRig.md) and weighted to the Sprite in the [Skinning Editor](SkinningEditor.md).

After [preparing and importing](PreparingArtwork.md) your artwork into Unity, bring the generated Prefab into the Scene view and Unity automatically adds the Sprite Skin component to the Prefab. This component is required for the bones to deform the Sprite meshes in the Scene view.

The Sprite Skin deforms a Sprite by using GameObject Transforms to represent the bones that were added to the Sprite in the Skinning Editor module.

| Property | Description |
|-|-|
| **Always Update** | Enable this to have the Sprite Skin continue to deform the Sprite even when the visual is not in the view of the Camera. |
| **Auto Rebind** | Enable this to have the component attempt to find the correct GameObject Transforms to use as bones for the Sprite by using the GameObject Transform set in the **Root Bone** property as the starting point. For more information, refer to [Map bones to transforms automatically](Rebind.md). |
| **Bounds Mode** | Sets how Unity calculates the area the sprite occupies, also known as the bounds or the axis-aligned bounding box (AABB). Unity uses the bounds to determine whether to cull the sprite. The options are: <ul><li>**Vertex Based**: Calculates the bounds based on the vertices of the sprite mesh. As a result, bounds calculations take more time but culling is more accurate.</li><li>**Bone Based**: Calculates the bounds based on the positions of the bones. This option is faster but can result in more conservative culling. Only select this option if you also enable [calculating sprite deformations on the GPU](GPUDeformation.md). CPU deformation uses only vertex-based bounds and ignores other types of bounds calculations.</li></ul> |
| **Root Bone** | Use this property to indicate which GameObject Transform to use as the Root Bone for the Sprite. |
| **Bones** | This shows the list of bones that are being set up for the Sprite in the Skinning Editor module. Each Sprite’s **Bone** entry must have a GameObject Transform associated with it for correct deformation. |
| **Create Bones** | The button lets you create GameObject Transform(s) to represent the Sprite’s Bone and assign them to the **Root Bone** property and the individual Bones entry. The Root Bone that is created is placed as a child of the GameObject of the Sprite Skin. The button is only enabled if the Root Bone property isn't assigned. |
| **Reset Bind Pose** | The button resets the GameObject Transforms assigned in the Bones entry to the bind pose value set up for the Sprite in the Skinning Editor module. |
