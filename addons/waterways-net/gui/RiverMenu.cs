using Godot;

namespace Waterways.Gui;

[Tool]
public partial class RiverMenu : MenuButton
{
    [Signal] public delegate void GenerateFlowmapEventHandler();
    [Signal] public delegate void GenerateMeshEventHandler();
    [Signal] public delegate void DebugViewChangedEventHandler();

    public enum RiverMenuType
    {
        Generate,
        GenerateMesh,
        DebugViewMenu
    }

    public int DebugViewMenuSelected { get; set; }
    public PopupMenu DebugViewMenu { get; set; }

    public override void _EnterTree()
    {
        GetPopup().Clear();
        GetPopup().Connect("id_pressed", Callable.From<int>(OnMenuItemSelected));
        GetPopup().AddItem("Generate Flow & Foam Map");
        GetPopup().AddItem("Generate MeshInstance3D Sibling");

        DebugViewMenu = new PopupMenu
        {
            Name = "DebugViewMenu"
        };
        DebugViewMenu.Connect("about_to_popup", Callable.From(OnDebugViewMenuAboutToPopup));
        DebugViewMenu.Connect("id_pressed", Callable.From<int>(OnDebugMenuItemSelected));

        GetPopup().AddChild(DebugViewMenu);
        GetPopup().AddSubmenuItem("Debug View", DebugViewMenu.Name);
    }

    public override void _ExitTree()
    {
        GetPopup().Disconnect("id_pressed", Callable.From<int>(OnMenuItemSelected));
        DebugViewMenu.Disconnect("about_to_popup", Callable.From(OnDebugViewMenuAboutToPopup));
        DebugViewMenu.Disconnect("id_pressed", Callable.From<int>(OnDebugMenuItemSelected));
    }

    private void OnMenuItemSelected(int index)
    {
        switch ((RiverMenuType)index)
        {
            case RiverMenuType.Generate:
                EmitSignal("generate_flowmap");
                break;

            case RiverMenuType.GenerateMesh:
                EmitSignal("generate_mesh");
                break;

            case RiverMenuType.DebugViewMenu:
                break;
        }
    }

    private void OnDebugMenuItemSelected(int index)
    {
        DebugViewMenuSelected = index;
        EmitSignal("debug_view_changed", index);
    }

    private void OnDebugViewMenuAboutToPopup()
    {
        DebugViewMenu.Clear();
        DebugViewMenu.AddRadioCheckItem("Display Normal");
        DebugViewMenu.AddRadioCheckItem("Display Debug Flow Map (RG)");
        DebugViewMenu.AddRadioCheckItem("Display Debug Foam Map (B)");
        DebugViewMenu.AddRadioCheckItem("Display Debug Noise Map (A)");
        DebugViewMenu.AddRadioCheckItem("Display Debug Distance Field Map (R)");
        DebugViewMenu.AddRadioCheckItem("Display Debug Pressure Map (G)");
        DebugViewMenu.AddRadioCheckItem("Display Debug Flow Pattern");
        DebugViewMenu.AddRadioCheckItem("Display Debug Flow Arrows");
        DebugViewMenu.AddRadioCheckItem("Display Debug Flow Strength");
        DebugViewMenu.AddRadioCheckItem("Display Debug Foam Mix");
        DebugViewMenu.SetItemChecked(DebugViewMenuSelected, true);
    }
}
