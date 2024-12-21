#if TOOLS

using Godot;
using System.Linq;
using Waterways.Data.UI;
using Waterways.UI;
using Waterways.Util;

namespace Waterways;

[Tool]
public partial class WaterwaysPlugin : EditorPlugin
{
    private const string AddPointMessage = "Add River Manager Point";
    private const string RemovePointMessage = "Remove River Manager Point";

    public const string PluginPath = "res://addons/waterways_net";
    public const string RiverControlNodePath = "/UI/river_control.tscn";

    public EditorSelection Selection { get; private set; }
    public RiverGizmo RiverGizmo { get; private set; }
    public RiverControl RiverControl { get; private set; }
    public RiverManager RiverManager { get; private set; }

    #region Util

    private void OnSelectionChange()
    {
        var selectedNode = Selection.GetSelectedNodes().FirstOrDefault();

        if (selectedNode is not RiverManager riverManager || riverManager == null)
        {
            RiverManager = null;
            SwitchRiverControl(false);
            return;
        }

        RiverManager = riverManager;
        SwitchRiverControl(true);
    }

    private void OnMenuActionPressed(RiverMenuActionType action)
    {
        if (RiverManager == null)
        {
            return;
        }

        switch (action)
        {
            case RiverMenuActionType.GenerateMeshSibling:
                if (RiverManager.Owner != null)
                {
                    var meshCopy = RiverManager.GetMeshCopy();
                    RiverManager.GetParent().AddChild(meshCopy);
                    meshCopy.Owner = RiverManager.GetTree().EditedSceneRoot;
                }
                else
                {
                    GD.PushWarning("Cannot create MeshInstance3D sibling when River is root.");
                }

                break;

            case RiverMenuActionType.RecenterRiver:
                RiverManager.RecenterCurve();
                var undoRedo = GetUndoRedo();
                var riverId = undoRedo.GetObjectHistoryId(RiverManager);
                undoRedo.GetHistoryUndoRedo(riverId).ClearHistory();
                GD.PushWarning("RiverManager UndoRedo history was cleared.");
                break;
        }
    }

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

    private void CommitPointAdd(Vector3 point, int segment, bool addToStart)
    {
        var ur = GetUndoRedo();
        ur.CreateAction(AddPointMessage);

        if (segment == -1)
        {
            ur.AddDoMethod(RiverManager, BaseMeshGenerator.MethodName.AddPoint, point, Vector3.Zero, addToStart ? 0 : -1, -1);
        }
        else
        {
            ur.AddDoMethod(RiverManager, BaseMeshGenerator.MethodName.AddPoint, point, Vector3.Zero, segment + 1, -1);
        }

        ur.AddDoMethod(RiverManager, BaseMeshGenerator.MethodName.UpdateMesh);

        if (segment == -1)
        {
            ur.AddUndoMethod(RiverManager, BaseMeshGenerator.MethodName.RemovePoint, addToStart ? 0 : RiverManager.Curve.PointCount);
        }
        else
        {
            ur.AddUndoMethod(RiverManager, BaseMeshGenerator.MethodName.RemovePoint, segment + 1);
        }

        ur.AddUndoMethod(RiverManager, BaseMeshGenerator.MethodName.UpdateMesh);
        ur.CommitAction();
    }

    private void CommitPointRemove(int index)
    {
        var ur = GetUndoRedo();
        ur.CreateAction(RemovePointMessage);
        ur.AddDoMethod(RiverManager, BaseMeshGenerator.MethodName.RemovePoint, index);
        ur.AddDoMethod(RiverManager, BaseMeshGenerator.MethodName.UpdateMesh);

        if (index == RiverManager.Curve.PointCount - 1)
        {
            ur.AddUndoMethod(RiverManager, BaseMeshGenerator.MethodName.AddPoint, RiverManager.Curve.GetPointPosition(index), Vector3.Zero, -1, -1);
        }
        else
        {
            ur.AddUndoMethod(RiverManager, BaseMeshGenerator.MethodName.AddPoint, RiverManager.Curve.GetPointPosition(index), RiverManager.Curve.GetPointOut(index), index - 1, RiverManager.PointWidths[index]);
        }

        ur.AddUndoMethod(RiverManager, BaseMeshGenerator.MethodName.UpdateMesh);
        ur.CommitAction();
    }

    #endregion

    public override void _EnterTree()
    {
        RiverControl = ResourceLoader.Load<PackedScene>(PluginPath + RiverControlNodePath).Instantiate<RiverControl>();
        RiverControl.MenuAction += OnMenuActionPressed;
        RiverGizmo = new RiverGizmo { EditorPlugin = this };

        Selection = EditorInterface.Singleton.GetSelection();
        Selection.SelectionChanged += OnSelectionChange;
        OnSelectionChange();

        AddNode3DGizmoPlugin(RiverGizmo);
        AddCustomType(RiverManager.PluginNodeAlias, RiverManager.PluginBaseAlias, RiverManager.ScriptPath, RiverManager.IconPath);
        AddCustomType(RiverFloatSystem.PluginNodeAlias, RiverFloatSystem.PluginBaseAlias, RiverFloatSystem.ScriptPath, RiverFloatSystem.IconPath);
    }

    public override void _ExitTree()
    {
        RemoveCustomType(RiverFloatSystem.PluginNodeAlias);
        RemoveCustomType(RiverManager.PluginNodeAlias);
        RemoveNode3DGizmoPlugin(RiverGizmo);
        SwitchRiverControl(false);
        Selection.SelectionChanged -= OnSelectionChange;
    }

    public override bool _Handles(GodotObject @object)
    {
        return @object is RiverManager;
    }

    public override int _Forward3DGuiInput(Camera3D camera, InputEvent @event)
    {
        if (RiverManager is null)
        {
            return 0;
        }

        if (@event is not InputEventMouseButton { ButtonIndex: MouseButton.Left } mouseEvent)
        {
            return RiverControl.SpatialGuiInput(@event) ? 1 : 0;
        }

        var cameraPoint = mouseEvent.Position;
        var (segment, point) = RiverCurveHelper.GetClosestPosition(RiverManager, camera, cameraPoint);

        switch (RiverControl.CurrentEditMode)
        {
            case RiverEditMode.Select:
            {
                return 0;
            }

            case RiverEditMode.Add when !mouseEvent.Pressed:
            {
                var newPoint = point;

                if (segment == -1)
                {
                    newPoint = RiverCurveHelper.GetNewPoint(RiverManager, camera, cameraPoint, RiverControl.CurrentConstraint, RiverControl.IsLocalEditing);
                }

                if (newPoint == null)
                {
                    return 0;
                }

                CommitPointAdd(newPoint.Value, segment, RiverCurveHelper.IsStartPointCloser(RiverManager, newPoint.Value));
                break;
            }

            case RiverEditMode.Remove when !mouseEvent.Pressed:
            {
                if (segment != -1 && point != null)
                {
                    var closestIndex = RiverCurveHelper.GetClosestPointTo(RiverManager, point.Value);
                    CommitPointRemove(closestIndex);
                }

                break;
            }
        }

        return 1;
    }
}

#else

public static class WaterwaysPlugin
{
    public const string PluginPath = "res://addons/waterways_net";
    public const string RiverControlNodePath = "/UI/river_control.tscn";
}

#endif