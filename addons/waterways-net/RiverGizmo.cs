using Godot;
using System.Collections.Generic;
using Waterways.Gui;

namespace Waterways;

public partial class RiverGizmo : EditorNode3DGizmoPlugin
{
    public const int HANDLES_PER_POINT = 5;
    public const float AXIS_CONSTRAINT_LENGTH = 4096f;

    public static readonly IReadOnlyDictionary<RiverControls.CONSTRAINTS, Vector3> AXIS_MAPPING = new Dictionary<RiverControls.CONSTRAINTS, Vector3>  {
        { RiverControls.CONSTRAINTS.AXIS_X, Vector3.Right },
        { RiverControls.CONSTRAINTS.AXIS_Y, Vector3.Up },
        { RiverControls.CONSTRAINTS.AXIS_Z, Vector3.Back}
    };

    public static readonly IReadOnlyDictionary<RiverControls.CONSTRAINTS, Vector3> PLANE_MAPPING = new Dictionary<RiverControls.CONSTRAINTS, Vector3> {
        { RiverControls.CONSTRAINTS.PLANE_YZ, Vector3.Right },
        { RiverControls.CONSTRAINTS.PLANE_XZ, Vector3.Up },
        { RiverControls.CONSTRAINTS.PLANE_XY, Vector3.Back}
    };

    public WaterwaysPlugin editor_plugin;

    private Material _path_mat;
    private Material _handle_lines_mat;
    private Transform3D? _handle_base_transform;

    // Ensure that the width handle can't end up inside the center handle
    // as then it is hard to separate them again.
    const float MIN_DIST_TO_CENTER_HANDLE = 0.02f;

    public RiverGizmo()
	{
		// Two materials for every handle type.
		// 1) Transparent handle that is always shown.
		// 2) Opaque handle that is only shown above terrain (when passing depth test)
		// Note that this impacts the point index of the handles. See table below.
		CreateHandleMaterial("handles_center");
		CreateHandleMaterial("handles_control_points");
		CreateHandleMaterial("handles_width");
		CreateHandleMaterial("handles_center_with_depth");
		CreateHandleMaterial("handles_control_points_with_depth");
		CreateHandleMaterial("handles_width_with_depth");

		var handlesCenterMat = GetMaterial("handles_center");
		var handlesCenterMatWd = GetMaterial("handles_center_with_depth");
		var handlesControlPointsMat = GetMaterial("handles_control_points");
		var handlesControlPointsMatWd = GetMaterial("handles_control_points_with_depth");
		var handlesWidthMat = GetMaterial("handles_width");
		var handlesWidthMatWd = GetMaterial("handles_width_with_depth");

		handlesCenterMat.AlbedoColor = new Color(1.0f, 1.0f, 0.0f, 0.25f);
		handlesCenterMatWd.AlbedoColor = new Color(1.0f, 1.0f, 0.0f, 1.0f);
		handlesControlPointsMat.AlbedoColor = new Color(1.0f, 0.5f, 0.0f, 0.25f);
		handlesControlPointsMatWd.AlbedoColor = new Color(1.0f, 0.5f, 0.0f, 1.0f);
		handlesWidthMat.AlbedoColor = new Color(0.0f, 1.0f, 1.0f, 0.25f);
		handlesWidthMatWd.AlbedoColor = new Color(0.0f, 1.0f, 1.0f, 1.0f);

		handlesCenterMat.NoDepthTest = true;
		handlesCenterMatWd.NoDepthTest = false;
		handlesControlPointsMat.NoDepthTest = true;
		handlesControlPointsMatWd.NoDepthTest = false;
		handlesWidthMat.NoDepthTest = true;
		handlesWidthMatWd.NoDepthTest = false;

        var mat = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            NoDepthTest = true,
            AlbedoColor = new Color(1.0f, 1.0f, 0.0f),
            RenderPriority = 10
        };

