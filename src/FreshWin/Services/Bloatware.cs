using FreshWin.Models;

namespace FreshWin.Services;

/// <summary>
/// Preinstalled Store apps that can be removed from a fresh PC.
///
/// This is a short, named list, not a debloat script. Nothing here is part of the shell
/// or of Windows itself: no Store, no App Installer (which provides winget), no Snipping
/// Tool, no Terminal, no runtime libraries. Everything listed can be reinstalled from the
/// Microsoft Store afterwards, which is the only way back — unlike the settings on the
/// Tune pages, a removal cannot be written into the undo file.
/// </summary>
public static class Bloatware
{
    public const string WindowsExtras = "Windows extras";
    public const string MediaGames = "Media & games";
    public const string Communication = "Communication";

    public static IReadOnlyList<(string Name, string Icon)> GroupOrder { get; } = new[]
    {
        (WindowsExtras, "M4 5.5h16v13H4z M4 9.5h16 M7 7.5v.01"),
        (MediaGames, "M7 9h10a5 5 0 0 1 0 10H7A5 5 0 0 1 7 9z M10 11.5v5 M7.5 14h5 M15 13a1 1 0 1 0 2 0 1 1 0 1 0-2 0"),
        (Communication, "M21 12a8 8 0 0 1-8 8H8.5L3.5 21.5 5 17.4A8 8 0 0 1 13 4a8 8 0 0 1 8 8z")
    };

