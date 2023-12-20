using Godot;
using System;
using Waterway;
using Waterway.Gui;
using Waterways.Gui;

namespace Waterways;

[Tool]
public partial class WaterwaysPlugin : EditorPlugin
{
    public RiverGizmo river_gizmo = new();
    public InspectorPlugin gradient_inspector = new();

    private RiverControls _river_controls = new();
    private WaterSystemControls _water_system_controls = new();
    private dynamic _edited_node = null;
    private ProgressWindow _progress_window = null;
    private EditorSelection _editor_selection = null;
    private object _heightmap_renderer = null;
    private string _mode = "select";

    public RiverControls.CONSTRAINTS constraint = RiverControls.CONSTRAINTS.NONE;
    public bool local_editing = false;

    public override void _EnterTree()
    {
        AddCustomType("River", "Node3D", ResourceLoader.Load<Script>("./river_manager.gd"), ResourceLoader.Load<Texture2D>("./icons/river.svg"));
        AddCustomType("WaterSystem", "Node3D", ResourceLoader.Load<Script>("./water_system_manager.gd"), ResourceLoader.Load<Texture2D>("./icons/system.svg"));
        AddCustomType("Buoyant", "Node3D", ResourceLoader.Load<Script>("./buoyant_manager.gd"), ResourceLoader.Load<Texture2D>("./icons/buoyant.svg"));

        AddNode3DGizmoPlugin(river_gizmo);
        AddInspectorPlugin(gradient_inspector);

        river_gizmo.editor_plugin = this;
        _river_controls.Connect("mode", Callable.From<string>(_on_mode_change));
        _river_controls.Connect("options", Callable.From<string, int>(_on_option_change));
        _progress_window = ResourceLoader.Load<ProgressWindow>("./gui/progress_window.tscn");
        _river_controls.AddChild(_progress_window);

        _editor_selection = GetEditorInterface().GetSelection();
        _editor_selection.Connect("selection_changed", Callable.From(_on_selection_change));
        SceneChanged += _on_scene_changed;
        SceneClosed += _on_scene_closed;
    }

    private void _on_generate_flowmap_pressed()
    {
        _edited_node.bake_texture();
    }

    private void _on_generate_mesh_pressed()
    {
        _edited_node.spawn_mesh();
    }

    private void _on_debug_view_changed(int index)
    {
        _edited_node.debug_view = index;
    }

    private void _on_generate_system_maps_pressed()
    {
        _edited_node.generate_system_maps();
    }

    public override void _ExitTree()
    {
        RemoveCustomType("River");
        RemoveCustomType("Water System");
        RemoveCustomType("Buoyant");
        RemoveNode3DGizmoPlugin(river_gizmo);
        RemoveInspectorPlugin(gradient_inspector);

        _river_controls.Disconnect("mode", Callable.From<string>(_on_mode_change));
        _river_controls.Disconnect("options", Callable.From<string, int>(_on_option_change));
        _editor_selection.Disconnect("selection_changed", Callable.From(_on_selection_change));
        Disconnect("scene_changed", Callable.From<Node>(_on_scene_changed));
        Disconnect("scene_closed", Callable.From<string>(_on_scene_closed));

        _hide_river_control_panel();
        _hide_water_system_control_panel();
    }

    private bool _handles(Node3D node)
    {
        return node is RiverManager or WaterSystemManager;
    }

    private void _on_selection_change()
    {
        _editor_selection = GetEditorInterface().GetSelection();
        var selected = _editor_selection.GetSelectedNodes();

        if (selected.Count == 0)
        {
            return;
        }


        if (selected[0] is RiverManager)
        {
            _show_river_control_panel();
            _edited_node = selected[0] as RiverManager;
            _river_controls.menu.debug_view_menu_selected = _edited_node.debug_view;

            if (!_edited_node.IsConnected("progress_notified", Callable.From<float, string>(_river_progress_notified)))
            {
                _edited_node.Connect("progress_notified", Callable.From<float, string>(_river_progress_notified));
            }

            _hide_water_system_control_panel();
        }
        else if (selected[0] is WaterSystemManager)
        {
            // TODO - is there anything we need to add here?
            _show_water_system_control_panel();
            _edited_node = selected[0] as WaterSystemManager;
            _hide_river_control_panel();
        }
        else
        {
            GD.Print("_edited_node set to null");
            _edited_node = null;
            _hide_river_control_panel();
            _hide_water_system_control_panel();
        }
    }

