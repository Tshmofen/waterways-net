using Godot;
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
        var pIndex = HandlesHelper.GetCurveIndex(index, pointCount);
        var basePoint = river.Curve.GetPointPosition(pIndex);

        // Logic to move handles
        var isCenter = HandlesHelper.IsCenterPoint(index, pointCount);
        var isPointIn = HandlesHelper.IsControlPointIn(index, pointCount);
        var isPointOut = HandlesHelper.IsControlPointOut(index, pointCount);
        var isWidthLeft = HandlesHelper.IsWidthPointLeft(index, pointCount);
        var isWidthRight = HandlesHelper.IsWidthPointRight(index, pointCount);

        if (isCenter)
        {
            oldPos = basePoint;
        }

        if (isPointIn)
        {
            oldPos = river.Curve.GetPointIn(pIndex) + basePoint;
        }

        if (isPointOut)
        {
            oldPos = river.Curve.GetPointOut(pIndex) + basePoint;
        }

        if (isWidthLeft)
        {
            oldPos = basePoint + (river.Curve.GetPointOut(pIndex).Cross(Vector3.Up).Normalized() * river.PointWidths[pIndex]);
        }

        if (isWidthRight)
        {
            oldPos = basePoint + (river.Curve.GetPointOut(pIndex).Cross(Vector3.Down).Normalized() * river.PointWidths[pIndex]);
        }

        var oldPosGlobal = river.ToGlobal(oldPos);

        if (_handleBaseTransform == null)
        {
            var z = river.Curve.GetPointOut(pIndex).Normalized();
            var x = z.Cross(Vector3.Down).Normalized();
            var y = z.Cross(x).Normalized();
            _handleBaseTransform = new Transform3D(new Basis(x, y, z) * globalTransform.Basis, oldPosGlobal);
        }

        // Point, in and out handles
        if (isCenter || isPointIn || isPointOut)
        {
            Vector3? newPos = null;

            switch (EditorPlugin.RiverControl.CurrentConstraint)
            {
                case ConstraintType.Colliders:
                    {
                        // TODO - make in / out handles snap to a plane based on the normal of
                        // the raycast hit instead.
                        var rayParams = new PhysicsRayQueryParameters3D
                        {
                            From = rayFrom,
                            To = rayFrom + (rayDir * 4096)
                        };

                        var result = spaceState.IntersectRay(rayParams);
                        if (result?.Count > 0)
                        {
                            newPos = result["position"].AsVector3();
                        }

                        break;
                    }
                case ConstraintType.None:
                    {
                        var plane = new Plane(oldPosGlobal, oldPosGlobal + camera.Transform.Basis.X, oldPosGlobal + camera.Transform.Basis.Y);
                        newPos = plane.IntersectsRay(rayFrom, rayDir);
                        break;
                    }
                default:
                    {
                        if (GizmoConstant.AxisMapping.TryGetValue(EditorPlugin.RiverControl.CurrentConstraint, out var axis))
                        {
                            if (EditorPlugin.RiverControl.IsLocalEditing)
                            {
                                axis = _handleBaseTransform.Value.Basis * (axis);
                            }

                            var axisFrom = oldPosGlobal + (axis * GizmoConstant.Constraints.AxisConstraintLength);
                            var axisTo = oldPosGlobal - (axis * GizmoConstant.Constraints.AxisConstraintLength);
                            var rayTo = rayFrom + (rayDir * GizmoConstant.Constraints.AxisConstraintLength);
                            var result = Geometry3D.GetClosestPointsBetweenSegments(axisFrom, axisTo, rayFrom, rayTo);
                            newPos = result[0];
                        }
                        else if (GizmoConstant.PlaneMapping.TryGetValue(EditorPlugin.RiverControl.CurrentConstraint, out var normal))
                        {
                            if (EditorPlugin.RiverControl.IsLocalEditing)
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
                river.Curve.SetPointPosition(pIndex, newPosLocal);
            }
            if (isPointIn)
            {
                river.Curve.SetPointIn(pIndex, newPosLocal - basePoint);
                river.Curve.SetPointOut(pIndex, -(newPosLocal - basePoint));
            }
            if (isPointOut)
            {
                river.Curve.SetPointOut(pIndex, newPosLocal - basePoint);
                river.Curve.SetPointIn(pIndex, -(newPosLocal - basePoint));
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

            var g1 = globalInverse * rayFrom;
            var g2 = globalInverse * (rayFrom + (rayDir * 4096));

            var geoPoints = Geometry3D.GetClosestPointsBetweenSegments(p1, p2, g1, g2);
            var dir = geoPoints[0].DistanceTo(basePoint) - oldPos.DistanceTo(basePoint);

            river.PointWidths[pIndex] += dir;
            river.PointWidths[pIndex] = Mathf.Max(river.PointWidths[pIndex], GizmoConstant.Constraints.MinDistanceToCenterHandle);
        }

        _Redraw(gizmo);
    }

    public override void _CommitHandle(EditorNode3DGizmo gizmo, int index, bool secondary, Variant restore, bool cancel)
    {
        var restoreF = restore.AsSingle();
        var river = (RiverManager)gizmo.GetNode3D();

        var curve = river.Curve;
        var pointCount = river.Curve.PointCount;

        var plugin = EditorPlugin.GetUndoRedo();
        plugin.CreateAction("Change River Shape");
        var pIndex = HandlesHelper.GetCurveIndex(index, pointCount);

        if (HandlesHelper.IsCenterPoint(index, pointCount))
        {
            plugin.AddDoMethod(curve, Curve3D.MethodName.SetPointPosition, pIndex, river.Curve.GetPointPosition(pIndex));
            plugin.AddUndoMethod(curve, Curve3D.MethodName.SetPointPosition, pIndex, restoreF);
        }

        if (HandlesHelper.IsControlPointIn(index, pointCount))
        {
            plugin.AddDoMethod(curve, Curve3D.MethodName.SetPointIn, pIndex, river.Curve.GetPointIn(pIndex));
            plugin.AddUndoMethod(curve, Curve3D.MethodName.SetPointIn, pIndex, restoreF);
            plugin.AddDoMethod(curve, Curve3D.MethodName.SetPointOut, pIndex, river.Curve.GetPointOut(pIndex));
            plugin.AddUndoMethod(curve, Curve3D.MethodName.SetPointOut, pIndex, -restoreF);
        }

        if (HandlesHelper.IsControlPointOut(index, pointCount))
        {
            plugin.AddDoMethod(curve, Curve3D.MethodName.SetPointOut, pIndex, river.Curve.GetPointOut(pIndex));
            plugin.AddUndoMethod(curve, Curve3D.MethodName.SetPointOut, pIndex, restoreF);
            plugin.AddDoMethod(curve, Curve3D.MethodName.SetPointIn, pIndex, river.Curve.GetPointIn(pIndex));
            plugin.AddUndoMethod(curve, Curve3D.MethodName.SetPointIn, pIndex, -restoreF);
        }

        if (HandlesHelper.IsWidthPointLeft(index, pointCount) || HandlesHelper.IsWidthPointRight(index, pointCount))
        {
            var riverWidthsUndo = river.PointWidths.Duplicate(true);
            riverWidthsUndo[pIndex] = restoreF;
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
