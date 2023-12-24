using System.Threading.Tasks;
using Godot;

namespace Waterways;

[Tool]
public partial class FilterRenderer : SubViewport
{
    private const string DilatePass1Path = $"{WaterwaysPlugin.PluginPath}/shaders/filters/dilate_filter_pass1.gdshader";
    private const string DilatePass2Path = $"{WaterwaysPlugin.PluginPath}/shaders/filters/dilate_filter_pass2.gdshader";
    private const string DilatePass3Path = $"{WaterwaysPlugin.PluginPath}/shaders/filters/dilate_filter_pass3.gdshader";
    private const string NormalMapPassPath = $"{WaterwaysPlugin.PluginPath}/shaders/filters/normal_map_pass.gdshader";
    private const string NormalToFlowPassPath = $"{WaterwaysPlugin.PluginPath}/shaders/filters/normal_to_flow_filter.gdshader";
    private const string BlurPass1Path = $"{WaterwaysPlugin.PluginPath}/shaders/filters/blur_pass1.gdshader";
    private const string BlurPass2Path = $"{WaterwaysPlugin.PluginPath}/shaders/filters/blur_pass2.gdshader";
    private const string FoamPassPath = $"{WaterwaysPlugin.PluginPath}/shaders/filters/foam_pass.gdshader";
    private const string CombinePassPath = $"{WaterwaysPlugin.PluginPath}/shaders/filters/combine_pass.gdshader";
    private const string DotProductPassPath = $"{WaterwaysPlugin.PluginPath}/shaders/filters/dotproduct.gdshader";
    private const string FlowPressurePassPath = $"{WaterwaysPlugin.PluginPath}/shaders/filters/flow_pressure_pass.gdshader";

    public Shader DilatePass1Shader { get; set; }
    public Shader DilatePass2Shader { get; set; }
    public Shader DilatePass3Shader { get; set; }
    public Shader NormalMapPassShader { get; set; }
    public Shader NormalToFlowPassShader { get; set; }
    public Shader BlurPass1Shader { get; set; }
    public Shader BlurPass2Shader { get; set; }
    public Shader FoamPassShader { get; set; }
    public Shader CombinePassShader { get; set; }
    public Shader DotProductPassShader { get; set; }
    public Shader FlowPressurePassShader { get; set; }
    public ShaderMaterial FilterMat { get; set; }

    #region Util

    private async Task<ImageTexture> RenderImageTextureAsync()
    {
        RenderTargetUpdateMode = UpdateMode.Once;
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        var image = GetTexture().GetImage();
        return ImageTexture.CreateFromImage(image);
    }

    private ShaderMaterial PrepareRenderingPlane(Shader shader, Texture2D texture)
    {
        FilterMat.Shader = shader;

        var size = texture.GetSize();
        Size = new Vector2I((int)size.X, (int)size.Y);

        var rect = GetNode<ColorRect>("ColorRect");
        rect.Position = new Vector2(0, 0);
        rect.Size = Size;

        return (ShaderMaterial) rect.Material;
    }

    #endregion

    public override void _EnterTree()
    {
        DilatePass1Shader = ResourceLoader.Load(DilatePass1Path) as Shader;
        DilatePass2Shader = ResourceLoader.Load(DilatePass2Path) as Shader;
        DilatePass3Shader = ResourceLoader.Load(DilatePass3Path) as Shader;
        NormalMapPassShader = ResourceLoader.Load(NormalMapPassPath) as Shader;
        NormalToFlowPassShader = ResourceLoader.Load(NormalToFlowPassPath) as Shader;
        BlurPass1Shader = ResourceLoader.Load(BlurPass1Path) as Shader;
        BlurPass2Shader = ResourceLoader.Load(BlurPass2Path) as Shader;
        FoamPassShader = ResourceLoader.Load(FoamPassPath) as Shader;
        CombinePassShader = ResourceLoader.Load(CombinePassPath) as Shader;
        DotProductPassShader = ResourceLoader.Load(DotProductPassPath) as Shader;
        FlowPressurePassShader = ResourceLoader.Load(FlowPressurePassPath) as Shader;

        FilterMat = new ShaderMaterial();
        GetNode<ColorRect>("ColorRect").Material = FilterMat;
    }

    public Task<ImageTexture> ApplyCombineAsync(Texture2D rTexture, Texture2D gTexture, Texture2D bTexture = null, Texture2D aTexture = null)
    {
        var shaderMaterial = PrepareRenderingPlane(CombinePassShader, rTexture);
        shaderMaterial.SetShaderParameter("r_texture", rTexture);
        shaderMaterial.SetShaderParameter("g_texture", gTexture);
        shaderMaterial.SetShaderParameter("b_texture", bTexture);
        shaderMaterial.SetShaderParameter("a_texture", aTexture);
        return RenderImageTextureAsync();
    }

