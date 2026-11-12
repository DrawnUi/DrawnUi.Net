using System.Globalization;
using System.Text;
using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;
using Xunit.Abstractions;

namespace DrawnUi.Net.Tests;

/// <summary>
/// The measure of a label (its size, its lines and every glyph's place and width) for texts that take each measuring
/// path: plain, wrapped, MonoForDigits, character spacing, fill alignments, spans, glyphs from a fallback font, shaped
/// emoji and right-to-left text. Each label is measured, measured again in a second label with the same settings, and
/// both must give the recorded result.
/// </summary>
[Collection("FontRegistration")] // registers fonts: never in parallel with another class that does
public class LabelMeasureGoldenTests
{
    private readonly ITestOutputHelper _out;
    public LabelMeasureGoldenTests(ITestOutputHelper o) { _out = o; }

    private static string RepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Path.Exists(Path.Combine(dir.FullName, ".git"))) // a worktree has a .git file
            dir = dir.Parent;
        return Path.Combine(dir!.FullName, relative);
    }

    static LabelMeasureGoldenTests()
    {
        SkiaFontManager.Instance.RegisterFont("GoldenText", RepoFile(@"Tests\PreviewTests\Resources\Fonts\OpenSans-Regular.ttf"));
        SkiaFontManager.Instance.RegisterFont("GoldenMath", RepoFile(@"src\Blazor\DrawnUi\wwwroot\fonts\NotoSansMathSymbols-Subset.ttf"));
        SkiaFontManager.Instance.RegisterFont("GoldenEmoji", RepoFile(@"src\Wpf\Samples\HelloWpf\fonts\NotoColorEmoji-Subset-COLRv0.ttf"));
        SkiaFontManager.Instance.Initialize();
    }

    static SkiaLabel Make(string kind) => kind switch
    {
        "plain" => new SkiaLabel("Hello measured world"),
        "wrapped" => new SkiaLabel("A longer sentence that has to wrap over several lines inside the narrow box it gets.") { WidthRequest = 160 },
        "mono" => new SkiaLabel("FPS 59.8") { MonoForDigits = "8" },
        "mono-same-text-plain" => new SkiaLabel("FPS 59.8"),
        "spacing" => new SkiaLabel("Spaced letters") { CharacterSpacing = 1.4 },
        "fill-words" => new SkiaLabel("Words spread over the whole width of the line here and more") { WidthRequest = 220, HorizontalTextAlignment = DrawTextAlignment.FillWordsFull },
        "fill-chars" => new SkiaLabel("Characters filled") { WidthRequest = 220, HorizontalTextAlignment = DrawTextAlignment.FillCharactersFull },
        "spans" => new SkiaLabel
        {
            Spans =
            {
                new TextSpan { Text = "Bold part " , IsBold = true },
                new TextSpan { Text = "then italic, ", IsItalic = true },
                new TextSpan { Text = "then bigger", FontSize = 22 },
            }
        },
        "fallback" => new SkiaLabel("Go → and ∑ done") { FontFamilyFallback = "GoldenMath" },
        "emoji" => new SkiaLabel("Play 😀 now") { FontFamilyFallback = "GoldenEmoji" },
        "rtl" => new SkiaLabel("שלום עולם hello"),
        _ => throw new ArgumentException(kind),
    };

    static readonly string[] Kinds =
    {
        "plain", "wrapped", "mono", "mono-same-text-plain", "spacing", "fill-words", "fill-chars", "spans", "fallback",
        "emoji", "rtl",
    };

    static string Signature(SkiaLabel label)
    {
        var f = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        sb.Append(label.MeasuredSize.Pixels.Width.ToString("F3", f)).Append('x').Append(label.MeasuredSize.Pixels.Height.ToString("F3", f));
        foreach (var line in label.Lines ?? Array.Empty<TextLine>())
        {
            sb.Append('|').Append(line.Value).Append(':').Append(line.Width.ToString("F3", f));
            foreach (var span in line.Spans ?? new List<LineSpan>())
            {
                sb.Append('[').Append(span.Text).Append(' ').Append(span.Size.Width.ToString("F3", f));
                foreach (var g in span.Glyphs ?? Array.Empty<LineGlyph>())
                    sb.Append(' ').Append(g.Position.ToString("F3", f)).Append('/').Append(g.Width.ToString("F3", f));
                sb.Append(']');
            }
        }
        return sb.ToString();
    }

    static string Measure(string kind)
    {
        using var host = new HeadlessCanvasHost(600, 400, 1.5f, Colors.Black);
        var a = Make(kind);
        var b = Make(kind);
        foreach (var l in new[] { a, b })
        {
            l.FontFamily = "GoldenText";
            l.FontSize = 15;
        }
        host.Canvas.Content = new SkiaStack
        {
            HorizontalOptions = LayoutOptions.Fill,
            Children = { a, b },
        };
        host.AdvanceFrames(3);
        var sa = Signature(a);
        Assert.Equal(sa, Signature(b));
        return sa;
    }

    [Fact]
    public void Measures_match_the_recorded_ones()
    {
        var all = string.Join("\n", Kinds.Select(k => k + " = " + Measure(k)));
        _out.WriteLine(all);
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(all)))[..16];
        _out.WriteLine("HASH " + hash);
        Assert.Equal(Recorded, hash);
    }

    const string Recorded = "CCED1C25A80A0BB0";
}
