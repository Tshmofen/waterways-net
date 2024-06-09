using Godot;
using Waterways.Data;

namespace Waterways.Util;

public static class RiverCurveHelper
{
    public static int GetClosestPointTo(RiverManager river, Vector3 point)
    {
        var closestDistance = 4096.0f;
        var closestIndex = -1;

        for (var p = 0; p < river.Curve.PointCount; p++)
        {
            var distance = point.DistanceTo(river.Curve.GetPointPosition(p));

            if (distance >= closestDistance)
            {
                continue;
            }

            closestDistance = distance;
            closestIndex = p;
        }

        return closestIndex;
    }

    public static (int segment, Vector3? bakedPoint) GetClosestPosition(RiverManager river, Camera3D camera, Vector2 cameraPoint)
    {
        var curve = river.Curve;
        var globalTransform = river.IsInsideTree() ? river.GlobalTransform : river.Transform;
        var globalInverse = globalTransform.AffineInverse();

        var rayFrom = camera.ProjectRayOrigin(cameraPoint);
        var rayDir = camera.ProjectRayNormal(cameraPoint);
        var g1 = globalInverse * rayFrom;
        var g2 = globalInverse * (rayFrom + (rayDir * 4096));

        var closestDistance = 4096f;
        var closestSegment = -1;

        for (var p = 0; p < curve.PointCount; p++)
        {
            var p1 = curve.GetPointPosition(p);
            var p2 = curve.GetPointPosition((p + 1) % curve.PointCount);
            var result = Geometry3D.GetClosestPointsBetweenSegments(p1, p2, g1, g2);
            var dist = result[0].DistanceTo(result[1]);

            if (dist >= closestDistance)
            {
                continue;
            }

            closestDistance = dist;
            closestSegment = p;
        }

        // Iterate through baked points to find the closest position on the curved path
        var bakedCurvePoints = curve.GetBakedPoints();
        var bakedClosestDistance = 4096f;
        var bakedClosestPoint = (Vector3?) null;

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
        }

        // In case we were close enough to a line segment to find a segment,
        // but not close enough to the curved line
        if (bakedClosestPoint == null)
        {
            closestSegment = -1;
        }

        return (closestSegment, bakedClosestPoint);
    }

    public static Vector3? GetNewPoint(RiverManager river, Camera3D camera, Vector2 cameraPoint, ConstraintType constraint, bool isLocalEditing)
    {
        var rayFrom = camera.ProjectRayOrigin(cameraPoint);
        var rayDir = camera.ProjectRayNormal(cameraPoint);
        var globalTransform = river.IsInsideTree() ? river.GlobalTransform : river.Transform;

        var endPointPosition = river.Curve.GetPointPosition(river.Curve.PointCount - 1);
        var endPointPositionGlobal = river.ToGlobal(endPointPosition);

        var z = river.Curve.GetPointOut(river.Curve.PointCount - 1).Normalized();
        var x = z.Cross(Vector3.Down).Normalized();
        var y = z.Cross(x).Normalized();
        var handleBaseTransform = new Transform3D(new Basis(x, y, z) * globalTransform.Basis, endPointPositionGlobal);

        var newPosition = constraint switch
        {
            ConstraintType.None => HandlesHelper.GetDefaultHandlePosition(rayFrom, rayDir, camera, endPointPositionGlobal),
            ConstraintType.Colliders => HandlesHelper.GetColliderHandlePosition(rayFrom, rayDir, river.GetWorld3D().DirectSpaceState),
            _ => HandlesHelper.GetConstrainedHandlePosition(rayFrom, rayDir, endPointPositionGlobal, handleBaseTransform, constraint, isLocalEditing)
        };

        return newPosition != null ? river.ToLocal(newPosition.Value) : null;
    }

    public static bool IsStartPointCloser(RiverManager river, Vector3 point)
    {
        var curve = river.Curve;
        var startDistance = curve.GetPointPosition(0).DistanceSquaredTo(point);
        var endDistance = curve.GetPointPosition(curve.PointCount - 1).DistanceSquaredTo(point);
        return startDistance < endDistance;
    }
}
