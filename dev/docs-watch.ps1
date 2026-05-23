param (
    [int]$Port = 8080,
    [string]$HostName = "localhost",
    [int]$DebounceMilliseconds = 750,
    [string]$ConfigPath = "docfx.json",
    [string]$SiteDirectory = "_site",
    [string]$PostBuildScriptPath,
    [string[]]$AdditionalWatchPaths = @(),
    [switch]$SkipInitialBuild
)

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Resolve-Path (Join-Path $scriptRoot "..")
$docsRoot = Resolve-Path (Join-Path $repoRoot "docs")
$siteRoot = Join-Path $docsRoot $SiteDirectory
$configFilePath = Join-Path $docsRoot $ConfigPath
$staticServerScript = Join-Path $scriptRoot "static-site-server.ps1"
$resolvedPostBuildScriptPath = $null
$watchRoots = [System.Collections.Generic.List[string]]::new()

$ignoredSegments = @(
    "\_site\",
    "\_site-content\",
    "\bin\",
    "\obj\",
    "\node_modules\",
    "\.git\"
)

$script:pendingBuild = $false
$script:lastChangeAt = [DateTime]::MinValue
$script:isBuilding = $false
$script:fileSnapshot = @{}

function Ensure-Docfx {
    $docfx = Get-Command docfx -ErrorAction SilentlyContinue
    if ($null -ne $docfx) {
        return $docfx.Source
    }

    Write-Host "DocFX not found in PATH. Installing DocFX as global tool..."
    dotnet tool install --global docfx
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to install DocFX."
    }

    $docfx = Get-Command docfx -ErrorAction SilentlyContinue
    if ($null -eq $docfx) {
        throw "DocFX is still unavailable after installation."
    }

    return $docfx.Source
}

