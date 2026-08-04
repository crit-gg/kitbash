namespace Kitbash.Core.Platform;

/// <summary>
/// Keeps one person's secrets in whatever the running OS provides. Every operation can
/// answer <see cref="SecretOutcome.Unavailable"/>, which is a machine with no keyring.
/// </summary>
/// <example>
/// <code>
/// var key = SecretKey.Parse("github", "octocat");
/// var read = await store.ReadAsync(key);
/// var token = read.Succeeded ? read.Secret : null;
/// </code>
/// </example>
public interface ISecretStore
{
    /// <summary>Reads a secret. The result carries it when the outcome is Ok.</summary>
    Task<SecretResult> ReadAsync(SecretKey key, CancellationToken cancellation = default);

    /// <summary>Writes a secret, replacing whatever was under that key.</summary>
    Task<SecretResult> WriteAsync(SecretKey key, string secret, CancellationToken cancellation = default);

    /// <summary>Removes a secret. A key that was never written answers NotFound.</summary>
    Task<SecretResult> RemoveAsync(SecretKey key, CancellationToken cancellation = default);
}
