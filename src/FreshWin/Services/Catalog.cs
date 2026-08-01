using FreshWin.Models;

namespace FreshWin.Services;

/// <summary>
/// The built-in list of programs most people put on a fresh Windows PC.
/// Every entry maps to a winget package id, which is what actually gets installed.
/// </summary>
public static class Catalog
{
    public const string Browsers = "Browsers";
    public const string Communication = "Communication";
    public const string Media = "Media";
    public const string Creative = "Creative";
    public const string Gaming = "Gaming";
    public const string Development = "Development";
    public const string Utilities = "Utilities";
    public const string Files = "Files & Backup";
    public const string Documents = "Documents";
    public const string Security = "Security";
    public const string Runtimes = "Runtimes";

    public static IReadOnlyList<(string Name, string Icon)> CategoryOrder { get; } = new[]
    {
        (Browsers, "M12 3a9 9 0 1 0 0 18 9 9 0 0 0 0-18z M3 12h18 M12 3c-2.6 2.4-2.6 15.6 0 18 2.6-2.4 2.6-15.6 0-18z"),
        (Communication, "M21 12a8 8 0 0 1-8 8H8.5L3.5 21.5 5 17.4A8 8 0 0 1 13 4a8 8 0 0 1 8 8z"),
        (Media, "M6 4.5v15l13-7.5z"),
        (Creative, "M4 20l4.5-1L19.5 8a2.1 2.1 0 0 0-3-3L5.5 15.5z M14 7l3 3"),
        (Gaming, "M7 9h10a5 5 0 0 1 0 10H7A5 5 0 0 1 7 9z M10 11.5v5 M7.5 14h5 M15 13a1 1 0 1 0 2 0 1 1 0 1 0-2 0 M17 16a1 1 0 1 0 2 0 1 1 0 1 0-2 0"),
        (Development, "M9 7l-5 5 5 5 M15 7l5 5-5 5"),
        (Utilities, "M4 7h16 M4 12h16 M4 17h16 M9 5.4v3.2 M15 10.4v3.2 M7 15.4v3.2"),
        (Files, "M3 8a2 2 0 0 1 2-2h4l2 2h8a2 2 0 0 1 2 2v8a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z"),
        (Documents, "M7 3h7l5 5v13H7z M14 3v5h5 M10 13h6 M10 17h4"),
        (Security, "M12 3l8 3v6c0 5-8 9-8 9s-8-4-8-9V6z M9 12l2.2 2.2L15.5 10"),
        (Runtimes, "M8.5 8.5h7v7h-7z M4 10.5h4.5 M4 14.5h4.5 M15.5 10.5H20 M15.5 14.5H20 M10.5 4v4.5 M14 4v4.5 M10.5 15.5V20 M14 15.5V20")
    };

