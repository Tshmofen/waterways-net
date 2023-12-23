using Godot;

namespace Waterways.Gui;

[Tool]
public partial class GradientInspector : HBoxContainer
{
    public ColorPickerButton Color1 { get; set; }
    public ColorPickerButton Color2 { get; set; }
    public ColorRect Gradient { get; set; }

    public Projection Projection
    {
        get => new()
        {
            [0] = new Vector4(Color1.Color.R, Color1.Color.G, Color1.Color.B, 0),
            [1] = new Vector4(Color2.Color.R, Color2.Color.G, Color2.Color.B, 0)
        };
        set
        {
            Color1.Color = new Color(value[0].X, value[0].Y, value[0].Z);
            Color2.Color = new Color(value[1].X, value[1].Y, value[1].Z);

            var shaderMaterial = (ShaderMaterial) Gradient.Material;
            shaderMaterial.SetShaderParameter("color1", Color1.Color);
            shaderMaterial.SetShaderParameter("color2", Color2.Color);
        }
    }

    public override void _Ready()
    {
        Color1 = GetNode<ColorPickerButton>("Color1");
        Color2 = GetNode<ColorPickerButton>("Color2");
        Gradient = GetNode<ColorRect>("Gradient");
    }
}