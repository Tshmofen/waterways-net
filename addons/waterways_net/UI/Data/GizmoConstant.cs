using Godot;
using System.Collections.Generic;
using Waterways.UI.Data;

namespace Waterwaysnet.addons.waterways_net.UI.Data;

public static class GizmoConstant
{
    public static class Materials
    {
        public const string Path = "path";
        public const string HandleLines = "handle_lines";
        public static readonly Color HandlesLineColor = new(1.0f, 1.0f, 0.0f);

        public const string HandlesWidth = "handles_width";
        public const string HandlesWidthDepth = "handles_width_with_depth";
        public static readonly Color HandlesWidthColor = new(0.0f, 1.0f, 1.0f, 0.25f);
        public static readonly Color HandlesWidthDepthColor = new(HandlesWidthColor, 1);

        public const string HandlesCenter = "handles_center";
        public const string HandlesCenterDepth = "handles_center_with_depth";
        public static readonly Color HandlesCenterColor = new(1.0f, 1.0f, 0.0f, 0.25f);
        public static readonly Color HandlesCenterDepthColor = new(HandlesCenterColor, 1);

        public const string HandlesControlPoints = "handles_control_points";
        public const string HandlesControlPointsDepth = "handles_control_points_with_depth";
        public static readonly Color HandlesControlColor = new(1.0f, 0.5f, 0.0f, 0.25f);
        public static readonly Color HandlesControlDepthColor = new(HandlesControlColor, 1);
    }

    public static class Constraints
    {
        // Ensure that the width handle can't end up inside the center handle
        // as then it is hard to separate them again.
        public const float MinDistanceToCenterHandle = 0.02f;
        public const float AxisConstraintLength = 4096f;
    }

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
}
