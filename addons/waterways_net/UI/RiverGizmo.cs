#if TOOLS

using Godot;
using System.Collections.Generic;
using System.Linq;
using Waterways.Data;
using Waterways.Data.UI;
using Waterways.Util;

namespace Waterways.UI;

[Tool]
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

        var material = IsCurrentNodeSelected(gizmo)
            ? GetMaterial(GizmoMaterials.Path)
            : GetMaterial(GizmoMaterials.PathBackground);

        gizmo.AddLines([.. path], material);
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

        var material = IsCurrentNodeSelected(gizmo)
            ? GetMaterial(GizmoMaterials.HandleLines)
            : GetMaterial(GizmoMaterials.HandleLinesBackground);
        gizmo.AddLines([.. lines], material);

        // Add each handle twice, for both material types.
        // Needs to be grouped by material "type" since that's what influences the handle indices.
        gizmo.AddHandles([.. handlesCenter], GetMaterial(GizmoMaterials.HandlesCenter, gizmo), []);
        gizmo.AddHandles([.. handlesControlPoints], GetMaterial(GizmoMaterials.HandlesControlPoints, gizmo), []);
        gizmo.AddHandles([.. handlesWidth], GetMaterial(GizmoMaterials.HandlesWidth, gizmo), []);

        gizmo.AddHandles([.. handlesCenter], GetMaterial(GizmoMaterials.HandlesCenterDepth, gizmo), []);
        gizmo.AddHandles([.. handlesControlPoints], GetMaterial(GizmoMaterials.HandlesControlPointsDepth, gizmo), []);
        gizmo.AddHandles([.. handlesWidth], GetMaterial(GizmoMaterials.HandlesWidthDepth, gizmo), []);
    }

    private void CreateRiverHandleMaterial(string name, Color color, bool noDepthTest)
    {
        CreateHandleMaterial(name);
        var handlesCenterMaterial = GetMaterial(name);
        handlesCenterMaterial.AlbedoColor = color;
        handlesCenterMaterial.NoDepthTest = noDepthTest;
    }

    private static StandardMaterial3D CreateRiverPathMaterial(Color color)
    {
        return new StandardMaterial3D
        {
            AlbedoColor = color,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            RenderPriority = 10,
            NoDepthTest = true
        };
    }

    private bool IsCurrentNodeSelected(EditorNode3DGizmo gizmo)
    {
        var selection = EditorPlugin.Selection.GetSelectedNodes().FirstOrDefault();
        return gizmo.GetNode3D() == selection;
    }

    #endregion

    public RiverGizmo()
    {
        // Two materials for every handle type.
        // 1) Transparent handle that is always shown.
        // 2) Opaque handle that is only shown above terrain (when passing depth test)
        // Note that this impacts the point index of the handles. See table in HandlesHelper.cs

        CreateRiverHandleMaterial(GizmoMaterials.HandlesCenter, GizmoMaterials.HandlesCenterColor, true);
        CreateRiverHandleMaterial(GizmoMaterials.HandlesCenterDepth, GizmoMaterials.HandlesCenterDepthColor, false);

        CreateRiverHandleMaterial(GizmoMaterials.HandlesControlPoints, GizmoMaterials.HandlesControlColor, true);
        CreateRiverHandleMaterial(GizmoMaterials.HandlesControlPointsDepth, GizmoMaterials.HandlesControlDepthColor, false);

        CreateRiverHandleMaterial(GizmoMaterials.HandlesWidth, GizmoMaterials.HandlesWidthColor, true);
        CreateRiverHandleMaterial(GizmoMaterials.HandlesWidthDepth, GizmoMaterials.HandlesWidthDepthColor, false);

        var lineMaterial = CreateRiverPathMaterial(GizmoMaterials.HandlesLineColor);
        AddMaterial(GizmoMaterials.Path, lineMaterial);
        AddMaterial(GizmoMaterials.HandleLines, lineMaterial);

        var lineMaterialBackground = CreateRiverPathMaterial(GizmoMaterials.HandlesLineColorBackground);
        AddMaterial(GizmoMaterials.PathBackground, lineMaterialBackground);
        AddMaterial(GizmoMaterials.HandleLinesBackground, lineMaterialBackground);
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

    public override void _SetHandle(EditorNode3DGizmo gizmo, int index, bool secondary, Camera3D camera, Vector2 point)
    {
        var river = (RiverManager)gizmo.GetNode3D();
        var state = new HandleState(river, index);
        var curve = river.Curve;

        if (!river.TryAssociateGizmo(gizmo, true))
        {
            // TODO: Workaround for gizmo duplication Godot bug
            return;
        }

        if (_handleBaseTransform == null)
        {
            var z = curve.GetPointOut(state.RiverPointIndex).Normalized();
            var x = z.Cross(Vector3.Down).Normalized();
            var y = z.Cross(x).Normalized();
            _handleBaseTransform = new Transform3D(new Basis(x, y, z) * state.GlobalTransform.Basis, state.PreviousGlobalPosition);
        }

        var spaceState = river.GetWorld3D().DirectSpaceState;
        var rayFrom = camera.ProjectRayOrigin(point);
        var rayDir = camera.ProjectRayNormal(point);

        if (state.HandleType is HandleType.Center or HandleType.PointIn or HandleType.PointOut)
        {
            var currentConstraint = EditorPlugin.RiverControl.CurrentConstraint;
            var isLocalEditing = EditorPlugin.RiverControl.IsLocalEditing;

            var newGlobalPosition = currentConstraint switch
            {
                ConstraintType.None => HandlesHelper.GetDefaultHandlePosition(rayFrom, rayDir, camera, state.PreviousGlobalPosition),
                ConstraintType.Colliders => HandlesHelper.GetColliderHandlePosition(rayFrom, rayDir, spaceState),
                _ => HandlesHelper.GetConstrainedHandlePosition(rayFrom, rayDir, state.PreviousGlobalPosition, _handleBaseTransform.Value, currentConstraint, isLocalEditing)
            };

            if (newGlobalPosition == null)
            {
                return;
            }

            var newPosition = river.ToLocal(newGlobalPosition.Value);

            if (state.HandleType is HandleType.Center)
            {
                curve.SetPointPosition(state.RiverPointIndex, newPosition);
            }
            else if (state.HandleType == HandleType.PointIn)
            {
                curve.SetPointIn(state.RiverPointIndex, newPosition - state.BasePointPosition);
                curve.SetPointOut(state.RiverPointIndex, -(newPosition - state.BasePointPosition));
            }
            else if (state.HandleType == HandleType.PointOut)
            {
                curve.SetPointOut(state.RiverPointIndex, newPosition - state.BasePointPosition);
                curve.SetPointIn(state.RiverPointIndex, -(newPosition - state.BasePointPosition));
            }
        }

        // Widths handles
        if (state.HandleType is HandleType.WidthLeft or HandleType.WidthRight)
        {
            var p1 = state.BasePointPosition;
            var p2 = Vector3.Zero;

            if (state.HandleType == HandleType.WidthLeft)
            {
                p2 = curve.GetPointOut(state.RiverPointIndex).Cross(Vector3.Up).Normalized() * 4096;
            }
            else if (state.HandleType == HandleType.WidthRight)
            {
                p2 = curve.GetPointOut(state.RiverPointIndex).Cross(Vector3.Down).Normalized() * 4096;
            }

            var q1 = state.GlobalInverse * rayFrom;
            var q2 = state.GlobalInverse * (rayFrom + (rayDir * 4096));

            var closestPoints = Geometry3D.GetClosestPointsBetweenSegments(p1, p2, q1, q2);
            var direction = closestPoints[0].DistanceTo(state.BasePointPosition) - state.PreviousPosition.DistanceTo(state.BasePointPosition);

            river.PointWidths[state.RiverPointIndex] += direction;
            river.PointWidths[state.RiverPointIndex] = Mathf.Max(river.PointWidths[state.RiverPointIndex], ConstraintConstants.MinDistanceToCenterHandle);
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
            plugin.AddUndoMethod(curve, Curve3D.MethodName.SetPointPosition, pointIndex, restore.AsVector3());
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

        plugin.AddDoMethod(river, BaseMeshGenerator.MethodName.UpdateMesh);
        plugin.AddUndoMethod(river, BaseMeshGenerator.MethodName.UpdateMesh);
        plugin.CommitAction();

        _Redraw(gizmo);
    }

    public override void _Redraw(EditorNode3DGizmo gizmo)
    {
        gizmo.Clear();
        var river = (RiverManager) gizmo.GetNode3D();

        if (!river.TryAssociateGizmo(gizmo))
        {
            // TODO: Workaround for gizmo duplication Godot bug
            return;
        }

        if (river.SelectMesh != null)
        {
            gizmo.AddCollisionTriangles(river.SelectMesh);
        }

        DrawPath(gizmo, river.Curve);
        DrawHandles(gizmo, river);
    }
}

#endif