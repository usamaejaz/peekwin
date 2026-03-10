# peekwin

peekwin is a Windows-native CLI for OS-level input, window control, inspection, and screenshots.

Current release: `0.1.0`

It is deliberately small right now. The goal is to get the core primitives right first so a future MCP server can wrap them without changing the internal architecture.

## Current scope

Implemented command surface:

- `version`
- `window list`
- `window focus`
- `window inspect`
- `click`
- `mouse move`
- `mouse down`
- `mouse up`
- `type`
- `press`
- `hotkey`
- `hold key`
- `hold mouse`
- `screenshot`

## Design goals

- Native Windows behavior via `user32.dll` and related APIs
- Simple CLI first, MCP later
- Structured output for automation via `--json`
- Clean separation between CLI parsing and Windows services
- Honest support for virtual desktops without pretending Windows gives more than it does

## Build

The CLI is Windows-only at runtime, but it can be built from a Linux host with the .NET 8 SDK installed.

Build on Windows:

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

Publish a single-file build if you want:

```powershell
dotnet publish .\src\peekwin.csproj -c Release -r win-x64 --self-contained false
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

- `peekwin-<tag>-win-x64.zip`
- `peekwin-<tag>-win-arm64.zip`

Each archive contains a self-contained Windows build of `peekwin`, so the target machine does not need a separate .NET runtime installation.

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
peekwin window list --json
```

Example JSON fields:

- `handle`
- `title`
- `className`
- `processId`
- `isVisible`
- `isMinimized`
- `isMaximized`
- `desktopLabel`
- `bounds`

`desktopLabel` is currently best-effort and reports whether a window appears to be on the current virtual desktop, another virtual desktop, or unknown.

### Focus a window

By handle:

```powershell
peekwin window focus --handle 0x001F09A2
```

By title contains-match:

```powershell
peekwin window focus --title "Notepad"
```

### Inspect a window

```powershell
peekwin window inspect --handle 0x001F09A2
peekwin window inspect --handle 0x001F09A2 --json
```

Inspection returns top-level metadata plus best-effort UI Automation child elements.

### Click by coordinates

```powershell
peekwin click --x 900 --y 540
peekwin click --x 900 --y 540 --button right
peekwin click --x 900 --y 540 --double
```

### Mouse primitives

Move only:

```powershell
peekwin mouse move --x 900 --y 540
```

Press and release explicitly:

```powershell
peekwin mouse down --button left --x 900 --y 540
peekwin mouse up --button left --x 1200 --y 700
```

Those primitives are the intended basis for drag composition: move to a start point, press `mouse down`, move again, then `mouse up`.

### Type text

```powershell
peekwin type --text "hello world"
peekwin type --text "slow typing" --delay-ms 45
peekwin type --text "steady typing" --speed 12
```

Typing uses Unicode keyboard injection through `SendInput`. `--speed` is characters per second and is converted internally into an inter-character delay. Use either `--delay-ms` or `--speed`, not both.

### Press a key

```powershell
peekwin press --key enter
peekwin press --key down --repeat 5
peekwin press --key f5
```

### Send a hotkey

```powershell
peekwin hotkey --keys ctrl,s
peekwin hotkey --keys alt,tab
peekwin hotkey --keys lwin,shift,s
```

### Hold a key

```powershell
peekwin hold key --key space --duration-ms 2000
```

### Hold a mouse button

```powershell
peekwin hold mouse --button left --duration-ms 1200
peekwin hold mouse --button right --duration-ms 800
```

`hold mouse` is useful when you want a timed press. For composed flows such as drag, prefer `mouse down` + `mouse move` + `mouse up`.

### Screenshot

Capture the full virtual desktop:

```powershell
peekwin screenshot --output .\desktop.png
```

Capture a specific monitor:

```powershell
peekwin screenshot --screen 1 --output .\monitor-1.png
```

Capture a specific window rectangle:

```powershell
peekwin screenshot --window 0x001F09A2 --output .\window.png
```

## Architecture

```text
Program.cs
└── Cli/CommandShell.cs
    ├── Services/WindowService.cs
    ├── Services/InputService.cs
    └── Services/ScreenshotService.cs

Infrastructure/
├── NativeMethods.cs
└── VirtualDesktopHelper.cs

Models/
├── CommandResult.cs
├── Geometry.cs
└── WindowInfo.cs
```

### Notes

- `WindowService` handles enumeration, focus, metadata inspection, and best-effort UI Automation child enumeration.
- `InputService` wraps `SendInput` and cursor positioning.
- `ScreenshotService` captures the full virtual desktop, a specific monitor, or a window rectangle.
- `VirtualDesktopHelper` only exposes what Windows safely gives us right now: whether a window appears to belong to the current desktop. This is enough for listing and caveats, not full desktop orchestration.
- `ScreenshotService` uses Win32 monitor enumeration and screen copying, so it can build from a Linux host without depending on WindowsDesktop SDK targets.

## Virtual desktop support

Windows virtual desktop APIs are limited and inconsistent across versions.

What peekwin currently does:

- detects whether a window is on the current desktop, another desktop, or unknown
- includes that status in `window list` and `window inspect`
- captures the full virtual screen across multiple monitors
- allows focus attempts by handle or title, with the usual Windows foreground restrictions still applying

What peekwin does **not** do yet:

- list every virtual desktop with friendly names
- switch desktops
- move windows between desktops
- guarantee cross-desktop focus behavior

That means the project is multiple-desktop aware, but not yet a full virtual desktop manager. That is intentional.

## Limitations

- Windows only
- elevated apps may require peekwin itself to run elevated
- `window inspect` depends on what UI Automation exposes for that app
- Electron, game, canvas, and custom-rendered UIs may expose weak automation trees and still need stronger inspection/OCR work later
- screenshot capture uses screen copying, not advanced Windows Graphics Capture yet
- window focusing is still subject to Windows foreground rules
- a window on another virtual desktop may enumerate as `other`, but Windows may still block bringing it foreground without switching desktops manually first
- the Linux host can build the project, but runtime behavior was not smoke-tested here because the binary still exits immediately outside Windows

## Safety note

This tool performs real OS-level input injection. It can click, type, and send shortcuts to the active desktop. Treat it carefully.
