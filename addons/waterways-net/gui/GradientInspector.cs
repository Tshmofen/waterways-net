using Godot;

namespace Waterway;

[Tool]
public partial class GradientInspector : HBoxContainer
{
    public ColorPickerButton color1 { get; set; }
    public ColorPickerButton color2 { get; set; }
    public ColorRect gradient { get; set; }


    public override void _Ready()
    {
        color1 = GetNode<ColorPickerButton>("$Color1");
        color2 = GetNode<ColorPickerButton>("$Color2");
        gradient = GetNode<ColorRect>("$Gradient");
    }

    public void set_value(Projection new_gradient)
    {

        color1.Color = new Color(new_gradient[0].X, new_gradient[0].Y, new_gradient[0].Z);
        color2.Color = new Color(new_gradient[1].X, new_gradient[1].Y, new_gradient[1].Z);
        var shaderMaterial = gradient.Material as ShaderMaterial;
        shaderMaterial.SetShaderParameter("color1", color1.Color);
        shaderMaterial.SetShaderParameter("color2", color2.Color);
    }


    public Projection get_value()
    {
        var gradient = new Projection();
        gradient[0] = new Vector4(color1.Color.R, color1.Color.G, color1.Color.B, 0);
        gradient[1] = new Vector4(color2.Color.R, color2.Color.G, color2.Color.B, 0);
        return gradient;
    }

}