using System.Threading.Tasks;
using Godot;

namespace Waterways;

[Tool]
public partial class FilterRenderer : SubViewport
{
    private const string DILATE_PASS1_PATH = "res://addons/waterways/shaders/filters/dilate_filter_pass1.gdshader";
    private const string DILATE_PASS2_PATH = "res://addons/waterways/shaders/filters/dilate_filter_pass2.gdshader";
    private const string DILATE_PASS3_PATH = "res://addons/waterways/shaders/filters/dilate_filter_pass3.gdshader";
    private const string NORMAL_MAP_PASS_PATH = "res://addons/waterways/shaders/filters/normal_map_pass.gdshader";
    private const string NORMAL_TO_FLOW_PASS_PATH = "res://addons/waterways/shaders/filters/normal_to_flow_filter.gdshader";
    private const string BLUR_PASS1_PATH = "res://addons/waterways/shaders/filters/blur_pass1.gdshader";
    private const string BLUR_PASS2_PATH = "res://addons/waterways/shaders/filters/blur_pass2.gdshader";
    private const string FOAM_PASS_PATH = "res://addons/waterways/shaders/filters/foam_pass.gdshader";
    private const string COMBINE_PASS_PATH = "res://addons/waterways/shaders/filters/combine_pass.gdshader";
    private const string DOTPRODUCT_PASS_PATH = "res://addons/waterways/shaders/filters/dotproduct.gdshader";
    private const string FLOW_PRESSURE_PASS_PATH = "res://addons/waterways/shaders/filters/flow_pressure_pass.gdshader";

    [Export] public Shader dilate_pass_1_shader { get; set; }
    [Export] public Shader dilate_pass_2_shader { get; set; }
    [Export] public Shader dilate_pass_3_shader { get; set; }
    [Export] public Shader normal_map_pass_shader { get; set; }
    [Export] public Shader normal_to_flow_pass_shader { get; set; }
    [Export] public Shader blur_pass1_shader { get; set; }
    [Export] public Shader blur_pass2_shader { get; set; }
    [Export] public Shader foam_pass_shader { get; set; }
    [Export] public Shader combine_pass_shader { get; set; }
    [Export] public Shader dotproduct_pass_shader { get; set; }
    [Export] public Shader flow_pressure_pass_shader { get; set; }
    [Export] public ShaderMaterial filter_mat { get; set; }

    public override void _EnterTree()
    {

        dilate_pass_1_shader = ResourceLoader.Load(DILATE_PASS1_PATH) as Shader;
        dilate_pass_2_shader = ResourceLoader.Load(DILATE_PASS2_PATH) as Shader;
        dilate_pass_3_shader = ResourceLoader.Load(DILATE_PASS3_PATH) as Shader;
        normal_map_pass_shader = ResourceLoader.Load(NORMAL_MAP_PASS_PATH) as Shader;
        normal_to_flow_pass_shader = ResourceLoader.Load(NORMAL_TO_FLOW_PASS_PATH) as Shader;
        blur_pass1_shader = ResourceLoader.Load(BLUR_PASS1_PATH) as Shader;
        blur_pass2_shader = ResourceLoader.Load(BLUR_PASS2_PATH) as Shader;
        foam_pass_shader = ResourceLoader.Load(FOAM_PASS_PATH) as Shader;
        combine_pass_shader = ResourceLoader.Load(COMBINE_PASS_PATH) as Shader;
        dotproduct_pass_shader = ResourceLoader.Load(DOTPRODUCT_PASS_PATH) as Shader;
        flow_pressure_pass_shader = ResourceLoader.Load(FLOW_PRESSURE_PASS_PATH) as Shader;
        filter_mat = new ShaderMaterial();
        GetNode<ColorRect>("$ColorRect").Material = filter_mat;
    }

    public async Task<ImageTexture> apply_combine(Texture2D r_texture, Texture2D g_texture, Texture2D b_texture = null, Texture2D a_texture = null)
    {
        filter_mat.Shader = combine_pass_shader;
        var size = r_texture.GetSize();
        Size = new Vector2I((int) size.X, (int) size.Y);
        var rect = GetNode<ColorRect>("$ColorRect");
        rect.Position = new Vector2(0, 0);
        rect.Size = Size;
        var shaderMaterial = rect.Material as ShaderMaterial;
        shaderMaterial.SetShaderParameter("r_texture", r_texture);
        shaderMaterial.SetShaderParameter("g_texture", g_texture);
        shaderMaterial.SetShaderParameter("b_texture", b_texture);
        shaderMaterial.SetShaderParameter("a_texture", a_texture);
        RenderTargetUpdateMode = UpdateMode.Once;
        await ToSignal(GetTree(), "process_frame");
        await ToSignal(GetTree(), "process_frame");
        var image = GetTexture().GetImage();
        var result = ImageTexture.CreateFromImage(image);
        return result;
    }

