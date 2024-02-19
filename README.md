# Waterways .NET (Godot 4.0+)

![Waterways-NET Add-on for Godot 4](https://github.com/Tshmofen/waterways-net/blob/main/images/river_flow.gif)

It is a port to `Godot 4.0+`/`.NET` of a tool to generate river meshes with correct flow based on bezier curves. Visit the original page for more context and info: [https://github.com/Arnklit/Waterways](https://github.com/Arnklit/Waterways).  

Note that starting from `v0.1.3` I've changed some of the systems in `Waterways`, if you want the version with all the original features use branch [original-net](https://github.com/Tshmofen/waterways-net/tree/original-net). Though, this version is not supported and no fixes will be published for it in the future. 

Even though the original intention of the plugin was to implement flow baking I didn't really found it much useful, but the mesh generation and instruments to create rivers with nice-and-correct flow across the curve is amazing, so it shouldn't be a wonder why I got rid of baking.

Main Differences - [Original](https://github.com/Arnklit/Waterways)
---
* Flow/height baking is removed because of the many issues it was introducing.
  * Flow baking was generally leading to really messy/not-pleasant results and the default behavior where flow just follows the main curve is much more nice and understandable (it also allows for waterfall-like river bends, unlike the original bake).
  * Height baking was heavily dependent on some strange camera positions and wasn't enginereed well enough to work for all the possible river locations; more than that when I was testing it was providing really bad height calculations, leading to unsmooth river floating. It turned out easier for me to just replace it with more straight forward, but much more reliable collision shape generation with raycasting to acquire correct heights.
* Some GUI was removed because of redundancy. It wasn't giving much additional info for end-user, and was just increasing complexity of plugin as well as introducing some issues on assembly re-building. The only remaining GUI part (and the most important one) is the tools menu that is appearing when user clicks on `RiverManager` node.
* `RiverGizmo` is reworked to be not so distracting - now it completely disappears when `RiverManager` node is not the last selected node. Also, the end and start points of the river is not connected anymore and shouldn't be confusing anyone.
* The whole `Buoyant` system was removed and replaced with the new `FloatSystem` that will be providing *correct* water height and waterflow through public methods `GetWaterHeight` and `GetWaterFlowDirection`. Note, that now `GetWaterHeight` returns global height of the river at any given point instead of not-so-obvious altitude above water. You can find example of the integration with this system as well as some code for floating objects in the [test_net_assets](https://github.com/Tshmofen/waterways-net/tree/main/test_net_assets) folder.
* The special node for floating objects is not present anymore, I feel like just providing public interface to acquire flow direction and height is much more flexible, and the user can decide how to use it without any restrictions (or copy the code from my test implementation).
* And in-general code quality shoud be much improved. When it was being ported, a lot of code was rewritten to decrease complexity, improve readability, remove copy-paste and decrease the general scope of the plugin. I hope it is now much better, especially property generation and float system parts that were rebuilt from the ground (though, I'm still not happy with amount of stuff in `RiverManager`, but let's leave it for later).

#### No additional licenses applied, just do not forget to mention the original authors and, well, enjoy the cool rivers! :)

![Waterways-NET Add-on for Godot 4](https://github.com/Tshmofen/waterways-net/blob/main/images/river_test_editor.png)

Some Context
---
I needed some light-weight and convenient river system for my own little project, but the last version of this (and the only) one was only for `Godot 3.5`. So, I just took it in my hands, and ported the functionality for `Godot 4`, in the mean time completely rewriting it in C# (Because - why not?) 

I really appreciate the convinience and performace of C# over GDScript (considering my little bit of disslike for any python-like dynamic languages). So, here we are, feel free to use that version of the plugin without any additional license applied (except for the original one, unfortunately) and just enjoy this cool plugin. 

I may be back later to refactor it more or adapt specifically for my needs, so stay tuned.

Issues
---
Obviously, as it is a rewrite for another language, it may have introduced a few new bugs, just create issues here if anything happens, and I will see what I can do.

For now, it seems the only issue that remains is intermittent-appearing `Use deffered` error - it is not affecting anything and the plugin is working as expected whenever it popups in the log; probably will be fixed later when I have some time & mood to debug it.

Documentatation `RiverManager`
---
The river's parameters are split into 3 sections.

**Shape**
- `Step Length Divs` - How many subdivision the river will have per step along its length.
- `Step Width Divs` - How many subdivision the river will have along its width.
- `Smoothing` - How much the shape of the river is relaxed to even out corners.

**Material**
- `Shader Type` - This option allows you to select between the two built-in shaders `Water` and `Lava` or to use your own with `Custom`.

**The following parameters in the Material section are parsed from the current shader**

**Parameters shared by Water and Lava shader**
- `Normal` - Subcategory
  - `Normal Scale` - The strength of the normal mapping.
  - `Normal Bump Texture` - The pattern used for the water. RG channels hold the normal map and B holds a bump or height map used for the foam.
- `Flow` - Subcategory
    - *Speed* - How fast is the river flows.
    - *Base* - Base multiplier of the flow vectors.
    - *Steepness* - Flow vectors multiplied by the steepness of the river.
    - *Distance* - Flow vectors multiplied by the distance field for faster flows further away from shore.
    - *Pressure* - Flow vectors multiplied by a pressure map, to imitate the flow increasing when there is less available space in the river.
    - *Max* - Clamps the maximum multiplier of the flow vectors.
- `UV Scale` - The UV scaling used for textures on the river.
- `Roughness` - The roughness of the river surface, also affects the blurring that occurs in the refractions.
- `Edge Fade` - The distance the river fades out when it intesects other objects to give the shore line a softer look.

**Parameters specific to Water shader**
- `Albedo` - Subcategory
    - `Color First` - The shallow color of the water mixed based on the depth set in *Depth*.
    - `Color Second` - The deep color of the water mixed based on the depth set in *Depth*.
    - `Depth` - The water depth at which the far color of the gradient is returned.
    - `Depth Curve` - The interpolation curve used for the depth gradient.
- `Transparency` - Subcategory
    - `Clarity` - How far light can travel in the water before only returning the albedo color.
    - `epth Curve` - The interpolation curve used for the clarity depth.
    - `Refraction` - How much the background gets bent by the water shape.
- `Foam` - Subcategory
    - `Color` - The color of the foam.
    - `Ammount` - Controls the foam cutoff in the shader, you may have to use the foam baking setting to change the amount of foam further. See below.
    - `Steepness` - Gives the option to add in foam where the river is steep.
    - `Smoothness` - Controls how the foam layers are combined to give a sharper or softer look.

**Parameters specific to the Lava shader**
- `Emission` - Subcategory
    - `Color` - The two colous multiplied by the emission texture of the lava mixed based on the depth set in *Depth*.
    - `Depth` - The lava depth at which the far color of the gradient is returned.
    - `Depth Curve` - The interpolation curve used for the depth gradient.
    - `Texture` - The emission texture.

**Lod**
- `Lod0 Distance` - Controls the cutoff point for whether the shader samples textures twice to create an FBM effect for the waves and foam.

 Documentation `RiverFloatSystem`
---
- `Max Depth` - Max depth of the river, effectively it is the offset of the raycast above of the water. If water height is being checked below the river on `MaxDepth` distance it will just return `DefaulHeight`. 
- `Default Height` - Default height to be returned when there was no river below for given position or water height check was done too deep below the river (see `Max Depth` setting).
- `Flow Bake Interval` - `RiverFloatSystem` is using the original curve of the river, but it is containing too much baked points that are not really needed for flow calculations, this setting is used to create curve copy with bigger intervals for improved perfomance.
- `Float Layer` - Physics layer that is used for river collisions raycasting, just should be using any layer that is not used by anyone else (to prevent any interference).

`float GetWaterHeight(Vector3 globalPosition)` - When this method receives global position, it will do a raycast from it and return global height of the river for this point or `DefaultHeight` if river wasn't hit.

`Vector3 GetWaterFlowDirection(Vector3 globalPosition)` - When this method receives global position, it will find a nearest baked river point and will interpolate river flow direction from it. The `Vector3` being returned is normalized.

Notes
---
- It is not included here, but `test_net_assets` are actually using Zylann's `HeightMap terrain plugin`, so before accessing the test correctly you should [[download it]](https://github.com/Zylann/godot_heightmap_plugin) and place it in the `addons` folder. If something goes wrong, try use version `1.7.2`, that was used by me during development.

(c) Tshmofen - Timofey Ivanov, 2023
