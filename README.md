# peekwin

peekwin is a Windows-native CLI for window control, input automation, screen inspection, targeted image capture, and UI wait commands.

## Install

Package manager listings can take a little time to show up after a release or moderation step. When available, use:

### WinGet

```powershell
winget install --id UsamaEjaz.PeekWin
```

### Chocolatey

```powershell
choco install peekwin
```

### Download the binary

Download the latest Windows executable from GitHub Releases:

- `peekwin-<tag>-win-x64.exe` for most Windows PCs
- `peekwin-<tag>-win-arm64.exe` for Windows on ARM
- `peekwin-<tag>-claude-desktop-win-x64.mcpb` for Claude Desktop on most Windows PCs
- `peekwin-<tag>-claude-desktop-win-arm64.mcpb` for Claude Desktop on Windows on ARM

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

## What it can do

- List, focus, move, resize, minimize, maximize, restore, and close windows
- Send mouse and keyboard input
- Inspect UI elements and reuse them later with `--ref`
- Capture screenshots of screens, windows, or UI elements
- Wait for windows, text, or saved UI refs to reach a state
- Expose the same command surface over MCP

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

Install the packaged Claude Desktop extension from a release asset:

1. Download the `.mcpb` file for your Windows architecture
2. Open Claude Desktop
3. Go to `Settings`
4. Open `Extensions`
5. Open `Advanced settings`
6. Choose `Install Extension...`
7. Select the `.mcpb` file

Run the installed `peekwin` executable over HTTP:

```powershell
peekwin mcp --transport http --urls http://127.0.0.1:3000 --path /mcp
```

Print the MCP host help:

```powershell
peekwin mcp --help
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
.\scripts\release.ps1 0.5.0
```

This updates `Directory.Build.props`, creates a version-bump commit, creates tag `v0.5.0`, and pushes the branch and tag. Use `-NoPush` to keep the commit and tag local, or `-DryRun` to preview the release steps. `-DryRun` also skips the Windows-only guard so you can preview the flow from non-Windows PowerShell.

You can still push a tag manually if needed:

```bash
git tag v0.5.0
git push origin v0.5.0
```

Produced release assets:

- `peekwin-<tag>-win-x64.exe`
- `peekwin-<tag>-win-arm64.exe`
- `peekwin-<tag>-claude-desktop-win-x64.mcpb`
- `peekwin-<tag>-claude-desktop-win-arm64.mcpb`

Each executable is a self-contained Windows build, so the target machine does not need a separate .NET runtime installation.

## Help

Use the built-in help for the full command reference:

```powershell
peekwin --help
peekwin window --help
peekwin wait ref --help
peekwin mcp --help
```

Use `--json` when you want stable, script-friendly output.
