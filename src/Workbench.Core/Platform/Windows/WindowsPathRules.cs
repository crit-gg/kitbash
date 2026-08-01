using Workbench.Core.IO;

namespace Workbench.Core.Platform.Windows;

/// <summary>Case does not count, so two names differing only in case are one place.</summary>
internal sealed class WindowsPathRules : PathRules
{
    protected override StringComparison Comparison => StringComparison.OrdinalIgnoreCase;
}
