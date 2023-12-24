using Godot;
using Waterways;

namespace TestAssets;

[Tool]
public partial class FilterTester : Node2D
{
	[Export] public Texture2D Input1 { get; set; }
	[Export] public Texture2D Input2 { get; set; }
    [Export] public Texture2D Output { get; set; }

    [Export] public bool ApplyFilter
    {
        get => false;
        set
        {
            var filterRenderer = ResourceLoader.Load<PackedScene>($"{WaterwaysPlugin.PluginPath}/filter_renderer.tscn").Instantiate<FilterRenderer>();
            AddChild(filterRenderer);
            Output = filterRenderer.ApplyDilateAsync(Input1, 0.1f, 0.0f, 512.0f).Result;
            RemoveChild(filterRenderer);
        }
    }
}