    private void _on_scene_changed(Node _)
    {
        _hide_river_control_panel();
        _hide_water_system_control_panel();
    }

    private void _on_scene_closed(string _)
    {
        _hide_river_control_panel();
        _hide_water_system_control_panel();
    }

    private void _on_mode_change(string mode)
    {
        _mode = mode;
    }

    private void _on_option_change(string option, int value)
    {
        switch (option)
        {
            case "constraint":
                constraint = (RiverControls.CONSTRAINTS) value;
                if (constraint == RiverControls.CONSTRAINTS.COLLIDERS)
                {
                    WaterHelperMethods.reset_all_colliders(_edited_node.GetTree().Root);
                }

                break;
            case "local_mode":
                local_editing = value == 1;
                break;
        }
    }

    private int _forward_3d_gui_input(Camera3D camera, InputEvent @event)
    {
        if (_edited_node == null)
        {
            // TODO - This should be updated to the enum when it's fixed https://github.com/godotengine/godot/pull/64465
            return 0;
        }

        var global_transform = _edited_node.Transform;
        if (_edited_node.IsInsideTree())
        {
            global_transform = _edited_node.GlobalTransform;
        }

        var global_inverse = global_transform.AffineInverse();

        if (@event is InputEventMouseButton mouseEvent && mouseEvent.ButtonIndex == MouseButton.Left)
        {
            var ray_from = camera.ProjectRayOrigin(mouseEvent.Position);
            var ray_dir = camera.ProjectRayNormal(mouseEvent.Position);
            var g1 = global_inverse * (ray_from);
            var g2 = global_inverse * (ray_from + ray_dir * 4096);

            // Iterate through points to find closest segment
            var curve_points = _edited_node.get_curve_points();
            var closest_distance = 4096f;
            var closest_segment = -1;

            for (var point = 0; point < curve_points.Count; point++)
            {
                var p1 = curve_points[point];
                var p2 = curve_points[point + 1];
                var result = Geometry3D.GetClosestPointsBetweenSegments(p1, p2, g1, g2);
                var dist = result[0].DistanceTo(result[1]);

                if (dist < closest_distance)
                {
                    closest_distance = dist;
                    closest_segment = point;
                }
            }

            // Iterate through baked points to find the closest position on the curved path
            var baked_curve_points = _edited_node.curve.GetBakedPoints();
            var baked_closest_distance = 4096f;
            var baked_closest_point = new Vector3();
            var baked_point_found = false;

            for (var baked_point = 0; baked_point < baked_curve_points.Length; baked_point++)
            {
                var p1 = baked_curve_points[baked_point];
                var p2 = baked_curve_points[baked_point + 1];
                var result = Geometry3D.GetClosestPointsBetweenSegments(p1, p2, g1, g2);
                var dist = result[0].DistanceTo(result[1]);
                if (dist < 0.1 && dist < baked_closest_distance)
                {
                    baked_closest_distance = dist;
                    baked_closest_point = result[0];
                    baked_point_found = true;
                }
            }

            // In case we were close enough to a line segment to find a segment,
            // but not close enough to the curved line
            if (!baked_point_found)
            {
                closest_segment = -1;
            }

            // We'll use this closest point to add a point in between if on the line
            // and to remove if close to a point
            if (_mode == "select")
            {
                if (!mouseEvent.Pressed)
                {
                    river_gizmo.reset();
                }
                return 0;
            }

            if (_mode == "add" && !mouseEvent.Pressed)
            {
                // if we don't have a point on the line, we'll calculate a point
                // based of a plane of the last point of the curve
                if (closest_segment == -1)
                {

                    var end_pos = _edited_node.curve.GetPointPosition(_edited_node.curve.PointCount - 1);
                    var end_pos_global = _edited_node.ToGlobal(end_pos);

                    var z = _edited_node.curve.GetPointOut(_edited_node.curve.PointCount - 1).Normalized();
                    var x = z.Cross(Vector3.Down).Normalized();
                    var y = z.Cross(x).Normalized();
                    var _handle_base_transform = new Transform3D(
                        new Basis(x, y, z) * global_transform.Basis,
                        end_pos_global
                    );

                    var plane = new Plane(end_pos_global, end_pos_global + camera.Transform.Basis.X, end_pos_global + camera.Transform.Basis.Y);
                    ; var new_pos = Vector3.Zero;
                    if (constraint == RiverControls.CONSTRAINTS.COLLIDERS)
                    {
                        var space_state = _edited_node.GetWorld3D().DirectSpaceState;
                        var param = new PhysicsRayQueryParameters3D
                        {

                            From = ray_from,
                            To = ray_from + ray_dir * 4096
                        };
                        var result = space_state.IntersectRay(param);
                        if (result?.Count > 0)
                        {
                            new_pos = result["position"].AsVector3();
                        }
                        else
                        {
                            return 0;
                        }
                    }
                    else if (constraint == (int) RiverControls.CONSTRAINTS.NONE)
                    {
                        new_pos = plane.IntersectsRay(ray_from, ray_from + ray_dir * 4096).Value;
                    }
                    else if (RiverGizmo.AXIS_MAPPING.ContainsKey(constraint))
                    {
                        var axis = RiverGizmo.AXIS_MAPPING[constraint];
                        if (local_editing)
                        {
                            axis = _handle_base_transform.Basis * (axis);
                        }

                        var axis_from = end_pos_global + (axis * RiverGizmo.AXIS_CONSTRAINT_LENGTH);
                        var axis_to = end_pos_global - (axis * RiverGizmo.AXIS_CONSTRAINT_LENGTH);
                        var ray_to = ray_from + (ray_dir * RiverGizmo.AXIS_CONSTRAINT_LENGTH);
                        var result = Geometry3D.GetClosestPointsBetweenSegments(axis_from, axis_to, ray_from, ray_to);
                        new_pos = result[0];
                    }
                    else if (RiverGizmo.PLANE_MAPPING.ContainsKey(constraint))
                    {

                        var normal = RiverGizmo.PLANE_MAPPING[constraint];
                        if (local_editing)
                        {
                            normal = _handle_base_transform.Basis * (normal);
                        }
                        var projected = end_pos_global.Project(normal);
                        var direction = Mathf.Sign(projected.Dot(normal));
                        var distance = direction * projected.Length();
                        plane = new Plane(normal, distance);
                        new_pos = plane.IntersectsRay(ray_from, ray_dir) ?? Vector3.Zero;
                    }

                    baked_closest_point = _edited_node.ToLocal(new_pos);
                }

                var ur = GetUndoRedo();
                ur.CreateAction("Add River point");
                ur.AddDoMethod(_edited_node, "add_point", baked_closest_point, closest_segment);
                ur.AddDoMethod(_edited_node, "properties_changed");
                ur.AddDoMethod(_edited_node, "set_materials", "i_valid_flowmap", false);
                ur.AddDoProperty(_edited_node, "valid_flowmap", false);
                ur.AddDoMethod(_edited_node, "update_configuration_warnings");

                if (closest_segment == -1)
                {
                    ur.AddUndoMethod(_edited_node, "remove_point", _edited_node.curve.PointCount);
                }
                else
                {
                    ur.AddUndoMethod(_edited_node, "remove_point", closest_segment + 1);
                }

                ur.AddUndoMethod(_edited_node, "properties_changed");
                ur.AddUndoMethod(_edited_node, "set_materials", "i_valid_flowmap", _edited_node.valid_flowmap);
                ur.AddUndoProperty(_edited_node, "valid_flowmap", _edited_node.valid_flowmap);
                ur.AddUndoMethod(_edited_node, "update_configuration_warnings");
                ur.CommitAction();
            }

            if (_mode == "remove" && !mouseEvent.Pressed)
            {
                // A closest_segment of -1 means we didn't press close enough to a
                // point for it to be removed
                if (closest_segment != -1)
                {
                    var closest_index = _edited_node.get_closest_point_to(baked_closest_point);
                    var ur = GetUndoRedo();
                    ur.CreateAction("Remove River point");
                    ur.AddDoMethod(_edited_node, "remove_point", closest_index);
                    ur.AddDoMethod(_edited_node, "properties_changed");
                    ur.AddDoMethod(_edited_node, "set_materials", "i_valid_flowmap", false);
                    ur.AddDoProperty(_edited_node, "valid_flowmap", false);
                    ur.AddDoMethod(_edited_node, "update_configuration_warnings");

                    if (closest_index == _edited_node.curve.PointCount - 1)
                    {
                        ur.AddUndoMethod(_edited_node, "add_point", _edited_node.curve.GetPointPosition(closest_index), -1);
                    }
                    else
                    {
                        ur.AddUndoMethod(_edited_node, "add_point", _edited_node.curve.GetPointPosition(closest_index), closest_index - 1, _edited_node.curve.GetPointOut(closest_index), _edited_node.widths[closest_index]);
                    }

                    ur.AddUndoMethod(_edited_node, "properties_changed");
                    ur.AddUndoMethod(_edited_node, "set_materials", "i_valid_flowmap", _edited_node.valid_flowmap);
                    ur.AddUndoProperty(_edited_node, "valid_flowmap", _edited_node.valid_flowmap);
                    ur.AddUndoMethod(_edited_node, "update_configuration_warnings");
                    ur.CommitAction();
                }
            }

            // TODO - This should be updated to the enum when it's fixed https://github.com/godotengine/godot/pull/64465
            return 1;
        }
        else if (_edited_node is RiverManager)
        {
            // Forward input to river controls. This is cleaner than handling
            // the keybindings here as the keybindings need to interact with
            // the buttons. Handling it here would expose more private details
            // of the controls than needed, instead only the spatial_gui_input()
            // method needs to be exposed.
            // TODO - so this was returning a bool before? Check this
            return _river_controls.spatial_gui_input(@event) ? 1 : 0;
        }

        // TODO - This should be updated to the enum when it's fixed https://github.com/godotengine/godot/pull/64465
        return 0;
    }

