using Godot;
using Waterways.Data;

namespace Waterways.Util;

public static class RiverCurveHelper
{
    public static int? GetClosestPointTo(RiverManager river, Vector3 point)
    {
        var closestDistance = 4096.0f;
        var closestIndex = (int?) null;

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

    public static int? GetRemovePointIndex(RiverManager river, int closestSegment, Vector3 bakedClosestPoint)
    {
        // The closest_segment of -1 means we didn't press close enough to a point for it to be removed
        if (closestSegment == -1)
        {
            return null;
        }

        return GetClosestPointTo(river, bakedClosestPoint);
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
}
