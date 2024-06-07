#if TOOLS

using Godot;
using Waterways.UI;
using Waterways.UI.Data;

namespace Waterways;

[Tool]
public partial class WaterwaysPlugin : EditorPlugin
{
    public const string PluginPath = "res://addons/waterways_net";

    private RiverGizmo _riverGizmo;

    public ConstraintType CurrentConstraint { get; set; } = ConstraintType.None;
    public bool IsLocalEditing { get; set; } = false;

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
        _riverGizmo = new RiverGizmo
        {
            EditorPlugin = this
        };

        AddNode3DGizmoPlugin(_riverGizmo);
        AddCustomType(RiverManager.PluginNodeAlias, RiverManager.PluginBaseAlias, RiverManager.ScriptPath, RiverManager.IconPath);
        AddCustomType(RiverFloatSystem.PluginNodeAlias, RiverFloatSystem.PluginBaseAlias, RiverFloatSystem.ScriptPath, RiverFloatSystem.IconPath);
    }

    public override void _ExitTree()
    {
        RemoveCustomType(RiverFloatSystem.PluginNodeAlias);
        RemoveCustomType(RiverManager.PluginNodeAlias);
        RemoveNode3DGizmoPlugin(_riverGizmo);
    }
}

#endif