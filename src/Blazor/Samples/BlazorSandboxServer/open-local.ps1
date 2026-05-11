param(
    [switch]$UseHttps
)

$ErrorActionPreference = 'Stop'

$baseUrl = if ($UseHttps) { 'https://localhost:53077' } else { 'http://localhost:53078' }
$target = '{0}/drawn' -f $baseUrl.TrimEnd('/')

Write-Host ("Opening {0}" -f $target)
Start-Process $target