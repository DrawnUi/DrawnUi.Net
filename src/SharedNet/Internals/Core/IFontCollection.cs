namespace DrawnUi.Draw;

public interface IFontCollection
{
    IFontCollection AddFont(string source, string alias);
    IFontCollection AddFont(string source, string alias, FontWeight weight);
}
