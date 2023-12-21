using Godot;

namespace Waterways.Gui;

[Tool]
public partial class WaterSystemMenu : MenuButton
{
	[Signal] public delegate void GenerateSystemMapsEventHandler();

    public enum RiverMenuType
	{
		GenerateSystemMaps
	}

	public override void _EnterTree()
	{
		GetPopup().Clear();
        GetPopup().IdPressed += OnMenuItemSelected;
        GetPopup().AddItem("Generate System Maps");
    }

	public override void _ExitTree()
	{
		GetPopup().IdPressed -= OnMenuItemSelected;
    }

	private void OnMenuItemSelected(long index)
	{
		if (index == (int)RiverMenuType.GenerateSystemMaps)
        {
            EmitSignal("GenerateSystemMaps");
        }
    }
}
