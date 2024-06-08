using Godot;
using System;
using System.Collections.Generic;
using Waterways.UI.Data;
using Waterways.UI.Util;
using Waterwaysnet.addons.waterways_net.UI.Data;

namespace Waterways.UI;

public partial class RiverGizmo : EditorNode3DGizmoPlugin
{
    private Transform3D? _handleBaseTransform;

    #region Util

    private void DrawPath(EditorNode3DGizmo gizmo, Curve3D curve)
    {
        var path = new List<Vector3>();
        var bakedPoints = curve.GetBakedPoints();

        for (var i = 0; i < bakedPoints.Length - 1; i++)
        {
            path.Add(bakedPoints[i]);
            path.Add(bakedPoints[i + 1]);
        }

        gizmo.AddLines([.. path], GetMaterial(GizmoConstant.Materials.Path));
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
            var pointWidthPosRight = river.Curve.GetPointPosition(i) + (river.Curve.GetPointOut(i).Cross(Vector3.Up).Normalized() * river.PointWidths[i]);
            var pointWidthPosLeft = river.Curve.GetPointPosition(i) + (river.Curve.GetPointOut(i).Cross(Vector3.Down).Normalized() * river.PointWidths[i]);

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

        gizmo.AddLines([.. lines], GetMaterial(GizmoConstant.Materials.HandleLines));

        // Add each handle twice, for both material types.
        // Needs to be grouped by material "type" since that's what influences the handle indices.
        gizmo.AddHandles([.. handlesCenter], GetMaterial(GizmoConstant.Materials.HandlesCenter, gizmo), []);
        gizmo.AddHandles([.. handlesControlPoints], GetMaterial(GizmoConstant.Materials.HandlesControlPoints, gizmo), []);
        gizmo.AddHandles([.. handlesWidth], GetMaterial(GizmoConstant.Materials.HandlesWidth, gizmo), []);

        gizmo.AddHandles([.. handlesCenter], GetMaterial(GizmoConstant.Materials.HandlesCenterDepth, gizmo), []);
        gizmo.AddHandles([.. handlesControlPoints], GetMaterial(GizmoConstant.Materials.HandlesControlPointsDepth, gizmo), []);
        gizmo.AddHandles([.. handlesWidth], GetMaterial(GizmoConstant.Materials.HandlesWidthDepth, gizmo), []);
    }

    private void CreateRiverHandleMaterial(string name, Color color, bool noDepthTest)
    {
        CreateHandleMaterial(name);
        var handlesCenterMaterial = GetMaterial(name);
        handlesCenterMaterial.AlbedoColor = color;
        handlesCenterMaterial.NoDepthTest = noDepthTest;
    }

    private static Vector3? GetColliderHandlePosition(Vector3 rayFrom, Vector3 rayDirection, PhysicsDirectSpaceState3D spaceState)
    {
        // TODO - make in / out handles snap to a plane based on the normal of
        // the raycast hit instead.
        var rayParams = new PhysicsRayQueryParameters3D
        {
            From = rayFrom,
            To = rayFrom + (rayDirection * 4096)
        };

        var newPos = (Vector3?) null;
        var result = spaceState.IntersectRay(rayParams);
        if (result?.Count > 0)
        {
            newPos = result["position"].AsVector3();
        }

        return newPos;
    }

    private static Vector3? GetDefaultHandlePosition(Vector3 rayFrom, Vector3 rayDirection, Camera3D camera, Vector3 positionPreviousGlobal)
    {
        var plane = new Plane(positionPreviousGlobal, positionPreviousGlobal + camera.Transform.Basis.X, positionPreviousGlobal + camera.Transform.Basis.Y);
        return plane.IntersectsRay(rayFrom, rayDirection);
    }

    private Vector3? GetConstrainedHandlePosition(Vector3 rayFrom, Vector3 rayDirection, Vector3 positionPreviousGlobal)
    {
        var newPos = (Vector3?)null;

        if (GizmoConstant.AxisMapping.TryGetValue(EditorPlugin.RiverControl.CurrentConstraint, out var axis))
        {
            if (EditorPlugin.RiverControl.IsLocalEditing)
            {
                axis = _handleBaseTransform.Value.Basis * (axis);
            }

            var axisFrom = positionPreviousGlobal + (axis * GizmoConstant.Constraints.AxisConstraintLength);
            var axisTo = positionPreviousGlobal - (axis * GizmoConstant.Constraints.AxisConstraintLength);
            var rayTo = rayFrom + (rayDirection * GizmoConstant.Constraints.AxisConstraintLength);
            var result = Geometry3D.GetClosestPointsBetweenSegments(axisFrom, axisTo, rayFrom, rayTo);
            newPos = result[0];
        }
        else if (GizmoConstant.PlaneMapping.TryGetValue(EditorPlugin.RiverControl.CurrentConstraint, out var normal))
        {
            if (EditorPlugin.RiverControl.IsLocalEditing)
            {
                normal = _handleBaseTransform.Value.Basis * (normal);
            }

            var projected = positionPreviousGlobal.Project(normal);
            var direction = Mathf.Sign(projected.Dot(normal));
            var distance = direction * projected.Length();
            var plane = new Plane(normal, distance);
            newPos = plane.IntersectsRay(rayFrom, rayDirection);
        }

        return newPos;
    }

