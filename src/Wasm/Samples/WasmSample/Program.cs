global using System.Runtime.InteropServices.JavaScript;
global using DrawnUi.Draw;
global using DrawnUi;
global using DrawnUi.Views;
global using SkiaSharp;

namespace DrawnUi.Web.Sample;

/*
 PowerShell($f="$env:TEMP\publish-testweb\wwwroot\_framework"; "RAW totals:"; "{0:N1} MB raw (all)" -f ((Get-ChildItem
             $f -File | Where-Object {$_.Name -notmatch '\.(br|gz)$'} | Measure-Object Length -Sum).Sum/1MB); "{0:N1} MB
             br (compressed transfer)" -f ((Get-ChildItem $f -File -Filter *.br | Measure-Object Length -Sum).Sum/1MB);
             ""; "Biggest assets (raw / .br):"; Get-ChildItem $f -File *.wasm | Sort-Object Length -Desc | Select-Object
             -First 6 | ForEach-Object { $br=Join-Path $f ($_.Name+'.br'); $brkb=if(Test-Path $br){'{0:N0} KB' -f
             ((Get-Item $br).Length/1KB)}else{'-'}; "{0,-40} {1,8:N0} KB raw  {2,10} br" -f $_.Name, ($_.Length/1KB),
             $brkb })
   ⎿  RAW totals:
      18.4 MB raw (all)
      5.6 MB br (compressed transfer)

Breakdown of biggest pieces (compressed):
   - dotnet.native.wasm (Skia+HarfBuzz native) — 2.1 MB ← floor, can't shrink without dropping features
   - System.Private.CoreLib — 570 KB
   - DrawnUi.Web — 403 KB
   - System.Private.Xml — 355 KB (pulled by Svg.Custom)
   - Svg.Custom — 281 KB
   - Newtonsoft.Json — 208 KB
 */

public static partial class Program
{
    private static SkiaLabel _label = null!;
    private static int _clicks;

    [JSExport]
    public static Task Main() =>
        Super.UseDrawnUi()
            .RunAsync("drawnui-canvas", () => new Canvas
            {
                Gestures = GesturesMode.Enabled,
                RenderingMode = RenderingModeType.Accelerated, // auto-falls back to raster
                BackgroundColor = SKColors.Pink,
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill,
                Content = new SkiaLayout
                {
                    Type = LayoutType.Column,
                    Spacing = 20,
                    Padding = new Thickness(40),
                    HorizontalOptions = LayoutOptions.Fill,
                    VerticalOptions = LayoutOptions.Fill,
                    BackgroundColor = SKColors.LightGray,
                    Children =
                    {
                        new SkiaLabel
                        {
                            Text = "Hello DrawnUI on Web!",
                            FontSize = 24,
                            TextColor = SKColors.DarkBlue,
                            HorizontalOptions = LayoutOptions.Center
                        }.Assign(out _label),

                        new SkiaButton
                        {
                            Text = "Click Me!",
                            FontSize = 18,
                            TextColor = SKColors.White,
                            BackgroundColor = SKColors.SeaGreen,
                            CornerRadius = 8,
                            Padding = new Thickness(20, 10),
                            HorizontalOptions = LayoutOptions.Center
                        }.OnTapped(_ => _label.Text = $"Clicked {++_clicks} time(s)!"),
                    }
                }
            });
}
