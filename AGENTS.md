# AI Coding Agent Instructions for peekwin

## Project Overview

peekwin is a **Windows-native CLI** for OS-level automation: window management, input simulation, UI inspection, and screenshots. Built with .NET 8, it targets Windows via P/Invoke to `user32.dll`, `gdi32.dll`, and COM interfaces. The design prioritizes **native Windows behavior**, structured output (`--json`), and clean separation between CLI parsing and platform services.

Target: future MCP server wrapping—architecture is stable primitives first, then automation layers.

## Architecture

**3-layer separation:**
- **CLI layer** ([CommandShell.cs](src/Cli/CommandShell.cs)): Argument parsing, help text, JSON output. Uses custom `OptionSet` parser (line ~506) for `--flag` and `--key value` styles.
- **Service layer** ([Services/](src/Services/)): Business logic. `WindowService`, `InputService`, `ScreenshotService` expose typed APIs.
- **Infrastructure** ([Infrastructure/](src/Infrastructure/)): P/Invoke to Win32 APIs (`NativeMethods.cs`), COM wrappers for virtual desktops (`VirtualDesktopHelper`) and UI Automation (`UiAutomationHelper`).

**Key design patterns:**
- Services never handle CLI parsing—they accept typed C# parameters
- All window handles stored as `nint` internally, formatted as hex (`0x...`) for CLI
- `CommandResult` record ([Models/CommandResult.cs](src/Models/CommandResult.cs)) for success/error results
- Records for immutable DTOs: `WindowInfo`, `WindowInspection`, `AutomationElementInfo`, `ScreenshotInfo`

## Critical Implementation Details

### Window Handle Format
Window handles are `nint` in code but **always displayed as hexadecimal** in CLI output (`0x{handle:X}`). The `OptionSet.TryGetHandle()` method accepts both:
- Hex with `0x` prefix: `--handle 0x12AB34`
- Decimal: `--handle 1234`

### Virtual Desktop Support
Windows COM API provides **limited** virtual desktop info via `IVirtualDesktopManager`. [VirtualDesktopHelper.cs](src/Infrastructure/VirtualDesktopHelper.cs) returns only:
- `"current"` - window on active desktop
- `"other"` - window on different desktop
- `"unknown"` - COM call failed

No desktop IDs or counts—this is a Windows platform limitation.

### Input Simulation
[InputService](src/Services/InputService.cs) uses `SendInput` with:
- **Unicode mode** for typing (`KEYEVENTF_UNICODE`)—sends Unicode chars directly, not virtual keys
- **Virtual key codes** for special keys (parsed by `VirtualKeyParser` in InputService.cs ~line 100+), including numpad, media, and `F13`-`F24`
- `MoveMouse()` + separate `MouseDown()`/`MouseUp()` for click simulation
- Hotkeys: press all keys in order, release in reverse (like `Ctrl+Shift+A`)
- Delay-based input methods are async (`ClickAsync`, `TypeTextAsync`, `HoldKeyAsync`, `HoldMouseAsync`) so future long-lived hosts do not block threads while waiting

### UI Automation
[UiAutomationHelper](src/Infrastructure/UiAutomationHelper.cs) uses COM `IUIAutomation` interfaces to inspect window elements (buttons, text boxes). Returns **direct children only** (`TreeScope.Children`), not deep hierarchy. COM objects must be manually released.

### CLI Option Parsing
Custom `OptionSet` class (bottom of [CommandShell.cs](src/Cli/CommandShell.cs)):
- Parses `--key value` or `--key=value` pairs
- Boolean flags: `--json`, `--all`
- Case-insensitive keys
- Throws on unexpected tokens (no positional args after main command)
- `CommandShell` catches parse failures and reports them as `Invalid arguments: ...`
- Global flag `--verbose` / `-v` is consumed only before the command name, so literal option values like `type --text -v` still work

## Development Workflow

**Build:**
```powershell
dotnet build -c Release
```

**Run locally:**
```powershell
dotnet run --project .\src\peekwin.csproj -- window list --json
```

By default `window list` filters to visible windows; use `--all` to include hidden ones.

**Smoke test:**
```powershell
.\scripts\smoke-test.ps1
```

**Publishing:** Self-contained releases via GitHub Actions on `v*` tags. Release assets are direct `.exe` files that include the runtime (no .NET install required). See [README.md](README.md#releases) for tagging.

## Common Patterns

**Adding a new command:**
1. Add switch case to `CommandShell.RunAsync()` (line ~32)
2. Create handler method: `HandleXyz(string[] args)`
3. Parse with `OptionSet.Parse(args)`, check for `--help`
4. Call service layer with typed params, get `CommandResult` or typed data
5. Return `--json` if requested, else human-readable text

**Adding Win32 API:**
1. Add P/Invoke signature to [NativeMethods.cs](src/Infrastructure/NativeMethods.cs)
2. Define constants (e.g., `MOUSEEVENTF_*`) as internal
3. Wrap in service method with error handling (never expose P/Invoke to CLI layer)

**JSON output convention:**
- Use `System.Text.Json` with `WriteIndented = true` (see `JsonOptions` in CommandShell)
- Serialize service layer results directly—DTOs designed for JSON
- Example: `window list --json` returns `WindowInfo[]`
- `screenshot info --json` returns virtual desktop bounds plus per-monitor bounds; default `screenshot` without `--screen` captures that full virtual rectangle

## Testing & Debugging

**Manual testing + smoke script**. Common test cases:
- Multi-monitor setups for screenshot bounds
- Hidden/minimized windows for focus behavior
- Virtual desktop switching (requires Windows 10+ with multiple desktops enabled)

**Debugging:** Set breakpoints in service methods, run with `dotnet run`, and add `--verbose` to print full exception details. Use VS Code launch config or Visual Studio 2022+.

## Constraints

- **Windows-only**: Runtime check in [Program.cs](src/Program.cs) exits on non-Windows
- **Mostly synchronous runtime**: Win32 calls are synchronous, but delay-based input paths now use async waits to avoid blocking future server hosts
- **.NET 8+ required**: Uses C# 12 features (primary constructors, `nint`, top-level statements)
- **System.Drawing.Common**: Only dependency, used for bitmap manipulation in [ScreenshotService](src/Services/ScreenshotService.cs)
