using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;
using FreshWin.Models;

namespace FreshWin.Services;

/// <summary>
/// Reads, applies and reverts the Windows settings in <see cref="Tweaks"/>.
///
/// Every applied change is written to an undo file first, recording the previous value —
/// or the fact that there was none — so a run can always be put back.
/// </summary>
public sealed class TweakEngine
{
    private sealed class UndoEntry
    {
        public string Tweak { get; set; } = "";
        public string? Key { get; set; }
        public string? Name { get; set; }
        public string? Kind { get; set; }
        public bool HadValue { get; set; }
        public string? PreviousValue { get; set; }
        public bool KeyExisted { get; set; }
        public bool DeleteKey { get; set; }
        public string[]? RevertCommand { get; set; }
    }

    private sealed class UndoFile
    {
        public string CreatedUtc { get; set; } = "";
        public List<UndoEntry> Entries { get; set; } = new();
    }

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    private readonly List<UndoEntry> _pending = new();

    public string UndoFolder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FreshWin");

    public string? LastUndoFile { get; private set; }

    // ------------------------------------------------------------------ reading

    /// <summary>Current state of a tweak, or null when it cannot be read.</summary>
    public static bool? ReadState(Tweak tweak)
    {
        try
        {
            if (tweak.Detect is { } detect)
                return Same(ReadValue(detect), detect.On);

            if (tweak.Values.Count == 0) return null;

            foreach (var value in tweak.Values)
                if (!Same(ReadValue(value), value.On)) return false;

            return true;
        }
        catch
        {
            return null;
        }
    }

    public static void RefreshState(IEnumerable<Tweak> tweaks)
    {
        foreach (var tweak in tweaks) tweak.IsOnNow = ReadState(tweak);
    }

    // ------------------------------------------------------------------ applying

    /// <summary>Applies one tweak. Throws with a readable message when it cannot be done.</summary>
    public async Task ApplyAsync(Tweak tweak, Action<string> log, CancellationToken ct)
    {
        if (tweak.ApplyCommand is { Length: > 0 } command)
        {
            await RunAsync(command, log, ct);
            _pending.Add(new UndoEntry { Tweak = tweak.Name, RevertCommand = tweak.RevertCommand });
            tweak.IsOnNow = ReadState(tweak);
            return;
        }

        foreach (var value in tweak.Values)
        {
            var (root, sub) = Split(value.Key);

            bool keyExisted;
            using (var probe = root.OpenSubKey(sub))
            {
                keyExisted = probe is not null;
                var previous = probe?.GetValue(value.Name);

                _pending.Add(new UndoEntry
                {
                    Tweak = tweak.Name,
                    Key = value.Key,
                    Name = value.Name,
                    Kind = value.Kind.ToString(),
                    HadValue = previous is not null,
                    PreviousValue = previous is null ? null : Convert.ToString(previous, CultureInfo.InvariantCulture),
                    KeyExisted = keyExisted,
                    DeleteKey = value.DeleteKeyOnRevert
                });
            }

            Write(value, value.On, log);
        }

        tweak.IsOnNow = ReadState(tweak);
    }

    /// <summary>Puts one tweak back to its documented default.</summary>
    public async Task RevertAsync(Tweak tweak, Action<string> log, CancellationToken ct)
    {
        if (tweak.RevertCommand is { Length: > 0 } command)
        {
            await RunAsync(command, log, ct);
            tweak.IsOnNow = ReadState(tweak);
            return;
        }

        foreach (var value in tweak.Values)
        {
            if (value.DeleteKeyOnRevert)
            {
                DeleteKey(value, log);
                continue;
            }

            if (value.Off is null) Delete(value, log);
            else Write(value, value.Off, log);
        }

        tweak.IsOnNow = ReadState(tweak);
    }

    /// <summary>Writes everything applied since the last call to an undo file.</summary>
    public string? FlushUndoFile(Action<string> log)
    {
        if (_pending.Count == 0) return null;

        try
        {
            Directory.CreateDirectory(UndoFolder);
            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            var path = Path.Combine(UndoFolder, $"undo-{stamp}.json");

            var file = new UndoFile
            {
                CreatedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                Entries = new List<UndoEntry>(_pending)
            };

            File.WriteAllText(path, JsonSerializer.Serialize(file, Json), Encoding.UTF8);
            _pending.Clear();
            LastUndoFile = path;
            log($"Undo information written to {path}");
            return path;
        }
        catch (Exception ex)
        {
            log($"!! could not write the undo file: {ex.Message}");
            return null;
        }
    }

