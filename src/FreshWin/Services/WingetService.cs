using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using FreshWin.Models;

namespace FreshWin.Services;

/// <summary>Thin wrapper around the winget CLI, which does the actual installing.</summary>
public sealed class WingetService
{
    private static readonly Regex AnsiEscape = new(@"\x1B\[[0-9;?]*[ -/]*[@-~]", RegexOptions.Compiled);
    private static readonly Regex ProgressNoise = new(@"^[\s█▒░▬─\-\\/|]*$", RegexOptions.Compiled);

    // winget exit codes we can turn into a friendlier message.
    private const int ErrUpdateNotApplicable = unchecked((int)0x8A15002B);
    private const int ErrNoApplicableInstaller = unchecked((int)0x8A150014);
    private const int ErrAccessDenied = unchecked((int)0x80070005);
    private const int ErrCancelled = unchecked((int)0x8A150023);
    private const int MsiRebootRequired = 3010;
    private const int MsiRebootInitiated = 1641;
    private const int MsiUserCancelled = 1602;
    private const int MsiFatalError = 1603;

    public string? ExecutablePath { get; private set; }
    public string? Version { get; private set; }
    public bool IsAvailable => ExecutablePath is not null;

    private bool _supportsDisableInteractivity;

    /// <summary>Finds winget.exe and reads its version. Safe to call once at start-up.</summary>
    public async Task ProbeAsync()
    {
        ExecutablePath = Locate();
        if (ExecutablePath is null) return;

        try
        {
            var (exit, output) = await CaptureAsync(ExecutablePath, "--version", TimeSpan.FromSeconds(20));
            if (exit == 0)
            {
                Version = output.Trim().Split('\n').FirstOrDefault()?.Trim();
                _supportsDisableInteractivity = SupportsDisableInteractivity(Version);
            }
        }
        catch
        {
            // A missing / broken winget is reported through IsAvailable and the UI banner.
        }
    }

