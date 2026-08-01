namespace Workbench.Core.Platform;

/// <summary>
/// A web address that is safe to hand to the desktop. Parsing is the only way to make
/// one, so a value of this type has already been checked.
/// </summary>
public readonly record struct WebAddress
{
    private WebAddress(Uri value)
    {
        Value = value;
    }

    public Uri Value { get; }

    /// <summary>
    /// Accepts absolute http and https only. The shell open commands would otherwise
    /// start local programs and registered scheme handlers.
    /// </summary>
    public static WebAddress Parse(Uri url)
    {
        ArgumentNullException.ThrowIfNull(url);

        if (!url.IsAbsoluteUri)
        {
            throw new ArgumentException("URL must be absolute.", nameof(url));
        }

        if (url.Scheme != Uri.UriSchemeHttp && url.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException(
                $"Scheme '{url.Scheme}' is not allowed. Use http or https.",
                nameof(url));
        }

        return new WebAddress(url);
    }

    public static WebAddress Parse(string url) => Parse(new Uri(url, UriKind.RelativeOrAbsolute));

    public override string ToString() => Value.AbsoluteUri;
}
