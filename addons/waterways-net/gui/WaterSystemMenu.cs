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
        GetPopup().Connect("id_pressed", Callable.From<int>(OnMenuItemSelected));
        GetPopup().AddItem("Generate System Maps");
    }

	public override void _ExitTree()
	{
		GetPopup().Disconnect("id_pressed", Callable.From<int>(OnMenuItemSelected));
    }

	private void OnMenuItemSelected(int index)
	{
		if (index == (int)RiverMenuType.GenerateSystemMaps)
        {
            EmitSignal("GenerateSystemMaps");
        }
    }
}