    public IReadOnlyList<string> UndoFiles()
    {
        try
        {
            if (!Directory.Exists(UndoFolder)) return Array.Empty<string>();
            var files = Directory.GetFiles(UndoFolder, "undo-*.json");
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            Array.Reverse(files);
            return files;
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    /// <summary>Restores every value recorded in an undo file. Returns how many were put back.</summary>
    public async Task<int> RestoreAsync(string path, Action<string> log, CancellationToken ct)
    {
        var file = JsonSerializer.Deserialize<UndoFile>(File.ReadAllText(path))
                   ?? throw new InvalidOperationException("The undo file could not be read.");

        var restored = 0;

        // Newest change first, so overlapping writes unwind in the right order.
        for (var i = file.Entries.Count - 1; i >= 0; i--)
        {
            var entry = file.Entries[i];

            try
            {
                if (entry.RevertCommand is { Length: > 0 } command)
                {
                    await RunAsync(command, log, ct);
                    restored++;
                    continue;
                }

                if (entry.Key is null || entry.Name is null) continue;

                var kind = Enum.TryParse<RegistryValueKind>(entry.Kind, out var parsed)
                    ? parsed
                    : RegistryValueKind.DWord;

                var value = new RegValue { Key = entry.Key, Name = entry.Name, On = 0, Kind = kind };

                if (entry.DeleteKey && !entry.KeyExisted) DeleteKey(value, log);
                else if (entry.HadValue) Write(value, Coerce(entry.PreviousValue, kind), log);
                else Delete(value, log);

                restored++;
            }
            catch (Exception ex)
            {
                log($"!! {entry.Tweak}: {ex.Message}");
            }
        }

        return restored;
    }

    // ------------------------------------------------------------------ Explorer

    /// <summary>Restarts Explorer so taskbar and Explorer settings take effect.</summary>
    public static void RestartExplorer()
    {
        foreach (var process in Process.GetProcessesByName("explorer"))
        {
            try { process.Kill(); process.WaitForExit(4000); }
            catch { /* Explorer restarts itself; a failure here is not fatal */ }
            finally { process.Dispose(); }
        }

        // Windows normally relaunches it, but not when it was killed while a shell
        // extension held it, so make sure a shell is running.
        try
        {
            if (Process.GetProcessesByName("explorer").Length == 0)
                Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true });
        }
        catch { /* nothing sensible to do */ }
    }

    // ------------------------------------------------------------------ plumbing

    private static async Task RunAsync(string[] command, Action<string> log, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(command[0])
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        for (var i = 1; i < command.Length; i++) psi.ArgumentList.Add(command[i]);

        log($"   > {string.Join(' ', command)}");

        using var process = Process.Start(psi)
                            ?? throw new InvalidOperationException($"could not start {command[0]}");

        var stdout = process.StandardOutput.ReadToEndAsync(ct);
        var stderr = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);

        var output = ((await stdout) + (await stderr)).Trim();
        if (output.Length > 0)
            foreach (var line in output.Split('\n'))
                log("   " + line.TrimEnd());

        if (process.ExitCode != 0)
            throw new InvalidOperationException(
                process.ExitCode == 1 && !IsElevated()
                    ? "needs administrator rights"
                    : $"{command[0]} exited with {process.ExitCode}");
    }

    private static bool IsElevated()
    {
        try
        {
            using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            return new System.Security.Principal.WindowsPrincipal(identity)
                .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    private static (RegistryKey Root, string SubKey) Split(string key)
    {
        var cut = key.IndexOf('\\');
        if (cut < 0) throw new ArgumentException($"malformed registry key: {key}");

        var hive = key[..cut];
        var sub = key[(cut + 1)..];

        RegistryKey root = hive.ToUpperInvariant() switch
        {
            "HKEY_CURRENT_USER" or "HKCU" => Registry.CurrentUser,
            "HKEY_LOCAL_MACHINE" or "HKLM" => Registry.LocalMachine,
            "HKEY_CLASSES_ROOT" or "HKCR" => Registry.ClassesRoot,
            _ => throw new ArgumentException($"unsupported registry hive: {hive}")
        };

        return (root, sub);
    }

    private static object? ReadValue(RegValue value)
    {
        var (root, sub) = Split(value.Key);
        using var key = root.OpenSubKey(sub);
        return key?.GetValue(value.Name);
    }

    private static void Write(RegValue value, object data, Action<string> log)
    {
        var (root, sub) = Split(value.Key);

        try
        {
            using var key = root.CreateSubKey(sub, writable: true)
                            ?? throw new InvalidOperationException($"could not open {value.Key}");

            key.SetValue(value.Name, data, value.Kind);
            log($"   {Describe(value)} = {data}");
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or SecurityException)
        {
            throw new InvalidOperationException("needs administrator rights");
        }
    }

    private static void Delete(RegValue value, Action<string> log)
    {
        var (root, sub) = Split(value.Key);

        try
        {
            using var key = root.OpenSubKey(sub, writable: true);
            if (key is null) return;

            key.DeleteValue(value.Name, throwOnMissingValue: false);
            log($"   {Describe(value)} removed");
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or SecurityException)
        {
            throw new InvalidOperationException("needs administrator rights");
        }
    }

    private static void DeleteKey(RegValue value, Action<string> log)
    {
        var (root, sub) = Split(value.Key);

        try
        {
            root.DeleteSubKeyTree(sub, throwOnMissingSubKey: false);
            log($"   {value.Key} removed");
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or SecurityException)
        {
            throw new InvalidOperationException("needs administrator rights");
        }
    }

    private static string Describe(RegValue value)
        => value.Name.Length == 0 ? value.Key : $"{value.Key}\\{value.Name}";

    private static object Coerce(string? text, RegistryValueKind kind)
    {
        if (kind is RegistryValueKind.DWord or RegistryValueKind.QWord)
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number : 0;

        return text ?? "";
    }

    private static bool Same(object? actual, object? expected)
    {
        if (actual is null || expected is null) return false;

        return string.Equals(
            Convert.ToString(actual, CultureInfo.InvariantCulture),
            Convert.ToString(expected, CultureInfo.InvariantCulture),
            StringComparison.OrdinalIgnoreCase);
    }
}