    public async Task<ImageTexture> apply_dotproduct(Texture2D input_texture, float resolution)
    {

        filter_mat.Shader = dotproduct_pass_shader;
        var size = input_texture.GetSize();
        Size = new Vector2I((int)size.X, (int)size.Y);
        var rect = GetNode<ColorRect>("$ColorRect");

        rect.Position = new Vector2(0, 0);
        rect.Size = Size;
        var shaderMaterial = rect.Material as ShaderMaterial;
        shaderMaterial.SetShaderParameter("input_texture", input_texture);

        RenderTargetUpdateMode = UpdateMode.Once;
        await ToSignal(GetTree(), "process_frame");
        await ToSignal(GetTree(), "process_frame");
        var image = GetTexture().GetImage();
        var result = ImageTexture.CreateFromImage(image);
        return result;
    }

    public async Task<ImageTexture> apply_flow_pressure(Texture2D input_texture, float resolution, float rows)
    {
        filter_mat.Shader = flow_pressure_pass_shader;
        var size = input_texture.GetSize();
        Size = new Vector2I((int)size.X, (int)size.Y);
        var rect = GetNode<ColorRect>("$ColorRect");

        rect.Position = new Vector2(0, 0);
        rect.Size = Size;
        var shaderMaterial = rect.Material as ShaderMaterial;
        shaderMaterial.SetShaderParameter("input_texture", input_texture);
        shaderMaterial.SetShaderParameter("size", resolution);
        shaderMaterial.SetShaderParameter("rows", rows);

        RenderTargetUpdateMode = UpdateMode.Once;
        await ToSignal(GetTree(), "process_frame");
        await ToSignal(GetTree(), "process_frame");
        // await RenderingServer.frame_post_draw - TODO, replace with these?

        var image = GetTexture().GetImage();
        var result = ImageTexture.CreateFromImage(image);
        return result;
    }

    public async Task<ImageTexture> apply_foam(Texture2D input_texture, float distance, float cutoff, float resolution)
    {
        filter_mat.Shader = foam_pass_shader;
        var size = input_texture.GetSize();
        Size = new Vector2I((int)size.X, (int)size.Y);
        var rect = GetNode<ColorRect>("$ColorRect");

        rect.Position = new Vector2(0, 0);
        rect.Size = Size;
        var shaderMaterial = rect.Material as ShaderMaterial;
        shaderMaterial.SetShaderParameter("input_texture", input_texture);
        shaderMaterial.SetShaderParameter("size", resolution);
        shaderMaterial.SetShaderParameter("offset", distance);
        shaderMaterial.SetShaderParameter("cutoff", cutoff);

        RenderTargetUpdateMode = UpdateMode.Once;
        await ToSignal(GetTree(), "process_frame");
        await ToSignal(GetTree(), "process_frame");
        var image = GetTexture().GetImage();
        var result = ImageTexture.CreateFromImage(image);
        return result;
    }

    public async Task<ImageTexture> apply_blur(Texture2D input_texture, float blur, float resolution)
    {
        filter_mat.Shader = blur_pass1_shader;
        var size = input_texture.GetSize();
        Size = new Vector2I((int)size.X, (int)size.Y);
        var rect = GetNode<ColorRect>("$ColorRect");

        rect.Position = new Vector2(0, 0);
        rect.Size = Size;
        var shaderMaterial = rect.Material as ShaderMaterial;
        shaderMaterial.SetShaderParameter("input_texture", input_texture);
        shaderMaterial.SetShaderParameter("size", resolution);
        shaderMaterial.SetShaderParameter("blur", blur);

        RenderTargetUpdateMode = UpdateMode.Once;
        await ToSignal(GetTree(), "process_frame");
        await ToSignal(GetTree(), "process_frame");
        var image = GetTexture().GetImage();
        var pass1_result = ImageTexture.CreateFromImage(image);

        // Pass 2
        filter_mat.Shader = blur_pass2_shader;
        shaderMaterial.SetShaderParameter("input_texture", pass1_result);
        shaderMaterial.SetShaderParameter("size", resolution);
        shaderMaterial.SetShaderParameter("blur", blur);

        RenderTargetUpdateMode = UpdateMode.Once;
        await ToSignal(GetTree(), "process_frame");
        await ToSignal(GetTree(), "process_frame");
        var image2 = GetTexture().GetImage();
        var pass2_result = ImageTexture.CreateFromImage(image2);
        return pass2_result;
    }

