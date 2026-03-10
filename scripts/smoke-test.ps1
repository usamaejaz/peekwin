param(
    [string]$Configuration = "Debug",
    [string]$ProjectPath = ".\src\peekwin.csproj",
    [string]$OutputPath = ".\artifacts\smoke-test.png"
)

$ErrorActionPreference = "Stop"

if (-not $IsWindows) {
    throw "peekwin smoke tests must run on Windows."
}

$workspaceRoot = Split-Path -Parent $PSScriptRoot
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

    $commands = @(
        @{ Name = "version"; Args = @("run", "--project", $ProjectPath, "--", "version") },
        @{ Name = "window list json"; Args = @("run", "--project", $ProjectPath, "--", "window", "list", "--json") },
        @{ Name = "screenshot"; Args = @("run", "--project", $ProjectPath, "--", "screenshot", "--output", $resolvedOutput) }
    )

    foreach ($command in $commands) {
        Write-Host "Running $($command.Name)..."
        & dotnet @($command.Args)
        if ($LASTEXITCODE -ne 0) {
            throw "Command '$($command.Name)' failed with exit code $LASTEXITCODE."
        }
    }

    if (-not (Test-Path $resolvedOutput)) {
        throw "Expected screenshot output was not created: $resolvedOutput"
    }

    Write-Host "Smoke test completed successfully."
    Write-Host "Screenshot saved to $resolvedOutput"
}
finally {
    Pop-Location
}
