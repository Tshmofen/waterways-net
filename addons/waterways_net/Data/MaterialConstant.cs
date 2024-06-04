using System.Collections.Generic;

namespace Waterways.Data;

public static class MaterialConstant
{
    public const string Albedo = "Albedo";
    public const string Emission = "Emission";
    public const string Transparency = "Transparency";
    public const string Normal = "Normal";
    public const string Flow = "Flow";
    public const string Foam = "Foam";
    public const string Custom = "Custom";

    public const string FoamNoisePath = $"{WaterwaysPlugin.PluginPath}/Textures/foam_noise.png";
    public const string CustomColorFirstProperty = "mat_albedo_color_first";
    public const string CustomColorSecondProperty = "mat_albedo_color_second";
    public const string CustomColorAlbedoProperty = "albedo_color";
    public const string MaterialParamPrefix = "mat_";

    public static readonly IReadOnlyDictionary<string, string> ShaderPrefixes = new Dictionary<string, string>()
    {
        { $"{nameof(Albedo).ToLower()}_", Albedo},
        { $"{nameof(Emission).ToLower()}_", Emission},
        { $"{nameof(Transparency).ToLower()}_", Transparency},
        { $"{nameof(Normal).ToLower()}_", Normal},
        { $"{nameof(Flow).ToLower()}_", Flow},
        { $"{nameof(Foam).ToLower()}_", Foam},
        { $"{nameof(Custom).ToLower()}_", Custom}
    };
}
