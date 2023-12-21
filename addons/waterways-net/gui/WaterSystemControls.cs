using Godot;
using Waterways.Gui;

namespace Waterway.Gui;

[Tool]
public partial class WaterSystemControls : HBoxContainer
{
    public WaterSystemMenu Menu { get; set; }

    public override void _EnterTree()
    {
        Menu = GetNode<WaterSystemMenu>("$WaterSystemMenu");
    }
}
