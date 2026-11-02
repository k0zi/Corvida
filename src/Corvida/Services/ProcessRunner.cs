using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Corvida.Services;

public sealed record ProcessResult(int ExitCode, string StdOut, string StdErr)
{
    public bool Succeeded => ExitCode == 0;

    /// <summary>What to show the user when the command failed: stderr, or stdout when stderr is empty.</summary>
    public string Error => string.IsNullOrWhiteSpace(StdErr) ? StdOut.Trim() : StdErr.Trim();
}

public static class ProcessRunner
{
    /// <summary>Exit code reported when the executable does not exist.</summary>
    public const int NotFound = 127;

    /// <summary>
    /// Runs <paramref name="fileName"/> with <paramref name="args"/> (passed as a list, never through a shell)
    /// and captures its output. A missing executable is reported as exit code <see cref="NotFound"/> instead
    /// of throwing, since "podman is not installed" is an expected answer here.
    /// </summary>
    public static async Task<ProcessResult> RunAsync(
        string fileName, IEnumerable<string> args, string? stdin = null, CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo(fileName)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = stdin is not null,
            UseShellExecute = false,
        };
        foreach (var arg in args) psi.ArgumentList.Add(arg);

        using var process = new Process { StartInfo = psi };
        try
        {
            process.Start();
        }
        catch (Win32Exception)
        {
            return new ProcessResult(NotFound, string.Empty, $"{fileName}: command not found");
        }

        if (stdin is not null)
        {
            await process.StandardInput.WriteAsync(stdin);
            process.StandardInput.Close();
        }

        // Read both streams concurrently: a full stderr pipe would otherwise block the child forever.
        var stdout = process.StandardOutput.ReadToEndAsync(ct);
        var stderr = process.StandardError.ReadToEndAsync(ct);
        try
        {
            await process.WaitForExitAsync(ct);
        }
        catch (System.OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw;
        }

        return new ProcessResult(process.ExitCode, await stdout, await stderr);
    }
}
