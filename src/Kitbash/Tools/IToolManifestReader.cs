namespace Kitbash.Tools;

/// <summary>Reads what a tool says about itself, and refuses anything it cannot trust.</summary>
public interface IToolManifestReader
{
    /// <summary>The newest file format this launcher understands.</summary>
    int NewestFormat { get; }

    /// <exception cref="ToolManifestException">The manifest is unreadable or unusable.</exception>
    ToolManifest Read(string json);

    /// <summary>Reads the manifest inside an installed version's folder.</summary>
    /// <exception cref="ToolManifestException">It is missing, unreadable or unusable.</exception>
    ToolManifest ReadFrom(string directory);

    /// <summary>Reads the loose form a folder on this machine may use instead.</summary>
    /// <param name="folderName">What the tool is called when the file does not say.</param>
    /// <exception cref="ToolManifestException">It is unreadable or unusable.</exception>
    ToolManifest ReadDevelopment(string json, string folderName);

    /// <summary>Reads the loose form out of a folder somebody pointed at.</summary>
    /// <exception cref="ToolManifestException">It is missing, unreadable or unusable.</exception>
    ToolManifest ReadDevelopmentFrom(string directory);
}
