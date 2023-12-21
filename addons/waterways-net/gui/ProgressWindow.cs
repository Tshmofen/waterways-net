using Godot;

namespace Waterway;

[Tool]
public partial class ProgressWindow : Window
{
    private ProgressBar _progressBar;

    public override void _Ready()
    {
        _progressBar = GetNode<ProgressBar>("ProgressBar");
    }

    public void ShowProgress(string message, float progress)
    {
        Title = message;
        _progressBar.Ratio = progress;
    }
}