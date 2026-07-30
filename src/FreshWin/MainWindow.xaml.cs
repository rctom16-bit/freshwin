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
using FreshWin.Models;
using FreshWin.Services;

namespace FreshWin;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private const string AppInstallerStoreLink = "ms-windows-store://pdp/?productid=9NBLGGH4NNS1";

    private enum BannerActionKind { None, RestartElevated, OpenStore }

    private enum Pane { Software, Tweaks }

    private readonly List<AppEntry> _apps = Catalog.Build();
    private readonly List<Tweak> _tweaks = Tweaks.Build();
    private readonly WingetService _winget = new();
    private readonly TweakEngine _engine = new();

    private readonly CollectionViewSource _appView;
    private readonly CollectionViewSource _tweakView;

    private Pane _pane = Pane.Software;
    private string _search = "";
    private CategoryEntry? _category;
    private CategoryEntry? _group;
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

        foreach (var app in _apps) app.PropertyChanged += OnItemPropertyChanged;
        foreach (var tweak in _tweaks) tweak.PropertyChanged += OnItemPropertyChanged;

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
    public ObservableCollection<QueueItem> RunQueue { get; } = new();

    public ICollectionView VisibleApps => _appView.View;
    public ICollectionView VisibleTweaks => _tweakView.View;

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

    private void SwitchPane(Pane pane)
    {
        _pane = pane;

        _syncingNav = true;
        if (pane == Pane.Software) GroupList.SelectedItem = null;
        else CategoryList.SelectedItem = null;
        _syncingNav = false;

        var software = pane == Pane.Software;

        AppScroller.Visibility = software ? Visibility.Visible : Visibility.Collapsed;
        TweakScroller.Visibility = software ? Visibility.Collapsed : Visibility.Visible;
        SoftwareTools.Visibility = software ? Visibility.Visible : Visibility.Collapsed;
        TweakTools.Visibility = software ? Visibility.Collapsed : Visibility.Visible;

        if (software)
        {
            PaneTitle = "Set up this PC";
            PaneSubtitle = "Tick everything you want, then hit Install – the rest is automatic.";
        }
        else
        {
            PaneTitle = "Tune Windows";
            PaneSubtitle = "Only documented, reversible settings. Every change is recorded so it can be undone.";
        }

        RefreshView();
    }

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

    private void RefreshView()
    {
        if (!_ready) return;

        _appView.View.Refresh();
        _tweakView.View.Refresh();

        EmptyHint.Visibility = _appView.View.IsEmpty ? Visibility.Visible : Visibility.Collapsed;
        EmptyTweakHint.Visibility = _tweakView.View.IsEmpty ? Visibility.Visible : Visibility.Collapsed;
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

        HasSelection = apps.Count + tweaks.Count > 0;

        RunButtonText = apps.Count == 0 && tweaks.Count > 0 ? "Apply selected" : "Install selected";

        if (!HasSelection)
        {
            SelectionHeadline = "Nothing selected yet";
            SelectionDetail = _pane == Pane.Software
                ? "Pick the apps you want, or start with Essentials."
                : "Pick the settings you want, or start with Recommended.";
            return;
        }

        var parts = new List<string>();
        if (apps.Count > 0) parts.Add(apps.Count == 1 ? "1 app" : $"{apps.Count} apps");
        if (tweaks.Count > 0) parts.Add(tweaks.Count == 1 ? "1 setting" : $"{tweaks.Count} settings");
        SelectionHeadline = string.Join(" + ", parts) + " selected";

        var names = apps.Select(a => a.Name).Concat(tweaks.Select(t => t.Name)).ToList();
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

    private void SelectAllShown_Click(object sender, RoutedEventArgs e)
    {
        if (_pane == Pane.Software)
            foreach (var app in _appView.View.OfType<AppEntry>().ToList()) app.IsSelected = true;
        else
            foreach (var tweak in _tweakView.View.OfType<Tweak>().ToList()) tweak.IsSelected = true;
    }

    private void ClearPane_Click(object sender, RoutedEventArgs e)
    {
        if (_pane == Pane.Software)
            foreach (var app in _apps) app.IsSelected = false;
        else
            foreach (var tweak in _tweaks) tweak.IsSelected = false;
    }

    // ------------------------------------------------------------------- run

    private async void Next_Click(object sender, RoutedEventArgs e)
    {
        if (_isRunning) return;

        var apps = _apps.Where(a => a.IsSelected).ToList();
        var tweaks = _tweaks.Where(t => t.IsSelected).ToList();
        if (apps.Count + tweaks.Count == 0) return;

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
        foreach (var item in apps.Cast<QueueItem>().Concat(tweaks))
        {
            item.Status = RunStatus.Pending;
            item.StatusDetail = null;
            RunQueue.Add(item);
        }

        PickPage.Visibility = Visibility.Collapsed;
        RunPage.Visibility = Visibility.Visible;

        LogBox.Clear();
        AppendLog($"FreshWin – {apps.Count} app(s) and {tweaks.Count} setting(s)");
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
        int installed = 0, applied = 0, already = 0, failed = 0, skipped = 0, restart = 0;
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
        Finish(installed, applied, already, failed, skipped, restart, needsExplorer);
    }

    private void Finish(int installed, int applied, int already, int failed, int skipped, int restart, bool needsExplorer)
    {
        StopButton.Visibility = Visibility.Collapsed;
        BackButton.Visibility = Visibility.Visible;
        FinishButton.Visibility = Visibility.Visible;
        RestartExplorerButton.Visibility = needsExplorer ? Visibility.Visible : Visibility.Collapsed;

        RunHeadline.Text = failed > 0 ? "Finished, with some failures" : "All done";

        var parts = new List<string>();
        if (installed > 0) parts.Add($"{installed} installed");
        if (applied > 0) parts.Add($"{applied} applied");
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
