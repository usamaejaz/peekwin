# peekwin

peekwin is a Windows-native CLI for window control, input automation, screen inspection, targeted image capture, and UI wait commands.

## Install

### Package managers

Package manager listings can take a little time to show up after a release or moderation step. When available, use:

```powershell
winget install --id UsamaEjaz.PeekWin
choco install peekwin
```

### Download the binary

Download the latest Windows executable from GitHub Releases:

- `peekwin-<tag>-win-x64.exe` for most Windows PCs
- `peekwin-<tag>-win-arm64.exe` for Windows on ARM

Rename it to `peekwin.exe` if you want, then run it directly from PowerShell or Command Prompt:

```powershell
.\peekwin.exe --help
.\peekwin.exe version
```

If you want it available from anywhere, add the folder containing `peekwin.exe` to your `PATH`.

If you need a specific version, download the asset from that release tag.

### Build from source

Build from source with the .NET 8 SDK:

```powershell
git clone https://github.com/usamaejaz/peekwin.git
cd peekwin
dotnet build -c Release
```

## Quick examples

```powershell
peekwin window list
peekwin app list
peekwin screens
peekwin click --x 400 --y 300
peekwin type --text "hello"
peekwin image --screen 0 --output .\shot.png
peekwin wait window --app notepad --state focused
```

## What it does

Implemented command surface:

- `version`
- `window list`
- `window focus`
- `window inspect`
- `window move`
- `window resize`
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
- `ref click`
- `ref focus`
- `type`
- `paste`
- `press`
- `hotkey`
- `keys`
- `see`
- `wait`
- `clipboard get`
- `clipboard set`
- `hold`
- `sleep`

## Design goals

- Native Windows behavior via `user32.dll`, `gdi32.dll`, COM, and related APIs
- Stable, script-friendly JSON output
- Simple, scriptable CLI
- Clean separation between CLI parsing and Windows services
- Virtual desktop support where Windows exposes it cleanly

## MCP server

peekwin includes an MCP server under the `mcp` subcommand. It supports both stdio and HTTP transports and exposes named MCP tools across the full command surface.

Examples:

- `window_list`
- `window_focus`
- `click`
- `see_ui`
- `wait_window`
- `capture_image`
- `clipboard_set`
- `get_help`

Run the installed `peekwin` executable over stdio:

```powershell
peekwin mcp
```

Run the installed `peekwin` executable over HTTP:

```powershell
peekwin mcp --transport http --urls http://127.0.0.1:3000 --path /mcp
```

Print the MCP host help:

```powershell
peekwin mcp --help
```

If you are running from a source checkout instead of an installed executable, use `dotnet run`:

Run from source over stdio:

```powershell
dotnet run --project .\src\peekwin.csproj -- mcp
```

Run from source over HTTP:

```powershell
dotnet run --project .\src\peekwin.csproj -- mcp --transport http --urls http://127.0.0.1:3000 --path /mcp
```

Print source-run help:

```powershell
dotnet run --project .\src\peekwin.csproj -- mcp --help
```

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

`Directory.Build.props` defines the release version. `peekwin version`, assembly/file metadata, and release tags should stay in sync.

Create and push a release with the helper script:

```powershell
.\scripts\release.ps1 0.4.1
```

This updates `Directory.Build.props`, creates a version-bump commit, creates tag `v0.4.1`, and pushes the branch and tag. Use `-NoPush` to keep the commit and tag local, or `-DryRun` to preview the release steps. `-DryRun` also skips the Windows-only guard so you can preview the flow from non-Windows PowerShell.

You can still push a tag manually if needed:

```bash
git tag v0.4.1
git push origin v0.4.1
```

Produced release assets:

- `peekwin-<tag>-win-x64.exe`
- `peekwin-<tag>-win-arm64.exe`

Each executable is a self-contained Windows build, so the target machine does not need a separate .NET runtime installation.

### Package manager publishing

Tagged releases can also publish package-manager updates:

