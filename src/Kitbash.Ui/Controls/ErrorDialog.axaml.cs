using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Markup.Xaml;

namespace Kitbash.Ui.Controls;

/// <summary>
/// One thing failed and here is what it said. The details are the program's own words, so
/// they are copyable rather than only readable.
/// </summary>
public partial class ErrorDialog : DialogWindow
{
    /// <summary>How long the copy button says it copied before going back.</summary>
    private static readonly TimeSpan Confirmation = TimeSpan.FromSeconds(1.5);

    private bool _copying;

    public ErrorDialog()
    {
        InitializeComponent();

        Copy.Click += async (_, _) => await CopyAsync();
    }

    /// <summary>
    /// Builds one. The title is the window's, the heading is the sentence a person reads,
    /// and the details are what a program said.
    /// </summary>
    public static ErrorDialog For(string title, string heading, string details)
    {
        var dialog = new ErrorDialog { Title = title };

        dialog.Heading.Text = heading;
        dialog.Details.Text = details;
        dialog.Well.IsVisible = !string.IsNullOrWhiteSpace(details);
        dialog.Copy.IsVisible = dialog.Well.IsVisible;

        return dialog;
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private async Task CopyAsync()
    {
        if (_copying || Clipboard is not { } clipboard)
        {
            return;
        }

        _copying = true;

        try
        {
            await clipboard.SetValueAsync(DataFormat.Text, Details.Text ?? string.Empty);

            Copy.Content = "Copied";

            await Task.Delay(Confirmation);

            Copy.Content = "Copy details";
        }
        finally
        {
            _copying = false;
        }
    }
}