    #endregion

    public RiverGizmo()
    {
        // Two materials for every handle type.
        // 1) Transparent handle that is always shown.
        // 2) Opaque handle that is only shown above terrain (when passing depth test)
        // Note that this impacts the point index of the handles. See table in HandlesHelper.cs

        CreateRiverHandleMaterial(GizmoConstant.Materials.HandlesCenter, GizmoConstant.Materials.HandlesCenterColor, true);
        CreateRiverHandleMaterial(GizmoConstant.Materials.HandlesCenterDepth, GizmoConstant.Materials.HandlesCenterDepthColor, false);

        CreateRiverHandleMaterial(GizmoConstant.Materials.HandlesControlPoints, GizmoConstant.Materials.HandlesControlColor, true);
        CreateRiverHandleMaterial(GizmoConstant.Materials.HandlesControlPointsDepth, GizmoConstant.Materials.HandlesControlDepthColor, false);

        CreateRiverHandleMaterial(GizmoConstant.Materials.HandlesWidth, GizmoConstant.Materials.HandlesWidthColor, true);
        CreateRiverHandleMaterial(GizmoConstant.Materials.HandlesWidthDepth, GizmoConstant.Materials.HandlesWidthDepthColor, false);

        var lineMaterial = new StandardMaterial3D
        {
            AlbedoColor = GizmoConstant.Materials.HandlesLineColor,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            RenderPriority = 10,
            NoDepthTest = true
        };

        AddMaterial(GizmoConstant.Materials.Path, lineMaterial);
        AddMaterial(GizmoConstant.Materials.HandleLines, lineMaterial);
    }

    public WaterwaysPlugin EditorPlugin { get; set; }

    public override string _GetGizmoName() => nameof(RiverGizmo);

    public override bool _HasGizmo(Node3D spatial)
    {
        return spatial is RiverManager;
    }

    public override string _GetHandleName(EditorNode3DGizmo gizmo, int index, bool secondary)
    {
        return $"Handle {index}";
    }

    public override Variant _GetHandleValue(EditorNode3DGizmo gizmo, int index, bool secondary)
    {
        var river = (RiverManager) gizmo.GetNode3D();
        var pointCount = river.Curve.PointCount;

        if (HandlesHelper.IsCenterPoint(index, pointCount))
        {
            return river.Curve.GetPointPosition(HandlesHelper.GetCurveIndex(index, pointCount));
        }

        if (HandlesHelper.IsControlPointIn(index, pointCount))
        {
            return river.Curve.GetPointIn(HandlesHelper.GetCurveIndex(index, pointCount));
        }

        if (HandlesHelper.IsControlPointOut(index, pointCount))
        {
            return river.Curve.GetPointOut(HandlesHelper.GetCurveIndex(index, pointCount));
        }

        if (HandlesHelper.IsWidthPointLeft(index, pointCount) || HandlesHelper.IsWidthPointRight(index, pointCount))
        {
            return river.PointWidths[HandlesHelper.GetCurveIndex(index, pointCount)];
        }

        return Variant.CreateFrom(-1);
    }

