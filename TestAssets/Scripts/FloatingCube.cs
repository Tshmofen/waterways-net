using Godot;

namespace Waterways.Scripts;

[GlobalClass]
public partial class FloatingCube : RigidBody3D
{
    private static readonly float Gravity = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle();

    [Export] public RiverFloatSystem FloatSystem { get; set; }
    [Export] public float MaxEffectiveDepth { get; set; } = 2;
    [Export] public float WaterHeightOffset { get; set; } = 0.7f;
    [Export] public float FloatForce { get; set; } = 12;
    [Export] public float FlowForce { get; set; } = 100;
    [Export] public float WaterDrag { get; set; } = 0.05f;
    [Export] public float WaterAngularDrag { get; set; } = 0.05f;

    public override void _IntegrateForces(PhysicsDirectBodyState3D state)
    {
        if (FloatSystem == null)
        {
            return;
        }

        var depth = FloatSystem.GetWaterHeight(GlobalPosition) + WaterHeightOffset - GlobalPosition.Y;
        depth = Mathf.Clamp(depth, -1, MaxEffectiveDepth);

        if (depth <= 0)
        {
            return;
        }

        var gravity = Gravity * GravityScale;
        var floatForce = (Vector3.Up * gravity * depth * FloatForce) + (FloatSystem.GetWaterFlowDirection(GlobalPosition) * FlowForce);
        state.ApplyForce(floatForce);

        state.LinearVelocity *= 1 - WaterDrag;
        state.AngularVelocity *= 1 - WaterAngularDrag;
    }
}