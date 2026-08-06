using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Kitbash.ViewModels;

namespace Kitbash.Views;

/// <summary>
/// Picks which card a tool is drawn with. The two are separate templates rather than one
/// carrying a condition per size, since only the header and the buttons survive between them.
/// </summary>
public sealed class ToolCardTemplates : IDataTemplate
{
    public IDataTemplate? Full { get; set; }

    public IDataTemplate? Compact { get; set; }

    public bool Match(object? data) => data is ToolCardViewModel;

    public Control? Build(object? param) =>
        param is ToolCardViewModel { IsCompact: true }
            ? Compact?.Build(param)
            : Full?.Build(param);
}
