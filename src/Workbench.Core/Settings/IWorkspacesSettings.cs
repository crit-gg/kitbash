namespace Workbench.Core.Settings;

/// <summary>The workspace choices that belong to one person on one machine.</summary>
public interface IWorkspacesSettings
{
    /// <summary>
    /// Where a new workspace is offered a home. Blank when there is no default, which is
    /// what a machine has until somebody sets one.
    /// </summary>
    string DefaultDirectory { get; }

    /// <summary>Blank clears it, so the machine goes back to having no default.</summary>
    void SetDefaultDirectory(string value);
}
