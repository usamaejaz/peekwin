# peekwin

peekwin is a Windows-native CLI for OS-level input, window control, inspection, and screenshots.

It is deliberately small right now. The goal is to get the core primitives right first so a future MCP server can wrap them without changing the internal architecture.

## Current scope

Implemented command surface:

- `window list`
- `window focus`
- `window inspect`
- `click`
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

Windows only:

```powershell
dotnet build -c Release
```

Run locally:

```powershell
dotnet run --project .\src\peekwin -- window list
```

Publish a single-file build if you want:

```powershell
dotnet publish .\src\peekwin\peekwin.csproj -c Release -r win-x64 --self-contained false
```

## Usage

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

Inspection returns top-level metadata and best-effort UI Automation children.

### Click by coordinates

```powershell
peekwin click --x 900 --y 540
peekwin click --x 900 --y 540 --button right
peekwin click --x 900 --y 540 --double
```

### Type text

```powershell
peekwin type --text "hello world"
peekwin type --text "slow typing" --delay-ms 45
```

Typing uses Unicode keyboard injection through `SendInput`.

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

This is the primitive you can combine with cursor movement to emulate drag operations later.

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

- `WindowService` handles enumeration, focus, metadata, and best-effort UI inspection.
- `InputService` wraps `SendInput` and cursor positioning.
- `ScreenshotService` captures the full virtual desktop, a specific monitor, or a window rectangle.
- `VirtualDesktopHelper` only exposes what Windows safely gives us right now: whether a window appears to belong to the current desktop. This is enough for listing and caveats, not full desktop orchestration.

## Virtual desktop support

Windows virtual desktop APIs are limited and inconsistent across versions.

What peekwin currently does:

- detects whether a window is on the current desktop, another desktop, or unknown
- includes that status in `window list` and `window inspect`
- captures the full virtual screen across multiple monitors

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
- Electron, game, canvas, and custom-rendered UIs may expose weak automation trees
- screenshot capture uses screen copying, not advanced Windows Graphics Capture yet
- window focusing is still subject to Windows foreground rules

## Roadmap

Next sensible steps:

1. Better argument parsing with completions and validation
2. Add `mousemove` and `mouseup`/`mousedown` split commands explicitly
3. Richer UI Automation inspection tree depth controls
4. Stable refs for inspected elements
5. Optional OCR and vision-backed fallback
6. Optional MCP server wrapping the same services
7. Stronger virtual desktop support where Windows APIs allow it

## Safety note

This tool performs real OS-level input injection. It can click, type, and send shortcuts to the active desktop. Treat it carefully.
