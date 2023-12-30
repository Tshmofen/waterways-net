using Godot;
using Waterways.Gui;
using Waterways.Util;

namespace Waterways;

[Tool]
public partial class WaterwaysPlugin : EditorPlugin
{
    public const string PluginPath = "res://addons/waterways_net";

    private WaterSystemControls _waterSystemControls;
    private RiverControls _riverControls;
    private EditorSelection _editorSelection;
    private ProgressWindow _progressWindow;
    private RiverMode _mode = RiverMode.Select;
    private RiverManager _editedRiverManager;
    private WaterSystemManager _editedSystemManager;

    public RiverGizmo RiverGizmo { get; set; } = new();
    public InspectorPlugin GradientInspector { get; set; } = new();
    public ConstraintType Constraint { get; set; } = ConstraintType.None;
    public bool LocalEditing { get; set; }

    #region Signal Handlers

    private void OnGenerateFlowMapPressed()
    {
        _editedRiverManager.BakeTexture();
    }

    private void OnGenerateMeshPressed()
    {
        _editedRiverManager.SpawnMesh();
    }

    private void OnDebugViewChanged(int index)
    {
        _editedRiverManager.DebugView = index;
    }

    private void OnGenerateSystemMapsPressed()
    {
        _ = _editedSystemManager.GenerateSystemMaps();
    }

    private void OnSelectionChange()
    {
        _editorSelection = EditorInterface.Singleton.GetSelection();
        var selected = _editorSelection.GetSelectedNodes();

        if (selected.Count == 0)
        {
            return;
        }

        switch (selected[0])
        {
            case RiverManager manager:
                ShowRiverControlPanel();
                _editedRiverManager = manager;
                _riverControls.Menu.DebugViewMenuSelected = _editedRiverManager.DebugView;

                if (!_editedRiverManager.IsConnected(RiverManager.SignalName.ProgressNotified, Callable.From<float, string>(OnRiverProgressNotified)))
                {
                    _editedRiverManager.Connect(RiverManager.SignalName.ProgressNotified, Callable.From<float, string>(OnRiverProgressNotified));
                }

                HideWaterSystemControlPanel();
                break;

            case WaterSystemManager:
                ShowWaterSystemControlPanel();
                _editedSystemManager = selected[0] as WaterSystemManager;
                HideRiverControlPanel();
                break;

            default:
                _editedRiverManager = null;
                HideRiverControlPanel();
                HideWaterSystemControlPanel();
                break;
        }
    }

    private void OnSceneChanged(Node _)
    {
        HideRiverControlPanel();
        HideWaterSystemControlPanel();
    }

    private void OnSceneClosed(string _)
    {
        HideRiverControlPanel();
        HideWaterSystemControlPanel();
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
                    WaterHelperMethods.ResetAllColliders(_editedRiverManager.GetTree().Root);
                }
                break;

