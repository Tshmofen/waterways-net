using Godot;
using System.Collections.Generic;

namespace Waterways.Data;

public static class ConstraintConstants
{
    public static readonly IReadOnlyDictionary<ConstraintType, Vector3> AxisMapping = new Dictionary<ConstraintType, Vector3>  {
        { ConstraintType.AxisX, Vector3.Right },
        { ConstraintType.AxisY, Vector3.Up },
        { ConstraintType.AxisZ, Vector3.Back}
    };

    public static readonly IReadOnlyDictionary<ConstraintType, Vector3> PlaneMapping = new Dictionary<ConstraintType, Vector3> {
        { ConstraintType.PlaneYz, Vector3.Right },
        { ConstraintType.PlaneXz, Vector3.Up },
        { ConstraintType.PlaneXy, Vector3.Back}
    };

    public const float MinDistanceToCenterHandle = 0.02f;
    public const float AxisConstraintLength = 4096f;
}
