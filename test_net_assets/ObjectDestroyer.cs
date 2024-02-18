using Godot;
using Waterways;

namespace TestAssets;

[GlobalClass]
public partial class ObjectDestroyer : Area3D
{
	public override void _Ready()
	{
		BodyEntered += OnBodyEntered;
    }

	private static void OnBodyEntered(Node3D node)
	{
		if (node is FloatingCube cube)
		{
            cube.QueueFree();
        }
    }
}
