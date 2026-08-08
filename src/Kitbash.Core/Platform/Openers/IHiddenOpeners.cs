using Kitbash.Core.Settings;

namespace Kitbash.Core.Platform.Openers;

/// <summary>
/// The detected tools a person does not want offered, held in the global config as
/// <c>tools.hidden</c>. A tool they added is removed from its own list instead.
/// </summary>
public interface IHiddenOpeners
{
    /// <summary>Every hidden id, including ones nothing on this machine answers to.</summary>
    IReadOnlyList<string> Read();

    /// <exception cref="SettingsFileUnreadableException">
    /// The file is there and could not be read, so nothing was written.
    /// </exception>
    void Write(IReadOnlyList<string> ids);
}
