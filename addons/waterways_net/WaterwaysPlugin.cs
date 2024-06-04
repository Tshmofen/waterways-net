#if TOOLS

using Godot;

namespace Waterways;

[Tool]
public partial class WaterwaysPlugin : EditorPlugin
{
    public const string PluginPath = "res://addons/waterways_net";

    #region Util

    private void AddCustomType(string type, string @base, string scriptPath, string iconPath)
    {
        var script = ResourceLoader.Load<Script>($"{PluginPath}/{scriptPath}");
        var icon = ResourceLoader.Load<Texture2D>($"{PluginPath}/Icons/{iconPath}");
        AddCustomType(type, @base, script, icon);
    }

    #endregion

    public override void _EnterTree()
    {
        AddCustomType(RiverManager.PluginNodeAlias, RiverManager.PluginBaseAlias, RiverManager.ScriptPath, RiverManager.IconPath);
        AddCustomType(RiverFloatSystem.PluginNodeAlias, RiverFloatSystem.PluginBaseAlias, RiverFloatSystem.ScriptPath, RiverFloatSystem.IconPath);
    }

    public override void _ExitTree()
    {
        RemoveCustomType(RiverManager.PluginNodeAlias);
        RemoveCustomType(RiverFloatSystem.PluginNodeAlias);
    }
}

#endif