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

    public async Task<ImageTexture> ApplyCombine(Texture2D rTexture, Texture2D gTexture, Texture2D bTexture = null, Texture2D aTexture = null)
    {
        FilterMat.Shader = CombinePassShader;
        var size = rTexture.GetSize();
        Size = new Vector2I((int)size.X, (int)size.Y);
        var rect = GetNode<ColorRect>("ColorRect");
        rect.Position = new Vector2(0, 0);
        rect.Size = Size;

        var shaderMaterial = (ShaderMaterial)rect.Material;
        shaderMaterial.SetShaderParameter("r_texture", rTexture);
        shaderMaterial.SetShaderParameter("g_texture", gTexture);
        shaderMaterial.SetShaderParameter("b_texture", bTexture);
        shaderMaterial.SetShaderParameter("a_texture", aTexture);
        RenderTargetUpdateMode = UpdateMode.Once;

        await ToSignal(GetTree(), "process_frame");
        await ToSignal(GetTree(), "process_frame");
        var image = GetTexture().GetImage();
        return ImageTexture.CreateFromImage(image);
    }

    public async Task<ImageTexture> ApplyFlowPressure(Texture2D inputTexture, float resolution, float rows)
    {
        FilterMat.Shader = FlowPressurePassShader;
        var size = inputTexture.GetSize();
        Size = new Vector2I((int)size.X, (int)size.Y);
        var rect = GetNode<ColorRect>("ColorRect");

        rect.Position = new Vector2(0, 0);
        rect.Size = Size;
        var shaderMaterial = (ShaderMaterial)rect.Material;
        shaderMaterial.SetShaderParameter("input_texture", inputTexture);
        shaderMaterial.SetShaderParameter("size", resolution);
        shaderMaterial.SetShaderParameter("rows", rows);

        RenderTargetUpdateMode = UpdateMode.Once;
        await ToSignal(GetTree(), "process_frame");
        await ToSignal(GetTree(), "process_frame");
        // await RenderingServer.frame_post_draw - TODO, replace with these?

        var image = GetTexture().GetImage();
        return ImageTexture.CreateFromImage(image);
    }

    public async Task<ImageTexture> ApplyFoam(Texture2D inputTexture, float distance, float cutoff, float resolution)
    {
        FilterMat.Shader = FoamPassShader;
        var size = inputTexture.GetSize();
        Size = new Vector2I((int)size.X, (int)size.Y);
        var rect = GetNode<ColorRect>("ColorRect");

        rect.Position = new Vector2(0, 0);
        rect.Size = Size;
        var shaderMaterial = (ShaderMaterial)rect.Material;
        shaderMaterial.SetShaderParameter("input_texture", inputTexture);
        shaderMaterial.SetShaderParameter("size", resolution);
        shaderMaterial.SetShaderParameter("offset", distance);
        shaderMaterial.SetShaderParameter("cutoff", cutoff);

        RenderTargetUpdateMode = UpdateMode.Once;
        await ToSignal(GetTree(), "process_frame");
        await ToSignal(GetTree(), "process_frame");
        var image = GetTexture().GetImage();
        return ImageTexture.CreateFromImage(image);
    }

    public async Task<ImageTexture> ApplyBlur(Texture2D inputTexture, float blur, float resolution)
    {
        FilterMat.Shader = BlurPass1Shader;
        var size = inputTexture.GetSize();
        Size = new Vector2I((int)size.X, (int)size.Y);
        var rect = GetNode<ColorRect>("ColorRect");

        rect.Position = new Vector2(0, 0);
        rect.Size = Size;
        var shaderMaterial = (ShaderMaterial)rect.Material;
        shaderMaterial.SetShaderParameter("input_texture", inputTexture);
        shaderMaterial.SetShaderParameter("size", resolution);
        shaderMaterial.SetShaderParameter("blur", blur);

        RenderTargetUpdateMode = UpdateMode.Once;
        await ToSignal(GetTree(), "process_frame");
        await ToSignal(GetTree(), "process_frame");
        var image = GetTexture().GetImage();
        var pass1Result = ImageTexture.CreateFromImage(image);

        // Pass 2
        FilterMat.Shader = BlurPass2Shader;
        shaderMaterial.SetShaderParameter("input_texture", pass1Result);
        shaderMaterial.SetShaderParameter("size", resolution);
        shaderMaterial.SetShaderParameter("blur", blur);

        RenderTargetUpdateMode = UpdateMode.Once;
        await ToSignal(GetTree(), "process_frame");
        await ToSignal(GetTree(), "process_frame");
        var image2 = GetTexture().GetImage();
        return ImageTexture.CreateFromImage(image2);
    }

    public async Task<ImageTexture> ApplyVerticalBlur(Texture2D inputTexture, float blur, float resolution)
    {
        FilterMat.Shader = BlurPass2Shader;
        var size = inputTexture.GetSize();
        Size = new Vector2I((int)size.X, (int)size.Y);
        var rect = GetNode<ColorRect>("ColorRect");

        rect.Position = new Vector2(0, 0);
        rect.Size = Size;
        var shaderMaterial = (ShaderMaterial)rect.Material;
        shaderMaterial.SetShaderParameter("input_texture", inputTexture);
        shaderMaterial.SetShaderParameter("size", resolution);
        shaderMaterial.SetShaderParameter("blur", blur);

        RenderTargetUpdateMode = UpdateMode.Once;
        await ToSignal(GetTree(), "process_frame");
        await ToSignal(GetTree(), "process_frame");
        var image2 = GetTexture().GetImage();
        return ImageTexture.CreateFromImage(image2);
    }

    public async Task<ImageTexture> ApplyNormalToFlow(Texture2D inputTexture, float resolution)
    {
        FilterMat.Shader = NormalToFlowPassShader;
        var size = inputTexture.GetSize();
        Size = new Vector2I((int)size.X, (int)size.Y);
        var rect = GetNode<ColorRect>("ColorRect");

        rect.Position = new Vector2(0, 0);
        rect.Size = Size;
        var shaderMaterial = (ShaderMaterial)rect.Material;

        shaderMaterial.SetShaderParameter("input_texture", inputTexture);
        shaderMaterial.SetShaderParameter("size", resolution);

        RenderTargetUpdateMode = UpdateMode.Once;
        await ToSignal(GetTree(), "process_frame");
        await ToSignal(GetTree(), "process_frame");
        var image2 = GetTexture().GetImage();
        return ImageTexture.CreateFromImage(image2);
    }

    public async Task<ImageTexture> ApplyNormal(Texture2D inputTexture, float resolution)
    {
        FilterMat.Shader = NormalMapPassShader;
        var size = inputTexture.GetSize();
        Size = new Vector2I((int)size.X, (int)size.Y);
        var rect = GetNode<ColorRect>("ColorRect");

        rect.Position = new Vector2(0, 0);
        rect.Size = Size;
        var shaderMaterial = (ShaderMaterial)rect.Material;

        shaderMaterial.SetShaderParameter("input_texture", inputTexture);
        shaderMaterial.SetShaderParameter("size", resolution);

        RenderTargetUpdateMode = UpdateMode.Once;
        await ToSignal(GetTree(), "process_frame");
        await ToSignal(GetTree(), "process_frame");
        var image2 = GetTexture().GetImage();
        return ImageTexture.CreateFromImage(image2);
    }

    public async Task<ImageTexture> ApplyDilate(Texture2D inputTexture, float dilation, float fill, float resolution, Texture2D fillTexture = null)
    {
        FilterMat.Shader = DilatePass1Shader;
        var size = inputTexture.GetSize();
        Size = new Vector2I((int)size.X, (int)size.Y);
        var rect = GetNode<ColorRect>("ColorRect");

        rect.Position = new Vector2(0, 0);
        rect.Size = Size;
        var shaderMaterial = (ShaderMaterial)rect.Material;

        shaderMaterial.SetShaderParameter("input_texture", inputTexture);
        shaderMaterial.SetShaderParameter("size", resolution);
        shaderMaterial.SetShaderParameter("dilation", dilation);

        RenderTargetUpdateMode = UpdateMode.Once;
        await ToSignal(GetTree(), "process_frame");
        await ToSignal(GetTree(), "process_frame");
        var image = GetTexture().GetImage();
        var pass1Result = ImageTexture.CreateFromImage(image);

        // Pass 2
        FilterMat.Shader = DilatePass2Shader;
        shaderMaterial.SetShaderParameter("input_texture", pass1Result);
        shaderMaterial.SetShaderParameter("size", resolution);
        shaderMaterial.SetShaderParameter("dilation", dilation);

        RenderTargetUpdateMode = UpdateMode.Once;
        await ToSignal(GetTree(), "process_frame");
        await ToSignal(GetTree(), "process_frame");
        var image2 = GetTexture().GetImage();
        var pass2Result = ImageTexture.CreateFromImage(image2);

        // Pass 3
        FilterMat.Shader = DilatePass3Shader;
        shaderMaterial.SetShaderParameter("distance_texture", pass2Result);
        if (fillTexture != null)
            shaderMaterial.SetShaderParameter("color_texture", fillTexture);
        shaderMaterial.SetShaderParameter("size", resolution);
        shaderMaterial.SetShaderParameter("fill", fill);

        RenderTargetUpdateMode = UpdateMode.Once;
        await ToSignal(GetTree(), "process_frame");
        await ToSignal(GetTree(), "process_frame");

        var image3 = GetTexture().GetImage();
        return ImageTexture.CreateFromImage(image3);
    }
}
