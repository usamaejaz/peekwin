# Package Publishing

This document is for maintainers. End users only need the install commands in the main `README.md`.

## Overview

A pushed `v*` tag now publishes only:

- GitHub release assets
- Claude Desktop `.mcpb` bundles
- matching `.sha256` files

Chocolatey and winget are no longer part of the default tag-based `Release` workflow. They are published separately from the manual `Publish package managers` workflow.

## Manual retry from GitHub UI

If a tagged release is already live and you only need to retry Chocolatey, winget, or both, run the manual GitHub Actions workflow `Publish package managers`.

Open GitHub Actions, choose `Publish package managers`, click `Run workflow`, and provide:

- `target`: `chocolatey`, `winget`, or `both`
- `winget_submit`: whether to pass `--submit` to `wingetcreate`

The workflow automatically resolves the latest published GitHub release and publishes package-manager updates for that version.

You can also trigger the same workflow from the GitHub CLI:

```bash
gh workflow run "Publish package managers" \
  --repo usamaejaz/peekwin \
  -f target=winget \
  -f winget_submit=true
```

The manual workflow reuses the existing packaging scripts and publishes against the already-live assets from the latest GitHub release.

Because it always resolves the latest GitHub release automatically, it is meant for retrying the newest release. If you ever need to publish an older version manually, run the packaging scripts directly from Windows instead of the GitHub UI workflow.

## Chocolatey

Chocolatey publishing runs from the manual `Publish package managers` workflow when `CHOCOLATEY_API_KEY` is configured in the repository secrets.

That workflow builds a Chocolatey package from the latest GitHub release and pushes it to the community feed. The package downloads the official `win-x64` asset on x64 Windows and the official `win-arm64` asset on ARM64 Windows, verifying the matching SHA256 checksum before exposing the `peekwin` shim.

## winget

winget publishing runs from the manual `Publish package managers` workflow when `WINGET_CREATE_GITHUB_TOKEN` is configured in the repository secrets.

That workflow uses `wingetcreate update --submit` for follow-up releases against the latest GitHub release. That works once the package already exists in `winget-pkgs`, but the first submission is still a one-time bootstrap.

Run this once from Windows after the first tagged release is live:

```powershell
wingetcreate new `
  https://github.com/usamaejaz/peekwin/releases/download/vX.Y.Z/peekwin-vX.Y.Z-win-x64.exe `
  https://github.com/usamaejaz/peekwin/releases/download/vX.Y.Z/peekwin-vX.Y.Z-win-arm64.exe `
  -t <github-token> `
  --submit
```

`wingetcreate` prompts for package metadata during that initial submission. After the package `UsamaEjaz.PeekWin` is merged into `winget-pkgs`, later `v*` tags can submit updates automatically.
