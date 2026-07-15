# Full docs rebuild: regenerates API yaml (docfx metadata) + builds site + serves at http://localhost:8080.
# Pre-publish check, mirrors CI (.github/workflows/docfx.yml). For fast article editing use ./watch.ps1.
param([switch]$NoServe)

if (-not (Get-Command docfx -ErrorAction SilentlyContinue)) {
    Write-Error "docfx not found. Install: dotnet tool install --global docfx"
    exit 1
}

$docs = Split-Path -Parent $MyInvocation.MyCommand.Path
Push-Location $docs
try {
    if (Test-Path _site) { Remove-Item _site -Recurse -Force }

    # metadata overwrites api/*.yml in place; orphans of deleted classes are removed
    # only AFTER metadata succeeds, so a failed/interrupted run never leaves api/ empty
    $before = Get-Date
    docfx metadata ./docfx.json
    if ($LASTEXITCODE -ne 0) { Write-Error "docfx metadata failed ($LASTEXITCODE); api/*.yml left untouched"; exit $LASTEXITCODE }
    if (Test-Path api) {
        Get-ChildItem api -Filter *.yml -File | Where-Object { $_.LastWriteTime -lt $before } | Remove-Item -Force
    }

    docfx build ./docfx.json
    if ($LASTEXITCODE -ne 0) { Write-Error "docfx build failed ($LASTEXITCODE)"; exit $LASTEXITCODE }

    if (-not $NoServe) {
        Write-Host "Serving http://localhost:8080 (Ctrl+C to stop)" -ForegroundColor Green
        docfx serve _site
    }
} finally { Pop-Location }