    private void _river_progress_notified(float progress, string message)
    {
        if (message == "finished")
        {
            _progress_window.Hide();
        }
        else
        {
            if (!_progress_window.Visible)
            {
                _progress_window.PopupCentered();
            }

            _progress_window.show_progress(message, progress);
        }
    }

    private void _show_river_control_panel()
    {
        if (_river_controls.GetParent() == null)
        {
            AddControlToContainer(CustomControlContainer.SpatialEditorMenu, _river_controls);
            _river_controls.menu.Connect("generate_flowmap", Callable.From(_on_generate_flowmap_pressed));
            _river_controls.menu.Connect("generate_mesh", Callable.From(_on_generate_mesh_pressed));
            _river_controls.menu.Connect("debug_view_changed", Callable.From<int>(_on_debug_view_changed));
        }
    }

    private void _hide_river_control_panel()
    {

        if (_river_controls.GetParent() != null)
        {
            RemoveControlFromContainer(CustomControlContainer.SpatialEditorMenu, _river_controls);
            _river_controls.menu.Disconnect("generate_flowmap", Callable.From(_on_generate_flowmap_pressed));
            _river_controls.menu.Disconnect("generate_mesh", Callable.From(_on_generate_mesh_pressed));
            _river_controls.menu.Disconnect("debug_view_changed", Callable.From<int>(_on_debug_view_changed));

        }
    }

    private void _show_water_system_control_panel()
    {
        if (_water_system_controls.GetParent() == null)
        {
            AddControlToContainer(CustomControlContainer.SpatialEditorMenu, _water_system_controls);
            _water_system_controls.menu.Connect("generate_system_maps", Callable.From(_on_generate_system_maps_pressed));
        }
    }

    private void _hide_water_system_control_panel()
    {
        if (_water_system_controls.GetParent() != null)
        {
            RemoveControlFromContainer(CustomControlContainer.SpatialEditorMenu, _water_system_controls);
            _water_system_controls.menu.Disconnect("generate_system_maps", Callable.From(_on_generate_system_maps_pressed));
        }
    }
}