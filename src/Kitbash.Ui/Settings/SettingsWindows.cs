using Avalonia.Controls;
using Kitbash.Core.IO;
using Kitbash.Core.Settings;
using Kitbash.Core.Settings.Schema;
using Kitbash.Ui.Controls;

namespace Kitbash.Ui.Settings;

internal sealed class SettingsWindows : ISettingsWindows
{
    private readonly SettingsSchema _schema;
    private readonly ISettingsInspector _inspector;
    private readonly ISettingsWriter _writer;
    private readonly ISettingsValueConverter _converter;
    private readonly IPathShortener _shortener;
    private readonly IWindowSettings _windows;
    private readonly IApplicationRestart _restart;

    private SettingsWindow? _open;

    public SettingsWindows(
        SettingsSchema schema,
        ISettingsInspector inspector,
        ISettingsWriter writer,
        ISettingsValueConverter converter,
        IPathShortener shortener,
        IWindowSettings windows,
        IApplicationRestart restart)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(inspector);
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(converter);
        ArgumentNullException.ThrowIfNull(shortener);
        ArgumentNullException.ThrowIfNull(windows);
        ArgumentNullException.ThrowIfNull(restart);

        _schema = schema;
        _inspector = inspector;
        _writer = writer;
        _converter = converter;
        _shortener = shortener;
        _windows = windows;
        _restart = restart;
    }

    public event EventHandler? Closed;

    public void Open(Window owner)
    {
        ArgumentNullException.ThrowIfNull(owner);

        if (_open is { } already)
        {
            already.Activate();
            return;
        }

        // Read once here, the way every window reads it. A change to the setting takes
        // effect the next time a window opens.
        var window = new SettingsWindow
        {
            UsesNativeChrome = _windows.UseNativeChrome,
            DataContext = new SettingsWindowViewModel(
                _schema, _inspector, _writer, _converter, _shortener, _restart),
        };

        window.Closed += (_, _) =>
        {
            _open = null;
            Closed?.Invoke(this, EventArgs.Empty);
        };
        _open = window;
        window.Show(owner);
    }
}
