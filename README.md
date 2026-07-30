# FreshWin

A desktop app for setting up a fresh Windows PC. Tick the software you want and the
Windows settings you want changed, press one button, and it does the whole lot on its own —
silent installs, one after another, no clicking through setup wizards.

It is a single portable `FreshWin.exe`. Nothing to install, no runtime to download, no
config files to edit.

```
┌─ FreshWin ──────────────────────────────────────────────────────────────┐
│ SOFTWARE       │  Set up this PC                                        │
│  All apps  91  │  Tick everything you want, then hit Install            │
│  Browsers   7  │  [search…]     ★ Essentials  Select all shown  Clear   │
│  Media      8  │                                                        │
│  …             │  ┌────────────────┐ ┌────────────────┐ ┌─────────────┐ │
│                │  │ GC  Google     │ │ MF  Mozilla  ✓ │ │ 7Z  7-Zip ✓ │ │
│ TUNE WINDOWS   │  │     Chrome     │ │     Firefox    │ │             │ │
│  All settings 40  └────────────────┘ └────────────────┘ └─────────────┘ │
│  Privacy      10                                                        │
│  Performance   9  12 apps + 5 settings selected  [ Install selected → ] │
└─────────────────────────────────────────────────────────────────────────┘
```

## What it does

**Software — 91 programs** grouped into Browsers, Communication, Media, Creative, Gaming,
Development, Utilities, Files & Backup, Documents, Security and Runtimes. Every package id
is verified against the official winget index. An **Essentials** preset ticks the 18 apps
almost every PC wants.

**Tune Windows — 40 settings** across Safety, File Explorer, Taskbar & Start, Appearance,
Privacy and Performance: show file extensions, open Explorer on This PC, hide the Widgets
and Chat buttons, "End task" in the taskbar menu, empty the Start "Recommended" list, dark
mode, the classic right-click menu, no menu delay, turn off Recall and Copilot, kill the
advertising ID, lock-screen ads, Settings promos and web results in search, stop Game Bar
recording in the background, high performance power plan, hardware-accelerated GPU
scheduling, mouse acceleration off, Storage Sense on, free the hibernation file, allow long
paths, and more. A **Recommended** preset ticks the 20 that are safe wins on any PC.

The first item is **Create a restore point** — it is hoisted to the front of the run, ahead
of every install, so there is a System Restore snapshot to fall back on before anything
changes.

Both lists feed one run page with live per-item status, overall progress and a details log
carrying winget's own output and every registry value written.

- **Stop after current** rather than killing an installer half-way.
- Cards show what is **already installed / already on**, so a second run is not destructive.
- Warns up front if winget is missing or the app is not elevated, and can restart itself
  as administrator.

Software is installed by [winget](https://learn.microsoft.com/windows/package-manager/),
the Windows Package Manager that ships with Windows 10 and 11. FreshWin downloads nothing
itself — every package comes from its official publisher.

## About the Windows settings

These are ordinary, documented preferences: the same values the Settings app writes, plus a
few power settings via `powercfg`. Each one shows exactly which registry value it changes,
right under its name on the run page.

**Every change is reversible.** Before writing anything, FreshWin records the previous
value — or the fact that there wasn't one — to an undo file in
`%LOCALAPPDATA%\FreshWin\undo-<timestamp>.json`. You can undo in three ways:

- the **revert** link on any card that is currently on,
- **Undo last run**, which puts back everything the most recent run changed,
- or by hand, since the undo file is readable JSON.

**What it deliberately does not do:** no registry "cleaning", no disabling Windows
services, no RAM "optimising", no removing Defender, no third-party debloat scripts. Those
break more than they fix and cannot be undone reliably. If that is what you are after, this
is the wrong tool.

Seven settings need administrator rights and are marked `admin` on the card. Explorer and
taskbar changes need Explorer restarted, which the app offers as a button when the run
finishes; a few need a sign-out or reboot and say so.

## Requirements

- Windows 10 (1809+) or Windows 11, 64-bit
- winget / "App Installer" for the software half — preinstalled on current Windows; the app
  links to the Microsoft Store page if it is missing. The settings half works without it.
- Administrator rights for most installs and the seven `admin` settings

## Getting the app

Download `FreshWin.exe` from the build artifacts of the
[build workflow](../../actions/workflows/build.yml), or build it yourself:

```powershell
git clone https://github.com/rctom16-bit/freshwin.git
cd freshwin
dotnet publish src/FreshWin/FreshWin.csproj -c Release -o publish
```

`publish\FreshWin.exe` is self-contained (~66 MB) and runs on a PC with no .NET installed —
which is the point, since a fresh Windows install has no runtime yet.

Building needs the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0). The
project sets `EnableWindowsTargeting`, so it also builds on Linux and macOS CI runners (it
just cannot run there).

## Adding software or settings

Software lives in [`src/FreshWin/Services/Catalog.cs`](src/FreshWin/Services/Catalog.cs) —
one entry per program:

```csharp
new() { Id = "Anki.Anki", Name = "Anki", Publisher = "Anki", Category = Documents,
        Description = "Spaced-repetition flashcards." },
```

Find ids with `winget search <name>`. Set `Essential = true` to include it in the Essentials
preset, and `Source = "msstore"` for Microsoft Store packages.

Settings live in [`src/FreshWin/Services/Tweaks.cs`](src/FreshWin/Services/Tweaks.cs). Give
the value to write and the value that restores the Windows default (`Off = null` means
"remove the value again"):

```csharp
new()
{
    Name = "Show hidden files",
    Description = "Reveals hidden files and folders such as AppData.",
    Group = Explorer, Restart = RestartNeed.Explorer,
    Values = new RegValue[] { new() { Key = Advanced, Name = "Hidden", On = 1, Off = 2 } }
},
```

## Project layout

| Path | Purpose |
| --- | --- |
| `src/FreshWin/MainWindow.xaml` | The window: pick page and run page |
| `src/FreshWin/MainWindow.xaml.cs` | Navigation, filtering, selection, the run loop |
| `src/FreshWin/Themes/Theme.xaml` | Dark theme, control styles, vector icons |
| `src/FreshWin/Services/Catalog.cs` | The software catalogue |
| `src/FreshWin/Services/Tweaks.cs` | The Windows settings catalogue |
| `src/FreshWin/Services/TweakEngine.cs` | Registry read/write, undo files, Explorer restart |
| `src/FreshWin/Services/WingetService.cs` | winget process handling and exit codes |
| `src/FreshWin/Models/` | `QueueItem` base, `AppEntry`, `Tweak`, `CategoryEntry` |
| `tools/make_icon.py` | Regenerates `Assets/icon.ico` |

## Notes

- Some packages (Visual Studio, Docker Desktop, Blender) are large downloads and take a
  while — the row stays on *Installing…* until winget returns.
- A few installs need a restart to finish; those report *Installed – restart needed*.
- Freeing the hibernation file also turns off Fast Startup. Revert it if you want that back.
- Closing the window mid-run leaves the current step to finish in the background.
