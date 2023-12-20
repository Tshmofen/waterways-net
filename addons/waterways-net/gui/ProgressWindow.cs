using Godot;

namespace Waterway;

[Tool]
public partial class ProgressWindow : Window
{
    private ProgressBar _progress_bar;

    public override void _Ready()
    {
        _progress_bar = GetNode<ProgressBar>("$ProgressBar");
    }

    public void show_progress(string message, float progress)
    {
        Title = message;
        _progress_bar.Ratio = progress;
    }
}