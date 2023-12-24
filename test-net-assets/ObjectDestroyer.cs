using Godot;

namespace TestAssets;

public partial class ObjectDestroyer : Area3D
{
	public override void _Ready()
	{
		BodyEntered += OnBodyEntered;
    }

	private static void OnBodyEntered(Node3D node)
	{
		if (node is RigidBody3D body)
		{
            body.QueueFree();
        }
    }
}