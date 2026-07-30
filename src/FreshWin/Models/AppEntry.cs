namespace FreshWin.Models;

/// <summary>One installable program from the catalogue.</summary>
public sealed class AppEntry : QueueItem
{
    public required string Id { get; init; }
    public required string Publisher { get; init; }
    public required string Category { get; init; }

    /// <summary>Part of the "Essentials" one-click preset.</summary>
    public bool Essential { get; init; }

    /// <summary>winget source, e.g. "winget" or "msstore".</summary>
    public string Source { get; init; } = "winget";

    public bool IsFromStore => Source == "msstore";

    public override string Subtitle => Id;

    public override string StatusText => Status switch
    {
        RunStatus.Pending => "Waiting",
        RunStatus.Working => "Installing…",
        RunStatus.Done => "Installed",
        RunStatus.AlreadyDone => "Already up to date",
        RunStatus.NeedsRestart => "Installed – restart needed",
        RunStatus.Failed => string.IsNullOrEmpty(StatusDetail) ? "Failed" : $"Failed – {StatusDetail}",
        RunStatus.Skipped => "Skipped",
        _ => ""
    };

    public bool Matches(string query) =>
        string.IsNullOrWhiteSpace(query)
        || Name.Contains(query, StringComparison.OrdinalIgnoreCase)
        || Publisher.Contains(query, StringComparison.OrdinalIgnoreCase)
        || Description.Contains(query, StringComparison.OrdinalIgnoreCase)
        || Id.Contains(query, StringComparison.OrdinalIgnoreCase);
}
