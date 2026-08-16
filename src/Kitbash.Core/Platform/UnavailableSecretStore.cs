namespace Kitbash.Core.Platform;

/// <summary>
/// A machine whose keyring Kitbash cannot speak to yet. macOS takes this: the Keychain is
/// reached through Security.framework, which wants the app signed with a stable identity
/// or it asks a person for permission on every launch.
/// </summary>
public sealed class UnavailableSecretStore : ISecretStore
{
    private const string Message = "Kitbash cannot reach the keychain on this machine yet.";

    public Task<SecretResult> ReadAsync(SecretKey key, CancellationToken cancellation = default) =>
        Answer;

    public Task<SecretResult> WriteAsync(
        SecretKey key,
        string secret,
        CancellationToken cancellation = default) => Answer;

    public Task<SecretResult> RemoveAsync(SecretKey key, CancellationToken cancellation = default) =>
        Answer;

    private static Task<SecretResult> Answer =>
        Task.FromResult(SecretResult.Unavailable(Message));
}