    public Task<ImageTexture> ApplyFlowPressureAsync(Texture2D inputTexture, float resolution, float rows)
    {
        var shaderMaterial = PrepareRenderingPlane(FlowPressurePassShader, inputTexture);
        shaderMaterial.SetShaderParameter("input_texture", inputTexture);
        shaderMaterial.SetShaderParameter("size", resolution);
        shaderMaterial.SetShaderParameter("rows", rows);
        return RenderImageTextureAsync();
    }

    public Task<ImageTexture> ApplyFoamAsync(Texture2D inputTexture, float distance, float cutoff, float resolution)
    {
        var shaderMaterial = PrepareRenderingPlane(FoamPassShader, inputTexture);
        shaderMaterial.SetShaderParameter("input_texture", inputTexture);
        shaderMaterial.SetShaderParameter("size", resolution);
        shaderMaterial.SetShaderParameter("offset", distance);
        shaderMaterial.SetShaderParameter("cutoff", cutoff);
        return RenderImageTextureAsync();
    }

    public async Task<ImageTexture> ApplyBlurAsync(Texture2D inputTexture, float blur, float resolution)
    {
        var shaderMaterial = PrepareRenderingPlane(BlurPass1Shader, inputTexture);
        shaderMaterial.SetShaderParameter("input_texture", inputTexture);
        shaderMaterial.SetShaderParameter("size", resolution);
        shaderMaterial.SetShaderParameter("blur", blur);
        var pass1Result = await RenderImageTextureAsync();

        shaderMaterial = PrepareRenderingPlane(BlurPass2Shader, pass1Result);
        shaderMaterial.SetShaderParameter("input_texture", pass1Result);
        shaderMaterial.SetShaderParameter("size", resolution);
        shaderMaterial.SetShaderParameter("blur", blur);
        return await RenderImageTextureAsync();
    }

    public Task<ImageTexture> ApplyVerticalBlurAsync(Texture2D inputTexture, float blur, float resolution)
    {
        var shaderMaterial = PrepareRenderingPlane(BlurPass2Shader, inputTexture);
        shaderMaterial.SetShaderParameter("input_texture", inputTexture);
        shaderMaterial.SetShaderParameter("size", resolution);
        shaderMaterial.SetShaderParameter("blur", blur);
        return RenderImageTextureAsync();
    }

    public Task<ImageTexture> ApplyNormalToFlowAsync(Texture2D inputTexture, float resolution)
    {
        var shaderMaterial = PrepareRenderingPlane(NormalToFlowPassShader, inputTexture);
        shaderMaterial.SetShaderParameter("input_texture", inputTexture);
        shaderMaterial.SetShaderParameter("size", resolution);
        return RenderImageTextureAsync();
    }

    public Task<ImageTexture> ApplyNormalAsync(Texture2D inputTexture, float resolution)
    {
        var shaderMaterial = PrepareRenderingPlane(NormalMapPassShader, inputTexture);
        shaderMaterial.SetShaderParameter("input_texture", inputTexture);
        shaderMaterial.SetShaderParameter("size", resolution);
        return RenderImageTextureAsync();
    }

    public async Task<ImageTexture> ApplyDilateAsync(Texture2D inputTexture, float dilation, float fill, float resolution, Texture2D fillTexture = null)
    {
        var shaderMaterial = PrepareRenderingPlane(DilatePass1Shader, inputTexture);
        shaderMaterial.SetShaderParameter("input_texture", inputTexture);
        shaderMaterial.SetShaderParameter("size", resolution);
        shaderMaterial.SetShaderParameter("dilation", dilation);
        var pass1Result = await RenderImageTextureAsync();

        shaderMaterial = PrepareRenderingPlane(DilatePass2Shader, pass1Result);
        shaderMaterial.SetShaderParameter("input_texture", pass1Result);
        shaderMaterial.SetShaderParameter("size", resolution);
        shaderMaterial.SetShaderParameter("dilation", dilation);
        var pass2Result = await RenderImageTextureAsync();

        shaderMaterial = PrepareRenderingPlane(DilatePass3Shader, pass2Result);
        shaderMaterial.SetShaderParameter("distance_texture", pass2Result);
        if (fillTexture != null)
        {
            shaderMaterial.SetShaderParameter("color_texture", fillTexture);
        }
        shaderMaterial.SetShaderParameter("size", resolution);
        shaderMaterial.SetShaderParameter("fill", fill);
        return await RenderImageTextureAsync();
    }
}
