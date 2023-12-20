using Godot;
using Waterways;

namespace Waterways;

public partial class InspectorPlugin : EditorInspectorPlugin
{
    public override bool _CanHandle(GodotObject @object)
    {
        return @object is RiverManager;
    }

    public override bool _ParseProperty(GodotObject @object, Variant.Type type, string path, PropertyHint hint, string hint_text, PropertyUsageFlags usage, bool wide)
    {
        if (type == Variant.Type.Projection && path.Contains("color"))
        {
            var editor_property = new CustomEditorProperty();
            AddPropertyEditor(path, editor_property);
			return true;
        }
        return false;
    }
}
