param (
    [int]$Port = 8080,
    [string]$HostName = "localhost",
    [int]$DebounceMilliseconds = 750,
    [switch]$SkipInitialBuild
)

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$publishScript = Join-Path $scriptRoot "publish-wasm-samples.ps1"

& (Join-Path $scriptRoot "docs-watch.ps1") `
    -Port $Port `
    -HostName $HostName `
    -DebounceMilliseconds $DebounceMilliseconds `
    -PostBuildScriptPath $publishScript `
    -AdditionalWatchPaths @(
        'src/Blazor',
        '..\AppoMobi.Maui.Gestures\src'
    ) `
    -SkipInitialBuild:$SkipInitialBuild
