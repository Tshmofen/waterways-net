using Godot;

namespace Waterways.Gui;

[Tool]
public partial class RiverControls : HBoxContainer
{
    private bool _mouseDown;

    public RiverMenu Menu { get; set; }
    public OptionButton ConstraintsOption { get; set; }

    [Signal] public delegate void ModeEventHandler(RiverMode mode);
    [Signal] public delegate void OptionsEventHandler(string type, int index);

    #region Util

    private void DisableConstraintUi(bool disable)
    {
        GetNode<BaseButton>("Constraints").Disabled = disable;
        GetNode<BaseButton>("LocalMode").Disabled = disable;
    }

    private void UnToggleButtons()
    {
        GetNode<BaseButton>("Select").ButtonPressed = false;
        GetNode<BaseButton>("Add").ButtonPressed = false;
        GetNode<BaseButton>("Remove").ButtonPressed = false;
    }

    #endregion

    #region Signal Handlers

    private void OnSelect()
    {
        UnToggleButtons();
        DisableConstraintUi(false);
        GetNode<BaseButton>("Select").ButtonPressed = true;
        EmitSignal(SignalName.Mode, (int) RiverMode.Select);
    }

    private void OnAdd()
    {
        UnToggleButtons();
        DisableConstraintUi(false);
        GetNode<BaseButton>("Add").ButtonPressed = true;
        EmitSignal(SignalName.Mode, (int) RiverMode.Add);
    }

    private void OnRemove()
    {
        UnToggleButtons();
        DisableConstraintUi(true);
        GetNode<BaseButton>("Remove").ButtonPressed = true;
        EmitSignal(SignalName.Mode, (int) RiverMode.Remove);
    }

    private void OnConstraintSelected(int index)
    {
        EmitSignal(SignalName.Options, "constraint", index);
    }

    private void OnLocalModeToggled(bool enabled)
    {
        EmitSignal(SignalName.Options, "local_mode", enabled);
    }

    #endregion

    public override void _EnterTree()
    {
        Menu = GetNode<RiverMenu>("RiverMenu");
        ConstraintsOption = GetNode<OptionButton>("Constraints");
    }

    public bool SpatialGuiInput(InputEvent @event)
    {
        switch (@event)
        {
            // This uses the forwarded spatial input in order to not react to events
            // while the spatial editor is not in focus
            // This is to avoid that the constraints are toggled while navigating 
            // the scene with WASD holding the right mouse button
            case InputEventMouseButton:
                _mouseDown = @event.IsPressed();
                break;

            case InputEventKey eventKey when eventKey.IsPressed() && !ConstraintsOption.Disabled:
                // Early exit if any of the modifiers (except shift) is pressed to not
                // override default shortcuts like Ctrl + Z
                if (eventKey.AltPressed || eventKey.CtrlPressed || eventKey.MetaPressed || _mouseDown)
                {
                    return false;
                }

                // Handle local mode keybinding for toggling
                if (eventKey.Keycode == Key.T)
                {
                    // Set the input as handled to prevent default actions from the keys
                    GetNode<BaseButton>("LocalMode").ButtonPressed = !GetNode<BaseButton>("LocalMode").ButtonPressed;
                    GetViewport().SetInputAsHandled();
                    return true;
                }

                // Fetch the constraint that the user requested to toggle
                int requested;
                switch (eventKey.Keycode)
                {
                    case Key.S:
                        requested = (int)ConstraintType.Colliders;
                        break;
                    case Key.X when !eventKey.ShiftPressed:
                        requested = (int)ConstraintType.AxisX;
                        break;
                    case Key.Y when !eventKey.ShiftPressed:
                        requested = (int)ConstraintType.AxisY;
                        break;
                    case Key.Z when !eventKey.ShiftPressed:
                        requested = (int)ConstraintType.AxisZ;
                        break;
                    case Key.X when eventKey.ShiftPressed:
                        requested = (int)ConstraintType.PlaneYz;
                        break;
                    case Key.Y when eventKey.ShiftPressed:
                        requested = (int)ConstraintType.PlaneXz;
                        break;
                    case Key.Z when eventKey.ShiftPressed:
                        requested = (int)ConstraintType.PlaneXy;
                        break;
                    default:
                        return false;
                }

                // If the user requested the current selection, we toggle it instead to off
                if (requested == ConstraintsOption.Selected)
                {
                    requested = (int)ConstraintType.None;
                }

                // Update the OptionsButton and call the signal callback as that is
                // only automatically called when the user clicks it
                ConstraintsOption.Select(requested);
                OnConstraintSelected(requested);

                // Set the input as handled to prevent default actions from the keys
                GetViewport().SetInputAsHandled();
                return true;
        }

        return false;
    }
}