---
name: peekwin
description: Use this skill when you need to inspect, target, and automate native Windows UI with the `peekwin` CLI. It covers window control, mouse and keyboard input, screenshots, UI inspection with `peekwin see`, and synchronization with `peekwin wait`. Use it for deterministic Windows desktop automation, not for non-Windows hosts or unsupported deep UI reasoning.
---

# PeekWin

## Overview

Use `peekwin` for Windows-native GUI automation from the command line. It works best when the user needs reliable, scriptable actions against desktop apps: find windows, focus them, inspect UI elements, move and click, type, capture images, and wait for exact window or element states.

Assume all of the following before using it:
- The host running commands is Windows
- `peekwin` is installed and on `PATH`
- Structured output is preferred, so use `--json` when another tool or script will consume results

## When to use it

Use this skill for:
- Desktop app automation on Windows
- Window discovery, focus, and state management
- Mouse input relative to a screen, window, or saved UI ref
- Screenshot or image capture of a specific monitor, window, or UI element
- Inspecting controls before automation with `peekwin see`
- Polling for readiness with `peekwin wait` instead of blind sleeps

Do not use this skill for:
- Non-Windows hosts
- Browser-only automation when browser-native tools are a better fit
- OCR-heavy or computer-vision-heavy tasks `peekwin` does not expose
- Guessing control refs without inspecting first

## Core workflow

1. Discover the target
   - Use `peekwin window list --json` for windows
   - Use `peekwin app list --json` when process name is easier than title matching
2. Inspect before acting
   - Use `peekwin window inspect ...` for bounds and state
   - Use `peekwin see ... --json` when you need element-level targeting
3. Target precisely
   - Prefer one of `--ref`, `--handle`, `--title`, or `--app`
   - Do not stack fuzzy selectors when one exact selector is available
4. Synchronize
   - Use `peekwin wait window ...` or `peekwin wait ref ...` before interacting
5. Act
   - Use `click`, `type`, `press`, `hotkey`, `move`, `drag`, `scroll`, or window commands
6. Verify
   - Capture with `peekwin image` or re-run `peekwin see` or `peekwin window inspect`

## Rules and gotchas

- Prefer `--json` for machine consumption
- Window handles are shown as hex like `0x001F09A2`, and `peekwin` accepts either hex or decimal input
- After `peekwin see`, refs are strict and session-bound. If the window identity changes or the saved element goes stale, rerun `peekwin see`
- Pointer coordinates are absolute unless a target flag is present. With `--screen`, `--app`, `--title`, `--handle`, `--window`, or `--ref`, coordinates become relative to that target
- `peekwin image` requires exactly one target
- Minimized windows are not valid image targets. Restore or focus them first
- Prefer `peekwin wait` over fixed `sleep` whenever a UI state can be observed directly
- Use `--verbose` only when debugging failures

## Common patterns

### List and focus a window

```powershell
peekwin window list --app notepad --json
peekwin window focus --app notepad
peekwin wait window --app notepad --state focused
```

### Inspect and click a control

```powershell
peekwin see --title "Notepad" --deep --json
peekwin wait ref --ref e12 --state visible
peekwin click --ref e12
```

### Type into a specific app

```powershell
peekwin wait window --app notepad --state focused
peekwin type --app notepad --text "hello from peekwin"
```

### Capture a target image

```powershell
peekwin image --title "Calculator" --path calc.png
peekwin image --ref e12 --path button.png
```

### Wait instead of sleeping blindly

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

When using this skill:
- State the exact `peekwin` command before running it when the workflow is not trivial
- Prefer short, verifiable steps over long automation chains
- If targeting is ambiguous, resolve it first with `window list`, `window inspect`, or `see`
- If a ref becomes stale, say so clearly and rerun `peekwin see` instead of guessing
