using System.ComponentModel;
using System.Diagnostics;

namespace Workbench.Core.Platform;

public sealed class ProcessRunner : IProcessRunner
{
    public void Run(ProcessRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var start = Describe(request);

        try
        {
            using var process = Process.Start(start);
        }
        catch (Win32Exception exception)
        {
            throw new ProcessStartException(request.FileName, exception);
        }
    }

    public async Task<ProcessOutput> ReadAsync(ProcessRequest request, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var start = Describe(request);

        // Reading needs the pipes, which the shell cannot give, so this always starts the
        // program itself whatever the request says.
        start.UseShellExecute = false;
        start.RedirectStandardOutput = true;
        start.RedirectStandardError = true;
        start.CreateNoWindow = true;

        Process? process;

        try
        {
            process = Process.Start(start);
        }
        catch (Win32Exception exception)
        {
            throw new ProcessStartException(request.FileName, exception);
        }

        if (process is null)
        {
            throw new ProcessStartException(request.FileName, new InvalidOperationException("No process was started."));
        }

        using (process)
        {
            try
            {
                // Both pipes are read before waiting. A program that fills one while nobody
                // reads it blocks, and then so does the wait.
                var output = process.StandardOutput.ReadToEndAsync(cancellation);
                var error = process.StandardError.ReadToEndAsync(cancellation);

                await process.WaitForExitAsync(cancellation).ConfigureAwait(false);

                return new ProcessOutput(
                    process.ExitCode,
                    await output.ConfigureAwait(false),
                    await error.ConfigureAwait(false));
            }
            catch (OperationCanceledException)
            {
                // Giving up on the wait does not stop the program. Left alone it keeps
                // running with nobody reading its pipes, so it fills them and hangs there
                // for as long as the app lives. Children go too, since the ones worth
                // cancelling are the ones that started a helper of their own.
                Stop(process);
                throw;
            }
        }
    }

    private static void Stop(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or NotSupportedException or Win32Exception or AggregateException)
        {
            // Already gone, or the operating system will not say. Either way there is
            // nothing further to do about it.
        }
    }

    private static ProcessStartInfo Describe(ProcessRequest request)
    {
        var start = new ProcessStartInfo(request.FileName)
        {
            UseShellExecute = request.UseShellExecute,
        };

        if (request.WorkingDirectory is { Length: > 0 } directory)
        {
            start.WorkingDirectory = directory;
        }

        foreach (var argument in request.Arguments)
        {
            start.ArgumentList.Add(argument);
        }

        // Environment needs the program started here rather than by the shell, so a request
        // that asks for both gets the variables and loses the shell.
        if (request.Environment is { Count: > 0 } environment)
        {
            start.UseShellExecute = false;

            foreach (var (name, value) in environment)
            {
                start.Environment[name] = value.Length == 0 ? null : value;
            }
        }

        return start;
    }
}
