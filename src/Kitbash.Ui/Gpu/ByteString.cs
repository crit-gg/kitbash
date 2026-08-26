using System.Runtime.InteropServices;

namespace Kitbash.Ui.Gpu;

/// <summary>A null terminated ANSI copy of a string, owned until disposed.</summary>
internal sealed unsafe class ByteString : IDisposable
{
    public ByteString(string value) => Pointer = Marshal.StringToHGlobalAnsi(value);

    public IntPtr Pointer { get; }

    public void Dispose() => Marshal.FreeHGlobal(Pointer);

    public static implicit operator byte*(ByteString value) => (byte*)value.Pointer;
}

/// <summary>The argv shape Vulkan wants for extension and layer lists.</summary>
internal sealed unsafe class ByteStringList : IDisposable
{
    private readonly List<ByteString> _items;
    private readonly byte** _pointer;

    public ByteStringList(IEnumerable<string> items)
    {
        _items = [.. items.Select(item => new ByteString(item))];
        _pointer = (byte**)Marshal.AllocHGlobal((IntPtr.Size * _items.Count) + 1);

        for (var i = 0; i < _items.Count; i++)
        {
            _pointer[i] = (byte*)_items[i].Pointer;
        }
    }

    public uint Count => (uint)_items.Count;

    public void Dispose()
    {
        foreach (var item in _items)
        {
            item.Dispose();
        }

        Marshal.FreeHGlobal(new IntPtr(_pointer));
    }

    public static implicit operator byte**(ByteStringList value) => value._pointer;
}
