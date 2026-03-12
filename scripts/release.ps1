param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$Version,
    [string]$Remote = "origin",
    [string]$Branch,
    [switch]$NoPush,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

if (-not $IsWindows) {
    throw "Release helper must run on Windows."
}

if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    throw "Version must use semver-like x.y.z format."
}

$tag = "v$Version"
$workspaceRoot = Split-Path -Parent $PSScriptRoot
$propsPath = Join-Path $workspaceRoot "Directory.Build.props"

function Invoke-Git {
    param(
        [Parameter(ValueFromRemainingArguments = $true)]
        [string[]]$Args
    )

    & git @Args
    if ($LASTEXITCODE -ne 0) {
        throw "git $($Args -join ' ') failed with exit code $LASTEXITCODE."
    }
}

function Get-GitOutput {
    param(
        [Parameter(ValueFromRemainingArguments = $true)]
        [string[]]$Args
    )

    $output = & git @Args
    if ($LASTEXITCODE -ne 0) {
        throw "git $($Args -join ' ') failed with exit code $LASTEXITCODE."
    }

    return [string]::Join("`n", $output).Trim()
}

Push-Location $workspaceRoot
try {
    $status = Get-GitOutput status --porcelain
    if (-not [string]::IsNullOrWhiteSpace($status)) {
        throw "Working tree is not clean. Commit or stash changes before running release.ps1."
    }

    if (-not $Branch) {
        $Branch = Get-GitOutput branch --show-current
        if (-not $Branch) {
            throw "Could not determine the current branch. Pass -Branch explicitly."
        }
    }

    $existingLocalTag = Get-GitOutput tag --list $tag
    if ($existingLocalTag) {
        throw "Tag $tag already exists locally."
    }

    Invoke-Git fetch $Remote --tags
    $existingRemoteTag = Get-GitOutput ls-remote --tags $Remote $tag
    if ($existingRemoteTag) {
        throw "Tag $tag already exists on $Remote."
    }

    $propsText = Get-Content -Path $propsPath -Raw
    $updatedPropsText = $propsText
    $updatedPropsText = [regex]::Replace($updatedPropsText, '<PeekWinVersion>[^<]+</PeekWinVersion>', "<PeekWinVersion>$Version</PeekWinVersion>")
    $updatedPropsText = [regex]::Replace($updatedPropsText, '<AssemblyVersion>[^<]+</AssemblyVersion>', "<AssemblyVersion>$Version.0</AssemblyVersion>")
    $updatedPropsText = [regex]::Replace($updatedPropsText, '<FileVersion>[^<]+</FileVersion>', "<FileVersion>$Version.0</FileVersion>")

    if ($updatedPropsText -eq $propsText) {
        throw "Directory.Build.props did not change."
    }

    if ($DryRun) {
        Write-Host "Dry run only. No files or git refs were changed."
        Write-Host "Version: $Version"
        Write-Host "Tag: $tag"
        Write-Host "Branch: $Branch"
        Write-Host "Remote: $Remote"
        if ($NoPush) {
            Write-Host "Push: disabled"
        }
        else {
            Write-Host "Push: branch + tag"
        }

        return
    }

    Set-Content -Path $propsPath -Value $updatedPropsText

    Invoke-Git add Directory.Build.props
    Invoke-Git commit -m "Bump version to $Version"
    Invoke-Git tag -a $tag -m $tag

    if (-not $NoPush) {
        Invoke-Git push $Remote $Branch
        Invoke-Git push $Remote $tag
    }

    Write-Host "Released $tag from branch $Branch."
    if ($NoPush) {
        Write-Host "Branch and tag were created locally only."
    }
}
finally {
    Pop-Location
}
