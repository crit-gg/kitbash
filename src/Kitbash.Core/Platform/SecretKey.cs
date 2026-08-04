namespace Kitbash.Core.Platform;

/// <summary>
/// Names one secret. Parsing is the only way to make one, so a key that reaches a store
/// has already been checked.
/// </summary>
public readonly record struct SecretKey
{
    /// <summary>The longest either part may be. Windows allows far more in a target name.</summary>
    public const int MaximumPartLength = 256;

    private SecretKey(string service, string account)
    {
        Service = service;
        Account = account;
    }

    /// <summary>What the secret is for, such as the name of a service being signed in to.</summary>
    public string Service { get; }

    /// <summary>Who the secret belongs to on that service.</summary>
    public string Account { get; }

    /// <summary>
    /// Both parts are required. A control character is refused because both stores carry
    /// these as text a person can read back in a keyring browser.
    /// </summary>
    public static SecretKey Parse(string service, string account)
    {
        return new SecretKey(Check(service, nameof(service)), Check(account, nameof(account)));
    }

    public override string ToString() => $"{Service}:{Account}";

    private static string Check(string part, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(part, name);

        if (part.Length > MaximumPartLength)
        {
            throw new ArgumentException(
                $"Must be {MaximumPartLength} characters or fewer.",
                name);
        }

        foreach (var character in part)
        {
            if (char.IsControl(character))
            {
                throw new ArgumentException("Must not contain control characters.", name);
            }
        }

        return part;
    }
}
