namespace Kitbash.Core.Settings;

/// <summary>
/// One staged change to one key. A batch of these is what Save writes, so a page costs
/// one read and one write per file rather than one of each per key.
/// </summary>
public sealed record SettingsEdit
{
    private SettingsEdit(string key, object? value, bool isRemoval)
    {
        Key = key;
        Value = value;
        IsRemoval = isRemoval;
    }

    /// <summary>Writes a value.</summary>
    public static SettingsEdit Set(string key, object value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        return new SettingsEdit(key, value, isRemoval: false);
    }

    /// <summary>
    /// Takes the key out of the file. This is reset, and it is not the same as writing
    /// the default, which would pin today's value forever.
    /// </summary>
    public static SettingsEdit Remove(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return new SettingsEdit(key, value: null, isRemoval: true);
    }

    public string Key { get; }

    /// <summary>Null when this is a removal.</summary>
    public object? Value { get; }

    public bool IsRemoval { get; }
}
