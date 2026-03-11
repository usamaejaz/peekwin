# peekwin

peekwin is a Windows-native CLI for window control, input automation, screen inspection, and targeted image capture.

Current release: `0.1.0`

## Current scope

Implemented command surface:

- `version`
- `window list`
- `window focus`
- `window inspect`
- `window close`
- `window minimize`
- `window maximize`
- `window restore`
- `app list`
- `desktop list`
- `desktop current`
- `desktop switch`
- `screens`
- `image`
- `screenshot` (alias)
- `move`
- `click`
- `drag`
- `scroll`
- `mouse down`
- `mouse up`
- `type`
- `paste`
- `press`
- `hotkey`
- `hold`
- `sleep`

## Design goals

- Native Windows behavior via `user32.dll`, `gdi32.dll`, COM, and related APIs
- Stable, script-friendly JSON output
- Simple CLI first, MCP later
- Clean separation between CLI parsing and Windows services
- Honest support for virtual desktops without pretending Windows gives more than it does

## Build

The CLI is Windows-only at runtime, but it can be built from a Linux host with the .NET 8 SDK installed.

Build:

```powershell
dotnet build -c Release
```

Run locally on Windows:

```powershell
dotnet run --project .\src\peekwin.csproj -- window list
```

Print the CLI version:

```powershell
dotnet run --project .\src\peekwin.csproj -- version
```

Run the lightweight Windows smoke test:

```powershell
.\scripts\smoke-test.ps1
```

## Releases

GitHub release publishing is automated for pushed version tags that match `v*`.

Tag format:

- `v1.0.0`
- `v1.2.3`

Push a tag like this to trigger the release workflow:

```bash
git tag v0.1.0
git push origin v0.1.0
```

Produced release assets:

- `peekwin-<tag>-win-x64.exe`
- `peekwin-<tag>-win-arm64.exe`

Each executable is a self-contained Windows build of `peekwin`, so the target machine does not need a separate .NET runtime installation.

## JSON output

Commands that support `--json` return a stable envelope:

```json
{
  "success": true,
  "command": "window list",
  "data": {
    "windows": []
  },
  "error": null
}
```

Error responses keep the same shape with `success: false` and `error` populated.

## Targeting model

Pointer and image commands share the same target flags:

- `--screen <n>`
- `--app <name>`
- `--app <name>`
- `--title <text>`
- `--handle <HWND>`
- `--window <HWND>` as an alias for `--handle`

Keyboard and text-entry commands support window targeting:

- `--title <text>`
- `--handle <HWND>`
- `--window <HWND>`

For pointer commands, coordinates are absolute by default. When you add `--screen`, `--app`, `--title`, `--handle`, or `--window`, coordinates become relative to that screen or window.

`peekwin image` requires exactly one target and captures only that monitor or window. Window-relative targeting also accepts `--app` when a process-name match is more convenient. `peekwin screenshot` remains as an alias.

## Usage

### Version

```powershell
peekwin version
peekwin --verbose version
```

`--verbose` (or `-v`) is a global flag. It prints exception details for troubleshooting command failures.

### List windows

```powershell
peekwin window list
peekwin window list --all
peekwin window list --app notepad
peekwin window list --title "chrome"
peekwin window list --json
```

Example window fields:

- `handle`
- `title`
- `className`
- `processId`
- `processName`
- `isVisible`
- `isMinimized`
- `isMaximized`
- `desktopLabel`
- `bounds`

`desktopLabel` is best-effort and reports whether a window appears to be on the current virtual desktop, another virtual desktop, or unknown.

### Window management

```powershell
peekwin window focus --handle 0x001F09A2
peekwin window inspect --title "Notepad"
peekwin window minimize --title "Notepad"
peekwin window focus --app notepad
peekwin window maximize --handle 0x001F09A2
peekwin window restore --title "Notepad"
peekwin window close --title "Notepad"
```

### List apps

```powershell
peekwin app list
peekwin app list --all
peekwin app list --name note
peekwin app list --json
```

`app list` groups titled windows by process name and reports process IDs plus visible-window counts.

### List screens

```powershell
peekwin screens
peekwin screens --json
peekwin image info --json
peekwin screenshot info --json
```

`peekwin image info` and `peekwin screenshot info` are aliases for `peekwin screens`.

