param(
    [string]$Configuration = 'Debug',
    [string]$Environment = 'Development',
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'

$sampleRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectPath = Join-Path $sampleRoot 'BlazorSandboxServer\BlazorSandboxServer.csproj'

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
    '--launch-profile'
    'BlazorSandboxServer'
)

Write-Host 'Starting BlazorSandboxServer.'
Write-Host 'Sample route: /drawn'

if ($DryRun) {
    Write-Host ("dotnet " + ($runArgs -join ' '))
    return
}

& dotnet @runArgs