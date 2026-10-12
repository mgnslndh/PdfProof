using System.Diagnostics;

namespace PdfProof.FixtureGenerator;

// Runs a command-line tool (Typst, LibreOffice) and captures its output. Arguments are passed as a list, so paths
// with spaces need no quoting. A tool that runs past the timeout (default 2 minutes) is killed.
internal static class ExternalProcess
{
    public static async Task<(int ExitCode, string Output, string Error)> RunAsync(
        string fileName, IReadOnlyList<string> arguments, string? workingDirectory = null, TimeSpan? timeout = null)
    {
        var start = new ProcessStartInfo(fileName)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = workingDirectory ?? "",
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start) ?? throw new InvalidOperationException($"{fileName} did not start.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        var limit = timeout ?? TimeSpan.FromMinutes(2);
        using var cancel = new CancellationTokenSource(limit);
        try
        {
            await process.WaitForExitAsync(cancel.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"{fileName} did not finish within {limit.TotalSeconds} s.");
        }

        return (process.ExitCode, await output, await error);
    }
}
