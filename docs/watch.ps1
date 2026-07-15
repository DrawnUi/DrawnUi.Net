# Live docs editing: watches articles, rebuilds on save (content-only config, no API metadata), serves with reload.
# For the full rebuild including API yaml use ./build.ps1.
$docs = Split-Path -Parent $MyInvocation.MyCommand.Path
& (Join-Path $docs '..\dev\docs-watch.ps1') -ConfigPath 'docfx.content.json' -SiteDirectory '_site-content'
