using Kitbash.Tools;
using Kitbash.ViewModels;

namespace Kitbash.Tests;

/// <summary>
/// What a form answer becomes on the command line, and what it is refused for being. The
/// order the manifest writes the inputs in is the order the script is handed them.
/// </summary>
public sealed class ToolInputTests
{
    [Fact]
    public void AValueGoesBehindItsFlagAsTwoArguments()
    {
        Assert.Equal(["--source", "/art"], Input(ToolInputKind.Folder, "--source").Arguments("/art"));
    }

    /// <summary>A flag written with a trailing equals is one token.</summary>
    [Fact]
    public void AFlagEndingInEqualsIsJoinedToItsValue()
    {
        Assert.Equal(["--mode=fast"], Input(ToolInputKind.Choice, "--mode=").Arguments("fast"));
    }

    [Fact]
    public void AnInputWithNoFlagIsThePlainValue()
    {
        Assert.Equal(["/art"], Input(ToolInputKind.Text, null).Arguments("/art"));
    }

    [Fact]
    public void AnEmptyAnswerAddsNothing()
    {
        Assert.Empty(Input(ToolInputKind.Text, "--name").Arguments("  "));
        Assert.Empty(Input(ToolInputKind.Text, null).Arguments(null));
    }

    /// <summary>A flag is there or it is not, which is what every command line means by one.</summary>
    [Fact]
    public void ATickedBoxIsItsFlagAloneAndAnEmptyOneIsNothing()
    {
        var input = Input(ToolInputKind.Boolean, "--dry-run");

        Assert.Equal(["--dry-run"], input.Arguments("true"));
        Assert.Empty(input.Arguments("false"));
    }

    /// <summary>With no flag to omit, the word itself is the answer.</summary>
    [Fact]
    public void ABooleanWithNoFlagWritesTheWord()
    {
        var input = Input(ToolInputKind.Boolean, null);

        Assert.Equal(["true"], input.Arguments("true"));
        Assert.Equal(["false"], input.Arguments("false"));
    }

    [Fact]
    public void TheWorkspaceTokenIsFilledInAndNothingElseIs()
    {
        var input = Input(ToolInputKind.Folder, null) with { Default = "{workspace}/art" };

        Assert.Equal("/home/a/ws/art", input.DefaultFor("/home/a/ws"));
        Assert.Equal("/art", input.DefaultFor(null));
    }

    [Theory]
    [InlineData(ToolInputKind.Number, "soon", "takes a number")]
    [InlineData(ToolInputKind.Integer, "1.5", "whole number")]
    [InlineData(ToolInputKind.Number, "0.1", "below")]
    [InlineData(ToolInputKind.Number, "99", "above")]
    public void ANumberIsCheckedAgainstItsKindAndItsRange(ToolInputKind kind, string value, string says)
    {
        var input = Input(kind, null) with { Minimum = 1, Maximum = 8 };

        Assert.Contains(says, input.Check(value) ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public void AChoiceOnlyTakesOneOfItsOptions()
    {
        var input = Input(ToolInputKind.Choice, null) with
        {
            Choices = [new ToolInputChoice("fast", "Fast")],
        };

        Assert.Null(input.Check("fast"));
        Assert.NotNull(input.Check("slow"));
    }

    [Fact]
    public void ARequiredInputIsNotUsableWhileItIsEmpty()
    {
        var input = Input(ToolInputKind.Text, null) with { Required = true };

        Assert.Contains("is needed", input.Check(" ") ?? string.Empty, StringComparison.Ordinal);
        Assert.Null(input.Check("something"));
    }

    /// <summary>A box is never empty, so requiring one would refuse the unticked half of it.</summary>
    [Fact]
    public void ARequiredBooleanIsStillAllowedToBeFalse()
    {
        var input = Input(ToolInputKind.Boolean, "--dry-run") with { Required = true };

        Assert.Null(input.Check("false"));
    }

    /// <summary>The form hands the script one list, in the order the manifest declared.</summary>
    [Fact]
    public void TheFormWritesEveryAnswerInDeclarationOrder()
    {
        var tool = FakeScriptTool.Built(
            Input(ToolInputKind.Folder, "--source") with { Key = "source", Default = "/art" },
            Input(ToolInputKind.Boolean, "--dry-run") with { Key = "dry", Default = "true" },
            Input(ToolInputKind.Text, null) with { Key = "target", Default = "out" });

        var form = new ToolInputsViewModel(tool, new Dictionary<string, string>(), workspaceRoot: null);

        Assert.True(form.CanRun);
        Assert.Equal(["--source", "/art", "--dry-run", "out"], form.Arguments());
    }

    /// <summary>What was remembered wins over the manifest's default, and only for its own key.</summary>
    [Fact]
    public void ARememberedAnswerFillsTheFormInsteadOfTheDefault()
    {
        var tool = FakeScriptTool.Built(
            Input(ToolInputKind.Text, null) with { Key = "source", Default = "/art", Remember = true },
            Input(ToolInputKind.Text, null) with { Key = "target", Default = "out" });

        var form = new ToolInputsViewModel(
            tool,
            new Dictionary<string, string> { ["source"] = "/sheets" },
            workspaceRoot: null);

        Assert.Equal(["/sheets", "out"], form.Arguments());
    }

    /// <summary>A form holding anything unusable cannot be run.</summary>
    [Fact]
    public void AFormWithSomethingMissingCannotBeRun()
    {
        var tool = FakeScriptTool.Built(
            Input(ToolInputKind.Text, null) with { Key = "source", Required = true, Default = string.Empty });

        var form = new ToolInputsViewModel(tool, new Dictionary<string, string>(), workspaceRoot: null);

        Assert.False(form.CanRun);

        form.Rows[0].Text = "/sheets";

        Assert.True(form.CanRun);
    }

    private static ToolInput Input(ToolInputKind kind, string? argument) =>
        new(
            "value",
            "Value",
            string.Empty,
            kind,
            argument,
            Required: false,
            Remember: false,
            Default: "/art",
            Choices: [],
            Minimum: null,
            Maximum: null);
}
