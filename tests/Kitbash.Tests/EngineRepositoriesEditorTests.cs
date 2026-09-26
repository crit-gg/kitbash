using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Kitbash.Core.Godot;
using Kitbash.Core.Platform;
using Kitbash.ViewModels;
using Kitbash.Views;

namespace Kitbash.Tests;

/// <summary>The global engine repository list as the settings window edits it, and the link that adds one.</summary>
public sealed class EngineRepositoriesEditorTests
{
    [Fact]
    public void ARepositoryOnGitHubIsKept()
    {
        var row = new EngineRepositoryRowViewModel("slopworks", "https://github.com/crit-gg/godot-slopworks");

        Assert.True(row.IsValid);
        Assert.Equal("github/crit-gg/godot-slopworks", row.Source!.Address.ToString());
    }

    [Theory]
    [InlineData("", "https://github.com/crit-gg/godot-slopworks")]
    [InlineData("slopworks", "")]
    [InlineData("slopworks", "https://gitlab.com/crit-gg/godot-slopworks")]
    [InlineData("slopworks", "not an address")]
    public void ARowThatCannotBeWrittenSaysWhy(string name, string address)
    {
        var row = new EngineRepositoryRowViewModel(name, address);

        Assert.False(row.IsValid);
        Assert.Null(row.Source);
        Assert.NotEmpty(row.Problem);
    }

    [Fact]
    public async Task TwoRowsOfOneNameCannotBeSaved()
    {
        var editor = new EngineRepositoriesEditor(new FakeList());

        await editor.LoadAsync(TestContext.Current.CancellationToken);

        editor.Rows.Add(new EngineRepositoryRowViewModel("slopworks", "https://github.com/crit-gg/godot-slopworks"));
        editor.Rows.Add(new EngineRepositoryRowViewModel("Slopworks", "https://github.com/someone/else"));

        Assert.False(editor.IsValid);
    }

    [Fact]
    public async Task SavingWritesTheRowsInOrder()
    {
        var list = new FakeList();
        var editor = new EngineRepositoriesEditor(list);

        await editor.LoadAsync(TestContext.Current.CancellationToken);

        editor.Rows.Add(new EngineRepositoryRowViewModel("slopworks", "https://github.com/crit-gg/godot-slopworks"));
        editor.Rows.Add(new EngineRepositoryRowViewModel("other", "https://github.com/someone/else"));

        Assert.True(editor.IsDirty);

        await editor.SaveAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["slopworks", "other"], list.Written.Select(source => source.Name));
    }

    [AvaloniaFact]
    public void TheLinkDialogNamesTheRepositoryAndWhatItInstalls()
    {
        var dialog = AddRepositoryDialog.ForEngines(
            WebAddress.Parse("https://github.com/crit-gg/godot-slopworks"), "slopworks");

        Assert.Equal("Add engine repository", dialog.Title);
        Assert.Equal("Add slopworks as an engine repository?", Text(dialog, "Heading"));
        Assert.Contains("Godot builds", Text(dialog, "Body"), StringComparison.Ordinal);
        Assert.Equal("https://github.com/crit-gg/godot-slopworks", Text(dialog, "Address"));
    }

    private static string? Text(AddRepositoryDialog dialog, string name) =>
        dialog.FindControl<TextBlock>(name)?.Text;

    private sealed class FakeList : IEngineRepositoryList
    {
        public IReadOnlyList<EngineRepositorySource> Written { get; private set; } = [];

        public IReadOnlyList<EngineRepositorySource> ReadGlobal() => [];

        public IReadOnlyList<EngineRepositorySource> ReadFor(string workspaceRoot) => [];

        public void WriteGlobal(IReadOnlyList<EngineRepositorySource> repositories) => Written = repositories;
    }
}
