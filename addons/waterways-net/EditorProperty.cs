using Godot;
using Waterways.Gui;

namespace Waterways;

public partial class CustomEditorProperty : EditorProperty
{
	private readonly GradientInspector _inspector;
	private bool _updating;

	public CustomEditorProperty()
	{
		var uiScene = (PackedScene)ResourceLoader.Load($"{WaterwaysPlugin.PluginPath}/Gui/gradient_inspector.tscn");
        _inspector = uiScene.Instantiate<GradientInspector>();

		AddChild(_inspector);
		SetBottomEditor(_inspector);

		_inspector.GetNode<ColorPickerButton>("Color1").ColorChanged += OnGradientChanged;
		_inspector.GetNode<ColorPickerButton>("Color2").ColorChanged += OnGradientChanged;
    }

    private void OnGradientChanged(Color color)
	{
		if (_updating)
        {
            return;
        }

        var value = _inspector.Projection;
		EmitChanged(GetEditedProperty(), value);
    }

    public override void _UpdateProperty()
    {
		_updating = true;
		_inspector.Projection = GetEditedObject().Get(GetEditedProperty()).AsProjection();
		_updating = false;
    }
}
