param(
    [int[]]$Ports = @(53077, 53078)
)

$ErrorActionPreference = 'Stop'

$connectionCommand = Get-Command Get-NetTCPConnection -ErrorAction SilentlyContinue

if (-not $connectionCommand) {
    throw 'Get-NetTCPConnection is not available on this machine.'
}

$processIds = $Ports |
    ForEach-Object {
        Get-NetTCPConnection -LocalPort $_ -State Listen -ErrorAction SilentlyContinue
    } |
    Select-Object -ExpandProperty OwningProcess -Unique

if (-not $processIds) {
    Write-Host 'No listening process found for the configured ports.'
    return
}

foreach ($processId in $processIds) {
    try {
        $process = Get-Process -Id $processId -ErrorAction Stop
        Write-Host ("Stopping PID {0} ({1})" -f $process.Id, $process.ProcessName)
        Stop-Process -Id $process.Id -Force
    } catch {
        Write-Warning $_
    }
}