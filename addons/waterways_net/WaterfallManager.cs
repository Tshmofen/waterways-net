using Godot;

namespace Waterways;

[Tool]
[GlobalClass]
public partial class WaterfallManager : Path3D
{
    public const string PluginBaseAlias = nameof(Path3D);
    public const string PluginNodeAlias = nameof(WaterfallManager);
    public const string ScriptPath = $"{nameof(WaterfallManager)}.cs";
    public const string IconPath = "river.svg";

    
}
