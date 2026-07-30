namespace FreshWin.Models;

/// <summary>A preinstalled Store app the user can choose to remove.</summary>
public sealed class BloatApp : QueueItem
{
    /// <summary>Appx package name, e.g. <c>Microsoft.BingNews</c>.</summary>
    public required string PackageName { get; init; }

    public required string Group { get; init; }

    /// <summary>Part of the "Recommended" preset on the Remove page.</summary>
    public bool Recommended { get; init; }

    /// <summary>Shown on the card when removing it has a real downside.</summary>
    public string? Caution { get; init; }

    public bool HasCaution => !string.IsNullOrEmpty(Caution);

    private bool? _isPresent;
    /// <summary>Whether the package is on this PC. Null while unknown.</summary>
    public bool? IsPresent
    {
        get => _isPresent;
        set
        {
            if (Set(ref _isPresent, value))
            {
                OnPropertyChanged(nameof(ShowAsPresent));
                OnPropertyChanged(nameof(ShowAsGone));
            }
        }
    }

    public bool ShowAsPresent => IsPresent == true;
    public bool ShowAsGone => IsPresent == false;

    public override string Subtitle => PackageName;

    public override string StatusText => Status switch
    {
        RunStatus.Pending => "Waiting",
        RunStatus.Working => "Removing…",
        RunStatus.Done => "Removed",
        RunStatus.AlreadyDone => "Not installed",
        RunStatus.Failed => string.IsNullOrEmpty(StatusDetail) ? "Failed" : $"Failed – {StatusDetail}",
        RunStatus.Skipped => "Skipped",
        _ => ""
    };

    public bool Matches(string query) =>
        string.IsNullOrWhiteSpace(query)
        || Name.Contains(query, StringComparison.OrdinalIgnoreCase)
        || Description.Contains(query, StringComparison.OrdinalIgnoreCase)
        || PackageName.Contains(query, StringComparison.OrdinalIgnoreCase);
}
