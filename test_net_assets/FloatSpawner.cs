using Godot;

namespace TestAssets;

public partial class FloatSpawner : Node3D 
{
	private PackedScene _spawnObject;

    [Export(PropertyHint.File)] public string SpawnObjectPath { get; set; }

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
		var obj = _spawnObject.Instantiate<RigidBody3D>();

		Owner.AddChild(obj);
		obj.GlobalPosition = GlobalPosition;
		obj.Rotation = new Vector3(GD.Randf() * float.Tau, GD.Randf() * float.Tau, GD.Randf() * float.Tau);
		obj.ApplyCentralImpulse(GlobalBasis.Z * -10.0f);
		obj.AngularVelocity = new Vector3((-0.5f + GD.Randf()) * 3.0f, (-0.5f + GD.Randf()) * 3.0f, (-0.5f + GD.Randf()) * 3.0f);
    }
}