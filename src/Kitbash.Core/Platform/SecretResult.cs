namespace Kitbash.Core.Platform;

/// <summary>How one secret store operation ended.</summary>
public enum SecretOutcome
{
    /// <summary>The operation did what was asked. A read carries the secret.</summary>
    Ok = 0,

    /// <summary>Nothing is stored under that key.</summary>
    NotFound = 1,

    /// <summary>This machine has no secret store, so nothing was read or written.</summary>
    Unavailable = 2,

    /// <summary>The store was there and refused.</summary>
    Failed = 3,
}

/// <summary>
/// What one secret store operation did. <paramref name="Secret"/> is set only by a read
/// that found something, and <paramref name="Message"/> is empty unless something failed.
/// </summary>
public sealed record SecretResult(SecretOutcome Outcome, string? Secret, string Message)
{
    public bool Succeeded => Outcome is SecretOutcome.Ok;

    public static SecretResult Stored() => new(SecretOutcome.Ok, null, string.Empty);

    public static SecretResult Found(string secret) => new(SecretOutcome.Ok, secret, string.Empty);

    public static SecretResult NotFound() => new(SecretOutcome.NotFound, null, string.Empty);

    public static SecretResult Unavailable(string message) =>
        new(SecretOutcome.Unavailable, null, message);

    public static SecretResult Failed(string message) => new(SecretOutcome.Failed, null, message);
}
