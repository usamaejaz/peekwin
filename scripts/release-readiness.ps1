param(
    [string]$Configuration = "Release",
    [string]$ProjectPath = ".\src\peekwin.csproj",
    [string]$ExpectedTag
)

$ErrorActionPreference = "Stop"

if (-not $IsWindows) {
    throw "Release-readiness checks must run on Windows."
}

$workspaceRoot = Split-Path -Parent $PSScriptRoot
$propsPath = Join-Path $workspaceRoot "Directory.Build.props"
$readmePath = Join-Path $workspaceRoot "README.md"
$devChecksProject = Join-Path $workspaceRoot "tests\PeekWin.DevChecks\PeekWin.DevChecks.csproj"

[xml]$props = Get-Content -Path $propsPath
$version = [string]$props.Project.PropertyGroup.PeekWinVersion
if (-not $version) {
    throw "Could not read PeekWinVersion from $propsPath"
}

if ($ExpectedTag -and $ExpectedTag -ne "v$version") {
    throw "Expected tag '$ExpectedTag' does not match version 'v$version'."
}

$readme = Get-Content -Path $readmePath -Raw
$releaseLinePattern = 'Current release:\s*`([^`]+)`'
$releaseLineMatches = [regex]::Matches($readme, $releaseLinePattern)
if ($releaseLineMatches.Count -gt 1) {
    throw "README contains multiple current release lines. Keep at most one."
}

if ($releaseLineMatches.Count -eq 1) {
    $readmeVersion = $releaseLineMatches[0].Groups[1].Value
    if ($readmeVersion -ne $version) {
        throw "README current release line does not match version $version."
    }
}

Push-Location $workspaceRoot
try {
    Write-Host "Running dev checks..."
    dotnet run --project $devChecksProject -c $Configuration | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "Dev checks failed with exit code $LASTEXITCODE."
    }

    Write-Host "Building peekwin ($Configuration)..."
    dotnet build $ProjectPath -c $Configuration | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet build failed with exit code $LASTEXITCODE."
    }

    Write-Host "Running smoke test..."
    & (Join-Path $PSScriptRoot "smoke-test.ps1") -Configuration $Configuration -ProjectPath $ProjectPath -SkipBuild -IncludeInputInjection
}
finally {
    Pop-Location
}
