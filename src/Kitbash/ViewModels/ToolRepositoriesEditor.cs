using Kitbash.Tools;

namespace Kitbash.ViewModels;

/// <summary>
/// The global repository list, edited in the settings window. It is its own editor
/// because <c>tools.repositories</c> is an array of tables and no descriptor can
/// describe one. A workspace's own list is read elsewhere and never written here.
/// </summary>
public sealed class ToolRepositoriesEditor : SettingsListEditor<ToolRepositoryRowViewModel>
{
    private readonly IToolRepositoryList _repositories;

    /// <summary>What the file said when it was last read. Dirty is measured against it.</summary>
    private IReadOnlyList<(string Kind, string Address)> _stored = [];

    public ToolRepositoriesEditor(IToolRepositoryList repositories)
    {
        ArgumentNullException.ThrowIfNull(repositories);
        _repositories = repositories;
    }

    public override bool IsDirty
    {
        get
        {
            var rows = Kept();

            return rows.Count != _stored.Count
                || rows.Where((row, index) => row != _stored[index]).Any();
        }
    }

    public override async Task LoadAsync(CancellationToken token = default)
    {
        var stored = await Task.Run(_repositories.ReadGlobal, token).ConfigureAwait(true);

        _stored = [.. stored.Select(repository => (repository.Type, repository.Url.ToString()))];
        Fill();
    }

    public override async Task SaveAsync(CancellationToken token = default)
    {
        var writing = Rows
            .Select(row => row.Source)
            .OfType<ToolRepositorySource>()
            .ToArray();

        await Task.Run(() => _repositories.WriteGlobal(writing), token).ConfigureAwait(true);
    }

    public override void Discard() => Fill();

    protected override ToolRepositoryRowViewModel NewRow() => new(ToolRepositorySource.GitHub, string.Empty);

    /// <summary>What would be written, in the order the rows are drawn.</summary>
    private IReadOnlyList<(string Kind, string Address)> Kept() =>
        [.. Rows.Select(row => (row.Kind, row.Address))];

    private void Fill()
    {
        Rows.Clear();

        foreach (var (kind, address) in _stored)
        {
            Rows.Add(new ToolRepositoryRowViewModel(kind, address));
        }

        Announce();
    }
}
