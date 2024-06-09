using Godot;
using Waterways.Data;

namespace Waterways.Util;

public static class RiverShaderHelper
{
    public static void SetStandardMaterialShader(ShaderMaterial material, ShaderType shaderType)
    {
        switch (shaderType)
        {
            case ShaderType.Water:
                material.Shader = ResourceLoader.Load<Shader>($"{WaterwaysPlugin.PluginPath}/Shaders/river.gdshader");
                material.Shader.ResourceLocalToScene = true;
                material.SetShaderParameter("normal_bump_texture", ResourceLoader.Load<Texture>($"{WaterwaysPlugin.PluginPath}/Textures/water_normal_bump.png"));
                break;

            case ShaderType.None:
                material.Shader = null;
                break;
        }
    }
}
