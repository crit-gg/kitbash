using Kitbash.Core.Platform.Openers;

namespace Kitbash.ViewModels;

/// <summary>
/// The tools a person added, edited in the settings window. It is its own editor because
/// <c>tools.custom</c> is an array of tables and no descriptor can describe one.
/// </summary>
public sealed class CustomToolsEditor : SettingsListEditor<CustomToolRowViewModel>
{
    private readonly ICustomOpeners _openers;

    /// <summary>What the file said when it was last read. Dirty is measured against it.</summary>
    private IReadOnlyList<CustomOpener> _stored = [];

    public CustomToolsEditor(ICustomOpeners openers)
    {
        ArgumentNullException.ThrowIfNull(openers);
        _openers = openers;
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
        _stored = await Task.Run(_openers.Read, token).ConfigureAwait(true);
        Fill();
    }

    public override async Task SaveAsync(CancellationToken token = default)
    {
        var writing = Kept();

        await Task.Run(() => _openers.Write(writing), token).ConfigureAwait(true);
    }

    public override void Discard() => Fill();

    protected override CustomToolRowViewModel NewRow() => new(string.Empty, string.Empty, string.Empty);

    /// <summary>What would be written, in the order the rows are drawn.</summary>
    private IReadOnlyList<CustomOpener> Kept() =>
        [.. Rows.Select(row => row.Opener).OfType<CustomOpener>()];

    /// <summary>Puts the rows back to what was read, which is both load and discard.</summary>
    private void Fill()
    {
        Rows.Clear();

        foreach (var opener in _stored)
        {
            Rows.Add(new CustomToolRowViewModel(opener.Name, opener.Path, opener.Arguments));
        }

        Announce();
    }
}
