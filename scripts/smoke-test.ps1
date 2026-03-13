param(
    [string]$Configuration = "Debug",
    [string]$ProjectPath = ".\src\peekwin.csproj",
    [string]$OutputPath = ".\artifacts\smoke-test.png",
    [switch]$IncludeInputInjection,
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

if (-not $IsWindows) {
    throw "peekwin smoke tests must run on Windows."
}

$workspaceRoot = Split-Path -Parent $PSScriptRoot

function Get-PeekwinVersion {
    $propsPath = Join-Path $workspaceRoot "Directory.Build.props"
    [xml]$props = Get-Content -Path $propsPath
    $version = $props.Project.PropertyGroup.PeekWinVersion
    if (-not $version) {
        throw "Could not read PeekWinVersion from $propsPath"
    }

    return [string]$version
}

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

function Get-UsableChildRef {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ProjectPath,

        [Parameter(Mandatory = $true)]
        [string]$WindowTitle
    )

    $seeJson = Invoke-PeekwinCommand -Name "see $WindowTitle json" -Args @("run", "--project", $ProjectPath, "--", "see", "--title", $WindowTitle, "--json") -ExpectedOutput '"snapshot"'
    $seeEnvelope = ConvertFrom-PeekwinJson -Json $seeJson
    if (-not $seeEnvelope.success -or -not $seeEnvelope.data.snapshot.id) {
        throw "Expected see JSON to include snapshot metadata."
    }

    $refTarget = @($seeEnvelope.data.elements | Where-Object { $_.depth -ge 1 -and $_.bounds.width -gt 0 -and $_.bounds.height -gt 0 }) | Select-Object -First 1
    if (-not $refTarget) {
        throw "Expected see to return at least one usable child ref."
    }

    return $refTarget.ref
}

