using Godot;
using Waterways.Data;

namespace Waterways.Utils;

public static class RiverShaderHelper
{
    public static void SetStandardMaterialShader(ShaderMaterial material, ShaderType shaderType)
    {
        switch (shaderType)
        {
            case ShaderType.Water:
                material.Shader = ResourceLoader.Load<Shader>($"{WaterwaysPlugin.PluginPath}/Shader/river.gdshader");
                material.SetShaderParameter("normal_bump_texture", ResourceLoader.Load<Texture>($"{WaterwaysPlugin.PluginPath}/Texture/water1_normal_bump.png"));
                break;

            case ShaderType.Lava:
                material.Shader = ResourceLoader.Load<Shader>($"{WaterwaysPlugin.PluginPath}/Shader/lava.gdshader");
                material.SetShaderParameter("normal_bump_texture", ResourceLoader.Load<Texture>($"{WaterwaysPlugin.PluginPath}/Texture/lava_normal_bump.png"));
                material.SetShaderParameter("emission_texture", ResourceLoader.Load<Texture>($"{WaterwaysPlugin.PluginPath}/Texture/lava_emission.png"));
                break;
        }
    }
}
