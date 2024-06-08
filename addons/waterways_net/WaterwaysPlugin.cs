#if TOOLS

using Godot;
using System.Linq;
using Waterways.UI;

namespace Waterways;

[Tool]
public partial class WaterwaysPlugin : EditorPlugin
{
    public const string PluginPath = "res://addons/waterways_net";
    public const string RiverControlNodePath = "/UI/river_control.tscn";

    public EditorSelection Selection { get; private set; }
    public RiverGizmo RiverGizmo { get; private set; }
    public RiverControl RiverControl { get; private set; }

    #region Util

    private void AddCustomType(string type, string @base, string scriptPath, string iconPath)
    {
        var script = ResourceLoader.Load<Script>($"{PluginPath}/{scriptPath}");
        var icon = ResourceLoader.Load<Texture2D>($"{PluginPath}/Icons/{iconPath}");
        AddCustomType(type, @base, script, icon);
    }

    private void SwitchRiverControl(bool show)
    {
        if (show && !RiverControl.IsInsideTree())
        {
            AddControlToContainer(CustomControlContainer.SpatialEditorMenu, RiverControl);
        }
        else if (!show && RiverControl.IsInsideTree())
        {
            RemoveControlFromContainer(CustomControlContainer.SpatialEditorMenu, RiverControl);
        }
    }

    private void OnSelectionChange()
    {
        var selectedNode = Selection.GetSelectedNodes().FirstOrDefault();
        if (selectedNode != null)
        {
            SwitchRiverControl(selectedNode is RiverManager);
        }
    }

    #endregion

    public WaterwaysPlugin()
    {
        RiverControl = ResourceLoader.Load<PackedScene>(PluginPath + RiverControlNodePath).Instantiate<RiverControl>();
        RiverGizmo = new RiverGizmo { EditorPlugin = this };
        Selection = EditorInterface.Singleton.GetSelection();
        Selection.SelectionChanged += OnSelectionChange;
    }

    public override void _EnterTree()
    {
        AddNode3DGizmoPlugin(RiverGizmo);
        AddCustomType(RiverManager.PluginNodeAlias, RiverManager.PluginBaseAlias, RiverManager.ScriptPath, RiverManager.IconPath);
        AddCustomType(RiverFloatSystem.PluginNodeAlias, RiverFloatSystem.PluginBaseAlias, RiverFloatSystem.ScriptPath, RiverFloatSystem.IconPath);
        OnSelectionChange();
    }

    public override void _ExitTree()
    {
        RemoveCustomType(RiverFloatSystem.PluginNodeAlias);
        RemoveCustomType(RiverManager.PluginNodeAlias);
        RemoveNode3DGizmoPlugin(RiverGizmo);
        SwitchRiverControl(false);
    }

    protected override void Dispose(bool disposing)
    {
        SwitchRiverControl(false);
        Selection.SelectionChanged -= OnSelectionChange;
        RiverControl.Dispose();
        RiverGizmo.Dispose();
    }

    public override bool _Handles(GodotObject @object)
    {
        return @object is RiverManager;
    }

    public override int _Forward3DGuiInput(Camera3D camera, InputEvent @event)
    {
        /*if (CurrentRiverManager == null)
        {
            return 0;
        }*/

        if (@event is not InputEventMouseButton { ButtonIndex: MouseButton.Left })
        {
            return RiverControl.SpatialGuiInput(@event) ? 1 : 0;
        }

        return 0;

        /*var globalTransform = CurrentRiverManager.Transform;

        if (CurrentRiverManager.IsInsideTree())
        {
            globalTransform = CurrentRiverManager.GlobalTransform;
        }

        var rayFrom = camera.ProjectRayOrigin(mouseEvent.Position);
        var rayDir = camera.ProjectRayNormal(mouseEvent.Position);
        var closestSegment = GetClosestSegment(globalTransform, rayFrom, rayDir, out var bakedClosestPoint);

        // We'll use this closest point to add a point in between if on the line
        // and to remove if close to a point
        switch (_mode)
        {
            case RiverMode.Select:
                if (!mouseEvent.Pressed)
                {
                    RiverGizmo.Reset();
                }
                return 0;

            case RiverMode.Add when !mouseEvent.Pressed:
                if (!AddPoint(closestSegment, globalTransform, camera, rayFrom, rayDir, bakedClosestPoint))
                {
                    return 0;
                }
                break;

            case RiverMode.Remove when !mouseEvent.Pressed:
                RemovePoint(closestSegment, bakedClosestPoint);
                break;
        }

        // TODO - This should be updated to the enum when it's fixed https://github.com/godotengine/godot/pull/64465
        return 1;*/
    }
}

#endif