#if TOOLS

using Godot;

namespace Waterways;

[Tool]
public partial class WaterwaysPlugin : EditorPlugin
{
    public const string PluginPath = "res://addons/waterways_net";

    public override void _EnterTree()
    {
        AddCustomType(RiverManager.PluginNodeAlias, RiverManager.PluginBaseAlias, ResourceLoader.Load<Script>($"{PluginPath}/RiverManager.cs"), ResourceLoader.Load<Texture2D>($"{PluginPath}/Icon/river.svg"));
        AddCustomType(RiverFloatSystem.PluginNodeAlias, RiverFloatSystem.PluginBaseAlias, ResourceLoader.Load<Script>($"{PluginPath}/RiverFloatSystem.cs"), ResourceLoader.Load<Texture2D>($"{PluginPath}/Icon/float.svg"));
    }

    public override void _ExitTree()
    {
        RemoveCustomType(RiverManager.PluginNodeAlias);
        RemoveCustomType(RiverFloatSystem.PluginNodeAlias);
    }
}

#endif