        AddMaterial("path", mat);
		AddMaterial("handle_lines", mat);
    }

    public override string _GetGizmoName()
    {
		return "Waterways";
    }

	public void reset()
	{
		_handle_base_transform = null;
    }


	public string get_name()
	{
		return "RiverInput";
    }


    public override bool _HasGizmo(Node3D spatial)
    {
        return spatial is RiverManager;
    }

    // TODO - figure out of this new "secondary" bool should be used
    public override string _GetHandleName(EditorNode3DGizmo gizmo, int index, bool secondary)
    {
        return $"Handle {index}";
    }

    /* 
    Handles are pushed to separate handle lists, one per material (using gizmo.add_handles).
	A handle's "index" is given (by Godot) in order it was added to a gizmo. 
	Given that N = points in the curve:
	- First we add the center ("actual") curve handles, therefore
	  the handle's index is the same as the curve point's index.
	- Then we add the in and out points together. So the first curve point's IN handle
	  gets an index of N. The OUT handle gets N+1.
	- Finally the left/right indices come last, and the first curve point's LEFT is N * 3 .
	  (3 because there are three rows before the left/right indices)
	
	Examples for N = 2, 3, 4:
	curve points 2:0   1      3:0   1   2        4:0   1   2   3
	------------------------------------------------------------------
	center         0   1        0   1   2          0   1   2   3
	in             2   4        3   5   7          4   6   8   10
	out            3   5        4   6   8          5   7   9   11
	left           6   8        9   11  13         12  14  16  18
	right          7   9        10  12  14         13  15  17  19
	
	The following utility functions calculate to and from curve/handle indices.
	*/

	private bool _is_center_point(int index, int river_curve_point_count)
	{
		return index < river_curve_point_count;

    }
	private bool _is_control_point_in(int index, int river_curve_point_count)
	{
		if (index < river_curve_point_count)
			return false;

		if (index >= river_curve_point_count * 3)
			return false;

		return (index - river_curve_point_count) % 2 == 0;
    }

	private bool _is_control_point_out(int index, int river_curve_point_count)
    {
		if (index < river_curve_point_count)
			return false;

		if (index >= river_curve_point_count * 3)
			return false;

		return (index - river_curve_point_count) % 2 == 1;
    }

	private bool _is_width_point_left(int index, int river_curve_point_count)
	{
		if (index < river_curve_point_count * 3)
			return false;

		var res = (index - river_curve_point_count * 3) % 2 == 0;
		return res;

    }

	private bool _is_width_point_right(int index, int river_curve_point_count)
	{

        if (index < river_curve_point_count * 3)
			return false;

		var res = (index - river_curve_point_count * 3) % 2 == 1;

		return res;

    }

	private int _get_curve_index(int index, int point_count)
	{
		if (_is_center_point(index, point_count))
			return index;

        if (_is_control_point_in(index, point_count))
			return (index - point_count) / 2;

        if (_is_control_point_out(index, point_count))
			return (index - point_count - 1) / 2;

        if (_is_width_point_left(index, point_count) || _is_width_point_right(index, point_count))
			return (index - point_count * 3) / 2;

		return -1;
    }

    private int _get_point_index(int curve_index, bool is_center, bool is_cp_in, bool is_cp_out, bool is_width_left, bool is_width_right, int point_count)
    {
        if (is_center)
			return curve_index;

        if (is_cp_in)
			return point_count + curve_index * 2;

        if (is_cp_out)
			return point_count + 1 + curve_index * 2;

        if (is_width_left)
			return point_count * 3 + curve_index * 2;

        if (is_width_right)
			return point_count * 3 + 1 + curve_index * 2;

		return -1;
    }

    // TODO - figure out of this new "secondary" bool should be used
    public override Variant _GetHandleValue(EditorNode3DGizmo gizmo, int index, bool secondary)
    {
		var river = gizmo.GetNode3D() as RiverManager;
		var point_count = river.curve.PointCount;

		if (_is_center_point(index, point_count))
			return river.curve.GetPointPosition(_get_curve_index(index, point_count));

		if (_is_control_point_in(index, point_count))
			return river.curve.GetPointIn(_get_curve_index(index, point_count));

		if (_is_control_point_out(index, point_count))
			return river.curve.GetPointOut(_get_curve_index(index, point_count));

		if (_is_width_point_left(index, point_count) || _is_width_point_right(index, point_count))
			return river.widths[_get_curve_index(index, point_count)];

		return Variant.CreateFrom(-1);
    }

	// Called when handle is moved
	// TODO - figure out of this new "secondary" bool should be used
	public override void _SetHandle(EditorNode3DGizmo gizmo, int index, bool secondary, Camera3D camera, Vector2 point)
	{

		var river = gizmo.GetNode3D() as RiverManager;
		var space_state = river.GetWorld3D().DirectSpaceState;

		var global_transform = river.Transform;
		if (river.IsInsideTree())
		{
			global_transform = river.GlobalTransform;
        }

		var global_inverse = global_transform.AffineInverse();

		var ray_from = camera.ProjectRayOrigin(point);
		var ray_dir = camera.ProjectRayNormal(point);

		var old_pos = Vector3.Zero;
		var point_count = river.curve.PointCount;
		var p_index = _get_curve_index(index, point_count);
		var basePoint = river.curve.GetPointPosition(p_index);

		// Logic to move handles
		var is_center = _is_center_point(index, point_count);
		var is_cp_in = _is_control_point_in(index, point_count);
		var is_cp_out = _is_control_point_out(index, point_count);
		var is_width_left = _is_width_point_left(index, point_count);
		var is_width_right = _is_width_point_right(index, point_count);

		if (is_center)
			old_pos = basePoint;
		if (is_cp_in)
			old_pos = river.curve.GetPointIn(p_index) + basePoint;
		if (is_cp_out)
			old_pos = river.curve.GetPointOut(p_index) + basePoint;
		if (is_width_left)
			old_pos = basePoint + river.curve.GetPointOut(p_index).Cross(Vector3.Up).Normalized() * river.widths[p_index];
		if (is_width_right)
			old_pos = basePoint + river.curve.GetPointOut(p_index).Cross(Vector3.Down).Normalized() * river.widths[p_index];

		var old_pos_global = river.ToGlobal(old_pos);
	
		if (_handle_base_transform == null)
		{
			// This is the first set_handle() call since the last reset so we
			// use the current handle position as our _handle_base_transform
			var z = river.curve.GetPointOut(p_index).Normalized();
			var x = z.Cross(Vector3.Down).Normalized();
			var y = z.Cross(x).Normalized();
			_handle_base_transform = new Transform3D(
				new Basis(x, y, z) * global_transform.Basis,
				old_pos_global
			);
		}
	
		// Point, in and out handles
		if (is_center || is_cp_in || is_cp_out)
		{
			Vector3? new_pos = null;
		
			if (editor_plugin.constraint == RiverControls.CONSTRAINTS.COLLIDERS)
			{
				// TODO - make in / out handles snap to a plane based on the normal of
				// the raycast hit instead.
				var ray_params = new PhysicsRayQueryParameters3D();
				ray_params.From = ray_from;
				ray_params.To = ray_from + ray_dir * 4096;
				var result = space_state.IntersectRay(ray_params);
				if (result?.Count > 0)
					new_pos = result["position"].AsVector3();
			}
			else if (editor_plugin.constraint == RiverControls.CONSTRAINTS.NONE)
            {
				var plane = new Plane(old_pos_global, old_pos_global + camera.Transform.Basis.X, old_pos_global + camera.Transform.Basis.Y);
				new_pos = plane.IntersectsRay(ray_from, ray_dir);
            }
			else if (AXIS_MAPPING.ContainsKey(editor_plugin.constraint))
			{
				var axis = AXIS_MAPPING[editor_plugin.constraint];
				if (editor_plugin.local_editing)
					axis = _handle_base_transform.Value.Basis * (axis);
				var axis_from = old_pos_global + (axis * AXIS_CONSTRAINT_LENGTH);
				var axis_to = old_pos_global - (axis * AXIS_CONSTRAINT_LENGTH);
				var ray_to = ray_from + (ray_dir * AXIS_CONSTRAINT_LENGTH);
				var result = Geometry3D.GetClosestPointsBetweenSegments(axis_from, axis_to, ray_from, ray_to);
				new_pos = result[0];
			}
			else if (PLANE_MAPPING.ContainsKey(editor_plugin.constraint)) 
			{
				var normal = PLANE_MAPPING[editor_plugin.constraint];
				if (editor_plugin.local_editing)
					normal = _handle_base_transform.Value.Basis * (normal);
				var projected = old_pos_global.Project(normal);
				var direction = Mathf.Sign(projected.Dot(normal));
				var distance = direction * projected.Length();
				var plane = new Plane(normal, distance);
				new_pos = plane.IntersectsRay(ray_from, ray_dir);
			}
		
			// Discard if no valid position was found
			if (new_pos == null)
				return;

			// TODO: implement rounding when control is pressed.
			// How do we round when in local axis/plane mode?

			var new_pos_local = river.ToLocal(new_pos.Value);


			if (is_center)
			{
				river.set_curve_point_position(p_index, new_pos_local);
            }
			if (is_cp_in)
            {
				river.set_curve_point_in(p_index, new_pos_local - basePoint);
				river.set_curve_point_out(p_index, -(new_pos_local - basePoint));
            }
			if (is_cp_out)
			{
				river.set_curve_point_out(p_index, new_pos_local - basePoint);
                river.set_curve_point_in(p_index, -(new_pos_local - basePoint));
            }
		}

		// Widths handles
		if (is_width_left || is_width_right)
		{
			var p1 = basePoint;
			var p2 = Vector3.Zero;
			if (is_width_left)
				p2 = river.curve.GetPointOut(p_index).Cross(Vector3.Up).Normalized() * 4096;
			if (is_width_right)
				p2 = river.curve.GetPointOut(p_index).Cross(Vector3.Down).Normalized() * 4096;

			var g1 = global_inverse * (ray_from);
			var g2 = global_inverse * (ray_from + ray_dir * 4096);

			var geo_points = Geometry3D.GetClosestPointsBetweenSegments(p1, p2, g1, g2);
			var dir = geo_points[0].DistanceTo(basePoint) - old_pos.DistanceTo(basePoint);

			river.widths[p_index] += dir;

			// Ensure width handles don't end up inside the center point
			river.widths[p_index] = Mathf.Max(river.widths[p_index], MIN_DIST_TO_CENTER_HANDLE);
		}

		_Redraw(gizmo);
    }

    // Handle Undo / Redo of handle movements
    // TODO - figure out of this new "secondary" bool should be used
    public override void _CommitHandle(EditorNode3DGizmo gizmo, int index, bool secondary, Variant restore, bool cancel)
    {
		var river = gizmo.GetNode3D() as RiverManager;
		var point_count = river.curve.PointCount;

		var ur = editor_plugin.GetUndoRedo();
		ur.CreateAction("Change River Shape");

		var p_index = _get_curve_index(index, point_count);
		if (_is_center_point(index, point_count))
		{
			ur.AddDoMethod(river, "set_curve_point_position", p_index, river.curve.GetPointPosition(p_index));
			ur.AddUndoMethod(river, "set_curve_point_position", p_index, restore);
		}
		if (_is_control_point_in(index, point_count))
		{
			ur.AddDoMethod(river, "set_curve_point_in", p_index, river.curve.GetPointIn(p_index));
			ur.AddUndoMethod(river, "set_curve_point_in", p_index, restore);
			ur.AddDoMethod(river, "set_curve_point_out", p_index, river.curve.GetPointOut(p_index));
			ur.AddUndoMethod(river, "set_curve_point_out", p_index, -restore.AsSingle());
		}
		if (_is_control_point_out(index, point_count))
		{
			ur.AddDoMethod(river, "set_curve_point_out", p_index, river.curve.GetPointIn(p_index));
			ur.AddUndoMethod(river, "set_curve_point_out", p_index, restore);
			ur.AddDoMethod(river, "set_curve_point_in", p_index, river.curve.GetPointOut(p_index));
			ur.AddUndoMethod(river, "set_curve_point_in", p_index, -restore.AsSingle());
		}
		if (_is_width_point_left(index, point_count) || _is_width_point_right(index, point_count))
		{
			var river_widths_undo = river.widths.Duplicate(true);
			river_widths_undo[p_index] = restore.AsSingle();
			ur.AddDoProperty(river, "widths", river.widths);
			ur.AddUndoProperty(river, "widths", river_widths_undo);
		}

		ur.AddDoMethod(river, "properties_changed");
		ur.AddDoMethod(river, "set_materials", "i_valid_flowmap", false);
		ur.AddDoProperty(river, "valid_flowmap", false);
		ur.AddDoMethod(river, "update_configuration_warnings");
		ur.AddUndoMethod(river, "properties_changed");
		ur.AddUndoMethod(river, "set_materials", "i_valid_flowmap", river.valid_flowmap);
		ur.AddUndoProperty(river, "valid_flowmap", river.valid_flowmap);
		ur.AddUndoMethod(river, "update_configuration_warnings");
		ur.CommitAction();

		_Redraw(gizmo);
	}

	public override void _Redraw(EditorNode3DGizmo gizmo)
	{
		// Work around for issue where using "get_material" doesn't return a
		// material when redraw is being called manually from _set_handle()
		// so I'm caching the materials instead
		if (_path_mat == null)
			_path_mat = GetMaterial("path", gizmo);
		if (_handle_lines_mat == null)
			_handle_lines_mat = GetMaterial("handle_lines", gizmo);
		gizmo.Clear();

		var river = gizmo.GetNode3D() as RiverManager;
	
		if (!river.IsConnected("river_changed", Callable.From<EditorNode3DGizmo>(_Redraw)))
			river.river_changed += gizmo._Redraw;

		_draw_path(gizmo, river.curve);
		_draw_handles(gizmo, river);
	}
		

	private void _draw_path(EditorNode3DGizmo gizmo, Curve3D curve)
	{
		var path = new List<Vector3>();
		var baked_points = curve.GetBakedPoints();
	
		for (var i = 0; i < baked_points.Length; i++)
		{
			path.Add(baked_points[i]);
			path.Add(baked_points[i + 1]);
		}

		gizmo.AddLines(path.ToArray(), _path_mat);
	}

	private void _draw_handles(EditorNode3DGizmo gizmo, RiverManager river)
	{
		var lines = new List<Vector3>();
		var handles_center = new List<Vector3>();
        var handles_center_wd = new List<Vector3>();
        var handles_control_points = new List<Vector3>();
        var handles_control_points_wd = new List<Vector3>();
        var handles_width = new List<Vector3>();
        var handles_width_wd = new List<Vector3>();
		var point_count = river.curve.PointCount;

		for (var i = 0; i < point_count; i++)
		{
			var point_pos = river.curve.GetPointPosition(i);
			var point_pos_in = river.curve.GetPointIn(i) + point_pos;
			var point_pos_out = river.curve.GetPointOut(i) + point_pos;
			var point_width_pos_right = river.curve.GetPointPosition(i) + river.curve.GetPointOut(i).Cross(Vector3.Up).Normalized() * river.widths[i];
			var point_width_pos_left = river.curve.GetPointPosition(i) + river.curve.GetPointOut(i).Cross(Vector3.Down).Normalized() * river.widths[i];

			handles_center.Add(point_pos);
			handles_control_points.Add(point_pos_in);
			handles_control_points.Add(point_pos_out);
			handles_width.Add(point_width_pos_right);
			handles_width.Add(point_width_pos_left);

			lines.Add(point_pos);
			lines.Add(point_pos_in);
			lines.Add(point_pos);
			lines.Add(point_pos_out);
			lines.Add(point_pos);
			lines.Add(point_width_pos_right);
			lines.Add(point_pos);
			lines.Add(point_width_pos_left);
		}

		gizmo.AddLines(lines.ToArray(), _handle_lines_mat);

		// Add each handle twice, for both material types.
		// Needs to be grouped by material "type" since that's what influences the handle indices.
		gizmo.AddHandles(handles_center.ToArray(), GetMaterial("handles_center", gizmo), []);
		gizmo.AddHandles(handles_control_points.ToArray(), GetMaterial("handles_control_points", gizmo), []);
		gizmo.AddHandles(handles_width.ToArray(), GetMaterial("handles_width", gizmo), []);
		gizmo.AddHandles(handles_center.ToArray(), GetMaterial("handles_center_with_depth", gizmo), []);
		gizmo.AddHandles(handles_control_points.ToArray(), GetMaterial("handles_control_points_with_depth", gizmo), []);
		gizmo.AddHandles(handles_width.ToArray(), GetMaterial("handles_width_with_depth", gizmo), []);
    }
}