- GitHub Releases always publishes the raw `.exe` assets and `.sha256` files.
- Chocolatey publishing runs when the `CHOCOLATEY_API_KEY` repository secret is configured.
- winget update submission runs when the `WINGET_CREATE_GITHUB_TOKEN` repository secret is configured and the package already exists in the community repository.
- Maintainer details for Chocolatey and winget publishing live in `docs/package-publishing.md`.

When the package listings are live, install with:

```powershell
winget install --id UsamaEjaz.PeekWin
choco install peekwin
```

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
- `--title <text>`
- `--handle <HWND>`
- `--window <HWND>` as an alias for `--handle`
- `--ref <id>` after `peekwin see`

Keyboard and text-entry commands support window targeting:

- `--app <name>`
- `--title <text>`
- `--handle <HWND>`
- `--window <HWND>`
- `--ref <id>` after `peekwin see`

Screen indexes are zero-based and match `peekwin screens` output. For pointer commands, coordinates are absolute by default. When you add `--screen`, `--app`, `--title`, `--handle`, `--window`, or `--ref`, coordinates become relative to that screen, window, or element.

peekwin now enables per-monitor DPI awareness at startup so cursor movement and capture bounds line up more reliably on mixed-scale multi-monitor setups.

`peekwin image` requires exactly one target and captures only that monitor, window, or live UI element bounds resolved from `--ref`. Window-relative targeting also accepts `--app` when a process-name match is more convenient. Minimized windows are rejected instead of capturing the desktop area behind them. `peekwin screenshot` remains as an alias.

`peekwin wait` adds polling-based wait commands for windows, saved UI refs, and text matching. The defaults are `--timeout-ms 5000` and `--interval-ms 100`. `wait window` supports `exists`, `visible`, `focused`, `gone`, `minimized`, `maximized`, and `restored`. `wait ref` supports `exists`, `visible`, `focused`, `gone`, `enabled`, and `disabled`. `wait text` polls either a window title or a saved ref name until it contains the requested text. Ref waits stay tied to the exact saved element instead of switching to a fuzzy replacement.

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

`desktopLabel` reports whether a window appears to be on the current virtual desktop, another virtual desktop, or unknown.

### Window management

```powershell
peekwin window focus --handle 0x001F09A2
peekwin window inspect --title "Notepad"
peekwin window minimize --title "Notepad"
peekwin window focus --app notepad
peekwin window move --title "Notepad" --x 40 --y 40
peekwin window resize --title "Notepad" --width 1280 --height 900
peekwin window maximize --handle 0x001F09A2
peekwin window restore --title "Notepad"
peekwin window close --title "Notepad"
```

### List apps

```powershell
peekwin app list
peekwin app list --name note
peekwin app list --json
```

`app list` groups visible titled windows by process name and reports process IDs plus visible-window counts.

### See UI automation tree

```powershell
peekwin see
peekwin see --json
peekwin see --title "Notepad"
peekwin see --app chrome --deep --json
peekwin see --max-depth 4 --json
peekwin see --role button --json
peekwin see --name Save --json
peekwin see --all --json
```

`see` inspects the UI Automation tree for a target window. Without a target it uses the current foreground window. By default it returns the root plus one child level in a compact mode that hides off-screen and 0x0 nodes, suppresses duplicate passive nodes, and keeps full element names plus full control type names like `ControlType.Pane`. Use `--all` or `--raw` to inspect the full saved tree. `--deep` expands farther and `--max-depth` gives explicit control. `--role` filters by normalized control type such as `button`, `edit`, or `pane`.

After `peekwin see`, PeekWin writes an immutable snapshot payload plus an atomic current-pointer file under local app data so later commands can target elements by `--ref`. Pointer, image, and keyboard/text commands accept `--ref <id>` and resolve it against the latest saved snapshot in the current Windows session only. Refs are strict: if the source window is gone, the saved handle now points at a different window identity, the session changed, or the saved element path no longer resolves to the same element, PeekWin returns an error and asks you to run `peekwin see` again.

