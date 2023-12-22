using Godot;
using System.Collections.Generic;
using Waterways.Gui;

namespace Waterways;

public partial class RiverGizmo : EditorNode3DGizmoPlugin
{
    public const int HandlesPerPoint = 5;
    public const float AxisConstraintLength = 4096f;

    public static readonly IReadOnlyDictionary<RiverControls.Constraints, Vector3> AxisMapping = new Dictionary<RiverControls.Constraints, Vector3>  {
        { RiverControls.Constraints.AxisX, Vector3.Right },
        { RiverControls.Constraints.AxisY, Vector3.Up },
        { RiverControls.Constraints.AxisZ, Vector3.Back}
    };

    public static readonly IReadOnlyDictionary<RiverControls.Constraints, Vector3> PlaneMapping = new Dictionary<RiverControls.Constraints, Vector3> {
        { RiverControls.Constraints.PlaneYz, Vector3.Right },
        { RiverControls.Constraints.PlaneXz, Vector3.Up },
        { RiverControls.Constraints.PlaneXy, Vector3.Back}
    };

    public WaterwaysPlugin EditorPlugin;

    private Material _pathMat;
    private Material _handleLinesMat;
    private Transform3D? _handleBaseTransform;

    // Ensure that the width handle can't end up inside the center handle
    // as then it is hard to separate them again.
    public const float MinDistToCenterHandle = 0.02f;

    public RiverGizmo()
    {
        // Two materials for every handle type.
        // 1) Transparent handle that is always shown.
        // 2) Opaque handle that is only shown above terrain (when passing depth test)
        // Note that this impacts the point index of the handles. See table below.
        CreateHandleMaterial("handles_center");
        CreateHandleMaterial("handles_control_points");
        CreateHandleMaterial("handles_width");
        CreateHandleMaterial("handles_center_with_depth");
        CreateHandleMaterial("handles_control_points_with_depth");
        CreateHandleMaterial("handles_width_with_depth");

        var handlesCenterMat = GetMaterial("handles_center");
        var handlesCenterMatWd = GetMaterial("handles_center_with_depth");
        var handlesControlPointsMat = GetMaterial("handles_control_points");
        var handlesControlPointsMatWd = GetMaterial("handles_control_points_with_depth");
        var handlesWidthMat = GetMaterial("handles_width");
        var handlesWidthMatWd = GetMaterial("handles_width_with_depth");

        handlesCenterMat.AlbedoColor = new Color(1.0f, 1.0f, 0.0f, 0.25f);
        handlesCenterMatWd.AlbedoColor = new Color(1.0f, 1.0f, 0.0f, 1.0f);
        handlesControlPointsMat.AlbedoColor = new Color(1.0f, 0.5f, 0.0f, 0.25f);
        handlesControlPointsMatWd.AlbedoColor = new Color(1.0f, 0.5f, 0.0f, 1.0f);
        handlesWidthMat.AlbedoColor = new Color(0.0f, 1.0f, 1.0f, 0.25f);
        handlesWidthMatWd.AlbedoColor = new Color(0.0f, 1.0f, 1.0f, 1.0f);

        handlesCenterMat.NoDepthTest = true;
        handlesCenterMatWd.NoDepthTest = false;
        handlesControlPointsMat.NoDepthTest = true;
        handlesControlPointsMatWd.NoDepthTest = false;
        handlesWidthMat.NoDepthTest = true;
        handlesWidthMatWd.NoDepthTest = false;

        var mat = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            NoDepthTest = true,
            AlbedoColor = new Color(1.0f, 1.0f, 0.0f),
            RenderPriority = 10
        };

