using Kitbash.Core.Platform.Openers;
using Microsoft.Extensions.DependencyInjection;

namespace Kitbash.Core.Tests.Openers;

public sealed class OpenerArgumentsTests
{
    private static IOpenerArguments Arguments()
    {
        var services = new ServiceCollection();
        services.AddKitbashWorkspaceOpeners();

        return services.BuildServiceProvider().GetRequiredService<IOpenerArguments>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankTemplateGivesTheWorkspaceAlone(string? template)
    {
        Assert.Equal(["/home/a/ws"], Arguments().Build(template, "/home/a/ws"));
    }

    [Fact]
    public void TemplateWithNoTokenGetsTheWorkspaceAppended()
    {
        Assert.Equal(["--new-window", "/home/a/ws"], Arguments().Build("--new-window", "/home/a/ws"));
    }

    [Fact]
    public void TokenIsFilledInWhereItStands()
    {
        Assert.Equal(["-a", "/home/a/ws", "-b"], Arguments().Build("-a {workspace} -b", "/home/a/ws"));
    }

    [Fact]
    public void TokenWorksInsideALongerArgument()
    {
        Assert.Equal(
            ["--folder-uri=file:///home/a/ws"],
            Arguments().Build("--folder-uri=file://{workspace}", "/home/a/ws"));
    }

    [Fact]
    public void AWorkspaceWithSpacesStaysOneArgument()
    {
        var built = Arguments().Build("--open {workspace}", "/home/a/my games/ws");

        Assert.Equal(["--open", "/home/a/my games/ws"], built);
    }

    [Fact]
    public void QuotesGroupAndAreRemoved()
    {
        Assert.Equal(["one two", "three", "/ws"], Arguments().Build("\"one two\" three {workspace}", "/ws"));
    }

    [Fact]
    public void AnUnbalancedQuoteClosesAtTheEnd()
    {
        Assert.Equal(["-a", "b c /ws"], Arguments().Build("-a \"b c {workspace}", "/ws"));
    }

    [Fact]
    public void ABackslashSurvivesUnescaped()
    {
        Assert.Equal([@"C:\Program Files\x", @"C:\ws"], Arguments().Build(@"""C:\Program Files\x"" {workspace}", @"C:\ws"));
    }
}
