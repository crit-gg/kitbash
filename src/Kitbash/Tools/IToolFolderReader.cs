namespace Kitbash.Tools;

/// <summary>Works out what a folder on this machine holds and how it would be run.</summary>
public interface IToolFolderReader
{
    /// <summary>The tool the folder describes, always linked, since the folder is not ours.</summary>
    /// <exception cref="ToolManifestException">It holds no tool this machine can run.</exception>
    InstalledTool Read(string directory);
}
