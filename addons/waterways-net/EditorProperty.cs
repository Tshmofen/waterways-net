using Godot;
using Waterway;

namespace Waterways;

public partial class CustomEditorProperty : EditorProperty
{
	private GradientInspector _ui;
	private bool _updating;

	public CustomEditorProperty()
	{
		var uiScene = (PackedScene)ResourceLoader.Load("res://addons/waterways/gui/gradient_inspector.tscn");
        _ui = uiScene.Instantiate<GradientInspector>();

		AddChild(_ui);
		SetBottomEditor(_ui);

		_ui.GetNode<ColorPickerButton>("Color1").ColorChanged += OnGradientChanged;
		_ui.GetNode<ColorPickerButton>("Color2").ColorChanged += OnGradientChanged;
    }

    private void OnGradientChanged(Color color)
	{
		if (_updating)
        {
            return;
        }

        var value = _ui.GetValue();
		EmitChanged(GetEditedProperty(), value);
    }

    public override void _UpdateProperty()
    {
		var newValue = GetEditedObject().Get(GetEditedProperty()).AsProjection();
		_updating = true;
		_ui.SetValue(newValue);
		_updating = false;
    }
}