function Should-IgnorePath {
    param (
        [string]$Path
    )

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return $true
    }

    $normalized = $Path.Replace('/', '\')
    foreach ($segment in $ignoredSegments) {
        if ($normalized.IndexOf($segment, [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
            return $true
        }
    }

    return $false
}

function Invoke-DocsBuild {
    if ($script:isBuilding) {
        return
    }

    $script:isBuilding = $true
    try {
        Push-Location $docsRoot
        try {
            Write-Host "[$(Get-Date -Format HH:mm:ss)] Building docs using $ConfigPath..."
            docfx build $ConfigPath --disableGitFeatures
            if ($LASTEXITCODE -eq 0) {
                if ($resolvedPostBuildScriptPath) {
                    Write-Host "[$(Get-Date -Format HH:mm:ss)] Running post-build step: $resolvedPostBuildScriptPath"
                    & $resolvedPostBuildScriptPath
                    if ($LASTEXITCODE -ne 0) {
                        Write-Warning "Post-build step failed with exit code $LASTEXITCODE."
                    }
                }

                Write-Host "[$(Get-Date -Format HH:mm:ss)] Build completed."
            }
            else {
                Write-Warning "DocFX build failed with exit code $LASTEXITCODE."
            }
        }
        finally {
            Pop-Location
        }
    }
    finally {
        $script:isBuilding = $false
    }
}

function Get-DocsFileSnapshot {
    $snapshot = @{}

    foreach ($watchRoot in $watchRoots) {
        $items = Get-ChildItem -Path $watchRoot -Recurse -File -ErrorAction SilentlyContinue
        foreach ($item in $items) {
            if (Should-IgnorePath -Path $item.FullName) {
                continue
            }

            $snapshot[$item.FullName] = $item.LastWriteTimeUtc.Ticks
        }
    }

    return $snapshot
}

function Get-ChangedDocsPaths {
    param (
        [hashtable]$PreviousSnapshot,
        [hashtable]$CurrentSnapshot
    )

    $changedPaths = [System.Collections.Generic.List[string]]::new()

    foreach ($path in $CurrentSnapshot.Keys) {
        if (-not $PreviousSnapshot.ContainsKey($path) -or $PreviousSnapshot[$path] -ne $CurrentSnapshot[$path]) {
            $changedPaths.Add($path)
        }
    }

    foreach ($path in $PreviousSnapshot.Keys) {
        if (-not $CurrentSnapshot.ContainsKey($path)) {
            $changedPaths.Add($path)
        }
    }

    return $changedPaths
}

function Kill-PortOwner {
    param ([int]$TargetPort)
    try {
        $connections = netstat -ano | Select-String ":$TargetPort\s"
        foreach ($line in $connections) {
            $parts = ($line.Line.Trim() -split '\s+')
            $pid = $parts[-1]
            if ($pid -match '^\d+$' -and [int]$pid -ne $PID) {
                Stop-Process -Id ([int]$pid) -Force -ErrorAction SilentlyContinue
            }
        }
    }
    catch { }
}

function Start-DocsServer {
    param ()

    if (-not (Test-Path $siteRoot)) {
        New-Item -ItemType Directory -Path $siteRoot | Out-Null
    }

    Kill-PortOwner -TargetPort $Port
    Start-Sleep -Milliseconds 300

    $powershellExe = (Get-Process -Id $PID).Path
    $arguments = @(
        '-ExecutionPolicy', 'Bypass',
        '-File', $staticServerScript,
        '-RootPath', $siteRoot,
        '-HostName', $HostName,
        '-Port', $Port
    )

    $process = Start-Process -FilePath $powershellExe -ArgumentList $arguments -WorkingDirectory $docsRoot -PassThru -WindowStyle Hidden
    Start-Sleep -Milliseconds 500
    if ($process.HasExited) {
        throw "Static server failed to start."
    }

    return $process
}

function Ensure-DocsServer {
    param (
        $Process
    )

    if ($null -ne $Process -and -not $Process.HasExited) {
        return $Process
    }

    if ($null -ne $Process -and $Process.HasExited) {
        Write-Warning "Static server exited. Restarting it."
    }

    return Start-DocsServer
}

$docfxPath = Ensure-Docfx
$serverProcess = $null

try {
    if (-not (Test-Path $configFilePath)) {
        throw "DocFX config not found: $configFilePath"
    }

    if ($PostBuildScriptPath) {
        if ([System.IO.Path]::IsPathRooted($PostBuildScriptPath)) {
            $resolvedPostBuildScriptPath = [System.IO.Path]::GetFullPath($PostBuildScriptPath)
        }
        else {
            $resolvedPostBuildScriptPath = [System.IO.Path]::GetFullPath((Join-Path $scriptRoot $PostBuildScriptPath))
        }

        if (-not (Test-Path -LiteralPath $resolvedPostBuildScriptPath)) {
            throw "Post-build script not found: $resolvedPostBuildScriptPath"
        }
    }

    $watchRoots.Add([System.IO.Path]::GetFullPath($docsRoot))
    foreach ($watchPath in $AdditionalWatchPaths) {
        if ([string]::IsNullOrWhiteSpace($watchPath)) {
            continue
        }

        $resolvedWatchPath = if ([System.IO.Path]::IsPathRooted($watchPath)) {
            [System.IO.Path]::GetFullPath($watchPath)
        }
        else {
            [System.IO.Path]::GetFullPath((Join-Path $repoRoot $watchPath))
        }

        if (-not (Test-Path -LiteralPath $resolvedWatchPath)) {
            throw "Watch path not found: $resolvedWatchPath"
        }

        if (-not $watchRoots.Contains($resolvedWatchPath)) {
            $watchRoots.Add($resolvedWatchPath)
        }
    }

    Write-Host "Starting DrawnUi docs preview at http://$HostName`:$Port"
    $serverProcess = Start-DocsServer
    Write-Host "Static server is live. Press Ctrl+C to stop watching."

    if (-not $SkipInitialBuild) {
        Invoke-DocsBuild
    }

    $script:fileSnapshot = Get-DocsFileSnapshot

    while ($true) {
        Start-Sleep -Milliseconds 250

        $serverProcess = Ensure-DocsServer -Process $serverProcess

        $currentSnapshot = Get-DocsFileSnapshot
        $changedPaths = Get-ChangedDocsPaths -PreviousSnapshot $script:fileSnapshot -CurrentSnapshot $currentSnapshot
        if ($changedPaths.Count -gt 0 -and -not $script:pendingBuild) {
            $script:pendingBuild = $true
            $script:lastChangeAt = Get-Date

            foreach ($path in $changedPaths | Select-Object -First 5) {
                Write-Host "[$(Get-Date -Format HH:mm:ss)] Change detected: $path"
            }
        }

        if (-not $script:pendingBuild) {
            continue
        }

        $elapsed = (Get-Date) - $script:lastChangeAt
        if ($elapsed.TotalMilliseconds -lt $DebounceMilliseconds) {
            continue
        }

        $script:pendingBuild = $false
        Invoke-DocsBuild
        $script:fileSnapshot = Get-DocsFileSnapshot
    }
}
finally {
    if ($null -ne $serverProcess -and -not $serverProcess.HasExited) {
        Stop-Process -Id $serverProcess.Id -Force -ErrorAction SilentlyContinue
    }
}