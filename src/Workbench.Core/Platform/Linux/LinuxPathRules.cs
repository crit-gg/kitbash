using Workbench.Core.IO;

namespace Workbench.Core.Platform.Linux;

/// <summary>
/// Case counts. A filesystem can be mounted case insensitive here, but the kernel
/// default is not, so two names differing only in case are two places.
/// </summary>
internal sealed class LinuxPathRules : PathRules
{
    protected override StringComparison Comparison => StringComparison.Ordinal;
}
