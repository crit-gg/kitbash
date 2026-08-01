using System.ComponentModel;
using System.Diagnostics;

namespace Workbench.Core.Platform;

public sealed class ProcessRunner : IProcessRunner
{
    public void Run(ProcessRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var start = new ProcessStartInfo(request.FileName)
        {
            UseShellExecute = request.UseShellExecute,
        };

        foreach (var argument in request.Arguments)
        {
            start.ArgumentList.Add(argument);
        }

        try
        {
            using var process = Process.Start(start);
        }
        catch (Win32Exception exception)
        {
            throw new ProcessStartException(request.FileName, exception);
        }
    }
}