    private static string? Locate()
    {
        var candidates = new List<string>();

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrEmpty(localAppData))
            candidates.Add(Path.Combine(localAppData, "Microsoft", "WindowsApps", "winget.exe"));

        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try { candidates.Add(Path.Combine(dir.Trim(), "winget.exe")); }
            catch { /* malformed PATH entry */ }
        }

        foreach (var candidate in candidates)
        {
            try { if (File.Exists(candidate)) return candidate; }
            catch { /* unreachable path */ }
        }

        return null;
    }

    /// <summary>--disable-interactivity landed in winget 1.4; older builds reject the flag.</summary>
    internal static bool SupportsDisableInteractivity(string? version)
    {
        if (string.IsNullOrWhiteSpace(version)) return false;

        var match = Regex.Match(version, @"(\d+)\.(\d+)");
        if (!match.Success) return false;

        var major = int.Parse(match.Groups[1].Value);
        var minor = int.Parse(match.Groups[2].Value);
        return major > 1 || (major == 1 && minor >= 4);
    }

    public string BuildInstallArguments(AppEntry app)
    {
        var args = new StringBuilder();
        args.Append("install --id ").Append(Quote(app.Id));
        args.Append(" --exact");
        args.Append(" --source ").Append(Quote(app.Source));
        args.Append(" --silent");
        args.Append(" --accept-package-agreements --accept-source-agreements");
        if (_supportsDisableInteractivity) args.Append(" --disable-interactivity");
        return args.ToString();
    }

    /// <summary>Runs one install, streaming winget's output through <paramref name="log"/>.</summary>
    public async Task<int> InstallAsync(AppEntry app, Action<string> log, CancellationToken ct)
    {
        if (ExecutablePath is null) throw new InvalidOperationException("winget was not found on this system.");

        var psi = new ProcessStartInfo(ExecutablePath, BuildInstallArguments(app))
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

        void Forward(string? raw)
        {
            var line = Clean(raw);
            if (line is not null) log(line);
        }

        process.OutputDataReceived += (_, e) => Forward(e.Data);
        process.ErrorDataReceived += (_, e) => Forward(e.Data);

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            await process.WaitForExitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            // The user asked to stop; let the running installer finish rather than
            // killing it half-way and leaving a broken install behind.
            await process.WaitForExitAsync(CancellationToken.None);
            throw;
        }

        return process.ExitCode;
    }

    /// <summary>
    /// Every winget package id installed on this PC, via <c>winget export</c>. That writes
    /// real JSON, which beats parsing the localised table `winget list` prints.
    /// </summary>
    public async Task<List<string>> ExportInstalledAsync(CancellationToken ct)
    {
        var ids = new List<string>();
        if (ExecutablePath is null) return ids;

        var temp = Path.Combine(Path.GetTempPath(), $"freshwin-export-{Guid.NewGuid():N}.json");

        try
        {
            var psi = new ProcessStartInfo(ExecutablePath)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            psi.ArgumentList.Add("export");
            psi.ArgumentList.Add("-o");
            psi.ArgumentList.Add(temp);
            psi.ArgumentList.Add("--accept-source-agreements");
            psi.ArgumentList.Add("--disable-interactivity");

            using var process = Process.Start(psi);
            if (process is null) return ids;

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromMinutes(2));

            _ = process.StandardOutput.ReadToEndAsync(cts.Token);
            _ = process.StandardError.ReadToEndAsync(cts.Token);
            await process.WaitForExitAsync(cts.Token);

            if (!File.Exists(temp)) return ids;

            using var document = JsonDocument.Parse(await File.ReadAllTextAsync(temp, ct));
            if (!document.RootElement.TryGetProperty("Sources", out var sources)) return ids;

            foreach (var source in sources.EnumerateArray())
            {
                if (!source.TryGetProperty("Packages", out var packages)) continue;

                foreach (var package in packages.EnumerateArray())
                {
                    if (package.TryGetProperty("PackageIdentifier", out var id) &&
                        id.GetString() is { Length: > 0 } value)
                    {
                        ids.Add(value);
                    }
                }
            }
        }
        catch
        {
            // A failed scan just leaves the cards without an "installed" badge.
        }
        finally
        {
            try { if (File.Exists(temp)) File.Delete(temp); } catch { /* temp file */ }
        }

        return ids;
    }

    /// <summary>Strips terminal escapes and winget's redrawn progress bars from a log line.</summary>
    internal static string? Clean(string? raw)
    {
        if (string.IsNullOrEmpty(raw)) return null;

        var line = raw;
        var lastReturn = line.LastIndexOf('\r');
        if (lastReturn >= 0) line = line[(lastReturn + 1)..];

        line = AnsiEscape.Replace(line, "").TrimEnd();
        if (line.Length == 0) return null;
        if (ProgressNoise.IsMatch(line)) return null;

        return line;
    }

    public static (RunStatus Status, string? Detail) Interpret(int exitCode) => exitCode switch
    {
        0 => (RunStatus.Done, null),
        MsiRebootRequired or MsiRebootInitiated => (RunStatus.NeedsRestart, null),
        ErrUpdateNotApplicable => (RunStatus.AlreadyDone, null),
        ErrNoApplicableInstaller => (RunStatus.Failed, "no matching package for this PC"),
        ErrAccessDenied => (RunStatus.Failed, "needs administrator rights"),
        ErrCancelled => (RunStatus.Failed, "cancelled"),
        MsiUserCancelled => (RunStatus.Failed, "cancelled by the installer"),
        MsiFatalError => (RunStatus.Failed, "the installer reported a fatal error"),
        _ => (RunStatus.Failed, $"winget exit 0x{exitCode:X8}")
    };

    private static async Task<(int ExitCode, string Output)> CaptureAsync(string exe, string arguments, TimeSpan timeout)
    {
        var psi = new ProcessStartInfo(exe, arguments)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        using var process = Process.Start(psi) ?? throw new InvalidOperationException($"Could not start {exe}.");
        using var cts = new CancellationTokenSource(timeout);

        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync(cts.Token);

        return (process.ExitCode, await stdout + await stderr);
    }

    private static string Quote(string value) => value.Contains(' ') ? $"\"{value}\"" : value;
}
