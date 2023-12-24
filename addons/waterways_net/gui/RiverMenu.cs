using Godot;

namespace Waterways.Gui;

[Tool]
public partial class RiverMenu : MenuButton
{
    [Signal] public delegate void GenerateFlowmapEventHandler();
    [Signal] public delegate void GenerateMeshEventHandler();
    [Signal] public delegate void DebugViewChangedEventHandler(int index);

    public int DebugViewMenuSelected { get; set; }
    public PopupMenu DebugViewMenu { get; set; }

    #region Signal Handlers

    private void OnMenuItemSelected(long index)
    {
        switch ((RiverMenuType)index)
        {
            case RiverMenuType.Generate:
                EmitSignal(SignalName.GenerateFlowmap);
                break;

            case RiverMenuType.GenerateMesh:
                EmitSignal(SignalName.GenerateMesh);
                break;

            case RiverMenuType.DebugViewMenu:
                break;
        }
    }

    private void OnDebugMenuItemSelected(long index)
    {
        DebugViewMenuSelected = (int)index;
        EmitSignal(SignalName.DebugViewChanged, (int)index);
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

    #endregion

    public override void _EnterTree()
    {
        GetPopup().Clear();
        GetPopup().IdPressed += OnMenuItemSelected;
        GetPopup().AddItem("Generate Flow & Foam Map");
        GetPopup().AddItem("Generate MeshInstance3D Sibling");

        DebugViewMenu = new PopupMenu { Name = "DebugViewMenu"};
        DebugViewMenu.AboutToPopup += OnDebugViewMenuAboutToPopup;
        DebugViewMenu.IdPressed += OnDebugMenuItemSelected;

        GetPopup().AddChild(DebugViewMenu);
        GetPopup().AddSubmenuItem("Debug View", DebugViewMenu.Name);
    }

    public override void _ExitTree()
    {
        GetPopup().IdPressed -= OnMenuItemSelected;
        DebugViewMenu.AboutToPopup -= OnDebugViewMenuAboutToPopup;
        DebugViewMenu.IdPressed -= OnDebugMenuItemSelected;
    }
}
