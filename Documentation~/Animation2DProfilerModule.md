# 2D Animation Profiler module reference

Explore the statistics in the 2D Animation module to find how much work Unity does to deform the Sprite Skin components in your project.

The 2D Animation module in the [Profiler window](https://docs.unity3d.com/6000.6/Documentation/Manual/ProfilerWindow.html) displays how many Sprite Skin components Unity deforms on the CPU and on the GPU. It also displays how many vertices and bones Unity processes, and how long the deformation takes. The module is available from 2D Animation 16 (Unity 6.6).

The module requires the [Unity Profiling Core](https://docs.unity3d.com/Packages/com.unity.profiling.core@latest) package, version 1.0.3 or later, which the 2D Animation package doesn't install for you. If your project doesn't contain this package, the details pane displays a message instead of any data. To add the package, refer to [Install a package from a registry](https://docs.unity3d.com/6000.6/Documentation/Manual/upm-ui-install.html).

Unity collects data for this module only while the Profiler records, and only in the Unity Editor or in a development build.

To open the Profiler module, follow these steps:

1. In the main menu, go to **Window** > **Analysis** > **Profiler** to open the [Profiler window](https://docs.unity3d.com/6000.6/Documentation/Manual/ProfilerWindow.html).
2. Select **Profiler Modules**, then enable **2D Animation**.
3. Select **Record**, then enter Play mode to profile your application.
4. Select a frame in the chart to display the details for that frame in the module details pane.

> [!NOTE]
> The details panes don't update while the Profiler records unless you enable **Live Update** in the **Sprite Skin Statistics** pane.

## Chart categories

The 2D Animation module's chart tracks the work Unity does to deform Sprite Skin components. Unity records each of the following values as a counter in the **U2D** category, and resets the counter at the end of every frame, so each value describes a single frame.

Unity deforms a Sprite Skin component only when the bone transforms that it uses change. This means that these values don't include Sprite Skin components that are visible but not moving.

| **Chart** | **Description** |
|---|---|
| **SpriteSkin Processed (CPU)** | Displays the number of Sprite Skin components that use CPU deformation and that Unity deforms in the frame. |
| **Vertices Processed (CPU)** | Displays the total number of vertices that Unity deforms on the CPU in the frame. |
| **SpriteSkin Processed (GPU)** | Displays the number of Sprite Skin components that use GPU deformation and that Unity deforms in the frame. |
| **Vertices Processed (GPU)** | Displays the total number of vertices that Unity deforms on the GPU in the frame. |
| **Bones Transformed** | Displays the number of bone transforms that change in the frame. |

## Module details pane

The details pane of the 2D Animation module contains the **Sprite Skin Statistics** pane and the Sprite Skin hierarchy.

### Sprite Skin Statistics

The **Sprite Skin Statistics** pane contains the counter values for the frame you select in the chart, together with the time Unity spends on deformation in that frame.

| **Property** | **Description** |
|---|---|
| **Live Update** | Enable this property to update the details panes while the Profiler records. If you disable this property, both panes update only when you select a frame in the chart. |
| **SpriteSkin Processed (CPU)** | Displays the number of Sprite Skin components that use CPU deformation and that Unity deforms in the selected frame. |
| **Vertices Processed (CPU)** | Displays the total number of vertices that Unity deforms on the CPU in the selected frame. |
| **SpriteSkin Processed (GPU)** | Displays the number of Sprite Skin components that use GPU deformation and that Unity deforms in the selected frame. |
| **Vertices Processed (GPU)** | Displays the total number of vertices that Unity deforms on the GPU in the selected frame. |
| **Bone Transformed** | Displays the number of bone transforms that change in the selected frame. |
| **Deformation Time** | Displays the time in milliseconds that Unity spends deforming all the Sprite Skin components in the selected frame. |

### Sprite Skin hierarchy

The Sprite Skin hierarchy lists every active and enabled Sprite Skin component that Unity tracks for deformation in the frame you select in the chart, including the components that Unity doesn't deform in that frame. Each top-level row is the GameObject that one or more Sprite Skin components use as their **Root Transform**. Expand a row to display the individual Sprite Skin components that share that root. A Sprite Skin component without a **Root Transform** appears as a top-level row of its own.

| **Column** | **Description** |
|---|---|
| **Name** | Displays the name of the GameObject. |
| **Bone Count** | Displays the number of bone transforms a Sprite Skin component uses. For a **Root Transform** row, displays the total number of bone transforms that all the Sprite Skin components under it use. |
| **Deformation** | Displays the method Unity uses to deform the Sprite Skin components under this **Root Transform**, either **CPU** or **GPU**. This column is empty for the individual Sprite Skin component rows. |

To sort the rows, select a column heading. To select the corresponding GameObject in the [Hierarchy window](https://docs.unity3d.com/6000.6/Documentation/Manual/Hierarchy.html), select a row.

If the frame you select contains no active and enabled Sprite Skin components, the hierarchy displays **No data to show. Select another frame.** instead of the table.

## Additional resources

- [Calculate sprite deformation on the GPU](GPUDeformation.md)
- [Sprite Skin component reference](SpriteSkin.md)
- [Profiler modules introduction](https://docs.unity3d.com/6000.6/Documentation/Manual/profiler-modules-introduction.html)
- [CPU Usage Profiler module reference](https://docs.unity3d.com/6000.6/Documentation/Manual/ProfilerCPU.html)
