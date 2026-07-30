using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FreshWin.Models;

public enum RunStatus
{
    Pending,
    Working,
    Done,
    AlreadyDone,
    NeedsRestart,
    Failed,
    Skipped
}

/// <summary>
/// Anything the user can tick on a page and then have carried out on the run page —
/// a program to install or a Windows setting to change.
/// </summary>
public abstract class QueueItem : INotifyPropertyChanged
{
    public required string Name { get; init; }
    public required string Description { get; init; }

    /// <summary>Second line on the run page: the package id, or the setting being written.</summary>
    public abstract string Subtitle { get; }

    /// <summary>Wording differs per kind: "Installed" versus "Applied".</summary>
    public abstract string StatusText { get; }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => Set(ref _isSelected, value);
    }

    private RunStatus _status = RunStatus.Pending;
    public RunStatus Status
    {
        get => _status;
        set
        {
            if (!Set(ref _status, value)) return;
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(StatusKind));
        }
    }

    private string? _statusDetail;
    /// <summary>Extra context for a failure, e.g. an exit code or "needs administrator rights".</summary>
    public string? StatusDetail
    {
        get => _statusDetail;
        set
        {
            if (Set(ref _statusDetail, value)) OnPropertyChanged(nameof(StatusText));
        }
    }

    /// <summary>Drives the row colour: neutral / busy / good / warn / bad.</summary>
    public string StatusKind => Status switch
    {
        RunStatus.Working => "Busy",
        RunStatus.Done or RunStatus.AlreadyDone => "Good",
        RunStatus.NeedsRestart => "Warn",
        RunStatus.Failed => "Bad",
        _ => "Neutral"
    };

    /// <summary>Two-letter badge, so the app ships without needing logo files.</summary>
    public string Initials
    {
        get
        {
            var parts = Name.Split(new[] { ' ', '-', '.', '+', '&' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return "?";
            if (parts.Length == 1) return parts[0].Length > 1 ? parts[0][..2].ToUpperInvariant() : parts[0].ToUpperInvariant();
            return string.Concat(char.ToUpperInvariant(parts[0][0]), char.ToUpperInvariant(parts[1][0]));
        }
    }

    /// <summary>Stable per-item colour derived from the name, so the grid looks designed.</summary>
    public string Accent
    {
        get
        {
            string[] palette =
            {
                "#4C7DF0", "#7C5CF0", "#E0559A", "#EF6C4D", "#F2B94B",
                "#3DD68C", "#2FB6C4", "#5C7CFA", "#B45CF0", "#E8574A"
            };

            var hash = 17;
            foreach (var c in Name) hash = unchecked(hash * 31 + c);

            // Mask rather than Math.Abs: Math.Abs(int.MinValue) throws.
            return palette[(hash & int.MaxValue) % palette.Length];
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }
}
