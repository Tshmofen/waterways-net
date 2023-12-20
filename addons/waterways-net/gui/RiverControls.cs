using Godot;

namespace Waterways.Gui;

[Tool]
public partial class RiverControls : HBoxContainer
{
	[Signal] public delegate void modeEventHandler();
	[Signal] public delegate void optionsEventHandler();

	public enum CONSTRAINTS 
	{
		NONE,
		COLLIDERS,
		AXIS_X,
		AXIS_Y,
		AXIS_Z,
		PLANE_YZ,
		PLANE_XZ,
		PLANE_XY,
	}

	public RiverMenu menu { get; set; }
	public OptionButton constraints { get; set; }

	private bool _mouse_down;

	public override void _EnterTree()
	{
		menu = GetNode<RiverMenu>("$RiverMenu");
		constraints = GetNode<OptionButton>("$Constraints");
    }

    public bool spatial_gui_input(InputEvent @event)
	{

		// This uses the forwarded spatial input in order to not react to events
		// while the spatial editor is not in focus
	
		// This is to avoid that the contraints are toggled while navigating 
		// the scene with WASD holding the right mouse button
		if (@event is InputEventMouseButton) { }
			_mouse_down = @event.IsPressed();
	
		if (@event is InputEventKey eventKey && eventKey.IsPressed() && !constraints.Disabled)
		{
			// Early exit if any of the modifiers (except shift) is pressed to not
			// override default shortcuts like Ctrl + Z
			if (eventKey.AltPressed || eventKey.CtrlPressed || eventKey.MetaPressed || _mouse_down)
				return false;

			// Handle local mode keybinding for toggling
			if (eventKey.Keycode == Key.T)
            {
				// Set the input as handled to prevent default actions from the keys
				GetNode<BaseButton>("$LocalMode").ButtonPressed = !GetNode<BaseButton>("$LocalMode").ButtonPressed;
				GetViewport().SetInputAsHandled();
				return true;
            }

			// Fetch the constraint that the user requested to toggle
			int requested;
			switch (eventKey.Keycode) 
			{ 
				case Key.S:
					requested = (int) CONSTRAINTS.COLLIDERS;
                    break;
                case Key.X when !eventKey.ShiftPressed:
					requested = (int) CONSTRAINTS.AXIS_X;
                    break;
                case Key.Y when !eventKey.ShiftPressed:
					requested = (int) CONSTRAINTS.AXIS_Y;
                    break;
                case Key.Z when !eventKey.ShiftPressed:
					requested = (int) CONSTRAINTS.AXIS_Z;
                    break;
                case Key.X when eventKey.ShiftPressed:
                    requested = (int) CONSTRAINTS.PLANE_YZ;
                    break;
                case Key.Y when eventKey.ShiftPressed:
                    requested = (int)CONSTRAINTS.PLANE_XZ;
                    break;
                case Key.Z when eventKey.ShiftPressed:
                    requested = (int)CONSTRAINTS.PLANE_XY;
                    break;
				default:
					return false;
            }

			// If the user requested the current selection, we toggle it instead to off
			if (requested == constraints.Selected)
				requested = (int)CONSTRAINTS.NONE;

			// Update the OptionsButton and call the signal callback as that is
			// only automatically called when the user clicks it
			constraints.Select(requested);
			_on_constraint_selected(requested);

			// Set the input as handled to prevent default actions from the keys
			GetViewport().SetInputAsHandled();
			return true;
        }

		return false;
	}

	private void _on_select()
	{
		_untoggle_buttons();
		_disable_constraint_ui(false);
		GetNode<BaseButton>("$Select").ButtonPressed = true;
		EmitSignal("mode", "select");
	}

	private void _on_add()
	{
		_untoggle_buttons();
		_disable_constraint_ui(false);
		GetNode<BaseButton>("$Add").ButtonPressed = true;
		EmitSignal("mode", "add");
    }


    private void _on_remove()
	{

		_untoggle_buttons();
		_disable_constraint_ui(true);
        GetNode<BaseButton>("$Remove").ButtonPressed = true;
		EmitSignal("mode", "remove");

    }
    private void _on_constraint_selected(int index)
    {
		EmitSignal("options", "constraint", index);
    }


	private void _on_local_mode_toggled(bool enabled)
	{
		EmitSignal("options", "local_mode", enabled);
	}


	private void _disable_constraint_ui(bool disable)
	{
        GetNode<BaseButton>("$Constraints").Disabled = disable;
        GetNode<BaseButton>("$LocalMode").Disabled = disable;
    }

    private void _untoggle_buttons()
    {
		GetNode<BaseButton>("$Select").ButtonPressed = false;
		GetNode<BaseButton>("$Add").ButtonPressed = false;
		GetNode<BaseButton>("$Remove").ButtonPressed = false;
    }
}