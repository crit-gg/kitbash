using Workbench.Core.Settings.Schema;

namespace Workbench.Ui.Settings;

/// <summary>
/// One node of the settings tree. The roots are the stores a page can stand on, since
/// that is the question that decides which file a change lands in. A store whose places
/// are named, such as the workspaces a person has added, holds a level of those and the
/// pages hang under each one.
/// </summary>
public sealed class SettingsTreeNode
{
    private SettingsTreeNode(
        string title,
        SettingsPage? page,
        SettingsPlace? place,
        IReadOnlyList<SettingsTreeNode> children)
    {
        Title = title;
        Page = page;
        Place = place;
        Children = children;
    }

    public string Title { get; }

    /// <summary>Null on a store and on a place, which open nothing and only hold pages.</summary>
    public SettingsPage? Page { get; }

    /// <summary>Which of the store's places this stands for, or the one a page belongs to.</summary>
    public SettingsPlace? Place { get; }

    public IReadOnlyList<SettingsTreeNode> Children { get; }

    public bool IsGroup => Page is null;

    /// <summary>
    /// A store rather than one of its places. It reads as a heading, where a place is a
    /// thing with a name and reads like one.
    /// </summary>
    public bool IsStore => Page is null && Place is null;

    /// <summary>
    /// What a node is, kept across a rebuild of the tree, since the objects are new every
    /// time and the open page has to be found again among them.
    /// </summary>
    public string Key => $"{Place?.Id}{Page?.Id}";

    public static SettingsTreeNode Store(string title, IReadOnlyList<SettingsTreeNode> children) =>
        new(title, page: null, place: null, children);

    public static SettingsTreeNode For(SettingsPlace place, IReadOnlyList<SettingsTreeNode> children)
    {
        ArgumentNullException.ThrowIfNull(place);

        return new SettingsTreeNode(place.Name, page: null, place, children);
    }

    public static SettingsTreeNode For(SettingsPage page, SettingsPlace place)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(place);

        return new SettingsTreeNode(page.Title, page, place, []);
    }

    public override string ToString() => Title;
}
