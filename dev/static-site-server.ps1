param (
    [Parameter(Mandatory = $true)]
    [string]$RootPath,

    [string]$HostName = "localhost",
    [int]$Port = 8080
)

$resolvedRoot = Resolve-Path $RootPath
$listener = [System.Net.HttpListener]::new()
$listener.Prefixes.Add("http://${HostName}:${Port}/")

function Get-ContentType {
    param (
        [string]$Path
    )

    switch ([System.IO.Path]::GetExtension($Path).ToLowerInvariant()) {
        '.html' { return 'text/html; charset=utf-8' }
        '.htm' { return 'text/html; charset=utf-8' }
        '.css' { return 'text/css; charset=utf-8' }
        '.js' { return 'application/javascript; charset=utf-8' }
        '.json' { return 'application/json; charset=utf-8' }
        '.svg' { return 'image/svg+xml' }
        '.png' { return 'image/png' }
        '.jpg' { return 'image/jpeg' }
        '.jpeg' { return 'image/jpeg' }
        '.gif' { return 'image/gif' }
        '.webp' { return 'image/webp' }
        '.ico' { return 'image/x-icon' }
        '.map' { return 'application/json; charset=utf-8' }
        '.txt' { return 'text/plain; charset=utf-8' }
        '.xml' { return 'application/xml; charset=utf-8' }
        '.woff' { return 'font/woff' }
        '.woff2' { return 'font/woff2' }
        default { return 'application/octet-stream' }
    }
}

try {
    $listener.Start()

    while ($listener.IsListening) {
        $context = $listener.GetContext()
        try {
            $requestPath = [System.Uri]::UnescapeDataString($context.Request.Url.AbsolutePath.TrimStart('/'))
            if ([string]::IsNullOrWhiteSpace($requestPath)) {
                $requestPath = 'index.html'
            }

            $localPath = Join-Path $resolvedRoot ($requestPath -replace '/', '\\')
            if (Test-Path $localPath -PathType Container) {
                $localPath = Join-Path $localPath 'index.html'
            }

            if (-not (Test-Path $localPath -PathType Leaf)) {
                $context.Response.StatusCode = 404
                $context.Response.Headers['Cache-Control'] = 'no-store, no-cache, must-revalidate, max-age=0'
                $context.Response.Headers['Pragma'] = 'no-cache'
                $context.Response.Headers['Expires'] = '0'
                $bytes = [System.Text.Encoding]::UTF8.GetBytes('Not Found')
                $context.Response.OutputStream.Write($bytes, 0, $bytes.Length)
                continue
            }

            $context.Response.ContentType = Get-ContentType -Path $localPath
            $context.Response.Headers['Cache-Control'] = 'no-store, no-cache, must-revalidate, max-age=0'
            $context.Response.Headers['Pragma'] = 'no-cache'
            $context.Response.Headers['Expires'] = '0'
            $fileStream = [System.IO.File]::Open($localPath, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::ReadWrite)
            try {
                $context.Response.ContentLength64 = $fileStream.Length
                $fileStream.CopyTo($context.Response.OutputStream)
            }
            finally {
                $fileStream.Dispose()
            }
        }
        finally {
            $context.Response.OutputStream.Close()
        }
    }
}
finally {
    if ($listener.IsListening) {
        $listener.Stop()
    }

    $listener.Close()
}