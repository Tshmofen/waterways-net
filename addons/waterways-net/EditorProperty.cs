using Godot;
using Waterway;

namespace Waterways;

public partial class CustomEditorProperty : EditorProperty
{
	private GradientInspector _ui;
	private bool _updating;

	public CustomEditorProperty()
	{
		_ui = (ResourceLoader.Load("res://addons/waterways/gui/gradient_inspector.tscn") as PackedScene)!.Instantiate() as GradientInspector;
		AddChild(_ui);
		SetBottomEditor(_ui);
		(_ui.GetNode("Color1") as ColorPickerButton).ColorChanged += gradient_changed;
		(_ui.GetNode("Color2") as ColorPickerButton).ColorChanged += gradient_changed;
    }

    private void gradient_changed(Color _val)
	{
		if (_updating)
			return;
		var value = _ui.get_value();
		EmitChanged(GetEditedProperty(), value);

    }

    public override void _UpdateProperty()
    {
		var new_value = GetEditedObject().Get(GetEditedProperty()).AsProjection();
		_updating = true;
		_ui.set_value(new_value);
		_updating = false;
    }
}
