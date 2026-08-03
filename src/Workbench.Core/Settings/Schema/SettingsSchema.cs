namespace Workbench.Core.Settings.Schema;

/// <summary>
/// Every page one app's settings window draws, and the one scope all of them write.
/// </summary>
public sealed class SettingsSchema
{
    public SettingsSchema(SettingsScope scope, IReadOnlyList<SettingsPage> pages)
    {
        ArgumentNullException.ThrowIfNull(pages);

        if (pages.Count == 0)
        {
            throw new ArgumentException("A schema needs at least one page.", nameof(pages));
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var page in pages)
        {
            ArgumentNullException.ThrowIfNull(page);

            if (!seen.Add(page.Id))
            {
                throw new ArgumentException($"Page id '{page.Id}' appears twice.", nameof(pages));
            }
        }

        Scope = scope;
        Pages = pages;
    }

    public SettingsScope Scope { get; }

    /// <summary>In the order the tree draws them, grouped by <see cref="SettingsHome"/>.</summary>
    public IReadOnlyList<SettingsPage> Pages { get; }

    /// <summary>The pages standing on one store, in their declared order.</summary>
    public IEnumerable<SettingsPage> PagesIn(SettingsHome home) =>
        Pages.Where(page => page.Home == home);

    /// <summary>Null when no page declares the key.</summary>
    public ISettingDescriptor? Find(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        foreach (var page in Pages)
        {
            foreach (var descriptor in page.Descriptors)
            {
                if (string.Equals(descriptor.Key, key, StringComparison.Ordinal))
                {
                    return descriptor;
                }
            }
        }

        return null;
    }
}
