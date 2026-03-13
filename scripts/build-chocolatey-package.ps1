param(
    [Parameter(Mandatory = $true)]
    [string]$Version,
    [Parameter(Mandatory = $true)]
    [string]$Url64,
    [Parameter(Mandatory = $true)]
    [string]$Checksum64,
    [string]$OutputDirectory = ".\artifacts\chocolatey"
)

$ErrorActionPreference = "Stop"

if (-not $IsWindows) {
    throw "Chocolatey package generation must run on Windows."
}

if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    throw "Version must use x.y.z format."
}

if ($Checksum64 -notmatch '^[0-9a-fA-F]{64}$') {
    throw "Checksum64 must be a SHA256 hex string."
}

$workspaceRoot = Split-Path -Parent $PSScriptRoot
$templateRoot = Join-Path $workspaceRoot "packaging\chocolatey"
$stagingRoot = Join-Path $workspaceRoot ".artifacts\chocolatey-build"
$outputPath = Join-Path $workspaceRoot $OutputDirectory

if (Test-Path $stagingRoot) {
    Remove-Item $stagingRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $stagingRoot -Force | Out-Null
New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
Copy-Item -Path (Join-Path $templateRoot "*") -Destination $stagingRoot -Recurse -Force

$replacements = @{
    '__VERSION__' = $Version
    '__URL64__' = $Url64
    '__CHECKSUM64__' = $Checksum64.ToLowerInvariant()
}

Get-ChildItem -Path $stagingRoot -Recurse -File | ForEach-Object {
    $content = Get-Content -Path $_.FullName -Raw
    foreach ($entry in $replacements.GetEnumerator()) {
        $content = $content.Replace($entry.Key, $entry.Value)
    }

    $destinationPath = $_.FullName
    if ($destinationPath.EndsWith('.template')) {
        $destinationPath = $destinationPath.Substring(0, $destinationPath.Length - '.template'.Length)
        Remove-Item -Path $_.FullName -Force
    }

    Set-Content -Path $destinationPath -Value $content -NoNewline
}

Push-Location $stagingRoot
try {
    choco pack .\peekwin.nuspec --outputdirectory $outputPath | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "choco pack failed with exit code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}
