using Godot;
using System;
using Waterways.Gui;
using Waterways.Util;

namespace Waterways;

[Tool]
public partial class WaterwaysPlugin : EditorPlugin
{
    public const string PluginPath = "res://addons/waterways_net";

    private RiverControls _riverControls;
    private EditorSelection _editorSelection;
    private RiverMode _mode = RiverMode.Select;

    public Action CurrentGizmoRedraw { get; set; }
    public RiverManager CurrentRiverManager { get; set; }
    public RiverGizmo RiverGizmo { get; set; } = new();
    public ConstraintType Constraint { get; set; } = ConstraintType.None;
    public bool LocalEditing { get; set; }

    #region Signal Handlers

    private void OnGenerateMeshPressed()
    {
        CurrentRiverManager.CreateMeshDuplicate();
    }

    private void OnDebugViewChanged(int index)
    {
        CurrentRiverManager.DebugView = index;
    }

    private void HandleRiverManagerChange(RiverManager manager)
    {
        if (manager == null)
        {
            CurrentRiverManager = null;
            CurrentGizmoRedraw?.Invoke();
            HideRiverControlPanel();
            return;
        }

        ShowRiverControlPanel();
        CurrentRiverManager = manager;
        CurrentGizmoRedraw?.Invoke();
        _riverControls.Menu.SelectedDebugViewMenuIndex = manager.DebugView;
    }

    private void OnSelectionChange()
    {
        var selected = _editorSelection.GetSelectedNodes();

        if (selected.Count == 0)
        {
            return;
        }

        HandleRiverManagerChange(selected[0] as RiverManager);
    }

    private void OnSceneChanged(Node _)
    {
        HideRiverControlPanel();
    }

    private void OnSceneClosed(string _)
    {
        HideRiverControlPanel();
    }

    private void OnModeChange(RiverMode mode)
    {
        _mode = mode;
    }

    private void OnOptionChange(string option, int value)
    {
        switch (option)
        {
            case "constraint":
                Constraint = (ConstraintType)value;
                if (Constraint == ConstraintType.Colliders)
                {
                    WaterHelperMethods.ResetAllColliders(CurrentRiverManager.GetTree().Root);
                }
                break;

            case "local_mode":
                LocalEditing = value == 1;
                break;
        }
    }

    #endregion

    #region Util

    private void ShowRiverControlPanel()
    {
        if (_riverControls.GetParent() != null)
        {
            return;
        }

        AddControlToContainer(CustomControlContainer.SpatialEditorMenu, _riverControls);

        if (!_riverControls.Menu.IsConnected(RiverMenu.SignalName.GenerateMesh, Callable.From(OnGenerateMeshPressed)))
        {
            _riverControls.Menu.Connect(RiverMenu.SignalName.GenerateMesh, Callable.From(OnGenerateMeshPressed));
        }

        if (!_riverControls.Menu.IsConnected(RiverMenu.SignalName.DebugViewChanged, Callable.From<int>(OnDebugViewChanged)))
        {
            _riverControls.Menu.Connect(RiverMenu.SignalName.DebugViewChanged, Callable.From<int>(OnDebugViewChanged));
        }
    }

    private void HideRiverControlPanel()
    {
        if (_riverControls.GetParent() == null)
        {
            return;
        }

        RemoveControlFromContainer(CustomControlContainer.SpatialEditorMenu, _riverControls);

        if (_riverControls.Menu.IsConnected(RiverMenu.SignalName.GenerateMesh, Callable.From(OnGenerateMeshPressed)))
        {
            _riverControls.Menu.Disconnect(RiverMenu.SignalName.GenerateMesh, Callable.From(OnGenerateMeshPressed));
        }

        if (_riverControls.Menu.IsConnected(RiverMenu.SignalName.GenerateMesh, Callable.From(OnGenerateMeshPressed)))
        {
            _riverControls.Menu.Disconnect(RiverMenu.SignalName.GenerateMesh, Callable.From(OnGenerateMeshPressed));
        }
    }