    public static List<BloatApp> Build() => new()
    {
        // ----------------------------------------------------------- Windows extras
        new()
        {
            Name = "News", PackageName = "Microsoft.BingNews", Group = WindowsExtras, Recommended = true,
            Description = "Microsoft's news feed. Also what fills the Widgets panel with headlines."
        },
        new()
        {
            Name = "Weather", PackageName = "Microsoft.BingWeather", Group = WindowsExtras, Recommended = true,
            Description = "The weather app behind the taskbar weather tile."
        },
        new()
        {
            Name = "Maps", PackageName = "Microsoft.WindowsMaps", Group = WindowsExtras, Recommended = true,
            Description = "Windows Maps. Microsoft has retired it in favour of Bing Maps in the browser."
        },
        new()
        {
            Name = "Tips", PackageName = "Microsoft.Getstarted", Group = WindowsExtras, Recommended = true,
            Description = "The \"Get started\" tour that pops up on a new install."
        },
        new()
        {
            Name = "Get Help", PackageName = "Microsoft.GetHelp", Group = WindowsExtras, Recommended = true,
            Description = "Support chat app. Windows opens it from some error dialogs.",
            Caution = "A few troubleshooters launch this"
        },
        new()
        {
            Name = "Feedback Hub", PackageName = "Microsoft.WindowsFeedbackHub", Group = WindowsExtras, Recommended = true,
            Description = "For sending feedback to Microsoft. Only useful on Insider builds."
        },
        new()
        {
            Name = "Family Safety", PackageName = "MicrosoftCorporationII.MicrosoftFamily", Group = WindowsExtras, Recommended = true,
            Description = "Parental controls and screen-time reporting."
        },
        new()
        {
            Name = "Cortana", PackageName = "Microsoft.549981C3F5F10", Group = WindowsExtras, Recommended = true,
            Description = "The old voice assistant, discontinued and no longer functional."
        },
        new()
        {
            Name = "Quick Assist", PackageName = "MicrosoftCorporationII.QuickAssist", Group = WindowsExtras,
            Description = "Microsoft's remote-help tool.",
            Caution = "Handy if someone helps you remotely"
        },
        new()
        {
            Name = "Dev Home", PackageName = "Microsoft.Windows.DevHome", Group = WindowsExtras,
            Description = "Developer dashboard added in Windows 11. Rarely used even by developers."
        },
        new()
        {
            Name = "To Do", PackageName = "Microsoft.Todos", Group = WindowsExtras,
            Description = "Microsoft's task list, tied to a Microsoft account."
        },
        new()
        {
            Name = "Power Automate", PackageName = "Microsoft.PowerAutomateDesktop", Group = WindowsExtras,
            Description = "Desktop automation flows. Preinstalled but only useful with a licence."
        },
        new()
        {
            Name = "Alarms & Clock", PackageName = "Microsoft.WindowsAlarms", Group = WindowsExtras,
            Description = "Timers, alarms and a world clock.",
            Caution = "Actually useful for timers"
        },

        // ------------------------------------------------------------ Media & games
        new()
        {
            Name = "Solitaire Collection", PackageName = "Microsoft.MicrosoftSolitaireCollection",
            Group = MediaGames, Recommended = true,
            Description = "Card games with adverts and an optional subscription."
        },
        new()
        {
            Name = "Clipchamp", PackageName = "Clipchamp.Clipchamp", Group = MediaGames, Recommended = true,
            Description = "Microsoft's video editor. Pushes a subscription for most useful features."
        },
        new()
        {
            Name = "3D Viewer", PackageName = "Microsoft.Microsoft3DViewer", Group = MediaGames, Recommended = true,
            Description = "Views 3D models. Left over from the Paint 3D era."
        },
        new()
        {
            Name = "Paint 3D", PackageName = "Microsoft.MSPaint", Group = MediaGames, Recommended = true,
            Description = "The 3D spin-off, now discontinued. Classic Paint is a separate app and stays."
        },
        new()
        {
            Name = "Mixed Reality Portal", PackageName = "Microsoft.MixedReality.Portal", Group = MediaGames, Recommended = true,
            Description = "Setup for Windows Mixed Reality headsets, a platform Microsoft has ended."
        },
        new()
        {
            Name = "Movies & TV", PackageName = "Microsoft.ZuneVideo", Group = MediaGames,
            Description = "Microsoft's video store and player.",
            Caution = "Default player for some video files"
        },
        new()
        {
            Name = "Windows Media Player", PackageName = "Microsoft.ZuneMusic", Group = MediaGames,
            Description = "The modern Media Player, formerly Groove Music.",
            Caution = "This is the default music player"
        },
        new()
        {
            Name = "Xbox app", PackageName = "Microsoft.GamingApp", Group = MediaGames,
            Description = "Xbox library and Game Pass.",
            Caution = "Needed for Game Pass on PC"
        },
        new()
        {
            Name = "Xbox Game Bar", PackageName = "Microsoft.XboxGamingOverlay", Group = MediaGames,
            Description = "The Win+G overlay for capture and performance stats.",
            Caution = "Some games use it for capture and FPS overlay"
        },
        new()
        {
            Name = "Xbox Game Speech Window", PackageName = "Microsoft.XboxSpeechToTextOverlay", Group = MediaGames,
            Description = "Speech-to-text overlay for Xbox games."
        },

        // ------------------------------------------------------------ Communication
        new()
        {
            Name = "Teams (personal)", PackageName = "MicrosoftTeams", Group = Communication, Recommended = true,
            Description = "The consumer Teams that Windows preinstalls. Not the work version you would install yourself."
        },
        new()
        {
            Name = "Skype", PackageName = "Microsoft.SkypeApp", Group = Communication, Recommended = true,
            Description = "Preinstalled Skype. Microsoft has retired it in favour of Teams."
        },
        new()
        {
            Name = "People", PackageName = "Microsoft.People", Group = Communication, Recommended = true,
            Description = "Contacts app tied to Mail and Calendar."
        },
        new()
        {
            Name = "Phone Link", PackageName = "Microsoft.YourPhone", Group = Communication,
            Description = "Links an Android or iPhone to the PC for messages and calls.",
            Caution = "Genuinely useful if you pair your phone"
        },
        new()
        {
            Name = "Outlook (new)", PackageName = "Microsoft.OutlookForWindows", Group = Communication,
            Description = "The web-based Outlook that replaced Mail and Calendar.",
            Caution = "Removing it leaves no built-in mail app"
        }
    };
}
