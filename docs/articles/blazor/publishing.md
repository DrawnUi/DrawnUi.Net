# Publishing Blazor WASM: AOT and Server Compression

This article exists because of one very bad evening. A DrawnUI game that ran at 144 FPS on the dev machine was *barely playable* on a fresh PC — stuttering, freezing, feeling broken. And after we fixed that, the deploy made the site look completely dead for two minutes on first load. Both problems were self-inflicted, both are invisible on a developer machine, and both take five lines to fix once you know they exist.

If you publish a DrawnUI Blazor WebAssembly app and skip this page, you will ship a slideshow. Please don't.

## The trap: your Release build is NOT compiled

Here is the thing almost everyone gets wrong, and we got wrong too.

You publish with `-c Release`, you see `WasmBuildNative=true` in your csproj, and you think: "native, compiled, fast." **No.** `WasmBuildNative` only relinks the .NET *runtime* (needed when you bundle native libs like SkiaSharp). Your C# — the game loop, the layout passes, the gesture math, every single frame of your beautiful DrawnUI canvas — ships as IL and runs on the **Mono interpreter**. In production. For every user. Forever.

On your monster dev machine the interpreter keeps up and you never notice. On a mid-range laptop, a cheap tablet, someone's five-year-old PC? The interpreter chokes, and your user closes the tab thinking your app is junk. They will not file a bug report. They will just leave.

The fix is one line:

```xml
<PropertyGroup>
  <RunAOTCompilation>true</RunAOTCompilation>
</PropertyGroup>
```

That's it. That's the line that decides whether your users get compiled code or an interpreter.

### What it actually buys you

We measured this properly — same app, same browser, same 6x CPU throttle to simulate weak hardware, scripted identical gameplay:

| | Release, no AOT | Release + AOT |
|---|---|---|
| Main thread blocked by long tasks | **41%** of play time | **17%** |
| Steady FPS (throttled) | 23–35, dips to 5 | 40–80 |
| Stalls over 200ms | 25 | 6 |

Roughly **2x the framerate and 4x fewer visible hitches**, from one csproj line. There is no other single change in this entire framework that gives you that much.

And the bonus nobody expects: AOT does **not** bloat the download. Yes, `dotnet.native.wasm` grows about 5x (ours went 9.8 MB → 46 MB), but the IL gets stripped after compilation and native code compresses beautifully — our total Brotli payload actually went **down**, from 14.6 MB to 13.5 MB.

### The costs, honestly

- **Publish time**: ~1 minute becomes ~10 minutes. Every AOT publish recompiles all assemblies to wasm. Annoying, worth it.
- **Publish only**: `dotnet run` and Debug builds still use the interpreter. Your dev loop doesn't change — which is exactly why you never feel the production pain locally. Don't trust your dev-machine FPS. Ever.
- You need the `wasm-tools` workload installed (`dotnet workload install wasm-tools`) — if you already build with SkiaSharp/`WasmBuildNative`, you have it.

## The second trap: your server is serving 48 MB raw

This one hurt more, because we walked into it *immediately after* fixing the first one.

We enabled AOT, published, deployed — and the site stopped opening. Loader frozen, progress bar dead, looked completely broken. Panic. Rollback instinct. The actual cause? The publish output contains beautiful precompressed files — `dotnet.native.wasm.br` at 9.7 MB, `.gz` at 14.8 MB — sitting right there on the server, **and nginx was ignoring them**, serving the raw 46 MB wasm to every visitor.

Read that again: .NET compresses everything for you at publish time, for free, and the default nginx config throws that work away.

Before AOT, the raw file was 9.8 MB and nobody noticed the missing compression. AOT made the file 5x bigger and turned a hidden inefficiency into a dead site. The compression config was *always* wrong — AOT just sent the bill.

### The fix (nginx)

Install the brotli static module (Ubuntu):

```bash
apt-get install -y libnginx-mod-http-brotli-static
```

Add two lines to your server block:

```nginx
server {
    root /var/www/yourapp/www;

    brotli_static on;   # serves .br when the browser accepts it
    gzip_static on;     # fallback, module is built into stock nginx

    location / {
        try_files $uri $uri/ /index.html;
    }
}
```

Then `nginx -t && systemctl reload nginx`. Done. nginx now serves the precompressed files .NET already made — zero CPU cost per request, because nothing is compressed on the fly.

### Verify it. Do not assume. We assumed.

```bash
curl -sI -H "Accept-Encoding: br" https://yoursite.com/_framework/dotnet.native.<hash>.wasm | grep -iE "content-encoding|content-length"
```

You want to see `Content-Encoding: br` and a number around 10 MB, **not** a naked 48 MB `Content-Length`. This one curl command would have saved us the whole evening. Make it part of your deploy ritual.

## The loading bar will lie to you

One more thing we learned the hard way: the Blazor boot progress bar tracks *downloads only*. While the browser compiles a 46 MB wasm module, the bar sits completely frozen and your app looks hung — even when everything is fine. With compression fixed, the whole cold boot is a few seconds and this stops mattering. But if anyone ever tells you "the site is stuck at the loading bar," check `Content-Encoding` before you check anything else.

## A note on hand-patching deployed files

Blazor's integrity checking (those SHA-256 hashes in the boot manifest) covers **only** the `_framework` assets. Your static content — sounds, images, fonts outside the manifest — can be uploaded or replaced individually on the server without any integrity errors. Touching anything under `_framework` by hand, however, will break the boot. Republish instead.

## The checklist

Every DrawnUI Blazor WASM production deploy, in order:

1. `<RunAOTCompilation>true</RunAOTCompilation>` in the csproj — non-negotiable for anything animation- or game-like.
2. `dotnet publish -c Release` (budget ~10 minutes).
3. Server serves precompressed: `brotli_static on; gzip_static on;` (or your CDN equivalent).
4. **Verify with curl** that `Content-Encoding: br` actually comes back for `dotnet.native.*.wasm`.
5. Test on a weak machine or with DevTools CPU throttling at 6x — your dev box tells you nothing about your users.

Five steps. The difference between "this framework is amazing" and "this site is broken" lives entirely in this list.

## Related Docs

- [Blazor WebAssembly](wasm.md)
- [Blazor](index.md)
- [Blazor FAQ](faq.md)
