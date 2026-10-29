namespace Kitbash.Settings;

/// <summary>Whether the launcher shows its developer features. Read again whenever asked.</summary>
public interface IDeveloperSettings
{
    bool ShowsDeveloperFeatures { get; }
}
