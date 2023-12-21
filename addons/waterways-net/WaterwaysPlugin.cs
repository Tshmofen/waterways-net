using Godot;
using Waterway;
using Waterway.Gui;
using Waterways.Gui;

namespace Waterways;

[Tool]
public partial class WaterwaysPlugin : EditorPlugin
{
    private readonly RiverControls _riverControls = new();
    private readonly WaterSystemControls _waterSystemControls = new();

    private EditorSelection _editorSelection;
    private ProgressWindow _progressWindow;
    private string _mode = "select";
    // TODO: find a way to make static!!!
    private dynamic _editedNode;

    public const string PluginPath = "res://addons/waterways-net";

    public RiverGizmo RiverGizmo { get; set; } = new();
    public InspectorPlugin GradientInspector { get; set; } = new();
    public RiverControls.Constraints Constraint { get; set; } = RiverControls.Constraints.None;
    public bool LocalEditing { get; set; }

    public override void _EnterTree()
    {
        AddCustomType("River", "Node3D", ResourceLoader.Load<Script>($"{PluginPath}/RiverManager.cs"), ResourceLoader.Load<Texture2D>($"{PluginPath}/icons/river.svg"));
        AddCustomType("WaterSystem", "Node3D", ResourceLoader.Load<Script>($"{PluginPath}/WaterSystemManager.cs"), ResourceLoader.Load<Texture2D>($"{PluginPath}/icons/system.svg"));
        AddCustomType("Buoyant", "Node3D", ResourceLoader.Load<Script>($"{PluginPath}/BuoyantManager.cs"), ResourceLoader.Load<Texture2D>($"{PluginPath}/icons/buoyant.svg"));

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

    private void OnGenerateFlowMapPressed()
    {
        _editedNode.bake_texture();
    }

    private void OnGenerateMeshPressed()
    {
        _editedNode.spawn_mesh();
    }

    private void OnDebugViewChanged(int index)
    {
        _editedNode.debug_view = index;
    }

    private void OnGenerateSystemMapsPressed()
    {
        _editedNode.generate_system_maps();
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
                _editedNode = manager;
                _riverControls.Menu.DebugViewMenuSelected = _editedNode.debug_view;

                if (!_editedNode.IsConnected(RiverManager.SignalName.ProgressNotified, Callable.From<float, string>(RiverProgressNotified)))
                {
                    _editedNode.Connect(RiverManager.SignalName.ProgressNotified, Callable.From<float, string>(RiverProgressNotified));
                }

                HideWaterSystemControlPanel();
                break;

            case WaterSystemManager:
                // TODO - is there anything we need to add here?
                ShowWaterSystemControlPanel();
                _editedNode = selected[0] as WaterSystemManager;
                HideRiverControlPanel();
                break;

            default:
                _editedNode = null;
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

    private void OnModeChange(string mode)
    {
        _mode = mode;
    }

    private void OnOptionChange(string option, int value)
    {
        switch (option)
        {
            case "constraint":
                Constraint = (RiverControls.Constraints) value;
                if (Constraint == RiverControls.Constraints.Colliders)
                {
                    WaterHelperMethods.ResetAllColliders(_editedNode.GetTree().Root);
                }
                break;

            case "local_mode":
                LocalEditing = value == 1;
                break;
        }
    }

    private int Forward3DGuiInput(Camera3D camera, InputEvent @event)
    {
        if (_editedNode == null)
        {
            // TODO - This should be updated to the enum when it's fixed https://github.com/godotengine/godot/pull/64465
            return 0;
        }

        var globalTransform = _editedNode.Transform;
        if (_editedNode.IsInsideTree())
        {
            globalTransform = _editedNode.GlobalTransform;
        }

        var globalInverse = globalTransform.AffineInverse();

        if (@event is InputEventMouseButton {ButtonIndex: MouseButton.Left} mouseEvent)
        {
            var rayFrom = camera.ProjectRayOrigin(mouseEvent.Position);
            var rayDir = camera.ProjectRayNormal(mouseEvent.Position);
            var g1 = globalInverse * (rayFrom);
            var g2 = globalInverse * (rayFrom + (rayDir * 4096));

            // Iterate through points to find the closest segment
            var curvePoints = _editedNode.get_curve_points();
            var closestDistance = 4096f;
            var closestSegment = -1;

            for (var point = 0; point < curvePoints.Count; point++)
            {
                var p1 = curvePoints[point];
                var p2 = curvePoints[point + 1];
                var result = Geometry3D.GetClosestPointsBetweenSegments(p1, p2, g1, g2);
                var dist = result[0].DistanceTo(result[1]);

                if (dist < closestDistance)
                {
                    closestDistance = dist;
                    closestSegment = point;
                }
            }

            // Iterate through baked points to find the closest position on the curved path
            var bakedCurvePoints = _editedNode.curve.GetBakedPoints();
            var bakedClosestDistance = 4096f;
            var bakedClosestPoint = new Vector3();
            var bakedPointFound = false;

            for (var bakedPoint = 0; bakedPoint < bakedCurvePoints.Length; bakedPoint++)
            {
                var p1 = bakedCurvePoints[bakedPoint];
                var p2 = bakedCurvePoints[bakedPoint + 1];
                var result = Geometry3D.GetClosestPointsBetweenSegments(p1, p2, g1, g2);
                var dist = result[0].DistanceTo(result[1]);

                if (dist < 0.1 && dist < bakedClosestDistance)
                {
                    bakedClosestDistance = dist;
                    bakedClosestPoint = result[0];
                    bakedPointFound = true;
                }
            }

            // In case we were close enough to a line segment to find a segment,
            // but not close enough to the curved line
            if (!bakedPointFound)
            {
                closestSegment = -1;
            }

            switch (_mode)
            {
                // We'll use this closest point to add a point in between if on the line
                // and to remove if close to a point
                case "select":
                {
                    if (!mouseEvent.Pressed)
                    {
                        RiverGizmo.Reset();
                    }
                    return 0;
                }

                case "add" when !mouseEvent.Pressed:
                {
                    // if we don't have a point on the line, we'll calculate a point
                    // based of a plane of the last point of the curve
                    if (closestSegment == -1)
                    {
                        var endPos = _editedNode.curve.GetPointPosition(_editedNode.curve.PointCount - 1);
                        var endPosGlobal = _editedNode.ToGlobal(endPos);

                        var z = _editedNode.curve.GetPointOut(_editedNode.curve.PointCount - 1).Normalized();
                        var x = z.Cross(Vector3.Down).Normalized();
                        var y = z.Cross(x).Normalized();
                        var handleBaseTransform = new Transform3D(new Basis(x, y, z) * globalTransform.Basis, endPosGlobal);

                        var plane = new Plane(endPosGlobal, endPosGlobal + camera.Transform.Basis.X, endPosGlobal + camera.Transform.Basis.Y);
                        var newPos = Vector3.Zero;

                        switch (Constraint)
                        {
                            case RiverControls.Constraints.Colliders:
                            {
                                var spaceState = _editedNode.GetWorld3D().DirectSpaceState;
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
                                    return 0;
                                }

                                break;
                            }

                            case (int) RiverControls.Constraints.None:
                            {
                                newPos = plane.IntersectsRay(rayFrom, rayFrom + (rayDir * 4096))!.Value;
                                break;
                            }

                            default:
                            {
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
                        }

                        bakedClosestPoint = _editedNode.ToLocal(newPos);
                    }

                    var ur = GetUndoRedo();
                    ur.CreateAction("Add River point");
                    ur.AddDoMethod(_editedNode, "AddPoint", bakedClosestPoint, closestSegment);
                    ur.AddDoMethod(_editedNode, "properties_changed");
                    ur.AddDoMethod(_editedNode, "SetMaterials", "i_valid_flowmap", false);
                    ur.AddDoProperty(_editedNode, "valid_flowmap", false);
                    ur.AddDoMethod(_editedNode, "update_configuration_warnings");

                    if (closestSegment == -1)
                    {
                        ur.AddUndoMethod(_editedNode, "RemovePoint", _editedNode.curve.PointCount);
                    }
                    else
                    {
                        ur.AddUndoMethod(_editedNode, "RemovePoint", closestSegment + 1);
                    }

                    ur.AddUndoMethod(_editedNode, "properties_changed");
                    ur.AddUndoMethod(_editedNode, "SetMaterials", "i_valid_flowmap", _editedNode.valid_flowmap);
                    ur.AddUndoProperty(_editedNode, "valid_flowmap", _editedNode.valid_flowmap);
                    ur.AddUndoMethod(_editedNode, "update_configuration_warnings");
                    ur.CommitAction();
                    break;
                }

                case "remove" when !mouseEvent.Pressed:
                {
                    // A closest_segment of -1 means we didn't press close enough to a
                    // point for it to be removed
                    if (closestSegment != -1)
                    {
                        var closestIndex = _editedNode.get_closest_point_to(bakedClosestPoint);
                        var ur = GetUndoRedo();
                        ur.CreateAction("Remove River point");
                        ur.AddDoMethod(_editedNode, "RemovePoint", closestIndex);
                        ur.AddDoMethod(_editedNode, "properties_changed");
                        ur.AddDoMethod(_editedNode, "SetMaterials", "i_valid_flowmap", false);
                        ur.AddDoProperty(_editedNode, "valid_flowmap", false);
                        ur.AddDoMethod(_editedNode, "update_configuration_warnings");

                        if (closestIndex == _editedNode.curve.PointCount - 1)
                        {
                            ur.AddUndoMethod(_editedNode, "AddPoint", _editedNode.curve.GetPointPosition(closestIndex), -1);
                        }
                        else
                        {
                            ur.AddUndoMethod(_editedNode, "AddPoint", _editedNode.curve.GetPointPosition(closestIndex), closestIndex - 1, _editedNode.curve.GetPointOut(closestIndex), _editedNode.widths[closestIndex]);
                        }

                        ur.AddUndoMethod(_editedNode, "properties_changed");
                        ur.AddUndoMethod(_editedNode, "SetMaterials", "i_valid_flowmap", _editedNode.valid_flowmap);
                        ur.AddUndoProperty(_editedNode, "valid_flowmap", _editedNode.valid_flowmap);
                        ur.AddUndoMethod(_editedNode, "update_configuration_warnings");
                        ur.CommitAction();
                    }

                    break;
                }
            }

            // TODO - This should be updated to the enum when it's fixed https://github.com/godotengine/godot/pull/64465
            return 1;
        }

        if (_editedNode is RiverManager)
        {
            // Forward input to river controls. This is cleaner than handling
            // the keybindings here as the keybindings need to interact with
            // the buttons. Handling it here would expose more private details
            // of the controls than needed, instead only the SpatialGuiInput()
            // method needs to be exposed.
            // TODO - so this was returning a bool before? Check this
            return _riverControls.SpatialGuiInput(@event) ? 1 : 0;
        }

        // TODO - This should be updated to the enum when it's fixed https://github.com/godotengine/godot/pull/64465
        return 0;
    }

    private void RiverProgressNotified(float progress, string message)
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
}