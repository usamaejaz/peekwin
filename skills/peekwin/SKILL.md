---
name: peekwin
description: Inspect, target, and automate native Windows UI with the `peekwin` CLI.
---

# PeekWin

Use `peekwin` when you need deterministic Windows desktop automation from the command line: window discovery, focus and state changes, UI inspection, mouse and keyboard input, screenshots, clipboard operations, and waiting for exact window or element states.

## Assumptions

- The host running commands is Windows
- `peekwin` is installed and available on `PATH`
- `--json` should be preferred when another tool will consume the output

## Quick start

If you need the exact syntax for any command, use:

- `peekwin --help`
- `peekwin help`
- `peekwin <command> --help`
- `peekwin <command> <subcommand> --help`

Examples:

- `peekwin window --help`
- `peekwin click --help`
- `peekwin wait ref --help`

1. Find the target window
   - `peekwin window list --json`
   - `peekwin app list --json`
2. Inspect before acting
   - `peekwin window inspect --title "..."`
   - `peekwin see --title "..." --json`
3. Wait for the exact state you need
   - `peekwin wait window --title "..." --state focused`
   - `peekwin wait ref --ref e12 --state visible`
   - `peekwin wait text --title "..." --contains Draft`
4. Perform the action
   - `peekwin click ...`
   - `peekwin type ...`
   - `peekwin press ...`
   - `peekwin hotkey ...`
   - `peekwin clipboard set ...`
5. Verify with another inspection or capture
   - `peekwin image ...`
   - `peekwin window inspect ...`
   - `peekwin see ... --json`
   - `peekwin clipboard get --json`

## When to use it

Use these instructions for:
- Desktop app automation on Windows
- Window discovery, focus, move, resize, minimize, maximize, restore, and close
- Mouse input relative to a screen, window, or saved UI ref
- Keyboard and text entry into a specific target window
- Clipboard reads and writes inside automation chains
- Screenshot or image capture of a monitor, window, or saved UI element
- Polling for readiness with `peekwin wait` instead of blind sleeps

Do not use them for:
- Non-Windows hosts
- Browser-only tasks when browser-native automation is a better fit
- OCR-heavy or vision-heavy workflows `peekwin` does not expose
- Guessing refs or coordinates when the UI can be inspected first

## Targeting model

Prefer the most exact selector available:
- `--ref` after `peekwin see`
- `--handle` or `--window` when you already know the HWND
- `--title` when a title match is stable enough
- `--app` when process-name targeting is more convenient
- `--screen` for monitor-relative actions

Important rules:
- Pointer coordinates are absolute by default
- When you add `--screen`, `--app`, `--title`, `--handle`, `--window`, or `--ref`, coordinates become relative to that target
- `peekwin image` requires exactly one target
- Minimized windows are not valid image targets

## Refs and waiting

After `peekwin see`, refs are strict and session-bound.

Do not guess or reuse them loosely. If the source window identity changes, the Windows session changes, or the saved element goes stale, rerun `peekwin see` and get a fresh ref.

Prefer `peekwin ref click` over raw pointer clicks when you want button-like activation. It tries UI Automation invoke first and falls back to a center mouse click when needed.

Prefer `peekwin wait` over fixed sleeps whenever the UI exposes a real state to poll.

## Common patterns

### Move and resize a window

```powershell
peekwin window move --title "Notepad" --x 40 --y 40
peekwin window resize --title "Notepad" --width 1280 --height 900
```

### Focus a window and type

```powershell
peekwin window focus --app notepad
peekwin wait window --app notepad --state focused
peekwin type --app notepad --text "hello from peekwin"
```

### Inspect and activate a saved UI ref

```powershell
peekwin see --title "Notepad" --deep --json
peekwin wait ref --ref e12 --state visible
peekwin ref click --ref e12
```

### Read and write clipboard text

```powershell
peekwin clipboard set "hello from peekwin"
peekwin clipboard get --json
```

### Wait for text instead of sleeping

```powershell
peekwin wait text --title "Save As" --contains Save
peekwin wait text --ref e12 --contains Save --timeout-ms 3000
```

### Capture a specific target

```powershell
peekwin image --title "Calculator" --path calc.png
peekwin image --ref e12 --path button.png
```

## Command map

- Discovery: `window list`, `app list`, `screens`, `desktop list`, `desktop current`
- Inspection: `window inspect`, `see`, `image info`, `screenshot info`
- Window control: `window focus`, `window move`, `window resize`, `window minimize`, `window maximize`, `window restore`, `window close`, `desktop switch`
- Mouse and pointer: `move`, `click`, `drag`, `scroll`, `mouse down`, `mouse up`, `ref click`, `ref focus`
- Keyboard and text: `type`, `paste`, `press`, `hotkey`, `keys`, `hold`
- Clipboard: `clipboard get`, `clipboard set`
- Timing and sync: `wait window`, `wait ref`, `wait text`, `sleep`
- Capture: `image`, `screenshot`

## Working style

- Say the exact `peekwin` command before running it when the workflow is non-trivial
- Prefer short, verifiable steps over long automation chains
- Resolve ambiguity first with `window list`, `window inspect`, or `see`
- If a ref becomes stale, rerun `peekwin see` instead of guessing
