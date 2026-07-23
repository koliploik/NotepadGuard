# NotepadGuard

Auto-backup for Windows 11 Notepad. Runs in the system tray and silently saves a copy of every Notepad window's content at a configurable interval (30 / 60 / 120 seconds). If you close Notepad without saving, your text is already backed up.

## Features

- **Automatic backup** — captures all open Notepad windows via UI Automation
- **Change detection** — only saves when the text actually changes (SHA-256 hash)
- **Configurable interval** — right-click tray icon → Backup interval (30s / 60s / 120s)
- **Backup browser** — browse, preview and restore any backed-up version
- **Auto-refresh** — the backup browser updates live when new versions arrive
- **Auto-start** — optional Windows startup shortcut

## Download

Go to [Releases](../../releases) and grab:

| File | Size | Notes |
|------|------|-------|
| `NotepadGuard-portable.zip` | ~150 MB | Self-contained — works on any Windows 10/11 x64, no dependencies |
| `NotepadGuard-light.zip` | ~150 KB | Requires [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) installed |

## Usage

1. Run `NotepadGuard.exe`
2. A tray icon appears (page with green checkmark)
3. Open Notepad and type something — it gets backed up automatically
4. Right-click the tray icon for options:
   - **Open backup folder** — opens `%USERPROFILE%\NotepadGuard\backups\` in Explorer
   - **Browse backups** — GUI to preview and restore any version
   - **Backup interval** — choose 30s, 60s, or 120s
   - **Capture now** — immediate backup without waiting
   - **Exit** — final capture + quit

### Auto-start with Windows

Create a shortcut to `NotepadGuard.exe` in:
```
%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup\
```

Or via PowerShell:
```powershell
$s = (New-Object -ComObject WScript.Shell).CreateShortcut("$env:APPDATA\Microsoft\Windows\Start Menu\Programs\Startup\NotepadGuard.lnk")
$s.TargetPath = "C:\path\to\NotepadGuard.exe"
$s.Save()
```

## Build from source

Requires [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```bash
# Framework-dependent (small, requires .NET 8 Desktop Runtime)
dotnet publish -c Release -r win-x64 --self-contained false -o publish

# Self-contained (large, no dependencies)
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o standalone
```

## Backup location

All backups are stored in:
```
%USERPROFILE%\NotepadGuard\backups\<filename>\<timestamp>.txt
```

Configuration is saved in:
```
%USERPROFILE%\NotepadGuard\config.json
```

## License

MIT