### Wait for a window or UI ref

```powershell
peekwin wait window --title "Notepad" --state focused
peekwin wait window --app chrome --state visible --timeout-ms 10000
peekwin wait window --handle 0x001F09A2 --state gone --json

peekwin wait ref --ref e12 --state visible
peekwin wait ref --ref e12 --state enabled --timeout 3000
peekwin wait ref --ref e12 --state gone --json
peekwin wait text --title "Notepad" --contains Draft
peekwin wait text --ref e12 --contains Save --timeout-ms 3000 --json
```

`wait window` uses the same matching rules as the rest of the window-targeted command set. `wait ref` keeps polling the exact saved ref, not a fuzzy replacement. `wait text` matches against a window title or saved ref name using case-insensitive contains semantics. If the saved element becomes stale, `gone` succeeds and the other ref states keep waiting until timeout.

### List screens

```powershell
peekwin screens
peekwin screens --json
peekwin image info --json
peekwin screenshot info --json
```

`peekwin image info` and `peekwin screenshot info` are aliases for `peekwin screens`.

Screen indexes are zero-based.

### Virtual desktops

```powershell
peekwin desktop list
peekwin desktop current --json
peekwin desktop switch 1
peekwin desktop switch --index 2 --delay-ms 150
```

Desktop switching uses the standard Windows virtual-desktop shortcuts and then re-checks the current desktop index. If Windows exposes only one desktop, list/current report that single desktop.

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
peekwin move --screen 0 --x 100 --y 100
peekwin click --title "Notepad" --x 20 --y 30
peekwin click --handle 0x001F09A2 --x 40 --y 40
peekwin move --ref e7 --x 5 --y 5
```

If a click target is provided and `--x/--y` are omitted, peekwin clicks the center of that target.

### Drag

```powershell
peekwin drag --x 100 --y 100 --to-x 400 --to-y 300
peekwin drag --screen 0 --x 20 --y 20 --to-x 500 --to-y 200
peekwin drag --title "Notepad" --x 30 --y 60 --to-x 300 --to-y 60 --duration-ms 250 --steps 16
peekwin drag --app notepad --x 30 --y 60 --to-x 300 --to-y 60 --duration-ms 250 --steps 16
```

### Scroll

```powershell
peekwin scroll --delta -120
peekwin scroll --delta -360 --screen 0 --x 500 --y 500
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

### Send a key sequence

```powershell
peekwin keys alt tab
peekwin keys down:alt tab right right up:alt
peekwin keys down:ctrl l sleep:500 up:ctrl --app chrome
peekwin keys --steps down:alt,tap:tab,tap:right,up:alt
```

Bare step tokens default to `tap:<key>`. Use `down:`, `up:`, and `sleep:` for stateful keyboard flows.

Element refs from `peekwin see` can be used directly in later commands:

```powershell
peekwin see --app notepad --json
peekwin click --ref e7
peekwin ref click --ref e7
peekwin ref focus --ref e12
peekwin move --ref e7 --x 5 --y 5
peekwin type "hello" --ref e12
peekwin image --ref e5 --output .\save-button.png
```

`peekwin ref click` tries UI Automation invoke first and falls back to a center mouse click when invoke is unavailable. `peekwin ref focus` uses the saved ref path to focus the live element directly.

### Clipboard

```powershell
peekwin clipboard get
peekwin clipboard get --json
peekwin clipboard set "hello from peekwin"
peekwin clipboard set --text "copied value"
```

`clipboard get` reads Unicode text from the current clipboard. `clipboard set` writes Unicode text directly, which is useful for automation chains and scripts.

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
peekwin image --screen 0 --output .\monitor-0.png
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
    ├── Services/ClipboardService.cs
    ├── Services/ScreenshotService.cs
    └── Services/VirtualDesktopService.cs

Infrastructure/
├── NativeMethods.cs
├── VirtualDesktopHelper.cs
└── UiAutomationHelper.cs

Models/
```
