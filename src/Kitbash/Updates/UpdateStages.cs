using Humanizer;

namespace Kitbash.Updates;

/// <summary>
/// What the splash says while one update is fetched and applied.
/// </summary>
public sealed class UpdateStages
{
    private readonly string _version;
    private readonly string _size;

    public UpdateStages(AvailableUpdate found)
    {
        ArgumentNullException.ThrowIfNull(found);

        _version = found.Version;

        // The feed states the size, so this is the real one rather than a total worked out
        // from what has arrived so far.
        _size = found.Size.Bytes().Humanize("0.#");
    }

    /// <summary>
    /// One report from the download.
    /// </summary>
    /// <param name="percent">Whole percent, clamped to 0 through 100.</param>
    public UpdateStage Downloading(int percent)
    {
        var done = Math.Clamp(percent, 0, 100);

        // Velopack counts the download alone and runs the checksum inside the same call, so
        // everything after this reaches a hundred is the check rather than the fetch.
        var status = done < 100
            ? $"Downloading Kitbash {_version}"
            : "Checking the download";

        // A percentage of a stated total, because Velopack reports whole percentages and
        // nothing else. Bytes so far would be that number multiplied back up, which is a
        // figure this code does not have.
        return new UpdateStage(status, done / 100d, $"{done}% of {_size}");
    }

    /// <summary>
    /// The download is in and the swap is running. Indeterminate, since replacing the copy
    /// on the machine reports nothing and takes what it takes.
    /// </summary>
    public UpdateStage Restarting() =>
        new("Restarting Kitbash", null, string.Empty);
}
