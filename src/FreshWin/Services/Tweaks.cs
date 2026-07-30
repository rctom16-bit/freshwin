using Microsoft.Win32;
using FreshWin.Models;

namespace FreshWin.Services;

/// <summary>
/// The built-in list of Windows settings the app can switch.
///
/// Deliberately limited to documented, per-setting, reversible changes: Explorer and
/// taskbar preferences, the advertising/telemetry switches Windows itself exposes, and a
/// handful of power settings. No registry "cleaning", no disabling services, no RAM
/// "optimising" — those break more than they fix and cannot be undone reliably.
/// Every change here records its previous value so it can be put back.
/// </summary>
public static class Tweaks
{
    public const string Safety = "Safety";
    public const string Explorer = "File Explorer";
    public const string Taskbar = "Taskbar & Start";
    public const string Appearance = "Appearance";
    public const string Privacy = "Privacy";
    public const string Performance = "Performance";

    private const string Advanced = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
    private const string ContentDelivery = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager";
    private const string Personalize = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string Desktop = @"HKEY_CURRENT_USER\Control Panel\Desktop";
    private const string Mouse = @"HKEY_CURRENT_USER\Control Panel\Mouse";

    private const string HighPerformancePlan = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
    private const string BalancedPlan = "381b4222-f694-41f0-9685-ff5bb260df2e";

    public static IReadOnlyList<(string Name, string Icon)> GroupOrder { get; } = new[]
    {
        (Safety, "M12 3l8 3v6c0 5-8 9-8 9s-8-4-8-9V6z M9.2 12l2.2 2.2 4.3-4.3"),
        (Explorer, "M3 8a2 2 0 0 1 2-2h4l2 2h8a2 2 0 0 1 2 2v8a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z"),
        (Taskbar, "M3 16.5h18 M6 19.2v.1 M9.5 19.2v.1 M13 19.2v.1 M4 4.5h16v9H4z"),
        (Appearance, "M12 3a9 9 0 1 0 0 18 4.5 4.5 0 0 1 0-9 4.5 4.5 0 0 0 0-9z M16 7.5v.1 M18.5 11v.1 M16 14.5v.1"),
        (Privacy, "M12 3l8 3v6c0 5-8 9-8 9s-8-4-8-9V6z M12 10v3.5 M12 16v.2"),
        (Performance, "M13 3l-8 11h6l-1 7 8-11h-6z")
    };

