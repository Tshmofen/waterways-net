using Godot;

namespace Waterways.Gui;

[Tool]
public partial class RiverMenu : MenuButton
{
    private PopupMenu _popup;

    [Signal] public delegate void GenerateMeshEventHandler();
    [Signal] public delegate void DebugViewChangedEventHandler(int index);

    public int SelectedDebugViewMenuIndex { get; set; }
    public PopupMenu DebugViewMenu { get; set; }

    #region Signal Handlers

    private void OnMenuItemSelected(long index)
    {
        switch ((RiverMenuType)index)
        {
            case RiverMenuType.GenerateMesh:
                EmitSignal(SignalName.GenerateMesh);
                break;

            case RiverMenuType.DebugViewMenu:
                break;
        }
    }

    private void OnDebugMenuItemSelected(long index)
    {
        SelectedDebugViewMenuIndex = (int)index;
        EmitSignal(SignalName.DebugViewChanged, (int)index);
    }

    private void OnDebugViewMenuAboutToPopup()
    {
        DebugViewMenu.Clear();
        DebugViewMenu.AddRadioCheckItem("Display Normal");
        DebugViewMenu.AddRadioCheckItem("Display Debug Foam Map (B)");
        DebugViewMenu.AddRadioCheckItem("Display Debug Noise Map (A)");
        DebugViewMenu.AddRadioCheckItem("Display Debug Distance Field Map (R)");
        DebugViewMenu.AddRadioCheckItem("Display Debug Pressure Map (G)");
        DebugViewMenu.AddRadioCheckItem("Display Debug Flow Pattern");
        DebugViewMenu.AddRadioCheckItem("Display Debug Flow Arrows");
        DebugViewMenu.AddRadioCheckItem("Display Debug Flow Strength");
        DebugViewMenu.AddRadioCheckItem("Display Debug Foam Mix");
        DebugViewMenu.SetItemChecked(SelectedDebugViewMenuIndex, true);
    }

    #endregion

    public override void _EnterTree()
    {
        _popup = GetPopup();

        _popup.Clear();
        _popup.Connect(PopupMenu.SignalName.IdPressed, Callable.From<long>(OnMenuItemSelected));
        _popup.AddItem("Generate MeshInstance3D Sibling");

        DebugViewMenu = new PopupMenu { Name = "DebugViewMenu"};
        DebugViewMenu.Connect(Window.SignalName.AboutToPopup, Callable.From(OnDebugViewMenuAboutToPopup));
        DebugViewMenu.Connect(PopupMenu.SignalName.IdPressed, Callable.From<long>(OnDebugMenuItemSelected));

        _popup.AddChild(DebugViewMenu);
        _popup.AddSubmenuItem("Debug View", DebugViewMenu.Name);
    }

    public override void _ExitTree()
    {
        if (_popup.IsConnected(PopupMenu.SignalName.IdPressed, Callable.From<long>(OnMenuItemSelected)))
        {
            _popup.Disconnect(PopupMenu.SignalName.IdPressed, Callable.From<long>(OnMenuItemSelected));
        }

        if (DebugViewMenu.IsConnected(Window.SignalName.AboutToPopup, Callable.From(OnDebugViewMenuAboutToPopup)))
        {
            DebugViewMenu.Disconnect(Window.SignalName.AboutToPopup, Callable.From(OnDebugViewMenuAboutToPopup));
        }

        if (DebugViewMenu.IsConnected(PopupMenu.SignalName.IdPressed, Callable.From<long>(OnDebugMenuItemSelected)))
        {
            DebugViewMenu.Disconnect(PopupMenu.SignalName.IdPressed, Callable.From<long>(OnDebugMenuItemSelected));
        }
    }
}
