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

function Get-JsonArrayCount {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Json
    )

    $parsed = $Json | ConvertFrom-Json
    if ($parsed -is [System.Array]) {
        return $parsed.Count
    }

    return @($parsed).Count
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
    $visibleWindowsJson = Invoke-PeekwinCommand -Name "window list json" -Args @("run", "--project", $ProjectPath, "--", "window", "list", "--json") -ExpectedOutput "desktopLabel"
    $allWindowsJson = Invoke-PeekwinCommand -Name "window list all json" -Args @("run", "--project", $ProjectPath, "--", "window", "list", "--all", "--json") -ExpectedOutput "desktopLabel"
    Invoke-PeekwinCommand -Name "parse error" -Args @("run", "--project", $ProjectPath, "--", "window", "list", "extra") -ExpectedExitCode 1 -ExpectedOutput "Invalid arguments: Unexpected token: extra" | Out-Null
    Invoke-PeekwinCommand -Name "screenshot info" -Args @("run", "--project", $ProjectPath, "--", "screenshot", "info", "--json") -ExpectedOutput "virtualBounds" | Out-Null
    Invoke-PeekwinCommand -Name "screenshot" -Args @("run", "--project", $ProjectPath, "--", "screenshot", "--output", $resolvedOutput) | Out-Null

    $visibleCount = Get-JsonArrayCount -Json $visibleWindowsJson
    $allCount = Get-JsonArrayCount -Json $allWindowsJson
    if ($allCount -lt $visibleCount) {
        throw "Expected '--all' window count ($allCount) to be greater than or equal to visible-only count ($visibleCount)."
    }

    if (-not (Test-Path $resolvedOutput)) {
        throw "Expected screenshot output was not created: $resolvedOutput"
    }

    if ($IncludeInputInjection) {
        Write-Host "Starting input-injection coverage..."
        $notepad = Start-Process -FilePath "notepad.exe" -PassThru
        try {
            Start-Sleep -Milliseconds 500
            Invoke-PeekwinCommand -Name "focus notepad" -Args @("run", "--project", $ProjectPath, "--", "window", "focus", "--title", "Notepad") | Out-Null
            Invoke-PeekwinCommand -Name "inspect notepad by title" -Args @("run", "--project", $ProjectPath, "--", "window", "inspect", "--title", "Notepad", "--json") -ExpectedOutput "automationId" | Out-Null
            Invoke-PeekwinCommand -Name "type literal -v" -Args @("run", "--project", $ProjectPath, "--", "type", "--text", "-v", "--delay-ms", "1") | Out-Null
            Invoke-PeekwinCommand -Name "double click" -Args @("run", "--project", $ProjectPath, "--", "click", "--x", "100", "--y", "100", "--double") | Out-Null
            Invoke-PeekwinCommand -Name "hold key" -Args @("run", "--project", $ProjectPath, "--", "hold", "key", "--key", "shift", "--duration-ms", "25") | Out-Null
            Invoke-PeekwinCommand -Name "hold mouse" -Args @("run", "--project", $ProjectPath, "--", "hold", "mouse", "--button", "left", "--duration-ms", "25") | Out-Null
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
    Write-Host "Screenshot saved to $resolvedOutput"
}
finally {
    Pop-Location
}
