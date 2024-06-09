using Godot;

namespace Waterways.Scripts;

[GlobalClass]
public partial class FloatSpawner : Node3D
{
    [Export] public PackedScene SpawnScene { get; set; }
    [Export] public RiverFloatSystem FloatSystem { get; set; }

    public override void _Input(InputEvent @event)
    {
        if (SpawnScene == null || FloatSystem == null)
        {
            return;
        }

        if (!Input.IsActionJustPressed("ui_select"))
        {
            return;
        }

        var cube = SpawnScene.Instantiate<FloatingCube>();
        Owner.AddChild(cube);

        cube.FloatSystem = FloatSystem;
        cube.GlobalPosition = GlobalPosition;
        cube.Rotation = new Vector3(GD.Randf() * float.Tau, GD.Randf() * float.Tau, GD.Randf() * float.Tau);
        cube.ApplyCentralImpulse(GlobalBasis.Z * -10.0f);
        cube.AngularVelocity = new Vector3((-0.5f + GD.Randf()) * 3.0f, (-0.5f + GD.Randf()) * 3.0f, (-0.5f + GD.Randf()) * 3.0f);
    }
}