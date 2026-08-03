using Workbench.Core;
using Workbench.ViewModels;

namespace Workbench.Mock;

/// <summary>
/// The version, update and install facts behind the tools page. Invented, because nothing
/// decides yet where a tool comes from or how one is updated. Delete this once it does.
/// </summary>
public sealed class MockToolCatalogue
{
    /// <summary>Keyed by <see cref="ITool.Id"/>. A tool with no entry lists as installed.</summary>
    private static readonly Dictionary<string, Facts> Known = new(StringComparer.Ordinal)
    {
        ["foundry"] = new(
            "F",
            "0.9.2",
            "0.10.0",
            Actions: [],
            Menu:
            [
                new ToolMenuItemViewModel("Open last session", "Shift Enter"),
                new ToolMenuItemViewModel("Open recipes"),
                new ToolMenuItemViewModel("Open graphs"),
            ]),
        ["balance-sim"] = new(
            "S",
            "0.4.6",
            Offered: null,
            Actions: ["Run last scenario"],
            Menu:
            [
                new ToolMenuItemViewModel("Open last run"),
                new ToolMenuItemViewModel("Open run history"),
            ]),
        ["pipeline"] = new(
            "P",
            "0.2.9",
            "0.6.0",
            Blocked: true,
            Actions: [],
            Menu: []),
        ["strings"] = new(
            "T",
            "1.4.0",
            Offered: null,
            Actions: ["Coverage", "Import"],
            Menu: [new ToolMenuItemViewModel("Open last table")]),
        ["atlas"] = new(
            "A",
            "0.3.1",
            Offered: null,
            Installed: false,
            Actions: [],
            Menu:
            [
                new ToolMenuItemViewModel("View on the tool registry"),
                new ToolMenuItemViewModel("Read the docs"),
            ]),
    };

    /// <summary>
    /// Splits the registered tools into the groups the page draws, each sorted by name. A
    /// group with nothing in it is left out rather than drawn empty.
    /// </summary>
    public IReadOnlyList<ToolGroupViewModel> Build(IReadOnlyList<ITool> tools)
    {
        ArgumentNullException.ThrowIfNull(tools);

        List<ToolCardViewModel> cards =
            [.. tools.Select(Card).OrderBy(card => card.Name, StringComparer.CurrentCulture)];

        List<ToolGroupViewModel> groups =
        [
            new("INSTALLED TOOLS", [.. cards.Where(card => card.IsInstalled)], offersUpdates: true),
            new("AVAILABLE TO INSTALL", [.. cards.Where(card => !card.IsInstalled)], offersUpdates: false),
        ];

        return [.. groups.Where(group => group.Tools.Count > 0)];
    }

    private static ToolCardViewModel Card(ITool tool)
    {
        var facts = Known.TryGetValue(tool.Id, out var found)
            ? found
            : new Facts(Initial(tool.Name), "0.1.0", null, Actions: [], Menu: []);

        return new ToolCardViewModel(
            tool,
            facts.Mark,
            facts.Version,
            facts.Offered,
            facts.Installed,
            facts.Blocked,
            facts.Actions,
            facts.Menu);
    }

    private static string Initial(string name) =>
        name.Length == 0 ? "?" : name[..1].ToUpperInvariant();

    /// <summary>What a tool card shows that <see cref="ITool"/> cannot answer.</summary>
    private sealed record Facts(
        string Mark,
        string Version,
        string? Offered,
        IReadOnlyList<string> Actions,
        IReadOnlyList<ToolMenuItemViewModel> Menu,
        bool Installed = true,
        bool Blocked = false);
}
