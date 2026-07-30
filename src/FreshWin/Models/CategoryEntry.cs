using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FreshWin.Models;

/// <summary>A row in the left-hand navigation.</summary>
public sealed class CategoryEntry : INotifyPropertyChanged
{
    public required string Name { get; init; }

    /// <summary>Stroke-only path geometry (24x24 box) used as the category icon.</summary>
    public required string Icon { get; init; }

    /// <summary>True for the synthetic "All apps" row.</summary>
    public bool IsAll { get; init; }

    public int Total { get; set; }

    private int _selectedCount;
    public int SelectedCount
    {
        get => _selectedCount;
        set
        {
            if (_selectedCount == value) return;
            _selectedCount = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(BadgeLabel));
        }
    }

    public bool HasSelection => SelectedCount > 0;

    /// <summary>The badge shows how many are ticked once anything is, otherwise the catalogue size.</summary>
    public string BadgeLabel => HasSelection ? SelectedCount.ToString() : Total.ToString();

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
