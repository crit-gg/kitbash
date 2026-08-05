using Kitbash.Core.Platform.Openers;
using Microsoft.Extensions.DependencyInjection;

namespace Kitbash.Core.Tests.Openers;

/// <summary>Walks a real tree in a temporary folder, so what it reports is what is there.</summary>
public sealed class WorkspaceFileFinderTests : IDisposable
{
    private static readonly string[] Solutions = [".sln", ".slnx"];

    private readonly string _root = Directory.CreateTempSubdirectory("kitbash-walk-").FullName;

    private static IWorkspaceFileFinder Finder()
    {
        var services = new ServiceCollection();
        services.AddKitbashWorkspaceOpeners();

        return services.BuildServiceProvider().GetRequiredService<IWorkspaceFileFinder>();
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string Write(params string[] segments)
    {
        var path = Path.Combine([_root, .. segments]);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, string.Empty);

        return path;
    }

    [Fact]
    public void TheRootAloneIsDepthOne()
    {
        var top = Write("Game.sln");
        Write("nested", "Other.sln");

        Assert.Equal([top], Finder().Find(_root, Solutions, 1));
    }

    [Fact]
    public void DepthIsHonoured()
    {
        Write("one", "two", "three", "Deep.sln");

        Assert.Empty(Finder().Find(_root, Solutions, 3));
        Assert.Single(Finder().Find(_root, Solutions, 4));
    }

    [Fact]
    public void ADotDirectoryIsSkipped()
    {
        Write(".git", "Hidden.sln");

        Assert.Empty(Finder().Find(_root, Solutions, 4));
    }

    [Fact]
    public void NodeModulesIsSkipped()
    {
        Write("node_modules", "Package.sln");

        Assert.Empty(Finder().Find(_root, Solutions, 4));
    }

    /// <summary>An extension describes the shape of a name, so case does not matter.</summary>
    [Fact]
    public void ExtensionsMatchIgnoringCase()
    {
        Write("Game.SLN");

        Assert.Single(Finder().Find(_root, Solutions, 1));
    }

    [Fact]
    public void BothSolutionShapesAreFound()
    {
        Write("Old.sln");
        Write("New.slnx");

        Assert.Equal(2, Finder().Find(_root, Solutions, 1).Count);
    }

    [Fact]
    public void TheOrderIsRepeatable()
    {
        Write("b", "B.sln");
        Write("a", "A.sln");
        Write("C.sln");

        var finder = Finder();

        Assert.Equal(finder.Find(_root, Solutions, 3), finder.Find(_root, Solutions, 3));
    }

    [Fact]
    public void NothingAskedForIsNothingFound()
    {
        Write("Game.sln");

        Assert.Empty(Finder().Find(_root, [], 4));
        Assert.Empty(Finder().Find(_root, Solutions, 0));
        Assert.Empty(Finder().Find(string.Empty, Solutions, 4));
    }

    /// <summary>A folder that cannot be read counts as empty rather than throwing.</summary>
    [Fact]
    public void AMissingFolderIsNotAnError()
    {
        Assert.Empty(Finder().Find(Path.Combine(_root, "gone"), Solutions, 4));
    }
}
