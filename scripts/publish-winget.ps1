param(
    [Parameter(Mandatory = $true)]
    [string]$Version,
    [Parameter(Mandatory = $true)]
    [string]$Token,
    [string]$PackageIdentifier = "UsamaEjaz.PeekWin",
    [string]$Repository = "usamaejaz/peekwin",
    [string]$WingetCreatePath = "wingetcreate.exe",
    [switch]$Submit
)

$ErrorActionPreference = "Stop"

if (-not $IsWindows) {
    throw "winget publishing must run on Windows."
}

if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    throw "Version must use x.y.z format."
}

$tag = "v$Version"
$urls = @(
    "https://github.com/$Repository/releases/download/$tag/peekwin-$tag-win-x64.exe",
    "https://github.com/$Repository/releases/download/$tag/peekwin-$tag-win-arm64.exe"
)

& $WingetCreatePath show $PackageIdentifier *> $null
$packageExists = $LASTEXITCODE -eq 0
if (-not $packageExists) {
    Write-Warning "winget package '$PackageIdentifier' does not exist yet. Run an initial 'wingetcreate new ... --submit' once, then future tagged releases can auto-submit updates."
    return
}

$args = @('update', $PackageIdentifier, '-u') + $urls + @('-v', $Version, '-t', $Token)
if ($Submit) {
    $args += '--submit'
}

& $WingetCreatePath @args
if ($LASTEXITCODE -ne 0) {
    throw "wingetcreate update failed with exit code $LASTEXITCODE."
}
