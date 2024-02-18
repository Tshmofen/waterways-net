using Godot;

namespace Waterways;

[GlobalClass]
public partial class FloatingCube : RigidBody3D
{
    [Export] public RiverFloatSystem FloatSystem { get; set; }
    [Export] public float BuoyancyForce { get; set; } = 70.0f;
    [Export] public float UpCorrectingForce { get; set; } = 5.0f;
    [Export] public float FlowForce { get; set; } = 25.0f;
    [Export] public float WaterResistance { get; set; } = 5.0f;

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

    public override void _PhysicsProcess(double delta)
    {
        if (FloatSystem == null)
        {
            return;
        }

        var altitude = GlobalPosition.Y - FloatSystem.GetWaterHeight(GlobalPosition);
        if (altitude >= 0)
        {
            LinearDamp = 0;
            AngularDamp = 0;
            return;
        }

        var flow = FloatSystem.GetWaterFlow(GlobalPosition);
        ApplyCentralForce(Vector3.Up * BuoyancyForce * -altitude);
        var rotation = GetRotationCorrection();
        ApplyTorque(rotation * UpCorrectingForce);
        ApplyCentralForce(flow * FlowForce);
        LinearDamp = WaterResistance;
        AngularDamp = WaterResistance;
    }
}