    public static List<AppEntry> Build() => new()
    {
        // ---------------------------------------------------------------- Browsers
        new() { Id = "Google.Chrome", Name = "Google Chrome", Publisher = "Google", Category = Browsers, Essential = true,
                Description = "The browser most websites are built and tested against." },
        new() { Id = "Mozilla.Firefox", Name = "Mozilla Firefox", Publisher = "Mozilla", Category = Browsers, Essential = true,
                Description = "Independent, privacy-minded browser with strong tracking protection." },
        new() { Id = "Brave.Brave", Name = "Brave", Publisher = "Brave Software", Category = Browsers,
                Description = "Chromium browser that blocks ads and trackers out of the box." },
        new() { Id = "Microsoft.Edge", Name = "Microsoft Edge", Publisher = "Microsoft", Category = Browsers,
                Description = "Ships with Windows – install to force it to the newest build." },
        new() { Id = "Opera.Opera", Name = "Opera", Publisher = "Opera", Category = Browsers,
                Description = "Browser with a built-in VPN, ad blocker and messenger sidebar." },
        new() { Id = "Opera.OperaGX", Name = "Opera GX", Publisher = "Opera", Category = Browsers,
                Description = "Browser built for gaming: caps its own CPU, RAM and network use, with Twitch and Discord in the sidebar." },
        new() { Id = "Vivaldi.Vivaldi", Name = "Vivaldi", Publisher = "Vivaldi Technologies", Category = Browsers,
                Description = "Highly customisable browser with tab stacks and built-in mail." },
        new() { Id = "TorProject.TorBrowser", Name = "Tor Browser", Publisher = "The Tor Project", Category = Browsers,
                Description = "Routes traffic through the Tor network for anonymous browsing." },

        // ----------------------------------------------------------- Communication
        new() { Id = "Discord.Discord", Name = "Discord", Publisher = "Discord Inc.", Category = Communication, Essential = true,
                Description = "Voice, video and text chat built around communities and gaming." },
        new() { Id = "Zoom.Zoom", Name = "Zoom", Publisher = "Zoom", Category = Communication,
                Description = "Video meetings and webinars – the usual default for external calls." },
        new() { Id = "Microsoft.Teams", Name = "Microsoft Teams", Publisher = "Microsoft", Category = Communication,
                Description = "Chat, calls and meetings for Microsoft 365 workplaces." },
        new() { Id = "SlackTechnologies.Slack", Name = "Slack", Publisher = "Slack Technologies", Category = Communication,
                Description = "Channel-based team messaging with a huge integration catalogue." },
        new() { Id = "Telegram.TelegramDesktop", Name = "Telegram", Publisher = "Telegram FZ-LLC", Category = Communication,
                Description = "Fast cloud messenger that syncs across every device." },
        new() { Id = "OpenWhisperSystems.Signal", Name = "Signal", Publisher = "Signal Foundation", Category = Communication,
                Description = "End-to-end encrypted messaging with no ads and no tracking." },
        new() { Id = "9NKSQGP7F2NH", Source = "msstore", Name = "WhatsApp", Publisher = "WhatsApp LLC", Category = Communication,
                Description = "Desktop client for WhatsApp chats and calls." },
        new() { Id = "Mozilla.Thunderbird", Name = "Thunderbird", Publisher = "Mozilla", Category = Communication,
                Description = "Free desktop mail client with calendar and multi-account support." },

        // ------------------------------------------------------------------- Media
        new() { Id = "VideoLAN.VLC", Name = "VLC media player", Publisher = "VideoLAN", Category = Media, Essential = true,
                Description = "Plays essentially every video and audio format without extra codecs." },
        new() { Id = "Spotify.Spotify", Name = "Spotify", Publisher = "Spotify AB", Category = Media, Essential = true,
                Description = "Music and podcast streaming desktop client." },
        new() { Id = "CodecGuide.K-LiteCodecPack.Standard", Name = "K-Lite Codec Pack", Publisher = "Codec Guide", Category = Media,
                Description = "Codec bundle plus MPC-HC for playing awkward media files." },
        new() { Id = "PeterPawlowski.foobar2000", Name = "foobar2000", Publisher = "Peter Pawlowski", Category = Media,
                Description = "Lightweight, endlessly configurable local music player." },
        new() { Id = "Apple.iTunes", Name = "iTunes", Publisher = "Apple", Category = Media,
                Description = "Media library and device sync for older iPhones and iPods." },
        new() { Id = "Audacity.Audacity", Name = "Audacity", Publisher = "Muse Group", Category = Media,
                Description = "Free multi-track audio recorder and editor." },
        new() { Id = "OBSProject.OBSStudio", Name = "OBS Studio", Publisher = "OBS Project", Category = Media,
                Description = "Screen recording and live streaming with scenes and overlays." },
        new() { Id = "HandBrake.HandBrake", Name = "HandBrake", Publisher = "HandBrake Team", Category = Media,
                Description = "Converts and compresses video into modern formats." },

        // ---------------------------------------------------------------- Creative
        new() { Id = "ShareX.ShareX", Name = "ShareX", Publisher = "ShareX Team", Category = Creative,
                Description = "Screenshots, screen recording, OCR and instant uploads." },
        new() { Id = "GIMP.GIMP", Name = "GIMP", Publisher = "GIMP Team", Category = Creative,
                Description = "Full-featured free image editor – the classic Photoshop stand-in." },
        new() { Id = "dotPDN.PaintDotNet", Name = "Paint.NET", Publisher = "dotPDN LLC", Category = Creative,
                Description = "Fast, friendly image editor with layers – far beyond MS Paint." },
        new() { Id = "KDE.Krita", Name = "Krita", Publisher = "KDE", Category = Creative,
                Description = "Digital painting suite made for illustrators and concept art." },
        new() { Id = "Inkscape.Inkscape", Name = "Inkscape", Publisher = "Inkscape Project", Category = Creative,
                Description = "Vector graphics editor for logos, icons and SVG work." },
        new() { Id = "BlenderFoundation.Blender", Name = "Blender", Publisher = "Blender Foundation", Category = Creative,
                Description = "3D modelling, animation, sculpting and video editing in one app." },
        new() { Id = "IrfanSkiljan.IrfanView", Name = "IrfanView", Publisher = "Irfan Skiljan", Category = Creative,
                Description = "Featherweight image viewer with batch conversion tools." },
        new() { Id = "Greenshot.Greenshot", Name = "Greenshot", Publisher = "Greenshot", Category = Creative,
                Description = "Minimal screenshot tool with quick annotation." },
        new() { Id = "KDE.Kdenlive", Name = "Kdenlive", Publisher = "KDE", Category = Creative,
                Description = "Free multi-track video editor with effects and transitions." },
        new() { Id = "Figma.Figma", Name = "Figma", Publisher = "Figma", Category = Creative,
                Description = "Desktop app for collaborative interface design." },

        // ------------------------------------------------------------------ Gaming
        new() { Id = "Valve.Steam", Name = "Steam", Publisher = "Valve", Category = Gaming, Essential = true,
                Description = "The main PC games store and library manager." },
        new() { Id = "EpicGames.EpicGamesLauncher", Name = "Epic Games Launcher", Publisher = "Epic Games", Category = Gaming,
                Description = "Fortnite, Unreal Engine and the weekly free game giveaways." },
        new() { Id = "GOG.Galaxy", Name = "GOG Galaxy", Publisher = "GOG", Category = Gaming,
                Description = "DRM-free game library that can merge your other launchers." },
        new() { Id = "Ubisoft.Connect", Name = "Ubisoft Connect", Publisher = "Ubisoft", Category = Gaming,
                Description = "Required launcher for Ubisoft titles." },
        new() { Id = "ElectronicArts.EADesktop", Name = "EA app", Publisher = "Electronic Arts", Category = Gaming,
                Description = "Successor to Origin, needed for EA games." },
        new() { Id = "Blizzard.BattleNet", Name = "Battle.net", Publisher = "Blizzard Entertainment", Category = Gaming,
                Description = "Launcher for Blizzard and Activision games." },
        new() { Id = "Guru3D.Afterburner", Name = "MSI Afterburner", Publisher = "MSI", Category = Gaming,
                Description = "GPU overclocking, fan curves and an in-game stats overlay." },
        new() { Id = "Playnite.Playnite", Name = "Playnite", Publisher = "Playnite", Category = Gaming,
                Description = "One unified library and launcher for all your game stores." },

        // ------------------------------------------------------------- Development
        new() { Id = "Microsoft.VisualStudioCode", Name = "Visual Studio Code", Publisher = "Microsoft", Category = Development,
                Description = "The default code editor for most languages, with a huge extension market." },
        new() { Id = "Git.Git", Name = "Git", Publisher = "Git", Category = Development,
                Description = "Version control – required by nearly every dev toolchain." },
        new() { Id = "Notepad++.Notepad++", Name = "Notepad++", Publisher = "Notepad++ Team", Category = Development,
                Description = "Fast text editor for quick edits, logs and config files." },
        new() { Id = "Microsoft.WindowsTerminal", Name = "Windows Terminal", Publisher = "Microsoft", Category = Development,
                Description = "Tabbed terminal for PowerShell, CMD and WSL." },
        new() { Id = "Microsoft.PowerShell", Name = "PowerShell 7", Publisher = "Microsoft", Category = Development,
                Description = "The modern cross-platform PowerShell, alongside the built-in 5.1." },
        new() { Id = "OpenJS.NodeJS.LTS", Name = "Node.js LTS", Publisher = "OpenJS Foundation", Category = Development,
                Description = "JavaScript runtime and npm, on the long-term-support release." },
        new() { Id = "Python.Python.3.12", Name = "Python 3.12", Publisher = "Python Software Foundation", Category = Development,
                Description = "Python interpreter and pip." },
        new() { Id = "Microsoft.VisualStudio.2022.Community", Name = "Visual Studio 2022", Publisher = "Microsoft", Category = Development,
                Description = "Full IDE for .NET and C++ (large download – Community edition)." },
        new() { Id = "GitHub.GitHubDesktop", Name = "GitHub Desktop", Publisher = "GitHub", Category = Development,
                Description = "Visual Git client for commits, branches and pull requests." },
        new() { Id = "Docker.DockerDesktop", Name = "Docker Desktop", Publisher = "Docker Inc.", Category = Development,
                Description = "Containers on Windows – needs WSL 2 and a restart." },
        new() { Id = "SublimeHQ.SublimeText.4", Name = "Sublime Text", Publisher = "Sublime HQ", Category = Development,
                Description = "Very fast editor that opens huge files without complaint." },
        new() { Id = "JetBrains.Toolbox", Name = "JetBrains Toolbox", Publisher = "JetBrains", Category = Development,
                Description = "Installs and updates IntelliJ, PyCharm, Rider and friends." },
        new() { Id = "Postman.Postman", Name = "Postman", Publisher = "Postman", Category = Development,
                Description = "Build, test and document HTTP APIs." },
        new() { Id = "WinSCP.WinSCP", Name = "WinSCP", Publisher = "Martin Prikryl", Category = Development,
                Description = "SFTP/SCP file transfer with a two-pane Explorer view." },
        new() { Id = "PuTTY.PuTTY", Name = "PuTTY", Publisher = "Simon Tatham", Category = Development,
                Description = "Classic SSH and serial console client." },
        new() { Id = "WiresharkFoundation.Wireshark", Name = "Wireshark", Publisher = "Wireshark Foundation", Category = Development,
                Description = "Captures and dissects network traffic packet by packet." },

        // ---------------------------------------------------------------- Utilities
        new() { Id = "Microsoft.PowerToys", Name = "Microsoft PowerToys", Publisher = "Microsoft", Category = Utilities,
                Description = "FancyZones, PowerRename, Run, colour picker – the power-user pack." },
        new() { Id = "voidtools.Everything", Name = "Everything", Publisher = "voidtools", Category = Utilities,
                Description = "Instant filename search – finds any file the moment you type." },
        new() { Id = "CPUID.CPU-Z", Name = "CPU-Z", Publisher = "CPUID", Category = Utilities,
                Description = "Reports exactly which CPU, RAM and mainboard you have." },
        new() { Id = "REALiX.HWiNFO", Name = "HWiNFO", Publisher = "REALiX", Category = Utilities,
                Description = "Deep hardware monitoring: temperatures, clocks, fans, power." },
        new() { Id = "CrystalDewWorld.CrystalDiskInfo", Name = "CrystalDiskInfo", Publisher = "Crystal Dew World", Category = Utilities,
                Description = "Reads SMART data to warn you before a drive dies." },
        new() { Id = "Rufus.Rufus", Name = "Rufus", Publisher = "Akeo", Category = Utilities,
                Description = "Writes bootable Windows and Linux USB sticks." },
        new() { Id = "RevoUninstaller.RevoUninstaller", Name = "Revo Uninstaller", Publisher = "VS Revo Group", Category = Utilities,
                Description = "Removes programs and sweeps up the leftovers." },
        new() { Id = "Ditto.Ditto", Name = "Ditto", Publisher = "Ditto", Category = Utilities,
                Description = "Clipboard history you can search and paste from." },
        new() { Id = "AnyDesk.AnyDesk", Name = "AnyDesk", Publisher = "AnyDesk Software", Category = Utilities,
                Description = "Lightweight remote desktop for helping family fix things." },
        new() { Id = "TeamViewer.TeamViewer", Name = "TeamViewer", Publisher = "TeamViewer", Category = Utilities,
                Description = "Remote support and unattended access." },

        // ------------------------------------------------------------ Files & Backup
        new() { Id = "7zip.7zip", Name = "7-Zip", Publisher = "Igor Pavlov", Category = Files, Essential = true,
                Description = "Opens zip, rar, 7z, tar and iso – the one archiver everyone needs." },
        new() { Id = "RARLab.WinRAR", Name = "WinRAR", Publisher = "RARLAB", Category = Files,
                Description = "Creates and repairs RAR archives (trialware)." },
        new() { Id = "Giorgiotani.Peazip", Name = "PeaZip", Publisher = "Giorgio Tani", Category = Files,
                Description = "Open-source archiver with strong encryption support." },
        new() { Id = "AntibodySoftware.WizTree", Name = "WizTree", Publisher = "Antibody Software", Category = Files,
                Description = "Shows what is eating your disk space, in seconds." },
        new() { Id = "JAMSoftware.TreeSize.Free", Name = "TreeSize Free", Publisher = "JAM Software", Category = Files,
                Description = "Folder-size explorer for cleaning up full drives." },
        new() { Id = "qBittorrent.qBittorrent", Name = "qBittorrent", Publisher = "qBittorrent Project", Category = Files,
                Description = "Open-source BitTorrent client with no ads." },
        new() { Id = "Google.GoogleDrive", Name = "Google Drive", Publisher = "Google", Category = Files,
                Description = "Syncs Drive folders into Explorer." },
        new() { Id = "Dropbox.Dropbox", Name = "Dropbox", Publisher = "Dropbox", Category = Files,
                Description = "File sync and sharing across devices." },
        new() { Id = "Microsoft.OneDrive", Name = "OneDrive", Publisher = "Microsoft", Category = Files,
                Description = "Microsoft cloud storage integrated with Windows and Office." },

        // -------------------------------------------------------------- Documents
        new() { Id = "TheDocumentFoundation.LibreOffice", Name = "LibreOffice", Publisher = "The Document Foundation", Category = Documents,
                Description = "Free office suite that reads and writes Word, Excel and PowerPoint files." },
        new() { Id = "ONLYOFFICE.DesktopEditors", Name = "ONLYOFFICE", Publisher = "Ascensio System", Category = Documents,
                Description = "Office suite with the closest match to Microsoft formatting." },
        new() { Id = "SumatraPDF.SumatraPDF", Name = "SumatraPDF", Publisher = "Krzysztof Kowalczyk", Category = Documents,
                Description = "Tiny, instant PDF and e-book reader." },
        new() { Id = "Adobe.Acrobat.Reader.64-bit", Name = "Adobe Acrobat Reader", Publisher = "Adobe", Category = Documents, Essential = true,
                Description = "The reference PDF reader, for forms and signatures." },
        new() { Id = "Obsidian.Obsidian", Name = "Obsidian", Publisher = "Obsidian", Category = Documents,
                Description = "Markdown notes stored as plain files you own." },
        new() { Id = "Notion.Notion", Name = "Notion", Publisher = "Notion Labs", Category = Documents,
                Description = "Notes, wikis and databases in one workspace." },

        // --------------------------------------------------------------- Security
        new() { Id = "Bitwarden.Bitwarden", Name = "Bitwarden", Publisher = "Bitwarden", Category = Security, Essential = true,
                Description = "Open-source password manager that syncs everywhere." },
        new() { Id = "KeePassXCTeam.KeePassXC", Name = "KeePassXC", Publisher = "KeePassXC Team", Category = Security,
                Description = "Offline password vault kept in a local encrypted file." },
        new() { Id = "Malwarebytes.Malwarebytes", Name = "Malwarebytes", Publisher = "Malwarebytes", Category = Security,
                Description = "Second-opinion scanner for adware and malware cleanup." },
        new() { Id = "Proton.ProtonVPN", Name = "Proton VPN", Publisher = "Proton AG", Category = Security,
                Description = "VPN client with a genuinely usable free tier." },
        new() { Id = "IDRIX.VeraCrypt", Name = "VeraCrypt", Publisher = "IDRIX", Category = Security,
                Description = "Creates encrypted containers and full-disk encryption." },

        // --------------------------------------------------------------- Runtimes
        new() { Id = "Microsoft.VCRedist.2015+.x64", Name = "Visual C++ Redistributable", Publisher = "Microsoft", Category = Runtimes, Essential = true,
                Description = "Required by a huge share of desktop apps and games." },
        new() { Id = "Microsoft.DotNet.DesktopRuntime.8", Name = ".NET Desktop Runtime 8", Publisher = "Microsoft", Category = Runtimes, Essential = true,
                Description = "Runs modern .NET desktop applications." },
        new() { Id = "EclipseAdoptium.Temurin.21.JRE", Name = "Java Runtime (Temurin 21)", Publisher = "Eclipse Adoptium", Category = Runtimes,
                Description = "Open-source Java runtime for Java-based apps and games." },
        new() { Id = "Microsoft.DirectX", Name = "DirectX End-User Runtime", Publisher = "Microsoft", Category = Runtimes,
                Description = "Legacy DirectX components some older games still expect." }
    };
}
