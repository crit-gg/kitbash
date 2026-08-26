using Avalonia;
using Silk.NET.Vulkan;

namespace Kitbash.Ui.Gpu;

/// <summary>Everything a <see cref="VulkanImage"/> needs to exist. Defaults are a plain 2D color image.</summary>
public readonly record struct ImageDescription
{
    public required Format Format { get; init; }

    public required PixelSize Size { get; init; }

    public required ImageUsageFlags Usage { get; init; }

    public uint MipLevels { get; init; } = 1;

    /// <summary>Six for a cube. <see cref="Cube"/> is what decides how the view reads them.</summary>
    public uint Layers { get; init; } = 1;

    public bool Cube { get; init; }

    /// <summary>Carries the external memory chain, so the compositor can import it.</summary>
    public bool Exportable { get; init; }

    public bool Depth { get; init; }

    public ImageDescription()
    {
    }

    /// <summary>The full chain down to one pixel, which is what a sampled material map wants.</summary>
    public static uint MipsFor(PixelSize size)
        => (uint)(int.Log2(int.Max(size.Width, size.Height)) + 1);
}
