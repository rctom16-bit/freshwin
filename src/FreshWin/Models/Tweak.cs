using Microsoft.Win32;

namespace FreshWin.Models;

public enum RestartNeed
{
    None,
    Explorer,
    SignOut,
    Reboot
}

/// <summary>A single registry value a tweak writes, plus what to put back on revert.</summary>
public sealed class RegValue
{
    /// <summary>Full key path, e.g. <c>HKEY_CURRENT_USER\Software\...</c>.</summary>
    public required string Key { get; init; }

    /// <summary>Value name; an empty string means the key's default value.</summary>
    public required string Name { get; init; }

    /// <summary>Value written when the tweak is applied.</summary>
    public required object On { get; init; }

    /// <summary>Value written on revert. Null means "remove the value again".</summary>
    public object? Off { get; init; }

    public RegistryValueKind Kind { get; init; } = RegistryValueKind.DWord;

    /// <summary>For tweaks whose whole point is the presence of a key: delete it on revert.</summary>
    public bool DeleteKeyOnRevert { get; init; }
}

/// <summary>One Windows setting the user can switch on from the Tune page.</summary>
public sealed class Tweak : QueueItem
{
    public required string Group { get; init; }

    /// <summary>Part of the "Recommended" preset on the Tune page.</summary>
    public bool Recommended { get; init; }

    public bool RequiresAdmin { get; init; }

    public RestartNeed Restart { get; init; } = RestartNeed.None;

    public IReadOnlyList<RegValue> Values { get; init; } = Array.Empty<RegValue>();

    /// <summary>Set for tweaks that cannot be done by writing the registry directly.</summary>
    public string[]? ApplyCommand { get; init; }
    public string[]? RevertCommand { get; init; }

    /// <summary>Where a command's effect can be read back, so the current state is still known.</summary>
    public RegValue? Detect { get; init; }

    /// <summary>Only shown on Windows 11; harmless but pointless on Windows 10.</summary>
    public bool Windows11Only { get; init; }

    private bool? _isOnNow;
    /// <summary>Current state read from the system. Null while unknown.</summary>
    public bool? IsOnNow
    {
        get => _isOnNow;
        set
        {
            if (Set(ref _isOnNow, value)) OnPropertyChanged(nameof(ShowAsOn));
        }
    }

    /// <summary>Plain bool for the "already on" pill, so XAML never triggers off a nullable.</summary>
    public bool ShowAsOn => IsOnNow == true;

    public override string Subtitle => ApplyCommand is { Length: > 0 }
        ? string.Join(' ', ApplyCommand)
        : Values.Count > 0
            ? ShortKey(Values[0])
            : Group;

    public override string StatusText => Status switch
    {
        RunStatus.Pending => "Waiting",
        RunStatus.Working => "Applying…",
        RunStatus.Done => "Applied",
        RunStatus.AlreadyDone => "Already set",
        RunStatus.NeedsRestart => Restart == RestartNeed.Explorer
            ? "Applied – restart Explorer"
            : Restart == RestartNeed.Reboot
                ? "Applied – restart Windows"
                : "Applied – sign out to finish",
        RunStatus.Failed => string.IsNullOrEmpty(StatusDetail) ? "Failed" : $"Failed – {StatusDetail}",
        RunStatus.Skipped => "Skipped",
        _ => ""
    };

    public bool Matches(string query) =>
        string.IsNullOrWhiteSpace(query)
        || Name.Contains(query, StringComparison.OrdinalIgnoreCase)
        || Description.Contains(query, StringComparison.OrdinalIgnoreCase)
        || Group.Contains(query, StringComparison.OrdinalIgnoreCase);

    /// <summary>Keeps the hive plus the last two path segments, so the row stays readable.</summary>
    private static string ShortKey(RegValue value)
    {
        var parts = value.Key.Split('\\', StringSplitOptions.RemoveEmptyEntries);
        var hive = parts.Length > 0 ? parts[0].Replace("HKEY_CURRENT_USER", "HKCU").Replace("HKEY_LOCAL_MACHINE", "HKLM")
                                              .Replace("HKEY_CLASSES_ROOT", "HKCR") : "";
        var tail = parts.Length > 2 ? string.Join('\\', parts[^2..]) : string.Join('\\', parts.Skip(1));
        var name = value.Name.Length == 0 ? "(default)" : value.Name;
        return parts.Length > 3 ? $@"{hive}\…\{tail}\{name}" : $@"{hive}\{tail}\{name}";
    }
}
