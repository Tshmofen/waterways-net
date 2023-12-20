using Godot;

namespace Waterways;

[Tool]
public partial class BuoyantManager : Node3D
{
	[Export] public string water_system_group_name { get; set; } = "waterways_system";
	[Export] public float buoyancy_force { get; set; } = 50.0f;
	[Export] public float up_correcting_force { get; set; } = 5.0f;
	[Export] public float flow_force { get; set; } = 50.0f;
	[Export] public float water_resistance { get; set; } = 5.0f;

	private RigidBody3D _rb;
	private WaterSystemManager _system;
	private float _default_linear_damp = -1.0f;
	private float _default_angular_damp = -1.0f;


	public override void _EnterTree()
	{
		var parent = GetParent();
		if (parent is RigidBody3D)
		{
			_rb = parent as RigidBody3D;
			_default_linear_damp = _rb.LinearDamp;
			_default_angular_damp = _rb.AngularDamp;
        }
    }


    public override void _ExitTree()
    {
		_rb = null;
    }


	public override void _Ready()
	{
		var systems = GetTree().GetNodesInGroup(water_system_group_name);
		if (systems.Count > 0)
			if (systems[0] is WaterSystemManager)
				_system = systems[0] as WaterSystemManager;
    }


    public override string[] _GetConfigurationWarnings()
	{
		if (_rb == null)
			return ["Bouyant node must be a direct child of a RigidDynamicBody3D to function."];
		return [""];
    }

    private Vector3 _get_rotation_correction()
	{
		var rotation_transform = new Transform3D();
		var up_vector = GlobalTransform.Basis.Y;
		var angle = up_vector.AngleTo(Vector3.Up);
		if (angle < 0.1)
		{
			// Don't reaturn a rotation as object is almost upright, since the cross 
			// product at an angle that small might cause precission errors.
			return Vector3.Zero;
        }
		var cross = up_vector.Cross(Vector3.Up).Normalized();
		rotation_transform = rotation_transform.Rotated(cross, angle);
		return rotation_transform.Basis.GetEuler();
	}


	public override void _PhysicsProcess(double delta)
	{
		if (Engine.IsEditorHint() || _system == null || _rb == null)
		{
			return;
		}

		var altitude = _system.get_water_altitude(GlobalTransform.Origin);
		if (altitude < 0.0)
		{
			var flow = _system.get_water_flow(GlobalTransform.Origin);
			_rb.ApplyCentralForce(Vector3.Up * buoyancy_force * -altitude);
			var rot = _get_rotation_correction();
			_rb.ApplyTorque(rot * up_correcting_force);
			_rb.ApplyCentralForce(flow * flow_force);
			_rb.LinearDamp = water_resistance;
			_rb.AngularDamp = water_resistance;
		}
		else
		{
			_rb.LinearDamp = _default_linear_damp;
			_rb.AngularDamp = _default_angular_damp;
		}
    }
}
