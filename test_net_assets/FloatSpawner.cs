using Godot;
using Waterways;

namespace TestAssets;

[GlobalClass]
public partial class FloatSpawner : Node3D 
{
	private PackedScene _spawnObject;

    [Export(PropertyHint.File)] public string SpawnObjectPath { get; set; }
    [Export] public RiverFloatSystem FloatSystem { get; set; }

	public override void _Ready()
	{
		_spawnObject = ResourceLoader.Load<PackedScene>(SpawnObjectPath);
	}

	public override void _Process(double delta)
	{
		if (!Input.IsActionJustPressed("ui_select"))
		{
			return;
		}

		GD.Print("Spawned an object");
		var cube = _spawnObject.Instantiate<FloatingCube>();

		Owner.AddChild(cube);
		cube.FloatSystem = FloatSystem;
		cube.GlobalPosition = GlobalPosition;
		cube.Rotation = new Vector3(GD.Randf() * float.Tau, GD.Randf() * float.Tau, GD.Randf() * float.Tau);
		cube.ApplyCentralImpulse(GlobalBasis.Z * -10.0f);
		cube.AngularVelocity = new Vector3((-0.5f + GD.Randf()) * 3.0f, (-0.5f + GD.Randf()) * 3.0f, (-0.5f + GD.Randf()) * 3.0f);
    }
}