namespace Kitbash.Tools;

/// <summary>
/// Installing a tool from a folder on this machine. Nothing is downloaded and nothing is
/// copied: the folder is checked and remembered, and the tool runs where it already sits.
/// </summary>
public interface IToolFolderInstaller
{
    /// <summary>
    /// Points Kitbash at a folder holding a manifest and the program it names. Touches a
    /// disk, so call it off the UI thread.
    /// </summary>
    /// <exception cref="ToolInstallException">
    /// The folder is not one this machine can run a tool out of, and why.
    /// </exception>
    InstalledTool InstallFrom(string directory);
}
