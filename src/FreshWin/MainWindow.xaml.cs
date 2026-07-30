using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Principal;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Win32;
using FreshWin.Models;
using FreshWin.Services;

namespace FreshWin;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private const string AppInstallerStoreLink = "ms-windows-store://pdp/?productid=9NBLGGH4NNS1";

    private enum BannerActionKind { None, RestartElevated, OpenStore }

    private enum Pane { Software, Tweaks, Remove }

    private readonly List<AppEntry> _apps = Catalog.Build();
    private readonly List<Tweak> _tweaks = Tweaks.Build();
    private readonly List<BloatApp> _bloat = Bloatware.Build();
    private readonly WingetService _winget = new();
    private readonly TweakEngine _engine = new();

    private readonly CollectionViewSource _appView;
    private readonly CollectionViewSource _tweakView;
    private readonly CollectionViewSource _bloatView;

    private Pane _pane = Pane.Software;
    private string _search = "";
    private CategoryEntry? _category;
    private CategoryEntry? _group;
    private CategoryEntry? _bloatGroup;
    private BannerActionKind _bannerAction = BannerActionKind.None;
    private bool _isRunning;
    private bool _stopRequested;
    private bool _syncingNav;
    private bool _ready;

    public MainWindow()
    {
        InitializeComponent();

        _appView = new CollectionViewSource { Source = _apps };
        _appView.Filter += (_, e) => e.Accepted = e.Item is AppEntry app && ShowApp(app);

        _tweakView = new CollectionViewSource { Source = _tweaks };
        _tweakView.Filter += (_, e) => e.Accepted = e.Item is Tweak tweak && ShowTweak(tweak);

        _bloatView = new CollectionViewSource { Source = _bloat };
        _bloatView.Filter += (_, e) => e.Accepted = e.Item is BloatApp bloat && ShowBloat(bloat);

        foreach (var app in _apps) app.PropertyChanged += OnItemPropertyChanged;
        foreach (var tweak in _tweaks) tweak.PropertyChanged += OnItemPropertyChanged;
        foreach (var bloat in _bloat) bloat.PropertyChanged += OnItemPropertyChanged;

        BuildNavigation();
        _ready = true;

        // Only once the views and nav rows exist is it safe to let the bindings resolve.
        DataContext = this;

        CategoryList.SelectedIndex = 0;
        RefreshSelection();

        Loaded += OnLoaded;
    }

    // ------------------------------------------------------------------- state

    public ObservableCollection<CategoryEntry> Categories { get; } = new();
    public ObservableCollection<CategoryEntry> Groups { get; } = new();
    public ObservableCollection<CategoryEntry> BloatGroups { get; } = new();
    public ObservableCollection<QueueItem> RunQueue { get; } = new();

    public ICollectionView VisibleApps => _appView.View;
    public ICollectionView VisibleTweaks => _tweakView.View;
    public ICollectionView VisibleBloat => _bloatView.View;

    private string _paneTitle = "Set up this PC";
    public string PaneTitle
    {
        get => _paneTitle;
        private set => Set(ref _paneTitle, value);
    }

    private string _paneSubtitle = "Tick everything you want, then hit Install – the rest is automatic.";
    public string PaneSubtitle
    {
        get => _paneSubtitle;
        private set => Set(ref _paneSubtitle, value);
    }

    private string _selectionHeadline = "Nothing selected yet";
    public string SelectionHeadline
    {
        get => _selectionHeadline;
        private set => Set(ref _selectionHeadline, value);
    }

    private string _selectionDetail = "Pick the apps you want, or start with Essentials.";
    public string SelectionDetail
    {
        get => _selectionDetail;
        private set => Set(ref _selectionDetail, value);
    }

    private string _runButtonText = "Install selected";
    public string RunButtonText
    {
        get => _runButtonText;
        private set => Set(ref _runButtonText, value);
    }

    private bool _hasSelection;
    public bool HasSelection
    {
        get => _hasSelection;
        private set => Set(ref _hasSelection, value);
    }

    private double _progress;
    public double Progress
    {
        get => _progress;
        private set => Set(ref _progress, value);
    }

    private string _progressText = "0 / 0";
    public string ProgressText
    {
        get => _progressText;
        private set => Set(ref _progressText, value);
    }

    // ---------------------------------------------------------------- start-up

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        TweakEngine.RefreshState(_tweaks);
        _ = AppxService.RefreshStateAsync(_bloat, CancellationToken.None);
        UndoButton.IsEnabled = _engine.UndoFiles().Count > 0;
        TitleBarHint.Text = IsElevated() ? "administrator" : "";

        await _winget.ProbeAsync();

        EngineStatus.Text = _winget.IsAvailable
            ? $"winget {_winget.Version} ready"
            : "winget was not found";

        if (!_winget.IsAvailable)
        {
            ShowBanner(
                "Windows Package Manager (winget) is missing, so no software can be installed yet. " +
                "Install \"App Installer\" from the Microsoft Store and restart FreshWin. " +
                "The Windows settings on the Tune pages still work.",
                "Get App Installer",
                BannerActionKind.OpenStore);
        }
        else if (!IsElevated())
        {
            ShowBanner(
                "FreshWin is not running as administrator. Most installers, and the settings marked \"admin\", need admin rights.",
                "Restart as admin",
                BannerActionKind.RestartElevated);
        }

        if (_winget.IsAvailable) await ScanInstalledAsync();

        SearchInput.Focus();
    }

    private static bool IsElevated()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    private void ShowBanner(string message, string? actionText, BannerActionKind action)
    {
        BannerText.Text = message;
        _bannerAction = action;
        Banner.Visibility = Visibility.Visible;

        if (actionText is null)
        {
            BannerAction.Visibility = Visibility.Collapsed;
            return;
        }

        BannerAction.Content = actionText;
        BannerAction.Visibility = Visibility.Visible;
    }

    private void BannerAction_Click(object sender, RoutedEventArgs e)
    {
        switch (_bannerAction)
        {
            case BannerActionKind.OpenStore:
                OpenLink(AppInstallerStoreLink);
                break;

            case BannerActionKind.RestartElevated:
                RestartElevated();
                break;
        }
    }

    private static void OpenLink(string uri)
    {
        try
        {
            Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not open {uri}\n\n{ex.Message}", "FreshWin");
        }
    }

    private void RestartElevated()
    {
        var exe = Environment.ProcessPath;
        if (exe is null) return;

        try
        {
            Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true, Verb = "runas" });
            Application.Current.Shutdown();
        }
        catch (Exception)
        {
            // The user dismissed the UAC prompt – stay open, unelevated.
        }
    }

    // ------------------------------------------------------------- navigation

    private void BuildNavigation()
    {
        Categories.Add(new CategoryEntry
        {
            Name = "All apps",
            IsAll = true,
            Icon = "M4 4h6v6H4z M14 4h6v6h-6z M4 14h6v6H4z M14 14h6v6h-6z",
            Total = _apps.Count
        });

        foreach (var (name, icon) in Catalog.CategoryOrder)
        {
            Categories.Add(new CategoryEntry
            {
                Name = name,
                Icon = icon,
                Total = _apps.Count(a => a.Category == name)
            });
        }

        Groups.Add(new CategoryEntry
        {
            Name = "All settings",
            IsAll = true,
            Icon = "M4 7h16 M4 12h16 M4 17h16 M9 5.4v3.2 M15 10.4v3.2 M7 15.4v3.2",
            Total = _tweaks.Count
        });

        foreach (var (name, icon) in Tweaks.GroupOrder)
        {
            Groups.Add(new CategoryEntry
            {
                Name = name,
                Icon = icon,
                Total = _tweaks.Count(t => t.Group == name)
            });
        }

        BloatGroups.Add(new CategoryEntry
        {
            Name = "All preinstalled",
            IsAll = true,
            Icon = "M5 7h14 M9 7V5h6v2 M7 7l1 13h8l1-13 M10.5 10.5v6 M13.5 10.5v6",
            Total = _bloat.Count
        });

        foreach (var (name, icon) in Bloatware.GroupOrder)
        {
            BloatGroups.Add(new CategoryEntry
            {
                Name = name,
                Icon = icon,
                Total = _bloat.Count(b => b.Group == name)
            });
        }
    }

    private void CategoryList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingNav || CategoryList.SelectedItem is null) return;

        _category = CategoryList.SelectedItem as CategoryEntry;
        SwitchPane(Pane.Software);
    }

    private void GroupList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingNav || GroupList.SelectedItem is null) return;

        _group = GroupList.SelectedItem as CategoryEntry;
        SwitchPane(Pane.Tweaks);
    }

    private void BloatList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingNav || BloatList.SelectedItem is null) return;

        _bloatGroup = BloatList.SelectedItem as CategoryEntry;
        SwitchPane(Pane.Remove);
    }

    private void SwitchPane(Pane pane)
    {
        _pane = pane;

        _syncingNav = true;
        if (pane != Pane.Software) CategoryList.SelectedItem = null;
        if (pane != Pane.Tweaks) GroupList.SelectedItem = null;
        if (pane != Pane.Remove) BloatList.SelectedItem = null;
        _syncingNav = false;

        AppScroller.Visibility = Visible(pane == Pane.Software);
        TweakScroller.Visibility = Visible(pane == Pane.Tweaks);
        BloatScroller.Visibility = Visible(pane == Pane.Remove);

        SoftwareTools.Visibility = Visible(pane == Pane.Software);
        TweakTools.Visibility = Visible(pane == Pane.Tweaks);
        BloatTools.Visibility = Visible(pane == Pane.Remove);

        (PaneTitle, PaneSubtitle) = pane switch
        {
            Pane.Software => ("Set up this PC",
                "Tick everything you want, then hit Install – the rest is automatic."),
            Pane.Tweaks => ("Tune Windows",
                "Only documented, reversible settings. Every change is recorded so it can be undone."),
            _ => ("Remove preinstalled apps",
                "A short, named list – not a debloat script. Everything here can be reinstalled from the Store.")
        };

        RefreshView();
    }

    private static Visibility Visible(bool show) => show ? Visibility.Visible : Visibility.Collapsed;

    // ------------------------------------------------------------- filtering

    private bool ShowApp(AppEntry app)
    {
        if (!string.IsNullOrWhiteSpace(_search)) return app.Matches(_search);
        return _category is null || _category.IsAll || app.Category == _category.Name;
    }

    private bool ShowTweak(Tweak tweak)
    {
        if (!string.IsNullOrWhiteSpace(_search)) return tweak.Matches(_search);
        return _group is null || _group.IsAll || tweak.Group == _group.Name;
    }

    private bool ShowBloat(BloatApp bloat)
    {
        if (!string.IsNullOrWhiteSpace(_search)) return bloat.Matches(_search);
        return _bloatGroup is null || _bloatGroup.IsAll || bloat.Group == _bloatGroup.Name;
    }

    private void RefreshView()
    {
        if (!_ready) return;

        _appView.View.Refresh();
        _tweakView.View.Refresh();
        _bloatView.View.Refresh();

        EmptyHint.Visibility = Visible(_appView.View.IsEmpty);
        EmptyTweakHint.Visibility = Visible(_tweakView.View.IsEmpty);
        EmptyBloatHint.Visibility = Visible(_bloatView.View.IsEmpty);
    }

    private void SearchInput_TextChanged(object sender, TextChangedEventArgs e)
    {
        _search = SearchInput.Text.Trim();
        RefreshView();
    }

    // ------------------------------------------------------------- selection

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(QueueItem.IsSelected)) RefreshSelection();
    }

    private void RefreshSelection()
    {
        var apps = _apps.Where(a => a.IsSelected).ToList();
        var tweaks = _tweaks.Where(t => t.IsSelected).ToList();
        var bloat = _bloat.Where(b => b.IsSelected).ToList();

        foreach (var group in BloatGroups)
        {
            group.SelectedCount = group.IsAll
                ? bloat.Count
                : bloat.Count(b => b.Group == group.Name);
        }

        foreach (var category in Categories)
        {
            category.SelectedCount = category.IsAll
                ? apps.Count
                : apps.Count(a => a.Category == category.Name);
        }

        foreach (var group in Groups)
        {
            group.SelectedCount = group.IsAll
                ? tweaks.Count
                : tweaks.Count(t => t.Group == group.Name);
        }

        HasSelection = apps.Count + tweaks.Count + bloat.Count > 0;

        RunButtonText = apps.Count == 0 && (tweaks.Count > 0 || bloat.Count > 0)
            ? "Apply selected"
            : "Install selected";

        if (!HasSelection)
        {
            SelectionHeadline = "Nothing selected yet";
            SelectionDetail = _pane switch
            {
                Pane.Software => "Pick the apps you want, or start with Essentials.",
                Pane.Tweaks => "Pick the settings you want, or start with Recommended.",
                _ => "Pick what you want gone, or start with Recommended."
            };
            return;
        }

        var parts = new List<string>();
        if (apps.Count > 0) parts.Add(apps.Count == 1 ? "1 app" : $"{apps.Count} apps");
        if (tweaks.Count > 0) parts.Add(tweaks.Count == 1 ? "1 setting" : $"{tweaks.Count} settings");
        if (bloat.Count > 0) parts.Add(bloat.Count == 1 ? "1 removal" : $"{bloat.Count} removals");
        SelectionHeadline = string.Join(" + ", parts) + " selected";

        var names = apps.Select(a => a.Name)
            .Concat(tweaks.Select(t => t.Name))
            .Concat(bloat.Select(b => b.Name))
            .ToList();
        var rest = names.Count - 4;
        SelectionDetail = rest > 0
            ? $"{string.Join(", ", names.Take(4))} + {rest} more"
            : string.Join(", ", names);
    }

    private void Essentials_Click(object sender, RoutedEventArgs e)
    {
        foreach (var app in _apps) app.IsSelected = app.Essential;
    }

    private void RecommendedTweaks_Click(object sender, RoutedEventArgs e)
    {
        foreach (var tweak in _tweaks) tweak.IsSelected = tweak.Recommended;
    }

    private void RecommendedBloat_Click(object sender, RoutedEventArgs e)
    {
        // Never tick something the scan says is not on this PC.
        foreach (var bloat in _bloat)
            bloat.IsSelected = bloat.Recommended && bloat.IsPresent != false;
    }

    private void SelectAllShown_Click(object sender, RoutedEventArgs e)
    {
        switch (_pane)
        {
            case Pane.Software:
                foreach (var app in _appView.View.OfType<AppEntry>().ToList()) app.IsSelected = true;
                break;
            case Pane.Tweaks:
                foreach (var tweak in _tweakView.View.OfType<Tweak>().ToList()) tweak.IsSelected = true;
                break;
            default:
                foreach (var bloat in _bloatView.View.OfType<BloatApp>().ToList())
                    if (bloat.IsPresent != false) bloat.IsSelected = true;
                break;
        }
    }

    private void ClearPane_Click(object sender, RoutedEventArgs e)
    {
        switch (_pane)
        {
            case Pane.Software:
                foreach (var app in _apps) app.IsSelected = false;
                break;
            case Pane.Tweaks:
                foreach (var tweak in _tweaks) tweak.IsSelected = false;
                break;
            default:
                foreach (var bloat in _bloat) bloat.IsSelected = false;
                break;
        }
    }

    // ------------------------------------------------------------------- run

    private async void Next_Click(object sender, RoutedEventArgs e)
    {
        if (_isRunning) return;

        var apps = _apps.Where(a => a.IsSelected).ToList();
        var tweaks = _tweaks.Where(t => t.IsSelected).ToList();
        var bloat = _bloat.Where(b => b.IsSelected).ToList();
        if (apps.Count + tweaks.Count + bloat.Count == 0) return;

        if (apps.Count > 0 && !_winget.IsAvailable)
        {
            MessageBox.Show(
                "winget is not available on this PC, so software cannot be installed.\n\n" +
                "Install \"App Installer\" from the Microsoft Store, or clear the selected apps and " +
                "apply only the Windows settings.",
                "FreshWin", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        RunQueue.Clear();

        // A restore point is only worth anything if it is taken before the first change.
        // Removals run before installs so a removed app cannot fight a fresh one.
        var ordered = tweaks.Where(t => t.RunFirst).Cast<QueueItem>()
            .Concat(bloat)
            .Concat(apps)
            .Concat(tweaks.Where(t => !t.RunFirst));

        foreach (var item in ordered)
        {
            item.Status = RunStatus.Pending;
            item.StatusDetail = null;
            RunQueue.Add(item);
        }

        PickPage.Visibility = Visibility.Collapsed;
        RunPage.Visibility = Visibility.Visible;

        LogBox.Clear();
        AppendLog($"FreshWin – {apps.Count} app(s), {tweaks.Count} setting(s), {bloat.Count} removal(s)");
        if (apps.Count > 0) AppendLog($"winget {_winget.Version}");
        AppendLog(IsElevated() ? "Running elevated." : "Running WITHOUT admin rights – some steps may fail.");
        AppendLog("");

        await RunAsync();
    }

    private async Task RunAsync()
    {
        _isRunning = true;
        _stopRequested = false;

        StopButton.Visibility = Visibility.Visible;
        StopButton.IsEnabled = true;
        StopButton.Content = "Stop after current";
        BackButton.Visibility = Visibility.Collapsed;
        FinishButton.Visibility = Visibility.Collapsed;
        RestartExplorerButton.Visibility = Visibility.Collapsed;
        RunHeadline.Text = "Working…";

        var total = RunQueue.Count;
        var done = 0;
        int installed = 0, applied = 0, removed = 0, already = 0, failed = 0, skipped = 0, restart = 0;
        var needsExplorer = false;

        SetProgress(0, total);

        foreach (var item in RunQueue)
        {
            if (_stopRequested)
            {
                item.Status = RunStatus.Skipped;
                skipped++;
                SetProgress(++done, total);
                continue;
            }

            item.Status = RunStatus.Working;
            RunSubtitle.Text = $"{item.Name} – {done + 1} of {total}";
            AppendLog($"── {item.Name}  [{item.Subtitle}]");

            try
            {
                switch (item)
                {
                    case AppEntry app:
                    {
                        var exitCode = await _winget.InstallAsync(app, AppendLog, CancellationToken.None);
                        var (status, detail) = WingetService.Interpret(exitCode);
                        app.StatusDetail = detail;
                        app.Status = status;

                        if (status == RunStatus.Done) installed++;
                        else if (status == RunStatus.AlreadyDone) already++;
                        else if (status == RunStatus.NeedsRestart) { installed++; restart++; }
                        else failed++;
                        break;
                    }

                    case BloatApp bloat:
                    {
                        if (bloat.IsPresent == false)
                        {
                            bloat.Status = RunStatus.AlreadyDone;
                            already++;
                            break;
                        }

                        await AppxService.RemoveAsync(bloat, AppendLog, CancellationToken.None);
                        bloat.IsPresent = false;
                        bloat.Status = RunStatus.Done;
                        removed++;
                        break;
                    }

                    case Tweak tweak:
                    {
                        if (TweakEngine.ReadState(tweak) == true)
                        {
                            tweak.Status = RunStatus.AlreadyDone;
                            already++;
                            break;
                        }

                        await _engine.ApplyAsync(tweak, AppendLog, CancellationToken.None);

                        if (tweak.Restart == RestartNeed.None)
                        {
                            tweak.Status = RunStatus.Done;
                        }
                        else
                        {
                            tweak.Status = RunStatus.NeedsRestart;
                            if (tweak.Restart == RestartNeed.Explorer) needsExplorer = true;
                            else restart++;
                        }

                        applied++;
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                item.Status = RunStatus.Failed;
                item.StatusDetail = ex is InvalidOperationException ? ex.Message : "could not be carried out";
                AppendLog($"!! {ex.Message}");
                failed++;
            }

            AppendLog($"   → {item.StatusText}");
            AppendLog("");
            SetProgress(++done, total);
        }

        _engine.FlushUndoFile(AppendLog);
        UndoButton.IsEnabled = _engine.UndoFiles().Count > 0;

        _isRunning = false;
        Finish(installed, applied, removed, already, failed, skipped, restart, needsExplorer);
    }

    private void Finish(int installed, int applied, int removed, int already, int failed, int skipped, int restart, bool needsExplorer)
    {
        StopButton.Visibility = Visibility.Collapsed;
        BackButton.Visibility = Visibility.Visible;
        FinishButton.Visibility = Visibility.Visible;
        RestartExplorerButton.Visibility = needsExplorer ? Visibility.Visible : Visibility.Collapsed;

        RunHeadline.Text = failed > 0 ? "Finished, with some failures" : "All done";

        var parts = new List<string>();
        if (installed > 0) parts.Add($"{installed} installed");
        if (applied > 0) parts.Add($"{applied} applied");
        if (removed > 0) parts.Add($"{removed} removed");
        if (already > 0) parts.Add($"{already} already set");
        if (skipped > 0) parts.Add($"{skipped} skipped");
        if (failed > 0) parts.Add($"{failed} failed");
        if (parts.Count == 0) parts.Add("nothing to do");

        SummaryText.Text = string.Join("  ·  ", parts);

        RunSubtitle.Text = failed > 0
            ? "Open \"Show details\" to see exactly what was reported for the failures."
            : needsExplorer
                ? "Restart Explorer to make the Explorer and taskbar changes appear."
                : restart > 0
                    ? "Restart Windows when convenient to finish the remaining changes."
                    : "Everything you selected is done.";

        if (failed > 0) ShowLog(true);

        AppendLog($"Done – {SummaryText.Text}");
    }

    private void SetProgress(int done, int total)
    {
        Progress = total == 0 ? 0 : done * 100.0 / total;
        ProgressText = $"{done} / {total}";
    }

    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        _stopRequested = true;
        StopButton.IsEnabled = false;
        StopButton.Content = "Stopping…";
        AppendLog("Stop requested – finishing the current item, then stopping.");
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in RunQueue)
        {
            item.Status = RunStatus.Pending;
            item.StatusDetail = null;
        }

        TweakEngine.RefreshState(_tweaks);
        _ = AppxService.RefreshStateAsync(_bloat, CancellationToken.None);

        RunPage.Visibility = Visibility.Collapsed;
        PickPage.Visibility = Visibility.Visible;

        RunHeadline.Text = "Working…";
        SummaryText.Text = "";
        ShowLog(false);
    }

    private void RestartExplorer_Click(object sender, RoutedEventArgs e)
    {
        RestartExplorerButton.IsEnabled = false;
        TweakEngine.RestartExplorer();
        AppendLog("Explorer restarted.");
    }

    // ---------------------------------------------------------------- profiles

    /// <summary>Marks catalogue entries that are already on this PC.</summary>
    private async Task ScanInstalledAsync()
    {
        var ids = await _winget.ExportInstalledAsync(CancellationToken.None);
        if (ids.Count == 0) return;

        var installed = new HashSet<string>(ids, StringComparer.OrdinalIgnoreCase);
        foreach (var app in _apps) app.IsPresent = installed.Contains(app.Id);

        _installedIds = installed;
    }

    private HashSet<string> _installedIds = new(StringComparer.OrdinalIgnoreCase);

    private SetupProfile BuildProfile(IEnumerable<string> ids)
    {
        var profile = new SetupProfile
        {
            Install = ids.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList(),
            Settings = _tweaks.Where(t => t.IsSelected).Select(t => t.Name).ToList(),
            Remove = _bloat.Where(b => b.IsSelected).Select(b => b.PackageName).ToList()
        };

        // Ids the built-in catalogue does not know need a readable label of their own.
        var known = _apps.Select(a => a.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var id in profile.Install.Where(id => !known.Contains(id)))
            profile.Names[id] = PrettyName(id);

        return profile;
    }

    private static string PrettyName(string id)
    {
        var cut = id.LastIndexOf('.');
        return cut > 0 && cut < id.Length - 1 ? id[(cut + 1)..] : id;
    }

    private async void CloneThisPc_Click(object sender, RoutedEventArgs e)
    {
        if (_isRunning) return;

        if (!_winget.IsAvailable)
        {
            MessageBox.Show("winget is not available, so this PC cannot be scanned.", "FreshWin");
            return;
        }

        ScanButton.IsEnabled = false;
        ScanButton.Content = "Scanning…";

        try
        {
            await ScanInstalledAsync();

            if (_installedIds.Count == 0)
            {
                MessageBox.Show("winget did not report any installed packages it recognises.", "FreshWin");
                return;
            }

            var profile = BuildProfile(_installedIds);
            var inCatalogue = profile.Install.Count - profile.Names.Count;

            var dialog = new SaveFileDialog
            {
                Title = "Save this PC as a profile",
                FileName = $"{Environment.MachineName.ToLowerInvariant()}.freshwin.json",
                Filter = "FreshWin profile (*.json)|*.json|All files (*.*)|*.*",
                DefaultExt = ".json"
            };

            if (dialog.ShowDialog(this) != true) return;

            ProfileService.Save(dialog.FileName, profile);

            MessageBox.Show(
                $"{profile.Install.Count} installed program(s) written to the profile " +
                $"({inCatalogue} of them in the built-in catalogue).\n\n" +
                "Take the file to the new PC and use \"Load profile\" there.",
                "FreshWin");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"This PC could not be scanned.\n\n{ex.Message}", "FreshWin",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            ScanButton.IsEnabled = true;
            ScanButton.Content = "Clone this PC";
        }
    }

    private void SaveProfile_Click(object sender, RoutedEventArgs e)
    {
        var selected = _apps.Where(a => a.IsSelected).Select(a => a.Id).ToList();
        if (selected.Count == 0 && !_tweaks.Any(t => t.IsSelected) && !_bloat.Any(b => b.IsSelected))
        {
            MessageBox.Show("Nothing is selected yet, so there is nothing to save.", "FreshWin");
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Save the current selection",
            FileName = "my-setup.freshwin.json",
            Filter = "FreshWin profile (*.json)|*.json|All files (*.*)|*.*",
            DefaultExt = ".json"
        };

        if (dialog.ShowDialog(this) != true) return;

        try
        {
            ProfileService.Save(dialog.FileName, BuildProfile(selected));
        }
        catch (Exception ex)
        {
            MessageBox.Show($"The profile could not be saved.\n\n{ex.Message}", "FreshWin",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void LoadProfile_Click(object sender, RoutedEventArgs e)
    {
        if (_isRunning) return;

        var dialog = new OpenFileDialog
        {
            Title = "Load a profile",
            Filter = "FreshWin profile (*.json)|*.json|All files (*.*)|*.*"
        };

        if (dialog.ShowDialog(this) != true) return;

        SetupProfile profile;
        try
        {
            profile = ProfileService.Load(dialog.FileName);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"That file could not be read as a profile.\n\n{ex.Message}", "FreshWin",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        ApplyProfile(profile);
    }

    private void ApplyProfile(SetupProfile profile)
    {
        foreach (var app in _apps) app.IsSelected = false;
        foreach (var tweak in _tweaks) tweak.IsSelected = false;
        foreach (var bloat in _bloat) bloat.IsSelected = false;

        var byId = _apps.ToDictionary(a => a.Id, StringComparer.OrdinalIgnoreCase);
        var added = 0;

        foreach (var id in profile.Install)
        {
            if (byId.TryGetValue(id, out var known))
            {
                known.IsSelected = true;
                continue;
            }

            // Not in the catalogue: keep it, in its own category, so it still gets installed.
            var entry = new AppEntry
            {
                Id = id,
                Name = profile.Names.TryGetValue(id, out var label) ? label : PrettyName(id),
                Publisher = "from profile",
                Description = "Added from a profile. Not part of the built-in catalogue.",
                Category = FromProfileCategory,
                FromProfile = true,
                IsSelected = true
            };

            entry.PropertyChanged += OnItemPropertyChanged;
            _apps.Add(entry);
            byId[id] = entry;
            added++;
        }

        foreach (var tweak in _tweaks)
            if (profile.Settings.Contains(tweak.Name)) tweak.IsSelected = true;

        foreach (var bloat in _bloat)
            if (profile.Remove.Contains(bloat.PackageName)) bloat.IsSelected = true;

        if (added > 0) EnsureProfileCategory(added);

        RefreshView();
        RefreshSelection();

        var summary = $"{profile.Install.Count} program(s), {profile.Settings.Count} setting(s) " +
                      $"and {profile.Remove.Count} removal(s) selected.";
        if (added > 0) summary += $"\n\n{added} of them are not in the built-in catalogue and were " +
                                  "added under \"From profile\".";

        MessageBox.Show(summary, "FreshWin");
    }

    private const string FromProfileCategory = "From profile";

    private void EnsureProfileCategory(int added)
    {
        var row = Categories.FirstOrDefault(c => c.Name == FromProfileCategory);

        if (row is null)
        {
            Categories.Add(new CategoryEntry
            {
                Name = FromProfileCategory,
                Icon = "M7 3h7l5 5v13H7z M14 3v5h5 M10 13h6 M10 17h4",
                Total = added
            });
        }
        else
        {
            row.Total = _apps.Count(a => a.Category == FromProfileCategory);
        }

        Categories[0].Total = _apps.Count;
    }

    // ---------------------------------------------------------------- reverting

    private async void RevertTweak_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;

        if (_isRunning) return;
        if (sender is not Button { Tag: Tweak tweak }) return;

        try
        {
            await _engine.RevertAsync(tweak, _ => { }, CancellationToken.None);

            if (tweak.Restart == RestartNeed.Explorer &&
                MessageBox.Show(
                    $"\"{tweak.Name}\" was set back.\n\nRestart Explorer now so the change shows up?",
                    "FreshWin", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                TweakEngine.RestartExplorer();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"\"{tweak.Name}\" could not be set back.\n\n{ex.Message}", "FreshWin",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void UndoTweaks_Click(object sender, RoutedEventArgs e)
    {
        if (_isRunning) return;

        var newest = _engine.UndoFiles().FirstOrDefault();
        if (newest is null)
        {
            MessageBox.Show("There is nothing recorded to undo yet.", "FreshWin");
            return;
        }

        var answer = MessageBox.Show(
            $"Put back every setting changed in the last run?\n\n{Path.GetFileName(newest)}",
            "FreshWin", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (answer != MessageBoxResult.Yes) return;

        try
        {
            var restored = await _engine.RestoreAsync(newest, _ => { }, CancellationToken.None);
            TweakEngine.RefreshState(_tweaks);

            MessageBox.Show(
                $"{restored} value(s) put back.\n\nRestart Explorer to see Explorer and taskbar changes.",
                "FreshWin");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"The undo file could not be applied.\n\n{ex.Message}", "FreshWin",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // -------------------------------------------------------------------- log

    private void AppendLog(string line)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.InvokeAsync(() => AppendLog(line));
            return;
        }

        // Keep the box from growing without bound during long runs.
        if (LogBox.Text.Length > 400_000) LogBox.Clear();

        LogBox.AppendText(line + Environment.NewLine);
        LogBox.ScrollToEnd();
    }

    private void LogToggle_Click(object sender, RoutedEventArgs e)
        => ShowLog(LogPanel.Visibility != Visibility.Visible);

    private void ShowLog(bool show)
    {
        LogPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        LogToggle.Content = show ? "Hide details" : "Show details";
        if (show) LogBox.ScrollToEnd();
    }

    // ------------------------------------------------------------ window chrome

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleMaximise();
            return;
        }

        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    private void Minimise_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Maximise_Click(object sender, RoutedEventArgs e) => ToggleMaximise();

    private void ToggleMaximise()
        => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_isRunning)
        {
            var answer = MessageBox.Show(
                "A run is still in progress. Closing now leaves the current step to finish in the background.\n\nClose anyway?",
                "FreshWin", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (answer != MessageBoxResult.Yes)
            {
                e.Cancel = true;
                return;
            }
        }

        base.OnClosing(e);
    }

    // -------------------------------------------------------------------- MVVM

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
