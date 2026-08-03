using System.Windows.Input;

namespace Workbench.Ui.Controls;

/// <summary>
/// A button in a control template needs an <see cref="ICommand"/> to press, and a
/// control theme has no code behind to give it a handler. This is that command and
/// nothing more.
/// </summary>
internal sealed class TemplateCommand : ICommand
{
    private readonly Action<object?> _run;

    internal TemplateCommand(Action<object?> run) => _run = run;

    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) => _run(parameter);
}
