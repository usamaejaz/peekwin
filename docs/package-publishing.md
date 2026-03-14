# Package Publishing

This document is for maintainers. End users only need the install commands in the main `README.md`.

## Overview

Tagged releases can publish:

- GitHub release assets
- A Chocolatey package
- A winget update
- Claude Desktop `.mcpb` bundles

The release workflow always publishes the raw `.exe` assets, Claude Desktop `.mcpb` bundles, and matching `.sha256` files. Package-manager publishing is conditional on the relevant repository secrets being configured.

## Chocolatey

Chocolatey publishing runs when `CHOCOLATEY_API_KEY` is configured in the repository secrets.

The workflow builds a Chocolatey package from the tagged GitHub release and pushes it to the community feed. The package downloads the official `win-x64` asset on x64 Windows and the official `win-arm64` asset on ARM64 Windows, verifying the matching SHA256 checksum before exposing the `peekwin` shim.

## winget

winget publishing runs when `WINGET_CREATE_GITHUB_TOKEN` is configured in the repository secrets.

The workflow uses `wingetcreate update --submit` for follow-up releases. That works once the package already exists in `winget-pkgs`, but the first submission is still a one-time bootstrap.

Run this once from Windows after the first tagged release is live:

```powershell
wingetcreate new `
  https://github.com/usamaejaz/peekwin/releases/download/vX.Y.Z/peekwin-vX.Y.Z-win-x64.exe `
  https://github.com/usamaejaz/peekwin/releases/download/vX.Y.Z/peekwin-vX.Y.Z-win-arm64.exe `
  -t <github-token> `
  --submit
```

`wingetcreate` prompts for package metadata during that initial submission. After the package `UsamaEjaz.PeekWin` is merged into `winget-pkgs`, later `v*` tags can submit updates automatically.
