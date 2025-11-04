using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PrintingTools.Core;

namespace PrintingTools.Linux;

internal sealed class CupsCommandClient
{
    private const string DiagnosticsCategory = "CupsCommandClient";
    private static readonly string[] RequiredExecutables = ["lpstat", "lp", "lpoptions"];

    public static CupsCommandClient CreateDefault() => new();

    public static bool IsInstalled()
    {
        if (!OperatingSystem.IsLinux())
        {
            return false;
        }

        var pathValue = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathValue))
        {
            return false;
        }

        var lookup = pathValue
            .Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(p => p.Trim())
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        foreach (var executable in RequiredExecutables)
        {
            if (!CommandExists(executable, lookup))
            {
                return false;
            }
        }

        return true;
    }

    public async Task<CommandResult> RunAsync(string executable, IReadOnlyList<string>? arguments, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(executable))
        {
            throw new ArgumentException("Executable must be provided.", nameof(executable));
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = executable,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false
            };

            if (arguments is { Count: > 0 })
            {
                foreach (var argument in arguments)
                {
                    if (!string.IsNullOrWhiteSpace(argument))
                    {
                        startInfo.ArgumentList.Add(argument);
                    }
                }
            }

            using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            var stdout = new StringBuilder();
            var stderr = new StringBuilder();

            using var registration = cancellationToken.Register(static state =>
            {
                if (state is not Process proc)
                {
                    return;
                }

                try
                {
                    if (!proc.HasExited)
                    {
                        proc.Kill(entireProcessTree: true);
                    }
                }
                catch
                {
                    // Swallow exceptions raised while attempting to terminate the process.
                }
            }, process);

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data is not null)
                {
                    stdout.AppendLine(e.Data);
                }
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data is not null)
                {
                    stderr.AppendLine(e.Data);
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            return new CommandResult(process.ExitCode, stdout.ToString(), stderr.ToString(), null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            PrintDiagnostics.Report(DiagnosticsCategory, $"Failed to run '{executable}'.", ex, new { arguments });
            return new CommandResult(-1, string.Empty, ex.Message, ex);
        }
    }

    private static bool CommandExists(string executable, IEnumerable<string> searchDirectories)
    {
        foreach (var directory in searchDirectories)
        {
            try
            {
                var path = Path.Combine(directory, executable);
                if (File.Exists(path))
                {
                    return true;
                }
            }
            catch (Exception ex) when (ex is ArgumentException or PathTooLongException)
            {
                PrintDiagnostics.Report(DiagnosticsCategory, "Failed to probe command path.", ex, new { executable, directory });
            }
        }

        return false;
    }

    public readonly record struct CommandResult(int ExitCode, string StandardOutput, string StandardError, Exception? Exception)
    {
        public bool IsSuccess => Exception is null && ExitCode == 0;
    }
}
