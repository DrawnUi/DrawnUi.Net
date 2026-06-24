using Microsoft.Extensions.Logging;

namespace DrawnUI.Tutorials;

public class DiDiStack : SkiaStack
{
    public override void UpdateByChild(SkiaControl child)
    {
        base.UpdateByChild(child);
    }

    public override void Repaint()
    {
        base.Repaint();
    }

    public override void Update()
    {
        base.Update();
    }

    public override void InvalidateByChild(SkiaControl child)
    {
        base.InvalidateByChild(child);
    }

    public override void InvalidateInternal()
    {
        base.InvalidateInternal();
    }

    protected override void InvalidateMeasure()
    {
        base.InvalidateMeasure();
    }
}


public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseDrawnUi(new()
            {
                DesktopWindow = new()
                {
                    Width = 370,
                    Height = 750,
                    //IsFixedSize = true
                }
            })
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "FontText");
                fonts.AddFont("OpenSans-Semibold.ttf", "FontTextBold");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
