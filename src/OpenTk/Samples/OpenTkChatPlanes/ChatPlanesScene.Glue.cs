using System.ComponentModel;
using System.Runtime.CompilerServices;
using DrawnUi.Controls;
using DrawnUi.Draw;
using DrawnUi.Views;
using DrawnUiRepro;
using NetGrid = DrawnUi.Blazor.Views.Grid; // Net/OpenTk grid attached-property shim

namespace OpenTkChatPlanes;

/// <summary>
/// Glue to make the MAUI ChatPage's CreateCanvas() copied 1:1 compile + run under OpenTk.
/// The original references PAGE infrastructure (BasePageReloadable members, send/reply handlers,
/// MAUI-only fluent grid helpers). None of it is needed to reproduce the SCROLL/structure bug,
/// so the action handlers are no-op stubs; only the layout-affecting bits (chrome fields,
/// KeyboardSize, grid column helpers) are real.
/// </summary>
public sealed partial class ChatPlanesScene : INotifyPropertyChanged
{
    // .Observe(this, ...) in CreateCanvas requires INotifyPropertyChanged.
    public event PropertyChangedEventHandler PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    // soft-keyboard spacer driver — stays 0 on desktop OpenTk
    private double _keyboardSize;
    public double KeyboardSize
    {
        get => _keyboardSize;
        set { if (_keyboardSize == value) return; _keyboardSize = value; OnPropertyChanged(); }
    }

    // chrome fields the 1:1 layout assigns into (.Assign(out ...)); unused by the repro itself
    private Canvas Canvas;
    private SkiaLabel StatusLabel;
    private SkiaLabel ReplyName;
    private SkiaLabel ReplyText;
    private SkiaLayout ReplyPanel;
    private SkiaEditor Editor;
    private SkiaLayer FullscreenOverlay;
    private SkiaImage FullscreenImage;
    private SkiaShape BtnScrollToEnd;
    private ChatMessage _replyTo;
    private string _fullscreenUpgradeUrl;

    // SVG strings copied 1:1 from ChatPage
    private const string SvgChevronDown =
        "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24'><path d='M7.41 8.59 12 13.17l4.59-4.58L18 10l-6 6-6-6z'/></svg>";

    private const string SvgReply =
        "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24'><path d='M10 9V5l-7 7 7 7v-4.1c5 0 8.5 1.6 11 5.1-1-5-4-10-11-11z'/></svg>";

    private const string SvgClose =
        "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24'><path d='M19 6.41 17.59 5 12 10.59 6.41 5 5 6.41 10.59 12 5 17.59 6.41 19 12 13.41 17.59 19 19 17.59 13.41 12z'/></svg>";

    private const string SvgImage =
        "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24'><path d='M21 19V5a2 2 0 0 0-2-2H5a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2zM8.5 13.5l2.5 3 3.5-4.5 4.5 6H5z'/></svg>";

    private const string SvgSend =
        "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24'><path d='M2.01 21 23 12 2.01 3 2 10l15 2-15 2z'/></svg>";

    /// <summary>TEST-ONLY: seed a deep resident window (newest <paramref name="count"/> msgs) so the
    /// ScrollToIndex walk can recycle across many day boundaries without needing LoadMore (which stalls
    /// under synthetic slow-drag). Reset scrolls to content start = newest.</summary>
    public void SeedWindow(int count)
    {
        _windowEnd = _all.Count;
        _windowStart = System.Math.Max(0, _windowEnd - count);
        _items.ReplaceRangeReset(ReversedRange(_windowStart, _windowEnd - _windowStart));
    }

    // send/reply/overlay handlers — harness no-ops (repro drives the scroll, not these)
    private void SendMessage() { }
    private void SendImage() { }
    private void SendFile() { }
    private void CancelReply() { }
    private void ScrollToNewest(bool animate) { }
    private void ShowScrollDownButton(bool show) { }
    private void HideImageFullscreen() { }
}

/// <summary>
/// MAUI-only fluent grid helpers (WithColumn / WithColumnDefinitions live in FluentExtensions.Maui.cs)
/// reimplemented for OpenTk against the Net Grid shim + DrawnUi.Draw column types, so CreateCanvas stays 1:1.
/// </summary>
internal static class OpenTkGridFluent
{
    public static T WithColumn<T>(this T view, int column) where T : SkiaControl
    {
        NetGrid.SetColumn(view, column);
        return view;
    }

    public static SkiaLayout WithColumnDefinitions(this SkiaLayout grid, string columnDefinitions)
    {
        var cols = new ColumnDefinitionCollection();
        foreach (var raw in columnDefinitions.Split(','))
        {
            var token = raw.Trim();
            GridLength len;
            if (token == "*")
                len = GridLength.Star;
            else if (token.EndsWith("*"))
                len = new GridLength(double.Parse(token[..^1]), GridUnitType.Star);
            else if (token.Equals("Auto", System.StringComparison.OrdinalIgnoreCase))
                len = GridLength.Auto;
            else
                len = new GridLength(double.Parse(token), GridUnitType.Absolute);
            cols.Add(new ColumnDefinition(len));
        }
        grid.ColumnDefinitions = cols;
        return grid;
    }
}