        AddMaterial("path", mat);
        AddMaterial("handle_lines", mat);
    }

    public override string _GetGizmoName()
    {
        return "Waterways";
    }

    public void Reset()
    {
        _handleBaseTransform = null;
    }

    public string GetName()
    {
        return "RiverInput";
    }

    public override bool _HasGizmo(Node3D spatial)
    {
        return spatial is RiverManager;
    }

    // TODO - figure out of this new "secondary" bool should be used
    public override string _GetHandleName(EditorNode3DGizmo gizmo, int index, bool secondary)
    {
        return $"Handle {index}";
    }

    /* 
    Handles are pushed to separate handle lists, one per material (using gizmo.add_handles).
	A handle's "index" is given (by Godot) in order it was added to a gizmo. 
	Given that N = points in the curve:
	- First we add the center ("actual") curve handles, therefore
	  the handle's index is the same as the curve point's index.
	- Then we add the in and out points together. So the first curve point's IN handle
	  gets an index of N. The OUT handle gets N+1.
	- Finally the left/right indices come last, and the first curve point's LEFT is N * 3 .
	  (3 because there are three rows before the left/right indices)
	
	Examples for N = 2, 3, 4:
	curve points 2:0   1      3:0   1   2        4:0   1   2   3
	------------------------------------------------------------------
	center         0   1        0   1   2          0   1   2   3
	in             2   4        3   5   7          4   6   8   10
	out            3   5        4   6   8          5   7   9   11
	left           6   8        9   11  13         12  14  16  18
	right          7   9        10  12  14         13  15  17  19
	
	The following utility functions calculate to and from curve/handle indices.
	*/

    private bool IsCenterPoint(int index, int riverCurvePointCount)
    {
        return index < riverCurvePointCount;
    }
    private bool IsControlPointIn(int index, int riverCurvePointCount)
    {
        if (index < riverCurvePointCount)
        {
            return false;
        }

        if (index >= riverCurvePointCount * 3)
        {
            return false;
        }

        return (index - riverCurvePointCount) % 2 == 0;
    }

    private bool IsControlPointOut(int index, int riverCurvePointCount)
    {
        if (index < riverCurvePointCount)
        {
            return false;
        }

        if (index >= riverCurvePointCount * 3)
        {
            return false;
        }

        return (index - riverCurvePointCount) % 2 == 1;
    }

    private bool IsWidthPointLeft(int index, int riverCurvePointCount)
    {
        if (index < riverCurvePointCount * 3)
        {
            return false;
        }

        return (index - (riverCurvePointCount * 3)) % 2 == 0;
    }

    private bool IsWidthPointRight(int index, int riverCurvePointCount)
    {
        if (index < riverCurvePointCount * 3)
        {
            return false;
        }

        return (index - (riverCurvePointCount * 3)) % 2 == 1;
    }

    private int GetCurveIndex(int index, int pointCount)
    {
        if (IsCenterPoint(index, pointCount))
        {
            return index;
        }

        if (IsControlPointIn(index, pointCount))
        {
            return (index - pointCount) / 2;
        }

        if (IsControlPointOut(index, pointCount))
        {
            return (index - pointCount - 1) / 2;
        }

        if (IsWidthPointLeft(index, pointCount) || IsWidthPointRight(index, pointCount))
        {
            return (index - (pointCount * 3)) / 2;
        }

        return -1;
    }

    private int GetPointIndex(int curveIndex, bool isCenter, bool isCpIn, bool isCpOut, bool isWidthLeft, bool isWidthRight, int pointCount)
    {
        if (isCenter)
        {
            return curveIndex;
        }

        if (isCpIn)
        {
            return pointCount + (curveIndex * 2);
        }

        if (isCpOut)
        {
            return pointCount + 1 + (curveIndex * 2);
        }

        if (isWidthLeft)
        {
            return (pointCount * 3) + (curveIndex * 2);
        }

        if (isWidthRight)
        {
            return (pointCount * 3) + 1 + (curveIndex * 2);
        }

        return -1;
    }

    // TODO - figure out of this new "secondary" bool should be used
    public override Variant _GetHandleValue(EditorNode3DGizmo gizmo, int index, bool secondary)
    {
        var river = (RiverManager)gizmo.GetNode3D();
        var pointCount = river.Curve.PointCount;

        if (IsCenterPoint(index, pointCount))
        {
            return river.Curve.GetPointPosition(GetCurveIndex(index, pointCount));
        }

        if (IsControlPointIn(index, pointCount))
        {
            return river.Curve.GetPointIn(GetCurveIndex(index, pointCount));
        }

        if (IsControlPointOut(index, pointCount))
        {
            return river.Curve.GetPointOut(GetCurveIndex(index, pointCount));
        }

        if (IsWidthPointLeft(index, pointCount) || IsWidthPointRight(index, pointCount))
        {
            return river.Widths[GetCurveIndex(index, pointCount)];
        }

        return Variant.CreateFrom(-1);
    }

    // Called when handle is moved
    // TODO - figure out of this new "secondary" bool should be used
    public override void _SetHandle(EditorNode3DGizmo gizmo, int index, bool secondary, Camera3D camera, Vector2 point)
    {
        var river = (RiverManager)gizmo.GetNode3D();
        var spaceState = river.GetWorld3D().DirectSpaceState;

        var globalTransform = river.Transform;
        if (river.IsInsideTree())
        {
            globalTransform = river.GlobalTransform;
        }

        var globalInverse = globalTransform.AffineInverse();

        var rayFrom = camera.ProjectRayOrigin(point);
        var rayDir = camera.ProjectRayNormal(point);

        var oldPos = Vector3.Zero;
        var pointCount = river.Curve.PointCount;
        var pIndex = GetCurveIndex(index, pointCount);
        var basePoint = river.Curve.GetPointPosition(pIndex);

        // Logic to move handles
        var isCenter = IsCenterPoint(index, pointCount);
        var isCpIn = IsControlPointIn(index, pointCount);
        var isCpOut = IsControlPointOut(index, pointCount);
        var isWidthLeft = IsWidthPointLeft(index, pointCount);
        var isWidthRight = IsWidthPointRight(index, pointCount);

        if (isCenter)
        {
            oldPos = basePoint;
        }

        if (isCpIn)
        {
            oldPos = river.Curve.GetPointIn(pIndex) + basePoint;
        }

        if (isCpOut)
        {
            oldPos = river.Curve.GetPointOut(pIndex) + basePoint;
        }

        if (isWidthLeft)
        {
            oldPos = basePoint + (river.Curve.GetPointOut(pIndex).Cross(Vector3.Up).Normalized() * river.Widths[pIndex]);
        }

        if (isWidthRight)
        {
            oldPos = basePoint + (river.Curve.GetPointOut(pIndex).Cross(Vector3.Down).Normalized() * river.Widths[pIndex]);
        }

        var oldPosGlobal = river.ToGlobal(oldPos);

        if (_handleBaseTransform == null)
        {
            // This is the first set_handle() call since the last reset so we
            // use the current handle position as our _handle_base_transform
            var z = river.Curve.GetPointOut(pIndex).Normalized();
            var x = z.Cross(Vector3.Down).Normalized();
            var y = z.Cross(x).Normalized();
            _handleBaseTransform = new Transform3D(
                new Basis(x, y, z) * globalTransform.Basis,
                oldPosGlobal
            );
        }

        // Point, in and out handles
        if (isCenter || isCpIn || isCpOut)
        {
            Vector3? newPos = null;

            switch (EditorPlugin.Constraint)
            {
                case RiverControls.Constraints.Colliders:
                {
                    // TODO - make in / out handles snap to a plane based on the normal of
                    // the raycast hit instead.
                    var rayParams = new PhysicsRayQueryParameters3D();
                    rayParams.From = rayFrom;
                    rayParams.To = rayFrom + (rayDir * 4096);
                    var result = spaceState.IntersectRay(rayParams);
                    if (result?.Count > 0)
                    {
                        newPos = result["position"].AsVector3();
                    }

                    break;
                }
                case RiverControls.Constraints.None:
                {
                    var plane = new Plane(oldPosGlobal, oldPosGlobal + camera.Transform.Basis.X, oldPosGlobal + camera.Transform.Basis.Y);
                    newPos = plane.IntersectsRay(rayFrom, rayDir);
                    break;
                }
                default:
                {
                    if (AxisMapping.ContainsKey(EditorPlugin.Constraint))
                    {
                        var axis = AxisMapping[EditorPlugin.Constraint];
                        if (EditorPlugin.LocalEditing)
                        {
                            axis = _handleBaseTransform.Value.Basis * (axis);
                        }

                        var axisFrom = oldPosGlobal + (axis * AxisConstraintLength);
                        var axisTo = oldPosGlobal - (axis * AxisConstraintLength);
                        var rayTo = rayFrom + (rayDir * AxisConstraintLength);
                        var result = Geometry3D.GetClosestPointsBetweenSegments(axisFrom, axisTo, rayFrom, rayTo);
                        newPos = result[0];
                    }
                    else if (PlaneMapping.ContainsKey(EditorPlugin.Constraint))
                    {
                        var normal = PlaneMapping[EditorPlugin.Constraint];
                        if (EditorPlugin.LocalEditing)
                        {
                            normal = _handleBaseTransform.Value.Basis * (normal);
                        }

                        var projected = oldPosGlobal.Project(normal);
                        var direction = Mathf.Sign(projected.Dot(normal));
                        var distance = direction * projected.Length();
                        var plane = new Plane(normal, distance);
                        newPos = plane.IntersectsRay(rayFrom, rayDir);
                    }

                    break;
                }
            }

            // Discard if no valid position was found
            if (newPos == null)
            {
                return;
            }

            // TODO: implement rounding when control is pressed.
            // How do we round when in local axis/plane mode?

            var newPosLocal = river.ToLocal(newPos.Value);

            if (isCenter)
            {
                river.SetCurvePointPosition(pIndex, newPosLocal);
            }
            if (isCpIn)
            {
                river.SetCurvePointIn(pIndex, newPosLocal - basePoint);
                river.SetCurvePointOut(pIndex, -(newPosLocal - basePoint));
            }
            if (isCpOut)
            {
                river.SetCurvePointOut(pIndex, newPosLocal - basePoint);
                river.SetCurvePointIn(pIndex, -(newPosLocal - basePoint));
            }
        }

        // Widths handles
        if (isWidthLeft || isWidthRight)
        {
            var p1 = basePoint;
            var p2 = Vector3.Zero;

            if (isWidthLeft)
            {
                p2 = river.Curve.GetPointOut(pIndex).Cross(Vector3.Up).Normalized() * 4096;
            }

            if (isWidthRight)
            {
                p2 = river.Curve.GetPointOut(pIndex).Cross(Vector3.Down).Normalized() * 4096;
            }

            var g1 = globalInverse * (rayFrom);
            var g2 = globalInverse * (rayFrom + (rayDir * 4096));

            var geoPoints = Geometry3D.GetClosestPointsBetweenSegments(p1, p2, g1, g2);
            var dir = geoPoints[0].DistanceTo(basePoint) - oldPos.DistanceTo(basePoint);

            river.Widths[pIndex] += dir;

            // Ensure width handles don't end up inside the center point
            river.Widths[pIndex] = Mathf.Max(river.Widths[pIndex], MinDistToCenterHandle);
        }

        _Redraw(gizmo);
    }

    // Handle Undo / Redo of handle movements
    // TODO - figure out of this new "secondary" bool should be used
    public override void _CommitHandle(EditorNode3DGizmo gizmo, int index, bool secondary, Variant restore, bool cancel)
    {
        var river = (RiverManager) gizmo.GetNode3D();
        var pointCount = river.Curve.PointCount;

        var ur = EditorPlugin.GetUndoRedo();
        ur.CreateAction("Change River Shape");

        var pIndex = GetCurveIndex(index, pointCount);
        if (IsCenterPoint(index, pointCount))
        {
            ur.AddDoMethod(river, RiverManager.MethodName.SetCurvePointPosition, pIndex, river.Curve.GetPointPosition(pIndex));
            ur.AddUndoMethod(river, RiverManager.MethodName.SetCurvePointPosition, pIndex, restore);
        }
        if (IsControlPointIn(index, pointCount))
        {
            ur.AddDoMethod(river, RiverManager.MethodName.SetCurvePointIn, pIndex, river.Curve.GetPointIn(pIndex));
            ur.AddUndoMethod(river, RiverManager.MethodName.SetCurvePointIn, pIndex, restore);
            ur.AddDoMethod(river, RiverManager.MethodName.SetCurvePointOut, pIndex, river.Curve.GetPointOut(pIndex));
            ur.AddUndoMethod(river, RiverManager.MethodName.SetCurvePointOut, pIndex, -restore.AsSingle());
        }
        if (IsControlPointOut(index, pointCount))
        {
            ur.AddDoMethod(river, RiverManager.MethodName.SetCurvePointOut, pIndex, river.Curve.GetPointIn(pIndex));
            ur.AddUndoMethod(river, RiverManager.MethodName.SetCurvePointOut, pIndex, restore);
            ur.AddDoMethod(river, RiverManager.MethodName.SetCurvePointIn, pIndex, river.Curve.GetPointOut(pIndex));
            ur.AddUndoMethod(river, RiverManager.MethodName.SetCurvePointIn, pIndex, -restore.AsSingle());
        }
        if (IsWidthPointLeft(index, pointCount) || IsWidthPointRight(index, pointCount))
        {
            var riverWidthsUndo = river.Widths.Duplicate(true);
            riverWidthsUndo[pIndex] = restore.AsSingle();
            ur.AddDoProperty(river, RiverManager.PropertyName.Widths, river.Widths);
            ur.AddUndoProperty(river, RiverManager.PropertyName.Widths, riverWidthsUndo);
        }

        ur.AddDoMethod(river, RiverManager.MethodName.PropertiesChanged);
        ur.AddDoMethod(river, RiverManager.MethodName.SetMaterials, "i_valid_flowmap", false);
        ur.AddDoProperty(river, RiverManager.PropertyName.ValidFlowmap, false);
        ur.AddDoMethod(river,   Node.MethodName.UpdateConfigurationWarnings);
        ur.AddUndoMethod(river, RiverManager.MethodName.PropertiesChanged);
        ur.AddUndoMethod(river, RiverManager.MethodName.SetMaterials, "i_valid_flowmap", river.ValidFlowmap);
        ur.AddUndoProperty(river, RiverManager.PropertyName.ValidFlowmap, river.ValidFlowmap);
        ur.AddUndoMethod(river, Node.MethodName.UpdateConfigurationWarnings);
        ur.CommitAction();

        _Redraw(gizmo);
    }

    public override void _Redraw(EditorNode3DGizmo gizmo)
    {
        // Work around for issue where using "get_material" doesn't return a
        // material when redraw is being called manually from _set_handle()
        // so I'm caching the materials instead
        _pathMat ??= GetMaterial("path", gizmo);
        _handleLinesMat ??= GetMaterial("handle_lines", gizmo);

        gizmo.Clear();
        var river = (RiverManager) gizmo.GetNode3D();

        if (!river.IsConnected( RiverManager.SignalName.RiverChanged, Callable.From<EditorNode3DGizmo>(_Redraw)))
        {
            river.RiverChanged += gizmo._Redraw;
        }

        DrawPath(gizmo, river.Curve);
        DrawHandles(gizmo, river);
    }


    private void DrawPath(EditorNode3DGizmo gizmo, Curve3D curve)
    {
        var path = new List<Vector3>();
        var bakedPoints = curve.GetBakedPoints();

        for (var i = 0; i < bakedPoints.Length; i++)
        {
            path.Add(bakedPoints[i]);
            path.Add(bakedPoints[(i + 1) % bakedPoints.Length]);
        }

        gizmo.AddLines([.. path], _pathMat);
    }

    private void DrawHandles(EditorNode3DGizmo gizmo, RiverManager river)
    {
        var lines = new List<Vector3>();
        var handlesCenter = new List<Vector3>();
        var handlesControlPoints = new List<Vector3>();
        var handlesWidth = new List<Vector3>();
        var pointCount = river.Curve.PointCount;

        for (var i = 0; i < pointCount; i++)
        {
            var pointPos = river.Curve.GetPointPosition(i);
            var pointPosIn = river.Curve.GetPointIn(i) + pointPos;
            var pointPosOut = river.Curve.GetPointOut(i) + pointPos;
            var pointWidthPosRight = river.Curve.GetPointPosition(i) + (river.Curve.GetPointOut(i).Cross(Vector3.Up).Normalized() * river.Widths[i]);
            var pointWidthPosLeft = river.Curve.GetPointPosition(i) + (river.Curve.GetPointOut(i).Cross(Vector3.Down).Normalized() * river.Widths[i]);

            handlesCenter.Add(pointPos);
            handlesControlPoints.Add(pointPosIn);
            handlesControlPoints.Add(pointPosOut);
            handlesWidth.Add(pointWidthPosRight);
            handlesWidth.Add(pointWidthPosLeft);

            lines.Add(pointPos);
            lines.Add(pointPosIn);
            lines.Add(pointPos);
            lines.Add(pointPosOut);
            lines.Add(pointPos);
            lines.Add(pointWidthPosRight);
            lines.Add(pointPos);
            lines.Add(pointWidthPosLeft);
        }

        gizmo.AddLines([.. lines], _handleLinesMat);

        // Add each handle twice, for both material types.
        // Needs to be grouped by material "type" since that's what influences the handle indices.
        gizmo.AddHandles([.. handlesCenter], GetMaterial("handles_center", gizmo), []);
        gizmo.AddHandles([.. handlesControlPoints], GetMaterial("handles_control_points", gizmo), []);
        gizmo.AddHandles([.. handlesWidth], GetMaterial("handles_width", gizmo), []);
        gizmo.AddHandles([.. handlesCenter], GetMaterial("handles_center_with_depth", gizmo), []);
        gizmo.AddHandles([.. handlesControlPoints], GetMaterial("handles_control_points_with_depth", gizmo), []);
        gizmo.AddHandles([.. handlesWidth], GetMaterial("handles_width_with_depth", gizmo), []);
    }
}