            case "local_mode":
                LocalEditing = value == 1;
                break;
        }
    }

    private void OnRiverProgressNotified(float progress, string message)
    {
        if (message == "finished")
        {
            _progressWindow.Hide();
        }
        else
        {
            if (!_progressWindow.Visible)
            {
                _progressWindow.PopupCentered();
            }

            _progressWindow.ShowProgress(message, progress);
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
        _riverControls.Menu.GenerateFlowmap += OnGenerateFlowMapPressed;
        _riverControls.Menu.GenerateMesh += OnGenerateMeshPressed;
        _riverControls.Menu.DebugViewChanged += OnDebugViewChanged;
    }

    private void HideRiverControlPanel()
    {
        if (_riverControls.GetParent() == null)
        {
            return;
        }

        RemoveControlFromContainer(CustomControlContainer.SpatialEditorMenu, _riverControls);

        _riverControls.Menu.GenerateFlowmap -= OnGenerateFlowMapPressed;
        _riverControls.Menu.GenerateMesh -= OnGenerateMeshPressed;
        _riverControls.Menu.DebugViewChanged -= OnDebugViewChanged;
    }

    private void ShowWaterSystemControlPanel()
    {
        if (_waterSystemControls.GetParent() != null)
        {
            return;
        }

        AddControlToContainer(CustomControlContainer.SpatialEditorMenu, _waterSystemControls);
        _waterSystemControls.Menu.GenerateSystemMaps += OnGenerateSystemMapsPressed;
    }

    private void HideWaterSystemControlPanel()
    {
        if (_waterSystemControls.GetParent() == null)
        {
            return;
        }

        RemoveControlFromContainer(CustomControlContainer.SpatialEditorMenu, _waterSystemControls);
        _waterSystemControls.Menu.GenerateSystemMaps -= OnGenerateSystemMapsPressed;
    }

    private int GetClosestSegment(Transform3D globalTransform, Vector3 rayFrom, Vector3 rayDir, out Vector3 bakedClosestPoint)
    {
        var globalInverse = globalTransform.AffineInverse();
        var g1 = globalInverse * rayFrom;
        var g2 = globalInverse * (rayFrom + (rayDir * 4096));

        // Iterate through points to find the closest segment
        var curvePoints = _editedRiverManager.GetCurvePoints();
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
        var bakedCurvePoints = _editedRiverManager.Curve.GetBakedPoints();
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

        var closestIndex = _editedRiverManager.GetClosestPointTo(bakedClosestPoint);

        var ur = GetUndoRedo();
        ur.CreateAction("Remove River point");
        ur.AddDoMethod(_editedRiverManager, RiverManager.MethodName.RemovePoint, closestIndex);
        ur.AddDoMethod(_editedRiverManager, RiverManager.MethodName.PropertiesChanged);
        ur.AddDoMethod(_editedRiverManager, RiverManager.MethodName.SetMaterials, "i_valid_flowmap", false);
        ur.AddDoProperty(_editedRiverManager, RiverManager.PropertyName.ValidFlowmap, false);
        ur.AddDoMethod(_editedRiverManager, Node.MethodName.UpdateConfigurationWarnings);

        if (closestIndex == _editedRiverManager.Curve.PointCount - 1)
        {
            ur.AddUndoMethod(_editedRiverManager, RiverManager.MethodName.AddPoint, _editedRiverManager.Curve.GetPointPosition(closestIndex), -1, Vector3.Zero, 0f);
        }
        else
        {
            ur.AddUndoMethod(_editedRiverManager, RiverManager.MethodName.AddPoint, _editedRiverManager.Curve.GetPointPosition(closestIndex), closestIndex - 1, _editedRiverManager.Curve.GetPointOut(closestIndex), _editedRiverManager.Widths[closestIndex]);
        }

        ur.AddUndoMethod(_editedRiverManager, RiverManager.MethodName.PropertiesChanged);
        ur.AddUndoMethod(_editedRiverManager, RiverManager.MethodName.SetMaterials, "i_valid_flowmap", _editedRiverManager.ValidFlowmap);
        ur.AddUndoProperty(_editedRiverManager, RiverManager.PropertyName.ValidFlowmap, _editedRiverManager.ValidFlowmap);
        ur.AddUndoMethod(_editedRiverManager, Node.MethodName.UpdateConfigurationWarnings);
        ur.CommitAction();
    }

    private bool AddPoint(int closestSegment, Transform3D globalTransform, Camera3D camera, Vector3 rayFrom, Vector3 rayDir, Vector3 bakedClosestPoint)
    {
        // if we don't have a point on the line, we'll calculate a point
        // based of a plane of the last point of the curve
        if (closestSegment == -1)
        {
            var endPos = _editedRiverManager.Curve.GetPointPosition(_editedRiverManager.Curve.PointCount - 1);
            var endPosGlobal = _editedRiverManager.ToGlobal(endPos);

            var z = _editedRiverManager.Curve.GetPointOut(_editedRiverManager.Curve.PointCount - 1).Normalized();
            var x = z.Cross(Vector3.Down).Normalized();
            var y = z.Cross(x).Normalized();
            var handleBaseTransform = new Transform3D(new Basis(x, y, z) * globalTransform.Basis, endPosGlobal);

            var plane = new Plane(endPosGlobal, endPosGlobal + camera.Transform.Basis.X, endPosGlobal + camera.Transform.Basis.Y);
            var newPos = Vector3.Zero;

            switch (Constraint)
            {
                case ConstraintType.Colliders:
                    {
                        var spaceState = _editedRiverManager.GetWorld3D().DirectSpaceState;
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
                    if (RiverGizmo.AxisMapping.ContainsKey(Constraint))
                    {
                        var axis = RiverGizmo.AxisMapping[Constraint];
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
                    else if (RiverGizmo.PlaneMapping.ContainsKey(Constraint))
                    {
                        var normal = RiverGizmo.PlaneMapping[Constraint];
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

            bakedClosestPoint = _editedRiverManager.ToLocal(newPos);
        }

        var ur = GetUndoRedo();
        ur.CreateAction("Add River point");
        ur.AddDoMethod(_editedRiverManager, RiverManager.MethodName.AddPoint, bakedClosestPoint, closestSegment, Vector3.Zero, 0f);
        ur.AddDoMethod(_editedRiverManager, RiverManager.MethodName.PropertiesChanged);
        ur.AddDoMethod(_editedRiverManager, RiverManager.MethodName.SetMaterials, "i_valid_flowmap", false);
        ur.AddDoProperty(_editedRiverManager, RiverManager.PropertyName.ValidFlowmap, false);
        ur.AddDoMethod(_editedRiverManager, Node.MethodName.UpdateConfigurationWarnings);

        if (closestSegment == -1)
        {
            ur.AddUndoMethod(_editedRiverManager, RiverManager.MethodName.RemovePoint, _editedRiverManager.Curve.PointCount);
        }
        else
        {
            ur.AddUndoMethod(_editedRiverManager, RiverManager.MethodName.RemovePoint, closestSegment + 1);
        }

        ur.AddUndoMethod(_editedRiverManager, RiverManager.MethodName.PropertiesChanged);
        ur.AddUndoMethod(_editedRiverManager, RiverManager.MethodName.SetMaterials, "i_valid_flowmap", _editedRiverManager.ValidFlowmap);
        ur.AddUndoProperty(_editedRiverManager, RiverManager.PropertyName.ValidFlowmap, _editedRiverManager.ValidFlowmap);
        ur.AddUndoMethod(_editedRiverManager, Node.MethodName.UpdateConfigurationWarnings);
        ur.CommitAction();

        return true;
    }

    #endregion

    public override void _EnterTree()
    {
        AddCustomType("River", "Node3D", ResourceLoader.Load<Script>($"{PluginPath}/RiverManager.cs"), ResourceLoader.Load<Texture2D>($"{PluginPath}/icons/river.svg"));
        AddCustomType("WaterSystem", "Node3D", ResourceLoader.Load<Script>($"{PluginPath}/WaterSystemManager.cs"), ResourceLoader.Load<Texture2D>($"{PluginPath}/icons/system.svg"));
        AddCustomType("Buoyant", "Node3D", ResourceLoader.Load<Script>($"{PluginPath}/BuoyantManager.cs"), ResourceLoader.Load<Texture2D>($"{PluginPath}/icons/buoyant.svg"));

        _waterSystemControls = ResourceLoader.Load<PackedScene>($"{PluginPath}/gui/water_system_controls.tscn").Instantiate<WaterSystemControls>();
        _riverControls = ResourceLoader.Load<PackedScene>($"{PluginPath}/gui/river_controls.tscn").Instantiate<RiverControls>();

        AddNode3DGizmoPlugin(RiverGizmo);
        AddInspectorPlugin(GradientInspector);

        RiverGizmo.EditorPlugin = this;
        _riverControls.Mode += OnModeChange;
        _riverControls.Options += OnOptionChange;

        _progressWindow = ResourceLoader.Load<PackedScene>($"{PluginPath}/gui/progress_window.tscn").Instantiate<ProgressWindow>();
        _riverControls.AddChild(_progressWindow);

        _editorSelection = EditorInterface.Singleton.GetSelection();
        _editorSelection.SelectionChanged += OnSelectionChange;

        SceneChanged += OnSceneChanged;
        SceneClosed += OnSceneClosed;
    }

    public override void _ExitTree()
    {
        RemoveCustomType("River");
        RemoveCustomType("Water System");
        RemoveCustomType("Buoyant");
        RemoveNode3DGizmoPlugin(RiverGizmo);
        RemoveInspectorPlugin(GradientInspector);

        _riverControls.Mode -= OnModeChange;
        _riverControls.Options -= OnOptionChange;
        _editorSelection.SelectionChanged -= OnSelectionChange;

        SceneChanged -= OnSceneChanged;
        SceneClosed -= OnSceneClosed;

        HideRiverControlPanel();
        HideWaterSystemControlPanel();
    }

    public override bool _Handles(GodotObject @object)
    {
        return @object is RiverManager or WaterSystemManager;
    }

    public override int _Forward3DGuiInput(Camera3D camera, InputEvent @event)
    {
        if (_editedRiverManager == null)
        {
            return 0;
        }

        var globalTransform = _editedRiverManager.Transform;

        if (_editedRiverManager.IsInsideTree())
        {
            globalTransform = _editedRiverManager.GlobalTransform;
        }

        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left } mouseEvent)
        {
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

            return 1;
        }

        if (_editedRiverManager is not null)
        {
            // Forward input to river controls.
            return _riverControls.SpatialGuiInput(@event) ? 1 : 0;
        }

        // TODO - This should be updated to the enum when it's fixed https://github.com/godotengine/godot/pull/64465
        return 0;
    }
}
