using DrawnUi.Draw;

namespace VirtualizationHarnessDemo;

/// <summary>
/// Verifies the desktop (OpenTK/Net) fix: SkiaImageManager.OpenStreamAsync resolves a RELATIVE local
/// asset path (e.g. "Images/banana.gif") to a file next to the executable, instead of handing it to
/// HttpClient (which threw "invalid request URI / BaseAddress must be set" — the SkiaGif symptom).
/// </summary>
public static class AssetResolveRepro
{
    public static void Run()
    {
        Console.WriteLine();
        Console.WriteLine("=============== ASSET RESOLVE (OpenTK/Net) ===============");

        var dir = Path.Combine(AppContext.BaseDirectory, "Images");
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, "probe.bin");
        File.WriteAllBytes(file, new byte[] { 1, 2, 3, 4 });

        bool relativeOk = false;
        try
        {
            using var s = SkiaImageManager.OpenStreamAsync("Images/probe.bin").GetAwaiter().GetResult();
            relativeOk = s != null && s.Length == 4;
            Console.WriteLine($"relative \"Images/probe.bin\" -> {(relativeOk ? "FILE" : "null")} (len={s?.Length})");
        }
        catch (Exception e)
        {
            Console.WriteLine($"relative resolve THREW {e.GetType().Name}: {e.Message}  (regression — was going to HttpClient)");
        }

        Console.WriteLine(relativeOk ? "=> PASS" : "=> FAIL");
        Console.WriteLine("==========================================================");
    }
}
