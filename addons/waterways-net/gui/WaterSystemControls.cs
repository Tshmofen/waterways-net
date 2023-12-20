using Godot;
using Waterways.Gui;

namespace Waterway.Gui;

[Tool]
public partial class WaterSystemControls : HBoxContainer
{
    public WaterSystemMenu menu { get; set; }

    public override void _EnterTree()
    {
        menu = GetNode<WaterSystemMenu>("$WaterSystemMenu");
    }
}
