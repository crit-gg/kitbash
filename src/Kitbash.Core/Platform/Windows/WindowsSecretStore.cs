using System.ComponentModel;
using System.Runtime.Versioning;
using System.Text;
using Meziantou.Framework.Win32;

namespace Kitbash.Core.Platform.Windows;

/// <summary>
/// The Windows Credential Manager. What this writes is visible to a person under
/// Credential Manager in Control Panel, so they can remove it without Kitbash.
/// </summary>
// The version is the one the credential package declares, and every supported Windows is
// above it. Plain "windows" does not satisfy it and the build fails on CA1416.
[SupportedOSPlatform("windows5.1.2600")]
internal sealed class WindowsSecretStore : ISecretStore
{
    // CRED_MAX_CREDENTIAL_BLOB_SIZE. The blob is UTF-16, and Windows answers 1783 above this.
    private const int MaximumSecretBytes = 5 * 512;

    // ERROR_NOT_FOUND, which CredDelete gives for a target that was never written.
    private const int ErrorNotFound = 1168;

    private const string Comment = "Saved by Kitbash.";

    public Task<SecretResult> ReadAsync(SecretKey key, CancellationToken cancellation = default)
    {
        return Task.Run(() => Read(key), cancellation);
    }

    public Task<SecretResult> WriteAsync(
        SecretKey key,
        string secret,
        CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(secret);

        return Task.Run(() => Write(key, secret), cancellation);
    }

    public Task<SecretResult> RemoveAsync(SecretKey key, CancellationToken cancellation = default)
    {
        return Task.Run(() => Remove(key), cancellation);
    }

    // Credential Manager keys on a target name alone, so both halves of the key go into it.
    private static string TargetFor(SecretKey key) => $"Kitbash:{key.Service}:{key.Account}";

    private static SecretResult Read(SecretKey key)
    {
        try
        {
            var credential = CredentialManager.ReadCredential(TargetFor(key), CredentialType.Generic);

            return credential?.Password is { } secret
                ? SecretResult.Found(secret)
                : SecretResult.NotFound();
        }
        catch (Win32Exception failure)
        {
            return SecretResult.Failed(failure.Message);
        }
    }

    private static SecretResult Write(SecretKey key, string secret)
    {
        if (Encoding.Unicode.GetByteCount(secret) > MaximumSecretBytes)
        {
            return SecretResult.Failed(
                $"Windows will not store a secret longer than {MaximumSecretBytes} bytes.");
        }

        try
        {
            CredentialManager.WriteCredential(
                TargetFor(key),
                key.Account,
                secret,
                Comment,
                CredentialPersistence.LocalMachine,
                CredentialType.Generic);

            return SecretResult.Stored();
        }
        catch (Win32Exception failure)
        {
            return SecretResult.Failed(failure.Message);
        }
    }

    private static SecretResult Remove(SecretKey key)
    {
        try
        {
            CredentialManager.DeleteCredential(TargetFor(key), CredentialType.Generic);

            return SecretResult.Stored();
        }
        catch (Win32Exception failure) when (failure.NativeErrorCode == ErrorNotFound)
        {
            return SecretResult.NotFound();
        }
        catch (Win32Exception failure)
        {
            return SecretResult.Failed(failure.Message);
        }
    }
}