    private int GetClosestSegment(Transform3D globalTransform, Vector3 rayFrom, Vector3 rayDir, out Vector3 bakedClosestPoint)
    {
        var globalInverse = globalTransform.AffineInverse();
        var g1 = globalInverse * rayFrom;
        var g2 = globalInverse * (rayFrom + (rayDir * 4096));

        // Iterate through points to find the closest segment
        var curvePoints = CurrentRiverManager.GetCurvePoints();
        var closestDistance = 4096f;
        var closestSegment = -1;

        for (var point = 0; point < curvePoints.Count; point++)
        {
            var p1 = curvePoints[point];
            var p2 = curvePoints[(point + 1) % curvePoints.Count];
            var result = Geometry3D.GetClosestPointsBetweenSegments(p1, p2, g1, g2);
            var dist = result[0].DistanceTo(result[1]);

            if (dist >= closestDistance)
            {
                continue;
            }

            closestDistance = dist;
            closestSegment = point;
        }

        // Iterate through baked points to find the closest position on the curved path
        var bakedCurvePoints = CurrentRiverManager.Curve.GetBakedPoints();
        var bakedClosestDistance = 4096f;
        var bakedPointFound = false;

        bakedClosestPoint = new Vector3();
        for (var bakedPoint = 0; bakedPoint < bakedCurvePoints.Length; bakedPoint++)
        {
            var p1 = bakedCurvePoints[bakedPoint];
            var p2 = bakedCurvePoints[(bakedPoint + 1) % bakedCurvePoints.Length];
            var result = Geometry3D.GetClosestPointsBetweenSegments(p1, p2, g1, g2);
            var dist = result[0].DistanceTo(result[1]);

            if (dist >= 0.1f || dist >= bakedClosestDistance)
            {
                continue;
            }

            bakedClosestDistance = dist;
            bakedClosestPoint = result[0];
            bakedPointFound = true;
        }

        // In case we were close enough to a line segment to find a segment,
        // but not close enough to the curved line
        if (!bakedPointFound)
        {
            closestSegment = -1;
        }

        return closestSegment;
    }

    private void RemovePoint(int closestSegment, Vector3 bakedClosestPoint)
    {
        // The closest_segment of -1 means we didn't press close enough to a
        // point for it to be removed
        if (closestSegment == -1)
        {
            return;
        }

        var closestIndex = CurrentRiverManager.GetClosestPointTo(bakedClosestPoint);

        var ur = GetUndoRedo();
        ur.CreateAction("Remove River point");
        ur.AddDoMethod(CurrentRiverManager, nameof(RiverManager.RemovePoint), closestIndex);
        ur.AddDoMethod(CurrentRiverManager, nameof(RiverManager.PropertiesChanged));

        if (closestIndex == CurrentRiverManager.Curve.PointCount - 1)
        {
            ur.AddUndoMethod(CurrentRiverManager, nameof(RiverManager.AddPoint), CurrentRiverManager.Curve.GetPointPosition(closestIndex), -1, Vector3.Zero, 0f);
        }
        else
        {
            ur.AddUndoMethod(CurrentRiverManager, nameof(RiverManager.AddPoint), CurrentRiverManager.Curve.GetPointPosition(closestIndex), closestIndex - 1, CurrentRiverManager.Curve.GetPointOut(closestIndex), CurrentRiverManager.Widths[closestIndex]);
        }

        ur.AddUndoMethod(CurrentRiverManager, nameof(RiverManager.PropertiesChanged));
        ur.CommitAction();
    }

