using System.Text;
using DBus.Services.Secrets;
using Kitbash.Core.IO;

namespace Kitbash.Core.Platform.Linux;

/// <summary>
/// The freedesktop Secret Service, over the session bus. Whichever daemon implements it
/// answers, so no desktop is named here.
/// </summary>
internal sealed class LinuxSecretStore : ISecretStore
{
    // Every session bus client is given this. Without it there is no bus to reach.
    private const string BusAddressVariable = "DBUS_SESSION_BUS_ADDRESS";

    private const string ContentType = "text/plain; charset=utf8";
    private const string ServiceAttribute = "service";
    private const string AccountAttribute = "account";

    private const string NoBus = "This machine has no session bus, so there is no keyring to store secrets in.";
    private const string NoCollection = "No keyring answered, so there is nowhere to store secrets.";

    private readonly IEnvironment _environment;

    public LinuxSecretStore(IEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        _environment = environment;
    }

    public async Task<SecretResult> ReadAsync(SecretKey key, CancellationToken cancellation = default)
    {
        var (collection, unavailable) = await OpenAsync(cancellation).ConfigureAwait(false);

        if (collection is null)
        {
            return unavailable!;
        }

        try
        {
            var items = await collection.SearchItemsAsync(AttributesFor(key)).ConfigureAwait(false);

            if (items.Length == 0)
            {
                return SecretResult.NotFound();
            }

            cancellation.ThrowIfCancellationRequested();

            var secret = await items[0].GetSecretAsync().ConfigureAwait(false);

            return SecretResult.Found(Encoding.UTF8.GetString(secret));
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            return SecretResult.Failed(failure.Message);
        }
    }

    public async Task<SecretResult> WriteAsync(
        SecretKey key,
        string secret,
        CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(secret);

        var (collection, unavailable) = await OpenAsync(cancellation).ConfigureAwait(false);

        if (collection is null)
        {
            return unavailable!;
        }

        try
        {
            var item = await collection.CreateItemAsync(
                LabelFor(key),
                AttributesFor(key),
                Encoding.UTF8.GetBytes(secret),
                ContentType,
                true).ConfigureAwait(false);

            // A null item is a collection that would not unlock, so nothing was written.
            return item is null ? SecretResult.Unavailable(NoCollection) : SecretResult.Stored();
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            return SecretResult.Failed(failure.Message);
        }
    }

    public async Task<SecretResult> RemoveAsync(SecretKey key, CancellationToken cancellation = default)
    {
        var (collection, unavailable) = await OpenAsync(cancellation).ConfigureAwait(false);

        if (collection is null)
        {
            return unavailable!;
        }

        try
        {
            var items = await collection.SearchItemsAsync(AttributesFor(key)).ConfigureAwait(false);

            if (items.Length == 0)
            {
                return SecretResult.NotFound();
            }

            // The same attributes can match more than one item, so every match goes rather
            // than leaving a duplicate a later read could find.
            foreach (var item in items)
            {
                cancellation.ThrowIfCancellationRequested();

                await item.DeleteAsync().ConfigureAwait(false);
            }

            return SecretResult.Stored();
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            return SecretResult.Failed(failure.Message);
        }
    }

    /// <summary>
    /// The default collection, or the result to give back instead. A connection per call,
    /// since a token is read rarely and nothing here should outlive the operation.
    /// </summary>
    private async Task<(Collection? Collection, SecretResult? Unavailable)> OpenAsync(
        CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(_environment.GetVariable(BusAddressVariable)))
        {
            return (null, SecretResult.Unavailable(NoBus));
        }

        try
        {
            var service = await SecretService.ConnectAsync(EncryptionType.Dh).ConfigureAwait(false);

            cancellation.ThrowIfCancellationRequested();

            var collection = await service.GetDefaultCollectionAsync().ConfigureAwait(false);

            return collection is null
                ? (null, SecretResult.Unavailable(NoCollection))
                : (collection, null);
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            return (null, SecretResult.Unavailable(failure.Message));
        }
    }

    private static string LabelFor(SecretKey key) => $"Kitbash: {key.Service} ({key.Account})";

    private static Dictionary<string, string> AttributesFor(SecretKey key) =>
        new(StringComparer.Ordinal)
        {
            [ServiceAttribute] = key.Service,
            [AccountAttribute] = key.Account,
        };
}
