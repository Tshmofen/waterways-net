using Godot;

namespace Waterways.Gui;

[Tool]
public partial class WaterSystemControls : HBoxContainer
{
    public WaterSystemMenu Menu { get; set; }

    public override void _EnterTree()
    {
        Menu = GetNode<WaterSystemMenu>("WaterSystemMenu");
    }
}
