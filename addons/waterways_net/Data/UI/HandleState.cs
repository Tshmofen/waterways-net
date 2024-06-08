using Godot;
using Waterways.Util;

namespace Waterways.Data.UI;

public class HandleState
{
    public HandleType HandleType { get; private set; }
    public Transform3D GlobalTransform { get; private set; }
    public Transform3D GlobalInverse { get; private set; }

    public int RiverPointIndex { get; private set; }
    public Vector3 BasePointPosition { get; private set; }

    public Vector3 PreviousPosition { get; private set; }
    public Vector3 PreviousGlobalPosition { get; private set; }

    public HandleState(RiverManager river, int handleIndex)
    {
        var pointCount = river.Curve.PointCount;

        HandleType = HandleType.None;
        PreviousPosition = Vector3.Zero;
        RiverPointIndex = HandlesHelper.GetCurveIndex(handleIndex, pointCount);
        BasePointPosition = river.Curve.GetPointPosition(RiverPointIndex);

        GlobalTransform = river.IsInsideTree() ? river.GlobalTransform : river.Transform;
        GlobalInverse = GlobalTransform.AffineInverse();

        if (HandlesHelper.IsCenterPoint(handleIndex, pointCount))
        {
            PreviousPosition = BasePointPosition;
            HandleType = HandleType.Center;
        }
        else if (HandlesHelper.IsControlPointIn(handleIndex, pointCount))
        {
            PreviousPosition = river.Curve.GetPointIn(RiverPointIndex) + BasePointPosition;
            HandleType = HandleType.PointIn;
        }
        else if (HandlesHelper.IsControlPointOut(handleIndex, pointCount))
        {
            PreviousPosition = river.Curve.GetPointOut(RiverPointIndex) + BasePointPosition;
            HandleType = HandleType.PointOut;
        }
        else if (HandlesHelper.IsWidthPointLeft(handleIndex, pointCount))
        {
            PreviousPosition = BasePointPosition + river.Curve.GetPointOut(RiverPointIndex).Cross(Vector3.Up).Normalized() * river.PointWidths[RiverPointIndex];
            HandleType = HandleType.WidthLeft;
        }
        else if (HandlesHelper.IsWidthPointRight(handleIndex, pointCount))
        {
            PreviousPosition = BasePointPosition + river.Curve.GetPointOut(RiverPointIndex).Cross(Vector3.Down).Normalized() * river.PointWidths[RiverPointIndex];
            HandleType = HandleType.WidthRight;
        }

        PreviousGlobalPosition = river.ToGlobal(PreviousPosition);
    }
}
