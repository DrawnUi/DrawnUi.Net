param(
    [string]$OutputRoot = ".\docs\_site"
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot

if ([System.IO.Path]::IsPathRooted($OutputRoot)) {
    $publishRoot = [System.IO.Path]::GetFullPath($OutputRoot)
} else {
    $publishRoot = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $OutputRoot))
}

if (-not (Test-Path -LiteralPath $publishRoot)) {
    New-Item -ItemType Directory -Path $publishRoot -Force | Out-Null
}

$samples = @(
    @{
        Name = 'sandbox'
        Project = 'src/Blazor/Samples/BlazorSandbox/BlazorSandbox.csproj'
        BaseHref = '/sandbox/'
    }
)

foreach ($sample in $samples) {
    $projectPath = Join-Path $repoRoot $sample.Project
    $destination = Join-Path $publishRoot $sample.Name
    $publishOutput = Join-Path $destination '_publish'

    if (Test-Path -LiteralPath $destination) {
        Remove-Item -LiteralPath $destination -Recurse -Force
    }

    New-Item -ItemType Directory -Path $destination -Force | Out-Null

    $publishArgs = @(
        'publish'
        $projectPath
        '--configuration'
        'Release'
        '--output'
        $publishOutput
        "-p:BaseHref=$($sample.BaseHref)"
        '-p:NoWarn=1701%3B1702%3BXLS0505%3BCS0108'
    )

    & dotnet @publishArgs

    $siteRoot = Join-Path $publishOutput 'wwwroot'
    if (-not (Test-Path -LiteralPath $siteRoot)) {
        $siteRoot = $publishOutput
    }

    $indexPath = Join-Path $siteRoot 'index.html'
    if (-not (Test-Path -LiteralPath $indexPath)) {
        throw "Publish failed for sample '$($sample.Name)': index.html not found."
    }

    $indexHtml = Get-Content -LiteralPath $indexPath -Raw
    if ($indexHtml.Contains('%BASE_HREF%')) {
        $indexHtml = $indexHtml.Replace('%BASE_HREF%', $sample.BaseHref)
    } else {
        $baseTagPattern = '<base\s+href="[^"]*"\s*/?>'
        $baseTagReplacement = [string]::Format('<base href="{0}" />', $sample.BaseHref)
        $updatedIndexHtml = [System.Text.RegularExpressions.Regex]::Replace($indexHtml, $baseTagPattern, $baseTagReplacement, 1)

        if ($updatedIndexHtml -eq $indexHtml) {
            throw "Publish failed for sample '$($sample.Name)': base href tag not found."
        }

        $indexHtml = $updatedIndexHtml
    }

    Set-Content -LiteralPath $indexPath -Value $indexHtml -NoNewline

    Get-ChildItem -LiteralPath $siteRoot -Force | ForEach-Object {
        Move-Item -LiteralPath $_.FullName -Destination $destination -Force
    }

    if (Test-Path -LiteralPath $publishOutput) {
        Remove-Item -LiteralPath $publishOutput -Recurse -Force
    }
}