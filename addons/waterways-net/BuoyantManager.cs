using Godot;

namespace Waterways;

[Tool]
public partial class BuoyantManager : Node3D
{
    private RigidBody3D _body;
    private WaterSystemManager _system;
    private float _defaultLinearDamp = -1.0f;
    private float _defaultAngularDamp = -1.0f;

    [Export] public string WaterSystemGroupName { get; set; } = "waterways_system";
	[Export] public float BuoyancyForce { get; set; } = 50.0f;
	[Export] public float UpCorrectingForce { get; set; } = 5.0f;
	[Export] public float FlowForce { get; set; } = 50.0f;
	[Export] public float WaterResistance { get; set; } = 5.0f;

    #region Util

    private Vector3 GetRotationCorrection()
    {
        var rotationTransform = new Transform3D();
        var upVector = GlobalTransform.Basis.Y;
        var angle = upVector.AngleTo(Vector3.Up);

        if (angle < 0.1)
        {
            // Don't return a rotation as object is almost upright, since the cross 
            // product at an angle that small might cause precision errors.
            return Vector3.Zero;
        }

        var cross = upVector.Cross(Vector3.Up).Normalized();
        rotationTransform = rotationTransform.Rotated(cross, angle);
        return rotationTransform.Basis.GetEuler();
    }

    #endregion

    public override void _EnterTree()
	{
        if (GetParent() is not RigidBody3D body3D)
        {
            return;
        }

        _body = body3D;
        _defaultLinearDamp = _body.LinearDamp;
        _defaultAngularDamp = _body.AngularDamp;
    }

    public override void _ExitTree()
    {
		_body = null;
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
		if (_body == null)
        {
            return ["Buoyant node must be a direct child of a RigidDynamicBody3D to function."];
        }

        return [string.Empty];
    }

	public override void _PhysicsProcess(double delta)
	{
		if (Engine.IsEditorHint() || _system == null || _body == null)
		{
			return;
		}

		var altitude = _system.GetWaterAltitude(GlobalTransform.Origin);
		if (altitude < 0.0)
		{
			var flow = _system.GetWaterFlow(GlobalTransform.Origin);
			_body.ApplyCentralForce(Vector3.Up * BuoyancyForce * -altitude);
			var rot = GetRotationCorrection();
			_body.ApplyTorque(rot * UpCorrectingForce);
			_body.ApplyCentralForce(flow * FlowForce);
			_body.LinearDamp = WaterResistance;
			_body.AngularDamp = WaterResistance;
		}
		else
		{
			_body.LinearDamp = _defaultLinearDamp;
			_body.AngularDamp = _defaultAngularDamp;
		}
    }
}
