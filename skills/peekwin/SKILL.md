---
name: peekwin
description: Inspect, target, and automate native Windows UI with the `peekwin` CLI.
metadata: {"openclaw":{"os":["win32"],"requires":{"bins":["peekwin"]}}}
---

# PeekWin

Use `peekwin` when you need deterministic Windows desktop automation from the command line: window discovery, focus and state changes, UI inspection, mouse and keyboard input, screenshots, and waiting for exact window or element states.


## Assumptions

- The host running commands is Windows
- `peekwin` is installed and available on `PATH`
- `--json` should be preferred when another tool will consume the output

## Quick start

1. Find the target window
   - `peekwin window list --json`
   - `peekwin app list --json`
2. Inspect before acting
   - `peekwin window inspect --title "..."`
   - `peekwin see --title "..." --json`
3. Wait for the exact state you need
   - `peekwin wait window --title "..." --state focused`
   - `peekwin wait ref --ref e12 --state visible`
4. Perform the action
   - `peekwin click ...`
   - `peekwin type ...`
   - `peekwin press ...`
   - `peekwin hotkey ...`
5. Verify with another inspection or capture
   - `peekwin image ...`
   - `peekwin window inspect ...`
   - `peekwin see ... --json`

## When to use it

Use these instructions for:
- Desktop app automation on Windows
- Window discovery, focus, minimize, maximize, restore, and close
- Mouse input relative to a screen, window, or saved UI ref
- Keyboard and text entry into a specific target window
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

Prefer `peekwin wait` over fixed sleeps whenever the UI exposes a real state to poll.

## Common patterns

### Focus a window and type

```powershell
peekwin window focus --app notepad
peekwin wait window --app notepad --state focused
peekwin type --app notepad --text "hello from peekwin"
```

### Inspect and click a saved UI ref

```powershell
peekwin see --title "Notepad" --deep --json
peekwin wait ref --ref e12 --state visible
peekwin click --ref e12
```

### Capture a specific target

```powershell
peekwin image --title "Calculator" --path calc.png
peekwin image --ref e12 --path button.png
```

### Wait for a dialog instead of sleeping

```powershell
peekwin window focus --title "Save As"
peekwin wait window --title "Save As" --state visible --timeout-ms 10000
peekwin press --key enter
```

## Command map

- Discovery: `window list`, `app list`, `screens`, `desktop list`, `desktop current`
- Inspection: `window inspect`, `see`, `image info`, `screenshot info`
- Window control: `window focus`, `window minimize`, `window maximize`, `window restore`, `window close`, `desktop switch`
- Mouse and pointer: `move`, `click`, `drag`, `scroll`, `mouse down`, `mouse up`
- Keyboard and text: `type`, `paste`, `press`, `hotkey`, `keys`, `hold`
- Timing and sync: `wait`, `sleep`
- Capture: `image`, `screenshot`

## Working style

- Say the exact `peekwin` command before running it when the workflow is non-trivial
- Prefer short, verifiable steps over long automation chains
- Resolve ambiguity first with `window list`, `window inspect`, or `see`
- If a ref becomes stale, rerun `peekwin see` instead of guessing
