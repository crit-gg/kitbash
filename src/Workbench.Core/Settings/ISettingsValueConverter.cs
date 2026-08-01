namespace Workbench.Core.Settings;

/// <summary>Turns stored values into the types callers ask for.</summary>
internal interface ISettingsValueConverter
{
    bool TryConvert<T>(object? raw, out T value);
}
