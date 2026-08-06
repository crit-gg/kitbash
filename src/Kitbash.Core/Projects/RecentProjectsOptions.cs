using Kitbash.Core.Settings;

namespace Kitbash.Core.Projects;

/// <summary>
/// Where an app's recent list is kept and how long it runs. Register one before
/// <c>AddKitbashRecentProjects</c> to change either.
/// </summary>
/// <param name="Scope">
/// Which state file the list lands in. A tool passes its own scope so two tools never
/// share a list.
/// </param>
/// <param name="Limit">How many entries are kept. The oldest fall off the end.</param>
public sealed record RecentProjectsOptions(SettingsScope Scope, int Limit = 50);