Push-Location $workspaceRoot
try {
    $expectedVersion = Get-PeekwinVersion

    if (-not $SkipBuild) {
        Write-Host "Building peekwin ($Configuration)..."
        dotnet build $ProjectPath -c $Configuration | Out-Host
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet build failed with exit code $LASTEXITCODE."
        }
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

    $refImageOutput = Join-Path $workspaceRoot "artifacts\smoke-test-ref.png"
    if (Test-Path $refImageOutput) {
        Remove-Item $refImageOutput -Force
    }

    Invoke-PeekwinCommand -Name "version" -Args @("run", "--project", $ProjectPath, "--", "version") -ExpectedOutput $expectedVersion | Out-Null
    Invoke-PeekwinCommand -Name "mcp help" -Args @("run", "--project", $ProjectPath, "--", "mcp", "--help") -ExpectedOutput "window_list" | Out-Null
    Invoke-PeekwinCommand -Name "leading verbose version" -Args @("run", "--project", $ProjectPath, "--", "--verbose", "version") -ExpectedOutput $expectedVersion | Out-Null
    Invoke-PeekwinCommand -Name "move help" -Args @("run", "--project", $ProjectPath, "--", "move", "--help") -ExpectedOutput "--ref <id>" | Out-Null
    Invoke-PeekwinCommand -Name "type help" -Args @("run", "--project", $ProjectPath, "--", "type", "--help") -ExpectedOutput "--ref <id>" | Out-Null
    Invoke-PeekwinCommand -Name "image help" -Args @("run", "--project", $ProjectPath, "--", "image", "--help") -ExpectedOutput "--ref <id>" | Out-Null
    Invoke-PeekwinCommand -Name "screens help" -Args @("run", "--project", $ProjectPath, "--", "screens", "--help") -ExpectedOutput "zero-based" | Out-Null

    $visibleWindowsJson = Invoke-PeekwinCommand -Name "window list json" -Args @("run", "--project", $ProjectPath, "--", "window", "list", "--json") -ExpectedOutput '"command": "window list"'
    $allWindowsJson = Invoke-PeekwinCommand -Name "window list all json" -Args @("run", "--project", $ProjectPath, "--", "window", "list", "--all", "--json") -ExpectedOutput '"windows"'
    $appsJson = Invoke-PeekwinCommand -Name "app list json" -Args @("run", "--project", $ProjectPath, "--", "app", "list", "--json") -ExpectedOutput '"apps"'
    $screensJson = Invoke-PeekwinCommand -Name "screens json" -Args @("run", "--project", $ProjectPath, "--", "screens", "--json") -ExpectedOutput '"screenIndexBase"'
    $imageInfoJson = Invoke-PeekwinCommand -Name "image info json" -Args @("run", "--project", $ProjectPath, "--", "image", "info", "--json") -ExpectedOutput '"screenIndexBase"'
    $screenshotInfoJson = Invoke-PeekwinCommand -Name "screenshot info json" -Args @("run", "--project", $ProjectPath, "--", "screenshot", "info", "--json") -ExpectedOutput '"screenIndexBase"'
    $desktopCurrentJson = Invoke-PeekwinCommand -Name "desktop current json" -Args @("run", "--project", $ProjectPath, "--", "desktop", "current", "--json") -ExpectedOutput '"desktop"'
    Invoke-PeekwinCommand -Name "parse error json" -Args @("run", "--project", $ProjectPath, "--", "window", "list", "extra", "--json") -ExpectedExitCode 1 -ExpectedOutput '"success": false' | Out-Null
    Invoke-PeekwinCommand -Name "image" -Args @("run", "--project", $ProjectPath, "--", "image", "--screen", "0", "--output", $resolvedOutput) | Out-Null
    Invoke-PeekwinCommand -Name "sleep json" -Args @("run", "--project", $ProjectPath, "--", "sleep", "1", "--json") -ExpectedOutput '"durationMs"' | Out-Null

    $visibleEnvelope = ConvertFrom-PeekwinJson -Json $visibleWindowsJson
    $allEnvelope = ConvertFrom-PeekwinJson -Json $allWindowsJson
    $appsEnvelope = ConvertFrom-PeekwinJson -Json $appsJson
    $screensEnvelope = ConvertFrom-PeekwinJson -Json $screensJson
    $imageInfoEnvelope = ConvertFrom-PeekwinJson -Json $imageInfoJson
    $screenshotInfoEnvelope = ConvertFrom-PeekwinJson -Json $screenshotInfoJson
    $desktopCurrentEnvelope = ConvertFrom-PeekwinJson -Json $desktopCurrentJson

    if (-not $visibleEnvelope.success -or -not $allEnvelope.success -or -not $appsEnvelope.success -or -not $screensEnvelope.success -or -not $imageInfoEnvelope.success -or -not $screenshotInfoEnvelope.success -or -not $desktopCurrentEnvelope.success) {
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

    if ($screensEnvelope.data.screenIndexBase -ne 0 -or $imageInfoEnvelope.data.screenIndexBase -ne 0 -or $screenshotInfoEnvelope.data.screenIndexBase -ne 0) {
        throw "Expected zero-based screen indexes from screens/image info/screenshot info."
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
            Invoke-PeekwinCommand -Name "type positional text" -Args @("run", "--project", $ProjectPath, "--", "type", "v", "--delay-ms", "1") | Out-Null
            Invoke-PeekwinCommand -Name "paste positional" -Args @("run", "--project", $ProjectPath, "--", "paste", "hello") | Out-Null
            Invoke-PeekwinCommand -Name "move duration" -Args @("run", "--project", $ProjectPath, "--", "move", "--x", "100", "--y", "100", "--duration-ms", "25") | Out-Null
            Invoke-PeekwinCommand -Name "double click" -Args @("run", "--project", $ProjectPath, "--", "click", "--x", "100", "--y", "100", "--double") | Out-Null
            Invoke-PeekwinCommand -Name "scroll" -Args @("run", "--project", $ProjectPath, "--", "scroll", "--delta", "-120") | Out-Null
            Invoke-PeekwinCommand -Name "hold keys" -Args @("run", "--project", $ProjectPath, "--", "hold", "shift", "ctrl", "--duration-ms", "25") | Out-Null
            Invoke-PeekwinCommand -Name "hold mouse" -Args @("run", "--project", $ProjectPath, "--", "hold", "--button", "left", "--duration-ms", "25") | Out-Null
            Invoke-PeekwinCommand -Name "desktop list" -Args @("run", "--project", $ProjectPath, "--", "desktop", "list") | Out-Null

            $clickRef = Get-UsableChildRef -ProjectPath $ProjectPath -WindowTitle "Notepad"
            Invoke-PeekwinCommand -Name "click ref json" -Args @("run", "--project", $ProjectPath, "--", "click", "--ref", $clickRef, "--json") -ExpectedOutput '"message"' | Out-Null
            $imageRef = Get-UsableChildRef -ProjectPath $ProjectPath -WindowTitle "Notepad"
            Invoke-PeekwinCommand -Name "image ref" -Args @("run", "--project", $ProjectPath, "--", "image", "--ref", $imageRef, "--output", $refImageOutput) | Out-Null
            if (-not (Test-Path $refImageOutput)) {
                throw "Expected ref image output was not created: $refImageOutput"
            }
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