    public async Task<ImageTexture> apply_vertical_blur(Texture2D input_texture, float blur, float resolution)
    {
        filter_mat.Shader = blur_pass2_shader;
        var size = input_texture.GetSize();
        Size = new Vector2I((int)size.X, (int)size.Y);
        var rect = GetNode<ColorRect>("$ColorRect");

        rect.Position = new Vector2(0, 0);
        rect.Size = Size;
        var shaderMaterial = rect.Material as ShaderMaterial;
        shaderMaterial.SetShaderParameter("input_texture", input_texture);
        shaderMaterial.SetShaderParameter("size", resolution);
        shaderMaterial.SetShaderParameter("blur", blur);

        RenderTargetUpdateMode = UpdateMode.Once;
        await ToSignal(GetTree(), "process_frame");
        await ToSignal(GetTree(), "process_frame");
        var image2 = GetTexture().GetImage();
        var pass2_result = ImageTexture.CreateFromImage(image2);
        return pass2_result;
    }

    public async Task<ImageTexture> apply_normal_to_flow(Texture2D input_texture, float resolution)
    {

        filter_mat.Shader = normal_to_flow_pass_shader;
        var size = input_texture.GetSize();
        Size = new Vector2I((int)size.X, (int)size.Y);
        var rect = GetNode<ColorRect>("$ColorRect");

        rect.Position = new Vector2(0, 0);
        rect.Size = Size;
        var shaderMaterial = rect.Material as ShaderMaterial;

        shaderMaterial.SetShaderParameter("input_texture", input_texture);
        shaderMaterial.SetShaderParameter("size", resolution);

        RenderTargetUpdateMode = UpdateMode.Once;
        await ToSignal(GetTree(), "process_frame");
        await ToSignal(GetTree(), "process_frame");
        var image2 = GetTexture().GetImage();
        var pass2_result = ImageTexture.CreateFromImage(image2);
        return pass2_result;
    }

    public async Task<ImageTexture> apply_normal(Texture2D input_texture, float resolution)
    {

        filter_mat.Shader = normal_map_pass_shader; 
        var size = input_texture.GetSize();
        Size = new Vector2I((int)size.X, (int)size.Y);
        var rect = GetNode<ColorRect>("$ColorRect");

        rect.Position = new Vector2(0, 0);
        rect.Size = Size;
        var shaderMaterial = rect.Material as ShaderMaterial;

        shaderMaterial.SetShaderParameter("input_texture", input_texture);
        shaderMaterial.SetShaderParameter("size", resolution);

        RenderTargetUpdateMode = UpdateMode.Once;
        await ToSignal(GetTree(), "process_frame");
        await ToSignal(GetTree(), "process_frame");
        var image2 = GetTexture().GetImage();
        var pass2_result = ImageTexture.CreateFromImage(image2);
        return pass2_result;
    }

    public async Task<ImageTexture> apply_dilate(Texture2D input_texture, float dilation, float fill, float resolution, Texture2D fill_texture = null)
    {
        filter_mat.Shader = dilate_pass_1_shader;
        var size = input_texture.GetSize();
        Size = new Vector2I((int)size.X, (int)size.Y);
        var rect = GetNode<ColorRect>("$ColorRect");

        rect.Position = new Vector2(0, 0);
        rect.Size = Size;
        var shaderMaterial = rect.Material as ShaderMaterial;

        shaderMaterial.SetShaderParameter("input_texture", input_texture);
        shaderMaterial.SetShaderParameter("size", resolution);
        shaderMaterial.SetShaderParameter("dilation", dilation);

        RenderTargetUpdateMode = UpdateMode.Once;
        await ToSignal(GetTree(), "process_frame");
        await ToSignal(GetTree(), "process_frame");
        var image = GetTexture().GetImage();
        var pass1_result = ImageTexture.CreateFromImage(image);

        // Pass 2
        filter_mat.Shader = dilate_pass_2_shader;
        shaderMaterial.SetShaderParameter("input_texture", pass1_result);
        shaderMaterial.SetShaderParameter("size", resolution);
        shaderMaterial.SetShaderParameter("dilation", dilation);

        RenderTargetUpdateMode = UpdateMode.Once;
        await ToSignal(GetTree(), "process_frame");
        await ToSignal(GetTree(), "process_frame");
        var image2 = GetTexture().GetImage();
        var pass2_result = ImageTexture.CreateFromImage(image2);

        // Pass 3
        filter_mat.Shader = dilate_pass_3_shader;
        shaderMaterial.SetShaderParameter("distance_texture", pass2_result);
	    if (fill_texture != null)
            shaderMaterial.SetShaderParameter("color_texture", fill_texture);
        shaderMaterial.SetShaderParameter("size", resolution);
        shaderMaterial.SetShaderParameter("fill", fill);

        RenderTargetUpdateMode = UpdateMode.Once;
        await ToSignal(GetTree(), "process_frame");
        await ToSignal(GetTree(), "process_frame");

        var image3 = GetTexture().GetImage();
        var pass3_result = ImageTexture.CreateFromImage(image3);
        return pass3_result;
    }
}
