namespace Kitbash.Core.Settings.Schema;

/// <summary>
/// One node in a settings tree and the page it opens. A page belongs to one
/// <see cref="SettingsHome"/>, which decides which files stand behind it and whether
/// there is a layer to choose. There is no per setting home.
/// </summary>
public sealed class SettingsPage
{
    private readonly string _id = string.Empty;
    private readonly string _title = string.Empty;
    private readonly IReadOnlyList<SettingsSection> _sections = [];
    private readonly IReadOnlyList<ISettingDescriptor> _descriptors = [];

    /// <summary>Stable, since it is what a tree remembers as the open page.</summary>
    public required string Id
    {
        get => _id;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            _id = value;
        }
    }

    /// <summary>The tree node and the page heading, which are the same words.</summary>
    public required string Title
    {
        get => _title;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            _title = value;
        }
    }

    public required SettingsHome Home { get; init; }

    public required IReadOnlyList<SettingsSection> Sections
    {
        get => _sections;
        init
        {
            ArgumentNullException.ThrowIfNull(value);

            if (value.Count == 0)
            {
                throw new ArgumentException("A page needs at least one section.", nameof(value));
            }

            var descriptors = new List<ISettingDescriptor>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var section in value)
            {
                ArgumentNullException.ThrowIfNull(section);

                foreach (var row in section.Rows)
                {
                    if (row is not ISettingDescriptor descriptor)
                    {
                        continue;
                    }

                    if (!seen.Add(descriptor.Key))
                    {
                        throw new ArgumentException(
                            $"Key '{descriptor.Key}' appears twice on this page.",
                            nameof(value));
                    }

                    descriptors.Add(descriptor);
                }
            }

            _sections = value;
            _descriptors = descriptors;
        }
    }

    /// <summary>Shown but never written, whatever the home allows.</summary>
    public bool IsReadOnly { get; init; }

    /// <summary>
    /// Every row on the page that is a setting, in the order they are drawn. The rows an
    /// app supplies for itself are not here, since they have no key to read.
    /// </summary>
    public IReadOnlyList<ISettingDescriptor> Descriptors => _descriptors;

    /// <summary>
    /// Whether there is a layer to choose. Only a workspace layers, so this is the one
    /// place that fact is written down.
    /// </summary>
    public bool IsLayered => Home is SettingsHome.Workspace;
}
