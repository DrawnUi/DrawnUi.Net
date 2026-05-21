param(
    [string]$Url = 'http://localhost:5177'
)

$ErrorActionPreference = 'Stop'

$target = '{0}/hybrid' -f $Url.TrimEnd('/')

Write-Host ("Opening {0}" -f $target)
Start-Process $target