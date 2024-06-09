# Godot 4 Waterways (.NET)

<p align="center">
 <img height=260 src="https://github.com/Tshmofen/waterways-net/blob/main/Images/river_flow.gif"/>
 <img height=260 src="https://github.com/Tshmofen/waterways-net/blob/main/Images/waterfall.gif"/>
</p>

It is a port to `Godot 4`/`.NET 8.0` of a tool to generate river meshes with correct flow based on bezier curves. Visit the original page if you are interested in also using baking in the river, though, only for Godot 3: [https://github.com/Arnklit/Waterways](https://github.com/Arnklit/Waterways).  

Note that starting from `v0.1.3` I've changed and removed some of the systems in `Waterways`, if you want the version with all the original features - use branch [original-net](https://github.com/Tshmofen/waterways-net/tree/original-net). Though, this version is not supported anymore and no fixes will be published for it in the future. 

Even though the intention of the original plugin was to implement flow baking I didn't really found it much useful - mesh generation and the instruments to create rivers with nice-and-correct flow across the curve is much more interesting for me, so that's the main focus of this particular addon.

Main Differences
---
* **The baking is removed** because of the many issues I was experiencing in using it.
  * Flow baking was generally leading to really messy/not-pleasant results and the default behavior where flow just follows the main curve is much more nice IMHO, the pure flow is also allows for waterfall-like river bends, unlike the original bake result.
  * Height baking was heavily dependent on some strange camera positions and wasn't enginereed well enough to work for all the possible river locations. Besides that that when I was testing it was providing really bad height calculations, leading to unsmooth river floating. It turned out easier to just replace it with more straight forward, but much more reliable collision shape generation with raycasting to acquire correct heights.
* Most of the GUI was removed because of the redundancy. The only remaining GUI part (and the most important one) is **the tools menu** that is appearing when user clicks on `RiverManager` node.
* `RiverGizmo` is reworked to be much more **user-friendly**.
  * The river path is now transparent when is not selected and doesn't loop.
  * New nodes are being added to either `end` or `start` curve point depending on the distance, instead of always being added to the end.
  * Rivers are now actually selectable, you don't need to search for it in the editor tree anymore.
* Another big improvement is the **support of nodes duplication**, you can just now copy the `RiverManager` and it will do everything to create a complete copy automatically.
* The whole `Buoyant` system was removed and replaced with **the new `FloatSystem`** that will be providing *correct* water height and waterflow through public methods `GetWaterHeight` and `GetWaterFlowDirection`. Note, that now `GetWaterHeight` returns global height of the river at any given point instead of not-so-obvious altitude above water. You can find example of the integration with this system as well as some code for floating objects in the [test_net_assets](https://github.com/Tshmofen/waterways-net/tree/main/TestAssets) folder.
* `Lava` and `Debug` **shaders are removed**.
  * `Lava` shader just wasn't working for `Godot 4` and I'm not really interested in porting it.
  * `Deubg` shader was mostly useful for baking, and now doesn't provide any useful insights. 
* The special node for **floating objects is not present anymore**, I feel like just providing public interface to acquire flow direction and height is much more flexible, and the user can decide how to use it without any restrictions (or copy the code from the test implementation).
* **Whole plugin is now implemented using `C#`**. I have rewritten it completely, and hope the code quality is now much better. A lot of things were incapsulated in own special classes to decrease complexity, improve readbility, remove any copy-paste and just decrese the general scope of the plugin.

#### No additional licenses applied, just do not forget to mention the original authors and, well, enjoy the cool rivers! :)

![Waterways-NET Add-on for Godot 4](https://github.com/Tshmofen/waterways-net/blob/main/Images/river_test_editor.png)

# FAQ
* Addon is throwing errors when I'm trying to enable it, what should I do?
  * It's likely the aftermath of using .NET version of Godot. First of all be sure that you've created a solution with `Project` -> `Tools` -> `C#` -> `Create C# Solution` and make sure that it is using `.NET 8.0` (in `.csproj`). After that just build the project and it will generate .NET binaries, now reload Godot and enable the addon - it should now read the scripts correctly.

# Notes
- It is not included here, but `test_net_assets` are actually using Zylann's `HeightMap terrain plugin`, so before accessing the test correctly you should [[download it]](https://github.com/Zylann/godot_heightmap_plugin) and place it in the `addons` folder. If something goes wrong, try use version `1.7.2`, that was used by me during development.
- Test assets will likely will not work on Godot 4.0, because of `[GlobalClass]` annotations. You can just remove them and rebuild to make it compatible again, just new nodes will not be visible in the search. 

# Documentatation

## RiverManager
### Main
| Export variable | How it works |
|--|--|
| Shader Settings        | The resource that is providing access to current shader parameters. See standard shader description in the next table. |
| Shape Step Length Divs | How many subdivision the river will have per step along its length. |
| Shape Step Width Divs  | How many subdivision the river will have along its width. |
| Shape Smoothness       | How much the shape of the river is relaxed to even out the corners. |
| Point Widths           | The array containing the values of widths associated with each `Curve` point. (Usually should not be modified) |
| Curve                  | The internal `Curve` used by river to store all the positions and additional data. (Usually should not be modified) |

### Shader settings
**Most parameters in the settings section are parsed from the current shader**, the following data describes standard `Water` shader. 

| Export variable | How it works |
|--|--|
| **Material**        | _-- Subcategory --_ |
| Shader Type         | This option allows you to select between built-in `Water` shader, empty `None` shader or your own `Custom` shader. |
| Custom Shader       | Any custom shader you want to use for the river that will be parsed into this settings resource. |
| **Albedo**          | _-- Subcategory --_ |
| Color First         | The shallow color of the water mixed based on the depth set in *Depth*. |
| Color Second        | The deep color of the water mixed based on the depth set in *Depth*. |
| Depth               | The water depth at which the far color of the gradient is returned. |
| Depth Curve         | The interpolation curve used for the depth gradient. |
| **Transparency**    | _-- Subcategory --_ |
| Clarity             | How far light can travel in the water before only returning the albedo color. |
| Depth Curve         | The interpolation curve used for the clarity depth. |
| Refraction          | How much the background gets bent by the water shape. |
| **Normal**          | _-- Subcategory --_ |
| Normal Scale        | The strength of the normal mapping. |
| Normal Bump Texture | The pattern used for the water. RG channels hold the normal map and B holds a bump or height map used for the foam. |
| **Flow**            | _-- Subcategory --_ |
| Speed               | How fast is the river flows. |
| Base                | Base multiplier of the flow vectors. |
| Steepness           | Flow vectors multiplied by the steepness of the river. |
| Distance            | Flow vectors multiplied by the distance field for faster flows further away from shore. |
| Pressure            | Flow vectors multiplied by a pressure map, to imitate the flow increasing when there is less available space in the river. |
| Max                 | Clamps the maximum multiplier of the flow vectors. |
| **Other**           |  _-- Subcategory --_ |
| UV Scale            | The UV scaling used for textures on the river. |
| Roughness           | The roughness of the river surface, also affects the blurring that occurs in the refractions. |
| Edge Fade           | The distance the river fades out when it intesects other objects to give the shore line a softer look. |
| **Foam**            | _-- Subcategory --_ |
| Color               | The color of the foam. |
| Ammount             | Controls the foam cutoff in the shader, you may have to use the foam baking setting to change the amount of foam further. See below. |
| Steepness           | Gives the option to add in foam where the river is steep. |
| Smoothness          | Controls how the foam layers are combined to give a sharper or softer look. |
| **LOD**             | _-- Subcategory --_ |
| Lod0 Distance       | Controls the cutoff point for whether the shader samples textures twice to create an FBM effect for the waves and foam. |

### RiverFloatSystem
| Export variable | How it works |
|--|--|
| Max Depth | Max depth of the river, effectively it is the offset of the raycast above of the water. If water height is being checked below the river on `MaxDepth` distance it will just return `DefaulHeight`.  |
| Default Height | Default height to be returned when there was no river below for given position or water height check was done too deep below the river (see `Max Depth` setting). |
| Flow Bake Interval | `RiverFloatSystem` is using the original curve of the river, but it is containing too much baked points that are not really needed for flow calculations, this setting is used to create curve copy with bigger intervals for improved perfomance. |
| Float Layer | Physics layer that is used for river collisions raycasting, just should be using any layer that is not used by anyone else (to prevent any interference). |

| Public Method | How it works |
|--|--|
| **float&nbsp;GetWaterHeight(Vector3&nbsp;globalPosition)** | When this method receives global position, it will do a raycast from it and return global height of the river for this point or `DefaultHeight` if river wasn't hit. |
| **Vector3&nbsp;GetWaterFlowDirection(Vector3&nbsp;globalPosition)** |  When this method receives global position, it will find a nearest baked river point and will interpolate river flow direction from it. The `Vector3` being returned is normalized. |

###### That's it, thanks for reading! (c) Tshmofen / Timofey Ivanov
