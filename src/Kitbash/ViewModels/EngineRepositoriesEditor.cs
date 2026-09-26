using Kitbash.Core.Godot;

namespace Kitbash.ViewModels;

/// <summary>
/// The global engine repository list, edited in the settings window. Its own editor
/// because <c>godot.repositories</c> is an array of tables. A workspace's own list is a
/// team file and is never written here.
/// </summary>
public sealed class EngineRepositoriesEditor : SettingsListEditor<EngineRepositoryRowViewModel>
{
    private readonly IEngineRepositoryList _repositories;

    /// <summary>What the file said when it was last read. Dirty is measured against it.</summary>
    private IReadOnlyList<(string Name, string Address)> _stored = [];

    public EngineRepositoriesEditor(IEngineRepositoryList repositories)
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

    /// <summary>Two rows of one name would leave a workspace unable to say which it means.</summary>
    public override bool IsValid =>
        base.IsValid
        && Rows
            .Select(row => row.Name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count() == Rows.Count;

    public override async Task LoadAsync(CancellationToken token = default)
    {
        var stored = await Task.Run(_repositories.ReadGlobal, token).ConfigureAwait(true);

        _stored = [.. stored.Select(repository => (repository.Name, repository.Url.ToString()))];
        Fill();
    }

    public override async Task SaveAsync(CancellationToken token = default)
    {
        var writing = Rows
            .Select(row => row.Source)
            .OfType<EngineRepositorySource>()
            .ToArray();

        await Task.Run(() => _repositories.WriteGlobal(writing), token).ConfigureAwait(true);
    }

    public override void Discard() => Fill();

    protected override EngineRepositoryRowViewModel NewRow() => new(string.Empty, string.Empty);

    private IReadOnlyList<(string Name, string Address)> Kept() =>
        [.. Rows.Select(row => (row.Name, row.Address))];

    private void Fill()
    {
        Rows.Clear();

        foreach (var (name, address) in _stored)
        {
            Rows.Add(new EngineRepositoryRowViewModel(name, address));
        }

        Announce();
    }
}
