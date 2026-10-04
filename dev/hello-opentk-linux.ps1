# Builds HelloOpenTk for Linux on Windows and runs it in WSLg (X11), next to HelloRust for comparison.
#   pwsh dev\hello-opentk-linux.ps1            publish, copy into WSL (~/hello-opentk), start
#   pwsh dev\hello-opentk-linux.ps1 -NoBuild   start the copy already in WSL
# No .NET SDK is needed inside WSL: the build is self-contained. The app keeps running after the script returns.
param(
    [switch]$NoBuild,
    [string]$Distro = "Ubuntu-22.04"
)
$ErrorActionPreference = "Stop"
$repo = Split-Path $PSScriptRoot -Parent
$out = Join-Path $repo "src\OpenTk\Samples\HelloOpenTk\bin\publish-linux-x64"

if (-not $NoBuild) {
    dotnet publish (Join-Path $repo "src\OpenTk\Samples\HelloOpenTk\HelloOpenTk.csproj") -c Release -r linux-x64 --self-contained -o $out
    if ($LASTEXITCODE -ne 0) { throw "publish failed" }
    $wslOut = (wsl -d $Distro -e wslpath -a ($out -replace '\\', '/')).Trim()
    wsl -d $Distro -e bash -c "rm -rf ~/hello-opentk && cp -r '$wslOut' ~/hello-opentk && chmod +x ~/hello-opentk/HelloOpenTk"
    if ($LASTEXITCODE -ne 0) { throw "copy into WSL failed" }
}

# X11, not Wayland (WAYLAND_DISPLAY unset). The wsl call must stay alive while the app runs: a background process.
Start-Process wsl -ArgumentList "-d $Distro -e bash -c `"cd ~/hello-opentk && env -u WAYLAND_DISPLAY DISPLAY=:0 ./HelloOpenTk`"" -WindowStyle Hidden
Write-Host "HelloOpenTk started in WSL ($Distro): window 'DrawnUI for OpenTK ($Distro)'"
