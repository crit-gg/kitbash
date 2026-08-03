namespace Kitbash.Core.Settings;

/// <summary>
/// Turns stored values into the types callers ask for. Public because a schema judges a
/// stored value with it, and converting is the one part of that a descriptor cannot do
/// on its own.
/// </summary>
public interface ISettingsValueConverter
{
    bool TryConvert<T>(object? raw, out T value);
}