    private bool AddPoint(int closestSegment, Transform3D globalTransform, Camera3D camera, Vector3 rayFrom, Vector3 rayDir, Vector3 bakedClosestPoint)
    {
        // if we don't have a point on the line, we'll calculate a point
        // based of a plane of the last point of the curve
        if (closestSegment == -1)
        {
            var endPos = CurrentRiverManager.Curve.GetPointPosition(CurrentRiverManager.Curve.PointCount - 1);
            var endPosGlobal = CurrentRiverManager.ToGlobal(endPos);

            var z = CurrentRiverManager.Curve.GetPointOut(CurrentRiverManager.Curve.PointCount - 1).Normalized();
            var x = z.Cross(Vector3.Down).Normalized();
            var y = z.Cross(x).Normalized();
            var handleBaseTransform = new Transform3D(new Basis(x, y, z) * globalTransform.Basis, endPosGlobal);

            var plane = new Plane(endPosGlobal, endPosGlobal + camera.Transform.Basis.X, endPosGlobal + camera.Transform.Basis.Y);
            var newPos = Vector3.Zero;

            switch (Constraint)
            {
                case ConstraintType.Colliders:
                    {
                        var spaceState = CurrentRiverManager.GetWorld3D().DirectSpaceState;
                        var param = new PhysicsRayQueryParameters3D
                        {
                            From = rayFrom,
                            To = rayFrom + (rayDir * 4096)
                        };

                        var result = spaceState.IntersectRay(param);
                        if (result?.Count > 0)
                        {
                            newPos = result!["position"].AsVector3();
                        }
                        else
                        {
                            return false;
                        }

                        break;
                    }

                case ConstraintType.None:
                    newPos = plane.IntersectsRay(rayFrom, rayFrom + (rayDir * 4096))!.Value;
                    break;

                default:
                    if (RiverGizmo.AxisMapping.TryGetValue(Constraint, out var axis))
                    {
                        if (LocalEditing)
                        {
                            axis = handleBaseTransform.Basis * (axis);
                        }

                        var axisFrom = endPosGlobal + (axis * RiverGizmo.AxisConstraintLength);
                        var axisTo = endPosGlobal - (axis * RiverGizmo.AxisConstraintLength);
                        var rayTo = rayFrom + (rayDir * RiverGizmo.AxisConstraintLength);
                        var result = Geometry3D.GetClosestPointsBetweenSegments(axisFrom, axisTo, rayFrom, rayTo);
                        newPos = result[0];
                    }
                    else if (RiverGizmo.PlaneMapping.TryGetValue(Constraint, out var normal))
                    {
                        if (LocalEditing)
                        {
                            normal = handleBaseTransform.Basis * (normal);
                        }
                        var projected = endPosGlobal.Project(normal);
                        var direction = Mathf.Sign(projected.Dot(normal));
                        var distance = direction * projected.Length();
                        plane = new Plane(normal, distance);
                        newPos = plane.IntersectsRay(rayFrom, rayDir) ?? Vector3.Zero;
                    }

                    break;
            }

            bakedClosestPoint = CurrentRiverManager.ToLocal(newPos);
        }

        var ur = GetUndoRedo();
        ur.CreateAction("Add River point");
        ur.AddDoMethod(CurrentRiverManager, nameof(RiverManager.AddPoint), bakedClosestPoint, closestSegment, Vector3.Zero, 0f);
        ur.AddDoMethod(CurrentRiverManager, nameof(RiverManager.PropertiesChanged));

        if (closestSegment == -1)
        {
            ur.AddUndoMethod(CurrentRiverManager, nameof(RiverManager.RemovePoint), CurrentRiverManager.Curve.PointCount);
        }
        else
        {
            ur.AddUndoMethod(CurrentRiverManager, nameof(RiverManager.RemovePoint), closestSegment + 1);
        }

        ur.AddUndoMethod(CurrentRiverManager, nameof(RiverManager.PropertiesChanged));
        ur.CommitAction();

        return true;
    }

    #endregion

    public override void _EnterTree()
    {
        AddCustomType("River", nameof(Node3D), ResourceLoader.Load<Script>($"{PluginPath}/RiverManager.cs"), ResourceLoader.Load<Texture2D>($"{PluginPath}/icons/river.svg"));
        AddCustomType("RiverFloatSystem", nameof(Node3D), ResourceLoader.Load<Script>($"{PluginPath}/RiverFloatSystem.cs"), ResourceLoader.Load<Texture2D>($"{PluginPath}/icons/float.svg"));
        _riverControls = ResourceLoader.Load<PackedScene>($"{PluginPath}/gui/river_controls.tscn").Instantiate<RiverControls>();

        AddNode3DGizmoPlugin(RiverGizmo);

        RiverGizmo.EditorPlugin = this;
        _riverControls.Mode += OnModeChange;
        _riverControls.Options += OnOptionChange;

        _editorSelection = EditorInterface.Singleton.GetSelection();
        _editorSelection.SelectionChanged += OnSelectionChange;

        SceneChanged += OnSceneChanged;
        SceneClosed += OnSceneClosed;
    }

    public override void _ExitTree()
    {
        RemoveCustomType("River");
        RemoveNode3DGizmoPlugin(RiverGizmo);

        _riverControls.Mode -= OnModeChange;
        _riverControls.Options -= OnOptionChange;
        _editorSelection.SelectionChanged -= OnSelectionChange;

        SceneChanged -= OnSceneChanged;
        SceneClosed -= OnSceneClosed;

        HideRiverControlPanel();
    }

    public override bool _Handles(GodotObject @object)
    {
        var manager = @object as RiverManager;
        CallDeferred(MethodName.HandleRiverManagerChange, manager);
        return manager != null;
    }

    public override int _Forward3DGuiInput(Camera3D camera, InputEvent @event)
    {
        if (CurrentRiverManager == null)
        {
            return 0;
        }

        if (@event is not InputEventMouseButton { ButtonIndex: MouseButton.Left } mouseEvent)
        {
            return _riverControls.SpatialGuiInput(@event) ? 1 : 0;
        }

        var globalTransform = CurrentRiverManager.Transform;

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
        return 1;
    }
}
