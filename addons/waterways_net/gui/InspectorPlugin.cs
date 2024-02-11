using Godot;

namespace Waterways;

public partial class InspectorPlugin : EditorInspectorPlugin
{
    public override bool _CanHandle(GodotObject @object)
    {
        return @object is RiverManager;
    }

    public override bool _ParseProperty(GodotObject @object, Variant.Type type, string path, PropertyHint hint, string hintText, PropertyUsageFlags usage, bool wide)
    {
        if (type != Variant.Type.Projection || !path.Contains("color"))
        {
            return false;
        }

        var editorProperty = new CustomEditorProperty();
        AddPropertyEditor(path, editorProperty);
        return true;
    }
}
