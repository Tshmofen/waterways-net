
using Godot;

namespace Waterways.Gui;

[Tool]
public partial class WaterSystemMenu : MenuButton
{

	[Signal] public delegate void generate_system_mapsEventHandler();

    enum RIVER_MENU {
		GENERATE_SYSTEM_MAPS
	}

	public override void _EnterTree()
	{
		GetPopup().Clear();
        GetPopup().Connect("id_pressed", Callable.From<int>(_menu_item_selected));
        GetPopup().AddItem("Generate System Maps");
    }

	public override void _ExitTree()
	{
		GetPopup().Disconnect("id_pressed", Callable.From<int>(_menu_item_selected));
    }

	private void _menu_item_selected(int index)
	{
		if (index == (int)RIVER_MENU.GENERATE_SYSTEM_MAPS)
			EmitSignal("generate_system_maps");

    }
}
