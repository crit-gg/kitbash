namespace Kitbash.Core.Settings;

/// <summary>One scope with its layers merged.</summary>
public interface ISettings
{
    /// <summary>
    /// Reads a dotted key such as <c>editor.font.size</c>. A value that will not
    /// convert to <typeparamref name="T"/> counts as absent, so the next layer is
    /// tried instead of throwing.
    /// </summary>
    bool TryGet<T>(string key, out T value);

    /// <summary>As <see cref="TryGet{T}"/>, returning <paramref name="fallback"/> when absent.</summary>
    T Get<T>(string key, T fallback);

    /// <summary>Whether any layer defines the key, whatever its type.</summary>
    bool Contains(string key);
}
