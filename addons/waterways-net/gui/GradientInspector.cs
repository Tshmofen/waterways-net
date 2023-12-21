using Godot;

namespace Waterway;

[Tool]
public partial class GradientInspector : HBoxContainer
{
    public ColorPickerButton Color1 { get; set; }
    public ColorPickerButton Color2 { get; set; }
    public ColorRect Gradient { get; set; }

    public override void _Ready()
    {
        Color1 = GetNode<ColorPickerButton>("$Color1");
        Color2 = GetNode<ColorPickerButton>("$Color2");
        Gradient = GetNode<ColorRect>("$Gradient");
    }

    public void SetValue(Projection newGradient)
    {
        Color1.Color = new Color(newGradient[0].X, newGradient[0].Y, newGradient[0].Z);
        Color2.Color = new Color(newGradient[1].X, newGradient[1].Y, newGradient[1].Z);

        var shaderMaterial = (ShaderMaterial)Gradient.Material;
        shaderMaterial.SetShaderParameter("color1", Color1.Color);
        shaderMaterial.SetShaderParameter("color2", Color2.Color);
    }

    public Projection GetValue()
    {
        return new Projection
        {
            [0] = new(Color1.Color.R, Color1.Color.G, Color1.Color.B, 0),
            [1] = new(Color2.Color.R, Color2.Color.G, Color2.Color.B, 0)
        };
    }
}