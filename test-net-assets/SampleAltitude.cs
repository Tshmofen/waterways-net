using Godot;
using Waterways;

namespace TestAssets;

[Tool]
public partial class SampleAltitude : Node3D
{
	[Export] public bool CheckAltitude
	{
		get => false;
        set
        {
			var waterSystem = GetParent().GetNode<WaterSystemManager>("WaterSystem");
			var alt = waterSystem.GetWaterAltitude(GlobalPosition);
			var flow = waterSystem.GetWaterFlow(GlobalPosition);
			GD.Print($"Water Altitude = {alt}");
			GD.Print($"Water Flow = {flow}");
        }
	}
}