### Virtual desktops

```powershell
peekwin desktop list
peekwin desktop current --json
peekwin desktop switch 1
peekwin desktop switch --index 2 --delay-ms 150
```

Desktop switching uses the standard Windows virtual-desktop shortcuts and then re-checks the current desktop index. Desktop IDs are best-effort values read from Windows state; if Windows exposes only one desktop, list/current report that single desktop.

### Move and click

Absolute coordinates:

```powershell
peekwin move --x 900 --y 540
peekwin move --x 900 --y 540 --duration-ms 200 --steps 16
peekwin click --x 900 --y 540
peekwin click --x 900 --y 540 --button right
peekwin click --x 900 --y 540 --double --delay-ms 60
```

Relative to a monitor or window:

```powershell
peekwin move --screen 1 --x 100 --y 100
peekwin click --title "Notepad" --x 20 --y 30
peekwin click --handle 0x001F09A2 --x 40 --y 40
```

If a click target is provided and `--x/--y` are omitted, peekwin clicks the center of that target.

### Drag

```powershell
peekwin drag --x 100 --y 100 --to-x 400 --to-y 300
peekwin drag --screen 1 --x 20 --y 20 --to-x 500 --to-y 200
peekwin drag --title "Notepad" --x 30 --y 60 --to-x 300 --to-y 60 --duration-ms 250 --steps 16
peekwin drag --app notepad --x 30 --y 60 --to-x 300 --to-y 60 --duration-ms 250 --steps 16
```

### Scroll

```powershell
peekwin scroll --delta -120
peekwin scroll --delta -360 --screen 1 --x 500 --y 500
peekwin scroll --delta 240 --title "Notepad"
peekwin scroll --delta-x 120 --handle 0x001F09A2
```

When a target is provided and coordinates are omitted, scroll happens at the target center. Without a target, scroll happens at the current cursor position.

### Mouse button primitives

```powershell
peekwin mouse down --button left --x 900 --y 540
peekwin mouse up --button left
peekwin mouse down --title "Notepad"
peekwin mouse up --title "Notepad" --x 120 --y 40
```

### Type text

```powershell
peekwin type "hello world"
peekwin type --text "slow typing" --delay-ms 45
peekwin type "paste this" --method paste
peekwin type "focused text" --title "Notepad"
peekwin type "hello from app target" --app notepad
```

Typing uses Unicode keyboard injection through `SendInput`.

### Paste text

```powershell
peekwin paste "hello from clipboard"
peekwin paste --text "window-targeted paste" --title "Notepad"
peekwin paste "hello via app target" --app notepad
peekwin paste "restored clipboard text" --delay-ms 100
```

Paste uses the clipboard as a text-entry technique and restores previous text clipboard content when possible.

### Press a key

```powershell
peekwin press enter
peekwin press --key down --repeat 5 --delay-ms 20
peekwin press f5 --title "Notepad"
```

### Send a hotkey

```powershell
peekwin hotkey ctrl s
peekwin hotkey --keys alt,tab
peekwin hotkey lwin shift s
```

### Hold keys or a mouse button

```powershell
peekwin hold ctrl shift --duration-ms 500
peekwin hold --keys ctrl,shift --duration-ms 500 --title "Notepad"
peekwin hold --button left --duration-ms 1200
peekwin hold --button right --title "Notepad"
```

### Sleep

```powershell
peekwin sleep 250
peekwin sleep --duration-ms 1000 --json
```

### Image capture

Capture a specific monitor:

```powershell
peekwin image --screen 1 --output .\monitor-1.png
```

Capture a specific window:

```powershell
peekwin image --handle 0x001F09A2 --output .\window.png
peekwin image --title "Notepad" --output .\notepad.png
peekwin image --app notepad --output .\notepad.png
```

Keep using the old command spelling if needed:

```powershell
peekwin screenshot --handle 0x001F09A2 --output .\window.png
```

## Architecture

```text
Program.cs
└── Cli/CommandShell.cs
    ├── Services/WindowService.cs
    ├── Services/InputService.cs
    ├── Services/ScreenshotService.cs
    └── Services/VirtualDesktopService.cs

Infrastructure/
├── NativeMethods.cs
├── VirtualDesktopHelper.cs
└── UiAutomationHelper.cs

Models/
```