    // Called when handle is moved
    public override void _SetHandle(EditorNode3DGizmo gizmo, int index, bool secondary, Camera3D camera, Vector2 point)
    {
        var river = (RiverManager)gizmo.GetNode3D();
        var state = new HandleState(river, index);

        if (_handleBaseTransform == null)
        {
            var z = river.Curve.GetPointOut(state.RiverPointIndex).Normalized();
            var x = z.Cross(Vector3.Down).Normalized();
            var y = z.Cross(x).Normalized();
            _handleBaseTransform = new Transform3D(new Basis(x, y, z) * state.GlobalTransform.Basis, state.PreviousGlobalPosition);
        }

        // Point, in and out handles
        var spaceState = river.GetWorld3D().DirectSpaceState;
        var rayFrom = camera.ProjectRayOrigin(point);
        var rayDir = camera.ProjectRayNormal(point);

        if (state.HandleType is HandleType.Center or HandleType.PointIn or HandleType.PointOut)
        {
            var newGlobalPosition = EditorPlugin.RiverControl.CurrentConstraint switch
            {
                ConstraintType.None => GetDefaultHandlePosition(rayFrom, rayDir, camera, state.PreviousGlobalPosition),
                ConstraintType.Colliders => GetColliderHandlePosition(rayFrom, rayDir, spaceState),
                _ => GetConstrainedHandlePosition(rayFrom, rayDir, state.PreviousGlobalPosition),
            };

            if (newGlobalPosition == null)
            {
                return;
            }

            // TODO: implement rounding when control is pressed.
            // How do we round when in local axis/plane mode?
            var newPosition = river.ToLocal(newGlobalPosition.Value);

            if (state.HandleType is HandleType.Center)
            {
                river.Curve.SetPointPosition(state.RiverPointIndex, newPosition);
            }
            else if (state.HandleType == HandleType.PointIn)
            {
                river.Curve.SetPointIn(state.RiverPointIndex, newPosition - state.BasePointPosition);
                river.Curve.SetPointOut(state.RiverPointIndex, -(newPosition - state.BasePointPosition));
            }
            else if (state.HandleType == HandleType.PointOut)
            {
                river.Curve.SetPointOut(state.RiverPointIndex, newPosition - state.BasePointPosition);
                river.Curve.SetPointIn(state.RiverPointIndex, -(newPosition - state.BasePointPosition));
            }
        }

        // Widths handles
        if (state.HandleType is HandleType.WidthLeft or HandleType.WidthRight)
        {
            var p1 = state.BasePointPosition;
            var p2 = Vector3.Zero;

            if (state.HandleType == HandleType.WidthLeft)
            {
                p2 = river.Curve.GetPointOut(state.RiverPointIndex).Cross(Vector3.Up).Normalized() * 4096;
            }
            else if (state.HandleType == HandleType.WidthRight)
            {
                p2 = river.Curve.GetPointOut(state.RiverPointIndex).Cross(Vector3.Down).Normalized() * 4096;
            }

            var q1 = state.GlobalInverse * rayFrom;
            var q2 = state.GlobalInverse * (rayFrom + (rayDir * 4096));

            var closestPoints = Geometry3D.GetClosestPointsBetweenSegments(p1, p2, q1, q2);
            var direction = closestPoints[0].DistanceTo(state.BasePointPosition) - state.PreviousPosition.DistanceTo(state.BasePointPosition);

            river.PointWidths[state.RiverPointIndex] += direction;
            river.PointWidths[state.RiverPointIndex] = Mathf.Max(river.PointWidths[state.RiverPointIndex], GizmoConstant.Constraints.MinDistanceToCenterHandle);
        }

        _Redraw(gizmo);
    }

    public override void _CommitHandle(EditorNode3DGizmo gizmo, int index, bool secondary, Variant restore, bool cancel)
    {
        var river = (RiverManager)gizmo.GetNode3D();

        var curve = river.Curve;
        var pointCount = river.Curve.PointCount;

        var plugin = EditorPlugin.GetUndoRedo();
        plugin.CreateAction("Change River Shape");
        var pointIndex = HandlesHelper.GetCurveIndex(index, pointCount);

        if (HandlesHelper.IsCenterPoint(index, pointCount))
        {
            plugin.AddDoMethod(curve, Curve3D.MethodName.SetPointPosition, pointIndex, river.Curve.GetPointPosition(pointIndex));
            plugin.AddUndoMethod(curve, Curve3D.MethodName.SetPointPosition, pointIndex, restore.AsSingle());
        }
        else if (HandlesHelper.IsControlPointIn(index, pointCount))
        {
            plugin.AddDoMethod(curve, Curve3D.MethodName.SetPointIn, pointIndex, river.Curve.GetPointIn(pointIndex));
            plugin.AddUndoMethod(curve, Curve3D.MethodName.SetPointIn, pointIndex, restore.AsVector3());
            plugin.AddDoMethod(curve, Curve3D.MethodName.SetPointOut, pointIndex, river.Curve.GetPointOut(pointIndex));
            plugin.AddUndoMethod(curve, Curve3D.MethodName.SetPointOut, pointIndex, -restore.AsVector3());
        }
        else if (HandlesHelper.IsControlPointOut(index, pointCount))
        {
            plugin.AddDoMethod(curve, Curve3D.MethodName.SetPointOut, pointIndex, river.Curve.GetPointOut(pointIndex));
            plugin.AddUndoMethod(curve, Curve3D.MethodName.SetPointOut, pointIndex, restore.AsVector3());
            plugin.AddDoMethod(curve, Curve3D.MethodName.SetPointIn, pointIndex, river.Curve.GetPointIn(pointIndex));
            plugin.AddUndoMethod(curve, Curve3D.MethodName.SetPointIn, pointIndex, -restore.AsVector3());
        }
        else if (HandlesHelper.IsWidthPointLeft(index, pointCount) || HandlesHelper.IsWidthPointRight(index, pointCount))
        {
            var riverWidthsUndo = river.PointWidths.Duplicate(true);
            riverWidthsUndo[pointIndex] = restore.AsSingle();
            plugin.AddDoProperty(river, nameof(RiverManager.PointWidths), river.PointWidths);
            plugin.AddUndoProperty(river, nameof(RiverManager.PointWidths), riverWidthsUndo);
        }

        plugin.AddDoMethod(river, RiverManager.MethodName.UpdateRiver);
        plugin.AddUndoMethod(river, RiverManager.MethodName.UpdateRiver);
        plugin.CommitAction();

        _Redraw(gizmo);
    }

    public override void _Redraw(EditorNode3DGizmo gizmo)
    {
        if (gizmo.GetNode3D() is not RiverManager river)
        {
            gizmo.Clear();
            return;
        }

        gizmo.Clear();
        DrawPath(gizmo, river.Curve);
        DrawHandles(gizmo, river);
    }
}
