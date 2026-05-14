using DrawnUi.Draw;

namespace DrawnUi.Blazor.Server;

public readonly record struct DrawnUiServerFrameEncodingOptions(
    DrawnUiServerFrameFormat Format,
    Color BackgroundColor,
    int JpegQuality)
{
    public static DrawnUiServerFrameEncodingOptions Default => new(DrawnUiServerFrameFormat.Jpeg, Colors.Transparent, 85);
}