param (
    [int]$Port = 8080,
    [string]$HostName = "localhost",
    [int]$DebounceMilliseconds = 750,
    [switch]$SkipInitialBuild
)

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path

& (Join-Path $scriptRoot "docs-watch.ps1") `
    -Port $Port `
    -HostName $HostName `
    -DebounceMilliseconds $DebounceMilliseconds `
    -ConfigPath "docfx.content.json" `
    -SiteDirectory "_site-content" `
    -SkipInitialBuild:$SkipInitialBuild