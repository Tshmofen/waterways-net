﻿using Godot;

namespace Waterways;

[Tool]
public partial class BuoyantManager : Node3D
{
	[Export] public string WaterSystemGroupName { get; set; } = "waterways_system";
	[Export] public float BuoyancyForce { get; set; } = 50.0f;
	[Export] public float UpCorrectingForce { get; set; } = 5.0f;
	[Export] public float FlowForce { get; set; } = 50.0f;
	[Export] public float WaterResistance { get; set; } = 5.0f;

	private RigidBody3D _rb;
	private WaterSystemManager _system;
	private float _defaultLinearDamp = -1.0f;
	private float _defaultAngularDamp = -1.0f;

	public override void _EnterTree()
	{
        if (GetParent() is not RigidBody3D body3D)
        {
            return;
        }

        _rb = body3D;
        _defaultLinearDamp = _rb.LinearDamp;
        _defaultAngularDamp = _rb.AngularDamp;
    }

    public override void _ExitTree()
    {
		_rb = null;
    }

	public override void _Ready()
	{
		var systems = GetTree().GetNodesInGroup(WaterSystemGroupName);
        if (systems.Count > 0 && systems[0] is WaterSystemManager)
        {
            _system = systems[0] as WaterSystemManager;
        }
    }

    public override string[] _GetConfigurationWarnings()
	{
		if (_rb == null)
        {
            return ["Buoyant node must be a direct child of a RigidDynamicBody3D to function."];
        }

        return [string.Empty];
    }

	public override void _PhysicsProcess(double delta)
	{
		if (Engine.IsEditorHint() || _system == null || _rb == null)
		{
			return;
		}

		var altitude = _system.GetWaterAltitude(GlobalTransform.Origin);
		if (altitude < 0.0)
		{
			var flow = _system.GetWaterFlow(GlobalTransform.Origin);
			_rb.ApplyCentralForce(Vector3.Up * BuoyancyForce * -altitude);
			var rot = GetRotationCorrection();
			_rb.ApplyTorque(rot * UpCorrectingForce);
			_rb.ApplyCentralForce(flow * FlowForce);
			_rb.LinearDamp = WaterResistance;
			_rb.AngularDamp = WaterResistance;
		}
		else
		{
			_rb.LinearDamp = _defaultLinearDamp;
			_rb.AngularDamp = _defaultAngularDamp;
		}
    }

    private Vector3 GetRotationCorrection()
    {
        var rotationTransform = new Transform3D();
        var upVector = GlobalTransform.Basis.Y;
        var angle = upVector.AngleTo(Vector3.Up);

        if (angle < 0.1)
        {
            // Don't reaturn a rotation as object is almost upright, since the cross 
            // product at an angle that small might cause precission errors.
            return Vector3.Zero;
        }

        var cross = upVector.Cross(Vector3.Up).Normalized();
        rotationTransform = rotationTransform.Rotated(cross, angle);
        return rotationTransform.Basis.GetEuler();
    }
}
