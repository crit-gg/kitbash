using System.Globalization;
using Tmds.DBus.Protocol;

namespace Kitbash.Core.Platform.Linux;

/// <summary>
/// Picking through the desktop portal, which is the only way that works on both Wayland and
/// X11 and on any desktop that implements it. The portal draws the picker and hands back one
/// colour.
/// </summary>
public sealed class LinuxScreenColour : IScreenColour
{
    /// <summary>The portal's own names. PickColor arrives in version 2 of Screenshot.</summary>
    private const string Bus = "org.freedesktop.portal.Desktop";
    private const string Path = "/org/freedesktop/portal/desktop";
    private const string Screenshot = "org.freedesktop.portal.Screenshot";
    private const string Request = "org.freedesktop.portal.Request";

    /// <summary>Names one request, so a reply meant for another is not read as ours.</summary>
    private int _asked;

    /// <summary>
    /// Whether there is a session bus to ask at all. Whether the portal answers on it is not
    /// asked here, since asking costs a round trip on every draw.
    /// </summary>
    public bool CanPick => DBusAddress.Session is not null;

    public async Task<ScreenColour?> PickAsync(CancellationToken cancellation = default)
    {
        if (DBusAddress.Session is not { } address || cancellation.IsCancellationRequested)
        {
            return null;
        }

        // The portal answers on a signal rather than to the call, and it destroys a request
        // whose caller has gone, so the connection has to outlive the call.
        using var connection = new DBusConnection(address);

        try
        {
            await connection.ConnectAsync();
        }
        catch (Exception failure)
        {
            throw new ScreenColourException("There is no session bus to ask.", failure);
        }

        var token = string.Create(CultureInfo.InvariantCulture, $"kitbash_{Interlocked.Increment(ref _asked)}");
        var answer = new TaskCompletionSource<ScreenColour?>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var listening = await connection.AddMatchAsync<(bool Ours, ScreenColour? Colour)>(
            new MatchRule
            {
                Type = MessageType.Signal,
                Sender = Bus,
                Interface = Request,
                Member = "Response",
            },
            (Message message, object? _) => Read(message, token),

            // A notification carrying no value is the connection or the observer ending, and
            // the ordinary end is this request being disposed once the answer is in.
            notification =>
            {
                if (notification.Exception is { } failure)
                {
                    answer.TrySetException(new ScreenColourException("The desktop portal stopped answering.", failure));
                }
                else if (notification.HasValue && notification.Value.Ours)
                {
                    answer.TrySetResult(notification.Value.Colour);
                }
            },
            flags: ObserverFlags.None);

        // A person taking their time is the ordinary case, so nothing here times out, and
        // the caller's own token is what ends the wait early. It is registered before the
        // call rather than after, so a token cancelled in between never leaves a picker up.
        using var dropped = cancellation.Register(() => answer.TrySetResult(null));

        try
        {
            await connection.CallMethodAsync(Ask(connection, token));
        }
        catch (Exception failure)
        {
            throw new ScreenColourException(Said(failure), failure);
        }

        return await answer.Task;
    }

    /// <summary>The call itself. The window is left empty, since this is a desktop wide pick.</summary>
    private static MessageBuffer Ask(DBusConnection connection, string token)
    {
        using var writer = connection.GetMessageWriter();

        writer.WriteMethodCallHeader(
            destination: Bus,
            path: Path,
            @interface: Screenshot,
            member: "PickColor",
            signature: "sa{sv}");

        writer.WriteString(string.Empty);

        var options = writer.WriteDictionaryStart();

        writer.WriteDictionaryEntryStart();
        writer.WriteString("handle_token");
        writer.WriteVariantString(token);
        writer.WriteDictionaryEnd(options);

        return writer.CreateMessage();
    }

    /// <summary>
    /// Reads a Response. The signal is broadcast to every request the portal answers, so the
    /// path is what says whether this one is ours. A code other than zero is a person
    /// changing their mind, which is an answer rather than a failure.
    /// </summary>
    private static (bool Ours, ScreenColour? Colour) Read(Message message, string token)
    {
        if (!message.PathIsSet || message.PathAsString?.EndsWith('/' + token, StringComparison.Ordinal) != true)
        {
            return (false, null);
        }

        var reader = message.GetBodyReader();

        if (reader.ReadUInt32() != 0)
        {
            return (true, null);
        }

        var results = reader.ReadDictionaryStart();

        while (reader.HasNext(results))
        {
            var name = reader.ReadString();
            var value = reader.ReadVariantValue();

            if (name == "color" && value.Count == 3)
            {
                return (true, new ScreenColour(
                    value.GetItem(0).GetDouble(),
                    value.GetItem(1).GetDouble(),
                    value.GetItem(2).GetDouble()));
            }
        }

        return (true, null);
    }

    /// <summary>
    /// The portal's own words where there are any. A person putting them into a search is
    /// better served by those than by anything written here.
    /// </summary>
    private static string Said(Exception failure) =>
        failure is DBusErrorReplyException named
            ? $"{named.ErrorName}: {named.ErrorMessage}"
            : failure.Message;
}
