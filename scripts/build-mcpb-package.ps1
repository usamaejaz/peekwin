param(
    [Parameter(Mandatory = $true)]
    [string]$Version,
    [Parameter(Mandatory = $true)]
    [string]$Rid,
    [Parameter(Mandatory = $true)]
    [string]$InputExe,
    [string]$OutputDirectory = ".\artifacts",
    [string]$PackageName = "peekwin"
)

$ErrorActionPreference = "Stop"

if (-not $IsWindows) {
    throw "MCPB package generation must run on Windows."
}

if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    throw "Version must use x.y.z format."
}

if (-not (Test-Path $InputExe)) {
    throw "Input executable not found: $InputExe"
}

$workspaceRoot = Split-Path -Parent $PSScriptRoot
$templateRoot = Join-Path $workspaceRoot "packaging\mcpb"
$stagingRoot = Join-Path $workspaceRoot ".artifacts\mcpb-build\$Rid"
$outputRoot = Join-Path $workspaceRoot $OutputDirectory
$outputPath = Join-Path $outputRoot "$PackageName-v$Version-claude-desktop-$Rid.mcpb"

if (Test-Path $stagingRoot) {
    Remove-Item $stagingRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $stagingRoot -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $stagingRoot "server") -Force | Out-Null
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null

$manifestTemplatePath = Join-Path $templateRoot "manifest.json.template"
$manifestContent = (Get-Content -Path $manifestTemplatePath -Raw).Replace("__VERSION__", $Version)
Set-Content -Path (Join-Path $stagingRoot "manifest.json") -Value $manifestContent -NoNewline

Copy-Item -Path (Join-Path $templateRoot "README.md") -Destination (Join-Path $stagingRoot "README.md") -Force
Copy-Item -Path $InputExe -Destination (Join-Path $stagingRoot "server\peekwin.exe") -Force

npx -y @anthropic-ai/mcpb validate (Join-Path $stagingRoot "manifest.json") | Out-Host
if ($LASTEXITCODE -ne 0) {
    throw "mcpb validate failed with exit code $LASTEXITCODE."
}

npx -y @anthropic-ai/mcpb pack $stagingRoot $outputPath | Out-Host
if ($LASTEXITCODE -ne 0) {
    throw "mcpb pack failed with exit code $LASTEXITCODE."
}
