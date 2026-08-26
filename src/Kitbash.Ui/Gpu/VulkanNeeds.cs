namespace Kitbash.Ui.Gpu;

/// <summary>
/// What a tool needs of the device before it is worth taking. The first caller to acquire
/// the shared context is the one whose needs are used.
/// </summary>
public readonly record struct VulkanNeeds
{
    /// <summary>A device without the tessellation stage is rejected rather than taken.</summary>
    public bool Tessellation { get; init; }
}
