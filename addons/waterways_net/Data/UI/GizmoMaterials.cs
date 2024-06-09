using Godot;

namespace Waterways.Data.UI;

public static class GizmoMaterials
{
    public const string Path = "path";
    public const string PathBackground = "path_background";
    public const string HandleLines = "handle_lines";
    public const string HandleLinesBackground = "handle_lines_background";
    public static readonly Color HandlesLineColor = new(1.0f, 1.0f, 0.0f);
    public static readonly Color HandlesLineColorBackground = new(HandlesLineColor, 0.15f);

    public const string HandlesWidth = "handles_width";
    public const string HandlesWidthDepth = "handles_width_with_depth";
    public static readonly Color HandlesWidthColor = new(0.0f, 1.0f, 1.0f, 0.15f);
    public static readonly Color HandlesWidthDepthColor = new(HandlesWidthColor, 1);

    public const string HandlesCenter = "handles_center";
    public const string HandlesCenterDepth = "handles_center_with_depth";
    public static readonly Color HandlesCenterColor = new(1.0f, 1.0f, 0.0f, 0.15f);
    public static readonly Color HandlesCenterDepthColor = new(HandlesCenterColor, 1);

    public const string HandlesControlPoints = "handles_control_points";
    public const string HandlesControlPointsDepth = "handles_control_points_with_depth";
    public static readonly Color HandlesControlColor = new(1.0f, 0.5f, 0.0f, 0.15f);
    public static readonly Color HandlesControlDepthColor = new(HandlesControlColor, 1);
}
