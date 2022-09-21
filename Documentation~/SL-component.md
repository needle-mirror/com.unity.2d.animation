# Sprite Library component in Unity

The **Sprite Library** component defines which [Sprite Library Asset](SL-Asset.md) a GameObject refers to at runtime. When you attach this component to a GameObject, the [Sprite Resolver component](SL-Resolver.md) attached to the same GameObject or child GameObject will refer to the Sprite Library Asset set by the Sprite Library component. This allows you to change the Sprite referenced by a [Sprite Renderer](https://docs.unity3d.com/Manual/class-SpriteRenderer) with the Sprite Resolver component.

## Sprite Library Inspector

In the Sprite Library component’s Inspector window, assign the desired Sprite Library Asset to the **Sprite Library Asset** property. Alternatively, select the **New** button to create and save a new Sprite Library Asset which is automatically assigned to the Sprite Library Asset field.

![](images/2D-animation-SLComp-New.png)

After assigning a Sprite Library Asset, the Inspector window shows a **Open Sprite Library Editor** button.

![](images/2D-animation-SLComp-Open.png)

To edit the contents of a Sprite Library Asset, use the [Sprite Library Editor window](SL-Editor.md).  

## Export Sprite Library Entries from the Component

In the case a Sprite Library Component contains Categories and Labels created in previous 2D Animation package versions, they can no longer be modified within the Inspector.

To save your work and continue editing these libraries, select the **Export to Sprite Library Asset** button that allows you to export them to an asset. The asset is then automatically assigned to the Sprite Library Asset property.

![](images/2D-animation-SLComp-Export.png)

## Additional resources
- [Swapping Sprite Library Assets](SLASwap.md)
- [Overrides to the Main Library](SL-Main-Library.md) 