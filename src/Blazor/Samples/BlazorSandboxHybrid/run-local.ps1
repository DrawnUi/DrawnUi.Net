param(
    [string]$Configuration = 'Debug',
    [string]$Environment = 'Development',
    [string]$Url = 'http://localhost:5177',
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'

$sampleRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectPath = Join-Path $sampleRoot 'BlazorSandboxHybrid\BlazorSandboxHybrid.csproj'

if (-not (Test-Path -LiteralPath $projectPath)) {
    throw "Could not find project file at '$projectPath'."
}

$env:ASPNETCORE_ENVIRONMENT = $Environment

$runArgs = @(
    'run'
    '--project'
    $projectPath
    '--configuration'
    $Configuration
    '--urls'
    $Url
)

Write-Host 'Starting BlazorSandboxHybrid.'
Write-Host ("Sample route: {0}/hybrid" -f $Url.TrimEnd('/'))

if ($DryRun) {
    Write-Host ("dotnet " + ($runArgs -join ' '))
    return
}

& dotnet @runArgs