using Godot;

namespace Waterways.Gui;

[Tool]
public partial class RiverMenu : MenuButton
{
	[Signal] public delegate void generate_flowmapEventHandler();
	[Signal] public delegate void generate_meshEventHandler();
	[Signal] public delegate void debug_view_changedEventHandler();

	enum RIVER_MENU
	{
		GENERATE,
		GENERATE_MESH,
		DEBUG_VIEW_MENU
	}

	public int debug_view_menu_selected { get; set; }
	public PopupMenu _debug_view_menu { get; set; }


	public override void _EnterTree() 
	{
		GetPopup().Clear();
		GetPopup().Connect("id_pressed", Callable.From<int>(_menu_item_selected));
		GetPopup().AddItem("Generate Flow & Foam Map");
		GetPopup().AddItem("Generate MeshInstance3D Sibling");
		_debug_view_menu = new PopupMenu();
		_debug_view_menu.Name = "DebugViewMenu";
		_debug_view_menu.Connect("about_to_popup", Callable.From(_on_debug_view_menu_about_to_popup));
		_debug_view_menu.Connect("id_pressed", Callable.From<int>(_debug_menu_item_selected));
		GetPopup().AddChild(_debug_view_menu);
		GetPopup().AddSubmenuItem("Debug View", _debug_view_menu.Name);
	}

    public override void _ExitTree()
	{
		GetPopup().Disconnect("id_pressed", Callable.From<int>(_menu_item_selected));
		_debug_view_menu.Disconnect("about_to_popup", Callable.From(_on_debug_view_menu_about_to_popup));
		_debug_view_menu.Disconnect("id_pressed", Callable.From <int>(_debug_menu_item_selected));
    }


    private void _menu_item_selected(int index)
	{
		switch ((RIVER_MENU)index) 
		{
			case RIVER_MENU.GENERATE:
				EmitSignal("generate_flowmap");
                break;

			case RIVER_MENU.GENERATE_MESH:
                EmitSignal("generate_mesh");
                break;

			case RIVER_MENU.DEBUG_VIEW_MENU:
				break;
        }
	}

	private void _debug_menu_item_selected(int index)
	{
		debug_view_menu_selected = index;
		EmitSignal("debug_view_changed", index);
	}

	private void _on_debug_view_menu_about_to_popup()
	{
		_debug_view_menu.Clear();
		_debug_view_menu.AddRadioCheckItem("Display Normal");
		_debug_view_menu.AddRadioCheckItem("Display Debug Flow Map (RG)");
		_debug_view_menu.AddRadioCheckItem("Display Debug Foam Map (B)");
		_debug_view_menu.AddRadioCheckItem("Display Debug Noise Map (A)");
		_debug_view_menu.AddRadioCheckItem("Display Debug Distance Field Map (R)");
		_debug_view_menu.AddRadioCheckItem("Display Debug Pressure Map (G)");
		_debug_view_menu.AddRadioCheckItem("Display Debug Flow Pattern");
		_debug_view_menu.AddRadioCheckItem("Display Debug Flow Arrows");
		_debug_view_menu.AddRadioCheckItem("Display Debug Flow Strength");
		_debug_view_menu.AddRadioCheckItem("Display Debug Foam Mix");
		_debug_view_menu.SetItemChecked(debug_view_menu_selected, true);
    }
}