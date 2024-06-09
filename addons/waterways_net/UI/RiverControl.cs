using Godot;
using System;
using Waterways.Data;
using Waterways.Data.UI;

namespace Waterways.UI;

[Tool]
public partial class RiverControl : HBoxContainer
{
    private bool _isMouseDown;

    [Signal] public delegate void MenuActionEventHandler(RiverMenuActionType action);

    [Export] private OptionButton ConstraintButton { get; set; }
    [Export] private MenuButton RiverMenuButton { get; set; }
    [Export] private CheckBox LocalModeButton { get; set; }
    [Export] private BaseButton SelectButton { get; set; }
    [Export] private BaseButton RemoveButton { get; set; }
    [Export] private BaseButton AddButton { get; set; }

    public ConstraintType CurrentConstraint { get; private set; } = ConstraintType.None;
    public RiverEditMode CurrentEditMode { get; private set; } = RiverEditMode.Select;
    public bool IsLocalEditing { get; private set; } = false;

    #region Signal Handlers

    private void OnSelectModeChange(BaseButton button, RiverEditMode mode, bool useConstraints)
    {
        SelectButton.ButtonPressed = false;
        RemoveButton.ButtonPressed = false;
        AddButton.ButtonPressed = false;

        ConstraintButton.Disabled = !useConstraints;
        LocalModeButton.Disabled = !useConstraints;

        button.ButtonPressed = true;
        CurrentEditMode = mode;
    }

    private void OnConstraintSelected(long index)
    {
        CurrentConstraint = (ConstraintType) (index + 1);
    }

    private void OnLocalModeToggled(bool toggled)
    {
        IsLocalEditing = toggled;
    }

    private void OnMenuButtonPressed(long id)
    {
        if (Enum.IsDefined((RiverMenuActionType)id))
        {
            EmitSignal(SignalName.MenuAction, id);
        }
    }

    #endregion

    public override void _Ready()
    {
        ConstraintButton.ItemSelected += OnConstraintSelected;
        LocalModeButton.Toggled += OnLocalModeToggled;
        RiverMenuButton.GetPopup().IdPressed += OnMenuButtonPressed;

        SelectButton.Pressed += () => OnSelectModeChange(SelectButton, RiverEditMode.Select, true);
        RemoveButton.Pressed += () => OnSelectModeChange(RemoveButton, RiverEditMode.Remove, false);
        AddButton.Pressed += () => OnSelectModeChange(AddButton, RiverEditMode.Add, true);
    }

    public bool SpatialGuiInput(InputEvent @event)
    {
        // Avoid changes while navigating the scene with WASD holding the right mouse button
        if (@event is InputEventMouseButton)
        {
            _isMouseDown = @event.IsPressed();
            return false;
        }

        if (@event is not InputEventKey eventKey || !eventKey.IsPressed() || ConstraintButton.Disabled)
        {
            return false;
        }

        // Early exit if any of the modifiers (except shift) is pressed to not override default shortcuts like Ctrl + Z
        if (eventKey.AltPressed || eventKey.CtrlPressed || eventKey.MetaPressed || _isMouseDown)
        {
            return false;
        }

        // Handle local mode switch
        if (eventKey.Keycode == Key.T)
        {
            LocalModeButton.ButtonPressed = !LocalModeButton.ButtonPressed;
            GetViewport().SetInputAsHandled();
            return true;
        }

        var requestedIndex = eventKey.Keycode switch
        {
            Key.S => (int)ConstraintType.Colliders,
            Key.X when !eventKey.ShiftPressed => (int)ConstraintType.AxisX,
            Key.Y when !eventKey.ShiftPressed => (int)ConstraintType.AxisY,
            Key.Z when !eventKey.ShiftPressed => (int)ConstraintType.AxisZ,
            Key.X when eventKey.ShiftPressed => (int)ConstraintType.PlaneYz,
            Key.Y when eventKey.ShiftPressed => (int)ConstraintType.PlaneXz,
            Key.Z when eventKey.ShiftPressed => (int)ConstraintType.PlaneXy,
            _ => 0,
        };

        requestedIndex -= 1;
        if (requestedIndex == -1)
        {
            return false;
        }

        // If the user requested the current selection, we just disable it
        if (requestedIndex == ConstraintButton.Selected)
        {
            requestedIndex = (int) ConstraintType.None - 1;
        }

        ConstraintButton.Select(requestedIndex);
        OnConstraintSelected(requestedIndex);

        GetViewport().SetInputAsHandled();
        return true;
    }
}
