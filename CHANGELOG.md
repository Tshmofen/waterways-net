Changelog
=========
1.0.0 - Major Code Rework
-----
- Completely re-structured and rewrote the systems to be split into several helper classes and utils.
- Updated `Gizmo` behavior to allow rivers selection, ensured correct axis mappings and made points adding be aware of nearest `Curve` end point.
- Made `Gizmo` path become transparent when node is not selected, instead of completely not showing it.
- Added option to recenter `RiverManager` node to average `Curve` points location.
- Moved shader property generation into a separate resource to improve main logic readability.
- Added `RiverManager` duplication support, now it automatically makes all necessary resources unique.
- Updated test scene to show possible waterfall setup.
- Fixed bug with `call deferred` message that was caused by Godot spawning several gizmos.
- `Lava` shader is removed as it is wasn't working properly.
- `Debug` shader is removed as redundant.

0.1.3
-----
- The `Buoyant` system and height baking is replaced with the new `FloatSystem`, see documents section for more info.
- `RiverGizmo` is now completely disappears when `RiverManager` node is not the last selected node.
- `RiverGizmo` the end and start points of the river is not connected anymore.
- Some GUI was removed because of redundancy.
- Flow/height baking is removed.
- Fixed not-immediate river updates.
- Updated test scenes (not included in the files below, see repo)
- In-general code refactoring and improvements.

0.1.2
-----
- Fixed `SystemMapRenderer` use of existing `World3D` that was leading to incorrect height map baking.
- Fixed baking raycasting that was leading to incorrect bake with terrains.

0.1.1
-----
- Fixed rare Godot crash on assembly rebuild.

0.1.0 - Initial .NET Rewrite (Version drop)
-----
- Rewritten GD version to .NET and ported to 4.0+ Godot convention

0.2.1 - Last GD version
-----
- New axis constraints for adding and moving river curve points (implemented by Winston)
- Built in Lava shader
- Improved inspector that dynamically parses built-in and custom shaders
- Curve controls for colour and transparency depth
- Improved refractions with objects in front of water removed
- Various bug fixes

0.2.0
-----
- Options to control the flow based on steepness, shore distance and pressure
- Added WaterSystem node for baking global height and flow maps
- Added Buoyant node to allow Rigid Bodies to float on rivers

0.1.0 - Initial release
-----
- Generate river meshes
- Bake flow and foam maps