    public static List<Tweak> Build() => new()
    {
        // ------------------------------------------------------------------ Safety
        new()
        {
            Name = "Create a restore point first",
            Description = "Takes a System Restore snapshot before anything else runs, so the whole session can be rolled back from Windows itself. Windows only makes one per 24 hours.",
            Group = Safety, Recommended = true, RequiresAdmin = true, RunFirst = true,
            ApplyCommand = new[]
            {
                "powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-Command",
                @"Enable-ComputerRestore -Drive ""$env:SystemDrive""; " +
                @"Checkpoint-Computer -Description 'FreshWin' -RestorePointType MODIFY_SETTINGS"
            }
        },

        // ------------------------------------------------------------ File Explorer
        new()
        {
            Name = "Show file extensions",
            Description = "Stops Windows hiding \".exe\" and \".pdf\", which is also how fake files sneak past people.",
            Group = Explorer, Recommended = true, Restart = RestartNeed.Explorer,
            Values = new RegValue[] { new() { Key = Advanced, Name = "HideFileExt", On = 0, Off = 1 } }
        },
        new()
        {
            Name = "Show hidden files",
            Description = "Reveals hidden files and folders such as AppData and ProgramData.",
            Group = Explorer, Restart = RestartNeed.Explorer,
            Values = new RegValue[] { new() { Key = Advanced, Name = "Hidden", On = 1, Off = 2 } }
        },
        new()
        {
            Name = "Open Explorer on This PC",
            Description = "Starts in the drive list instead of the Home / Quick access view.",
            Group = Explorer, Recommended = true, Restart = RestartNeed.Explorer,
            Values = new RegValue[] { new() { Key = Advanced, Name = "LaunchTo", On = 1, Off = 2 } }
        },
        new()
        {
            Name = "Full path in the title bar",
            Description = "Shows the complete folder path instead of just the folder name.",
            Group = Explorer, Restart = RestartNeed.Explorer,
            Values = new RegValue[]
            {
                new()
                {
                    Key = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\CabinetState",
                    Name = "FullPath", On = 1, Off = 0
                }
            }
        },
        new()
        {
            Name = "Hide OneDrive advertising",
            Description = "Turns off the \"sync provider\" notices Explorer uses to advertise OneDrive.",
            Group = Explorer, Recommended = true, Restart = RestartNeed.Explorer,
            Values = new RegValue[] { new() { Key = Advanced, Name = "ShowSyncProviderNotifications", On = 0, Off = 1 } }
        },

        new()
        {
            Name = "Detailed copy dialog",
            Description = "Expands the file-copy window to show the speed and transfer graph by default.",
            Group = Explorer, Restart = RestartNeed.Explorer,
            Values = new RegValue[]
            {
                new()
                {
                    Key = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\OperationStatusManager",
                    Name = "EnthusiastMode", On = 1, Off = 0
                }
            }
        },

        // --------------------------------------------------------- Taskbar & Start
        new()
        {
            Name = "Hide the Widgets button",
            Description = "Removes the weather and news panel from the taskbar.",
            Group = Taskbar, Recommended = true, Windows11Only = true, Restart = RestartNeed.Explorer,
            Values = new RegValue[] { new() { Key = Advanced, Name = "TaskbarDa", On = 0, Off = 1 } }
        },
        new()
        {
            Name = "Hide the Task View button",
            Description = "Removes the virtual-desktop button; Win+Tab keeps working.",
            Group = Taskbar, Restart = RestartNeed.Explorer,
            Values = new RegValue[] { new() { Key = Advanced, Name = "ShowTaskViewButton", On = 0, Off = 1 } }
        },
        new()
        {
            Name = "Hide the Chat button",
            Description = "Removes the built-in Microsoft Teams chat icon from the taskbar.",
            Group = Taskbar, Windows11Only = true, Restart = RestartNeed.Explorer,
            Values = new RegValue[] { new() { Key = Advanced, Name = "TaskbarMn", On = 0, Off = 1 } }
        },
        new()
        {
            Name = "Search as an icon only",
            Description = "Shrinks the wide taskbar search box down to a single magnifier icon.",
            Group = Taskbar, Restart = RestartNeed.Explorer,
            Values = new RegValue[]
            {
                new()
                {
                    Key = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Search",
                    Name = "SearchboxTaskbarMode", On = 1, Off = 2
                }
            }
        },
        new()
        {
            Name = "No recommendations in Start",
            Description = "Empties the \"Recommended\" section that suggests files and new apps.",
            Group = Taskbar, Recommended = true, Windows11Only = true, Restart = RestartNeed.Explorer,
            Values = new RegValue[] { new() { Key = Advanced, Name = "Start_IrisRecommendations", On = 0, Off = 1 } }
        },
        new()
        {
            Name = "Don't track opened apps",
            Description = "Stops Windows recording which programs you launch to rank them in Start.",
            Group = Taskbar, Restart = RestartNeed.Explorer,
            Values = new RegValue[] { new() { Key = Advanced, Name = "Start_TrackProgs", On = 0, Off = 1 } }
        },

        new()
        {
            Name = "\"End task\" in the taskbar menu",
            Description = "Adds End task to the right-click menu on taskbar buttons, so a frozen app can be killed without Task Manager.",
            Group = Taskbar, Recommended = true, Windows11Only = true, Restart = RestartNeed.Explorer,
            Values = new RegValue[] { new() { Key = Advanced, Name = "TaskbarEndTask", On = 1, Off = 0 } }
        },

        // -------------------------------------------------------------- Appearance
        new()
        {
            Name = "Dark mode",
            Description = "Switches both Windows and app windows to the dark theme.",
            Group = Appearance, Recommended = true, Restart = RestartNeed.Explorer,
            Values = new RegValue[]
            {
                new() { Key = Personalize, Name = "AppsUseLightTheme", On = 0, Off = 1 },
                new() { Key = Personalize, Name = "SystemUsesLightTheme", On = 0, Off = 1 }
            }
        },
        new()
        {
            Name = "Classic right-click menu",
            Description = "Brings back the full Windows 10 context menu instead of \"Show more options\".",
            Group = Appearance, Windows11Only = true, Restart = RestartNeed.Explorer,
            Values = new RegValue[]
            {
                new()
                {
                    Key = @"HKEY_CURRENT_USER\Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32",
                    Name = "", On = "", Kind = RegistryValueKind.String, DeleteKeyOnRevert = true
                }
            }
        },
        new()
        {
            Name = "Taskbar icons on the left",
            Description = "Moves the Start button back to the left-hand corner.",
            Group = Appearance, Windows11Only = true, Restart = RestartNeed.Explorer,
            Values = new RegValue[] { new() { Key = Advanced, Name = "TaskbarAl", On = 0, Off = 1 } }
        },
        new()
        {
            Name = "Seconds in the clock",
            Description = "Shows seconds in the taskbar clock.",
            Group = Appearance, Restart = RestartNeed.Explorer,
            Values = new RegValue[] { new() { Key = Advanced, Name = "ShowSecondsInSystemClock", On = 1, Off = 0 } }
        },

        new()
        {
            Name = "No menu delay",
            Description = "Sub-menus open instantly instead of after the default 400 ms pause.",
            Group = Appearance, Recommended = true, Restart = RestartNeed.SignOut,
            Values = new RegValue[]
            {
                new() { Key = Desktop, Name = "MenuShowDelay", On = "0", Off = "400", Kind = RegistryValueKind.String }
            }
        },
        new()
        {
            Name = "Disable Aero Shake",
            Description = "Stops every other window minimising when you happen to drag one around quickly.",
            Group = Appearance, Restart = RestartNeed.Explorer,
            Values = new RegValue[] { new() { Key = Advanced, Name = "DisallowShaking", On = 1, Off = 0 } }
        },
        new()
        {
            Name = "Turn off transparency",
            Description = "Drops the blur behind the taskbar and menus. Noticeably lighter on weak or integrated graphics.",
            Group = Appearance,
            Values = new RegValue[] { new() { Key = Personalize, Name = "EnableTransparency", On = 0, Off = 1 } }
        },

        // ----------------------------------------------------------------- Privacy
        new()
        {
            Name = "Turn off the advertising ID",
            Description = "Stops apps using a per-device ID to tailor the ads they show you.",
            Group = Privacy, Recommended = true,
            Values = new RegValue[]
            {
                new()
                {
                    Key = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo",
                    Name = "Enabled", On = 0, Off = 1
                }
            }
        },
        new()
        {
            Name = "No tips and app suggestions",
            Description = "Silences the \"suggested\" apps in Start and the tips Windows pops up.",
            Group = Privacy, Recommended = true,
            Values = new RegValue[]
            {
                new() { Key = ContentDelivery, Name = "SystemPaneSuggestionsEnabled", On = 0, Off = 1 },
                new() { Key = ContentDelivery, Name = "SubscribedContent-338388Enabled", On = 0, Off = 1 },
                new() { Key = ContentDelivery, Name = "SubscribedContent-338389Enabled", On = 0, Off = 1 },
                new() { Key = ContentDelivery, Name = "SilentInstalledAppsEnabled", On = 0, Off = 1 }
            }
        },
        new()
        {
            Name = "No web results in search",
            Description = "Keeps the Start menu searching your PC instead of querying Bing.",
            Group = Privacy, Recommended = true, Restart = RestartNeed.Explorer,
            Values = new RegValue[]
            {
                new()
                {
                    Key = @"HKEY_CURRENT_USER\Software\Policies\Microsoft\Windows\Explorer",
                    Name = "DisableSearchBoxSuggestions", On = 1, Off = null
                }
            }
        },
        new()
        {
            Name = "Diagnostic data to the minimum",
            Description = "Sets Windows telemetry to the lowest level the edition allows.",
            Group = Privacy, RequiresAdmin = true, Restart = RestartNeed.Reboot,
            Values = new RegValue[]
            {
                new()
                {
                    Key = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\DataCollection",
                    Name = "AllowTelemetry", On = 1, Off = null
                }
            }
        },

        new()
        {
            Name = "Turn off Recall",
            Description = "Stops Windows AI taking and storing periodic snapshots of your screen. Only present on Copilot+ PCs, harmless elsewhere.",
            Group = Privacy, Recommended = true, Restart = RestartNeed.SignOut,
            Values = new RegValue[]
            {
                new()
                {
                    Key = @"HKEY_CURRENT_USER\Software\Policies\Microsoft\Windows\WindowsAI",
                    Name = "DisableAIDataAnalysis", On = 1, Off = null
                }
            }
        },
        new()
        {
            Name = "Turn off Copilot",
            Description = "Removes the Copilot button and keeps the assistant out of the taskbar.",
            Group = Privacy, Windows11Only = true, Restart = RestartNeed.SignOut,
            Values = new RegValue[]
            {
                new()
                {
                    Key = @"HKEY_CURRENT_USER\Software\Policies\Microsoft\Windows\WindowsCopilot",
                    Name = "TurnOffWindowsCopilot", On = 1, Off = null
                }
            }
        },
        new()
        {
            Name = "No ads on the lock screen",
            Description = "Turns off the \"fun facts, tips and tricks\" overlay Spotlight uses to advertise.",
            Group = Privacy, Recommended = true,
            Values = new RegValue[]
            {
                new() { Key = ContentDelivery, Name = "SubscribedContent-338387Enabled", On = 0, Off = 1 },
                new() { Key = ContentDelivery, Name = "RotatingLockScreenOverlayEnabled", On = 0, Off = 1 }
            }
        },
        new()
        {
            Name = "No suggested content in Settings",
            Description = "Silences the promo cards Microsoft slots into the Settings app.",
            Group = Privacy, Recommended = true,
            Values = new RegValue[]
            {
                new() { Key = ContentDelivery, Name = "SubscribedContent-338393Enabled", On = 0, Off = 1 },
                new() { Key = ContentDelivery, Name = "SubscribedContent-353694Enabled", On = 0, Off = 1 },
                new() { Key = ContentDelivery, Name = "SubscribedContent-353696Enabled", On = 0, Off = 1 }
            }
        },
        new()
        {
            Name = "Don't store activity history",
            Description = "Stops Windows keeping a local record of the apps you use and files you open.",
            Group = Privacy, RequiresAdmin = true, Restart = RestartNeed.SignOut,
            Values = new RegValue[]
            {
                new()
                {
                    Key = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\System",
                    Name = "PublishUserActivities", On = 0, Off = null
                }
            }
        },
        new()
        {
            Name = "No inking and typing data",
            Description = "Turns off the personalisation that sends handwriting and typing samples to Microsoft.",
            Group = Privacy, Recommended = true,
            Values = new RegValue[]
            {
                new()
                {
                    Key = @"HKEY_CURRENT_USER\Software\Microsoft\Input\TIPC",
                    Name = "Enabled", On = 0, Off = 1
                },
                new()
                {
                    Key = @"HKEY_CURRENT_USER\Software\Microsoft\Personalization\Settings",
                    Name = "AcceptedPrivacyPolicy", On = 0, Off = 1
                }
            }
        },

        // ------------------------------------------------------------- Performance
        new()
        {
            Name = "High performance power plan",
            Description = "Stops the CPU downclocking aggressively. Costs a little battery on laptops.",
            Group = Performance, RequiresAdmin = true,
            ApplyCommand = new[] { "powercfg", "/setactive", HighPerformancePlan },
            RevertCommand = new[] { "powercfg", "/setactive", BalancedPlan },
            Detect = new RegValue
            {
                Key = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes",
                Name = "ActivePowerScheme", On = HighPerformancePlan, Kind = RegistryValueKind.String
            }
        },
        new()
        {
            Name = "Free the hibernation file",
            Description = "Turns hibernation off and deletes hiberfil.sys, freeing roughly as much disk as you have RAM. Also disables Fast Startup.",
            Group = Performance, RequiresAdmin = true,
            ApplyCommand = new[] { "powercfg", "/hibernate", "off" },
            RevertCommand = new[] { "powercfg", "/hibernate", "on" },
            Detect = new RegValue
            {
                Key = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power",
                Name = "HibernateEnabled", On = 0, Off = 1
            }
        },
        new()
        {
            Name = "Allow long file paths",
            Description = "Lifts the old 260-character path limit, which otherwise breaks deep folders.",
            Group = Performance, RequiresAdmin = true, Restart = RestartNeed.Reboot,
            Values = new RegValue[]
            {
                new()
                {
                    Key = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\FileSystem",
                    Name = "LongPathsEnabled", On = 1, Off = 0
                }
            }
        },
        new()
        {
            Name = "Disable the Sticky Keys shortcut",
            Description = "Stops the beep and prompt when Shift is held down — the classic gaming annoyance.",
            Group = Performance, Recommended = true,
            Values = new RegValue[]
            {
                new()
                {
                    Key = @"HKEY_CURRENT_USER\Control Panel\Accessibility\StickyKeys",
                    Name = "Flags", On = "506", Off = "510", Kind = RegistryValueKind.String
                }
            }
        },
        new()
        {
            Name = "No Game Bar background recording",
            Description = "Stops the Xbox Game Bar recording in the background, which quietly costs frames in every game.",
            Group = Performance, Recommended = true,
            Values = new RegValue[]
            {
                new()
                {
                    Key = @"HKEY_CURRENT_USER\System\GameConfigStore",
                    Name = "GameDVR_Enabled", On = 0, Off = 1
                },
                new()
                {
                    Key = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\GameDVR",
                    Name = "AppCaptureEnabled", On = 0, Off = 1
                }
            }
        },
        new()
        {
            Name = "Hardware-accelerated GPU scheduling",
            Description = "Lets the GPU manage its own scheduling, which can cut latency. Needs a WDDM 2.7 driver; ignored by Windows if the GPU cannot do it.",
            Group = Performance, RequiresAdmin = true, Restart = RestartNeed.Reboot,
            Values = new RegValue[]
            {
                new()
                {
                    Key = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\GraphicsDrivers",
                    Name = "HwSchMode", On = 2, Off = 1
                }
            }
        },
        new()
        {
            Name = "Turn off mouse acceleration",
            Description = "Makes pointer movement track the mouse one-to-one. Standard for aiming in games; some people prefer it on for desktop work.",
            Group = Performance, Restart = RestartNeed.SignOut,
            Values = new RegValue[]
            {
                new() { Key = Mouse, Name = "MouseSpeed", On = "0", Off = "1", Kind = RegistryValueKind.String },
                new() { Key = Mouse, Name = "MouseThreshold1", On = "0", Off = "6", Kind = RegistryValueKind.String },
                new() { Key = Mouse, Name = "MouseThreshold2", On = "0", Off = "10", Kind = RegistryValueKind.String }
            }
        },
        new()
        {
            Name = "Turn on Storage Sense",
            Description = "Lets Windows clear temporary files and empty the Recycle Bin on its own when the disk fills up.",
            Group = Performance, Recommended = true,
            Values = new RegValue[]
            {
                new()
                {
                    Key = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\StorageSense\Parameters\StoragePolicy",
                    Name = "01", On = 1, Off = 0
                }
            }
        },
        new()
        {
            Name = "Enable clipboard history",
            Description = "Keeps recent clips so Win+V can paste something you copied earlier.",
            Group = Performance, Recommended = true,
            Values = new RegValue[]
            {
                new()
                {
                    Key = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Clipboard",
                    Name = "EnableClipboardHistory", On = 1, Off = 0
                }
            }
        }
    };
}
