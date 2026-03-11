param(
    [string]$Configuration = "Debug",
    [string]$ProjectPath = ".\src\peekwin.csproj",
    [string]$OutputPath = ".\artifacts\smoke-test.png",
    [switch]$IncludeInputInjection
)

$ErrorActionPreference = "Stop"

if (-not $IsWindows) {
    throw "peekwin smoke tests must run on Windows."
}

$workspaceRoot = Split-Path -Parent $PSScriptRoot

function Invoke-PeekwinCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter(Mandatory = $true)]
        [string[]]$Args,

        [int]$ExpectedExitCode = 0,

        [string]$ExpectedOutput
    )

    Write-Host "Running $Name..."
    $output = & dotnet @Args 2>&1 | Out-String
    if ($LASTEXITCODE -ne $ExpectedExitCode) {
        throw "Command '$Name' failed with exit code $LASTEXITCODE. Output:`n$output"
    }

    if ($ExpectedOutput -and $output -notmatch [regex]::Escape($ExpectedOutput)) {
        throw "Command '$Name' did not contain expected text '$ExpectedOutput'. Output:`n$output"
    }

    return $output
}

function ConvertFrom-PeekwinJson {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Json
    )

    return $Json | ConvertFrom-Json
}

Push-Location $workspaceRoot
try {
    Write-Host "Building peekwin ($Configuration)..."
    dotnet build $ProjectPath -c $Configuration | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet build failed with exit code $LASTEXITCODE."
    }

    $resolvedOutput = if ([System.IO.Path]::IsPathRooted($OutputPath)) {
        $OutputPath
    }
    else {
        Join-Path $workspaceRoot $OutputPath
    }

    $outputDir = Split-Path -Parent $resolvedOutput
    if ($outputDir) {
        New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
    }

    if (Test-Path $resolvedOutput) {
        Remove-Item $resolvedOutput -Force
    }

    Invoke-PeekwinCommand -Name "version" -Args @("run", "--project", $ProjectPath, "--", "version") | Out-Null
    Invoke-PeekwinCommand -Name "leading verbose version" -Args @("run", "--project", $ProjectPath, "--", "--verbose", "version") | Out-Null

    $visibleWindowsJson = Invoke-PeekwinCommand -Name "window list json" -Args @("run", "--project", $ProjectPath, "--", "window", "list", "--json") -ExpectedOutput '"command": "window list"'
    $allWindowsJson = Invoke-PeekwinCommand -Name "window list all json" -Args @("run", "--project", $ProjectPath, "--", "window", "list", "--all", "--json") -ExpectedOutput '"windows"'
    $appsJson = Invoke-PeekwinCommand -Name "app list json" -Args @("run", "--project", $ProjectPath, "--", "app", "list", "--json") -ExpectedOutput '"apps"'
    $screensJson = Invoke-PeekwinCommand -Name "screens json" -Args @("run", "--project", $ProjectPath, "--", "screens", "--json") -ExpectedOutput '"virtualBounds"'
    $desktopCurrentJson = Invoke-PeekwinCommand -Name "desktop current json" -Args @("run", "--project", $ProjectPath, "--", "desktop", "current", "--json") -ExpectedOutput '"desktop"'
    Invoke-PeekwinCommand -Name "parse error json" -Args @("run", "--project", $ProjectPath, "--", "window", "list", "extra", "--json") -ExpectedExitCode 1 -ExpectedOutput '"success": false' | Out-Null
    Invoke-PeekwinCommand -Name "image" -Args @("run", "--project", $ProjectPath, "--", "image", "--screen", "0", "--output", $resolvedOutput) | Out-Null
    Invoke-PeekwinCommand -Name "sleep json" -Args @("run", "--project", $ProjectPath, "--", "sleep", "1", "--json") -ExpectedOutput '"durationMs"' | Out-Null

    $visibleEnvelope = ConvertFrom-PeekwinJson -Json $visibleWindowsJson
    $allEnvelope = ConvertFrom-PeekwinJson -Json $allWindowsJson
    $appsEnvelope = ConvertFrom-PeekwinJson -Json $appsJson
    $screensEnvelope = ConvertFrom-PeekwinJson -Json $screensJson
    $desktopCurrentEnvelope = ConvertFrom-PeekwinJson -Json $desktopCurrentJson

    if (-not $visibleEnvelope.success -or -not $allEnvelope.success -or -not $appsEnvelope.success -or -not $screensEnvelope.success -or -not $desktopCurrentEnvelope.success) {
        throw "Expected JSON envelope success for list commands."
    }

    $visibleCount = @($visibleEnvelope.data.windows).Count
    $allCount = @($allEnvelope.data.windows).Count
    if ($allCount -lt $visibleCount) {
        throw "Expected '--all' window count ($allCount) to be greater than or equal to visible-only count ($visibleCount)."
    }

    if (@($screensEnvelope.data.screens).Count -lt 1) {
        throw "Expected at least one screen."
    }

    if (-not (Test-Path $resolvedOutput)) {
        throw "Expected image output was not created: $resolvedOutput"
    }

    if ($IncludeInputInjection) {
        Write-Host "Starting input-injection coverage..."
        $notepad = Start-Process -FilePath "notepad.exe" -PassThru
        try {
            Start-Sleep -Milliseconds 500
            Invoke-PeekwinCommand -Name "focus notepad" -Args @("run", "--project", $ProjectPath, "--", "window", "focus", "--title", "Notepad") | Out-Null
            Invoke-PeekwinCommand -Name "inspect notepad by title" -Args @("run", "--project", $ProjectPath, "--", "window", "inspect", "--title", "Notepad", "--json") -ExpectedOutput '"processName"' | Out-Null
            Invoke-PeekwinCommand -Name "type positional -v" -Args @("run", "--project", $ProjectPath, "--", "type", "-v", "--delay-ms", "1") | Out-Null
            Invoke-PeekwinCommand -Name "paste positional" -Args @("run", "--project", $ProjectPath, "--", "paste", "hello") | Out-Null
            Invoke-PeekwinCommand -Name "move duration" -Args @("run", "--project", $ProjectPath, "--", "move", "--x", "100", "--y", "100", "--duration-ms", "25") | Out-Null
            Invoke-PeekwinCommand -Name "double click" -Args @("run", "--project", $ProjectPath, "--", "click", "--x", "100", "--y", "100", "--double") | Out-Null
            Invoke-PeekwinCommand -Name "scroll" -Args @("run", "--project", $ProjectPath, "--", "scroll", "--delta", "-120") | Out-Null
            Invoke-PeekwinCommand -Name "hold keys" -Args @("run", "--project", $ProjectPath, "--", "hold", "shift", "ctrl", "--duration-ms", "25") | Out-Null
            Invoke-PeekwinCommand -Name "hold mouse" -Args @("run", "--project", $ProjectPath, "--", "hold", "--button", "left", "--duration-ms", "25") | Out-Null
            Invoke-PeekwinCommand -Name "desktop list" -Args @("run", "--project", $ProjectPath, "--", "desktop", "list") | Out-Null
        }
        finally {
            if ($notepad -and -not $notepad.HasExited) {
                $notepad.CloseMainWindow() | Out-Null
                Start-Sleep -Milliseconds 200
                if (-not $notepad.HasExited) {
                    $notepad | Stop-Process -Force
                }
            }
        }
    }

    Write-Host "Smoke test completed successfully."
    Write-Host "Image saved to $resolvedOutput"
}
finally {
    Pop-Location
}
