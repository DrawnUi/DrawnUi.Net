using DrawnUi.Draw;
using DrawnUi.Views;
using DrawnUi.Controls;
using Color = DrawnUi.Color;

namespace HelloWpf.Pages;

/// <summary>
/// SkiaEditor: drawn text input with caret, selection, placeholder, password, multiline and the
/// platform looks. Ported from the React demo's EditorPage.tsx. Keys arrive through
/// DrawnUiElement (OnPreviewKeyDown / OnTextInput) into the focused editor's stub methods.
/// </summary>
public class EditorPage : SkiaLayer
{
    private static readonly Color Muted = Color.Parse("#ADB5BD");

    private SkiaEditor _editor;
    private SkiaLabel _singleTitle;
    private SkiaLabel _passwordTitle;
    private SkiaLabel _chatTitle;
    private SkiaStack _chat;
    private string _state = "";
    private string _submitted = "";
    private bool _focused;
    private int _sent;

    /// <summary>Builds the page.</summary>
    public EditorPage()
    {
        HorizontalOptions = LayoutOptions.Fill;
        VerticalOptions = LayoutOptions.Fill;

        Children = new List<SkiaControl>
        {
            new SkiaScroll
            {
                Orientation = ScrollOrientation.Vertical,
                Content = new SkiaStack
                {
                    Spacing = 16,
                    Padding = new Thickness(16, 16, 16, 120),
                    HorizontalOptions = LayoutOptions.Center,
                    MaximumWidthRequest = 720,
                    Children = new List<SkiaControl>
                    {
                        new SkiaLabel("SkiaEditor") { FontSize = 24, TextColor = Colors.White, HorizontalOptions = LayoutOptions.Fill, HorizontalTextAlignment = DrawTextAlignment.Center },

                        Card(Title("").Assign(out _singleTitle),
                            new SkiaEditor { PlaceholderText = "Type here, Enter submits", FontSize = 16, HorizontalOptions = LayoutOptions.Fill }
                                .Assign(out _editor)
                                .Adapt(me =>
                                {
                                    me.TextChanged += (_, _) => { Describe(); PaintSingle(); };
                                    me.CursorMoved += (_, _) => { Describe(); PaintSingle(); };
                                    me.FocusChanged += (_, f) => { _focused = f; PaintSingle(); };
                                    me.TextSubmitted += (_, t) => { _submitted = t; PaintSingle(); };
                                }),
                            new SkiaWrap
                            {
                                Spacing = 8,
                                Children = new List<SkiaControl>
                                {
                                    new SkiaButton("Focus") { BackgroundColor = Color.Parse("#0D6EFD"), FontSize = 13 }.OnTapped(me => _editor.IsFocused = true),
                                    new SkiaButton("SelectAll()") { BackgroundColor = Color.Parse("#495057"), FontSize = 13 }.OnTapped(me => _editor.SelectAll()),
                                    new SkiaButton("InsertAtCursor(arrow)") { BackgroundColor = Color.Parse("#495057"), FontSize = 13 }.OnTapped(me => _editor.InsertAtCursor("→")),
                                    new SkiaButton("Set Text") { BackgroundColor = Color.Parse("#495057"), FontSize = 13 }.OnTapped(me => _editor.Text = "Hello from code"),
                                    new SkiaButton("Clear") { BackgroundColor = Color.Parse("#495057"), FontSize = 13 }.OnTapped(me => _editor.Text = ""),
                                },
                            },
                            new SkiaLabel("The WPF element forwards keys to the focused drawn editor: arrows, Shift+arrows select, Home / End, Backspace / Delete, Ctrl+A / C / X / V (system clipboard), Tab inserts spaces; text arrives through WPF TextInput so IME composition and dead keys work.")
                            {
                                FontSize = 12, TextColor = Muted, HorizontalOptions = LayoutOptions.Fill,
                            }),

                        Card(Title("ControlStyle — Cupertino, Material, Material3, Windows (ApplyControlStyleVisuals palettes)"),
                            new SkiaWrap
                            {
                                Spacing = 10,
                                Children = new List<SkiaControl>
                                {
                                    new SkiaEditor { ControlStyle = PrebuiltControlStyle.Cupertino, PlaceholderText = "Cupertino", WidthRequest = 200 },
                                    new SkiaEditor { ControlStyle = PrebuiltControlStyle.Material, PlaceholderText = "Material", WidthRequest = 200 },
                                    new SkiaEditor { ControlStyle = PrebuiltControlStyle.Material3, PlaceholderText = "Material3", WidthRequest = 200 },
                                    new SkiaEditor { ControlStyle = PrebuiltControlStyle.Windows, PlaceholderText = "Windows", WidthRequest = 200 },
                                },
                            }),

                        Card(Title(PasswordTitle(0)).Assign(out _passwordTitle),
                            new SkiaWrap
                            {
                                Spacing = 10,
                                Children = new List<SkiaControl>
                                {
                                    new SkiaEditor { IsPassword = true, PlaceholderText = "Password", WidthRequest = 220 }
                                        .Adapt(me => me.TextChanged += (_, t) => _passwordTitle.Text = PasswordTitle(t?.Length ?? 0)),
                                    new SkiaEditor { KeyboardType = SkiaEditor.SkiaEditorKeyboard.Numeric, PlaceholderText = "Numeric", WidthRequest = 220 },
                                    new SkiaEditor { KeyboardType = SkiaEditor.SkiaEditorKeyboard.Email, PlaceholderText = "Email", WidthRequest = 220 },
                                },
                            }),

                        Card(Title("Multiline — MaxLines=4: Enter inserts a line, the box scrolls to the caret"),
                            new SkiaEditor { MaxLines = 4, PlaceholderText = "Write a few lines… wrapping, arrows, Shift+arrows select, double tap selects a word", FontSize = 15, HorizontalOptions = LayoutOptions.Fill }),

                        Card(Title("Multiline + AutoHeight — MaxLines=-1: the editor grows with the text"),
                            new SkiaEditor { MaxLines = -1, AutoHeight = true, PlaceholderText = "Grows as you type", FontSize = 15, Text = "First line\nSecond line", HorizontalOptions = LayoutOptions.Fill }),

                        Card(Title(ChatTitle(0)).Assign(out _chatTitle),
                            new SkiaStack { Spacing = 6, HorizontalOptions = LayoutOptions.Fill }.Assign(out _chat),
                            new SkiaEditor { MaxLines = 3, ReturnType = ReturnType.Send, PlaceholderText = "Message", FontSize = 15, HorizontalOptions = LayoutOptions.Fill }
                                .Adapt(me => me.TextSubmitted += (_, t) =>
                                {
                                    if (string.IsNullOrWhiteSpace(t))
                                        return;

                                    _sent++;
                                    _chat.AddSubView(new SkiaLabel(t) { FontSize = 13, TextColor = Color.Parse("#DEE2E6"), BackgroundColor = Color.Parse("#0F3460"), Padding = new Thickness(10, 6), HorizontalOptions = LayoutOptions.End });
                                    while (_chat.Views.Count > 4)
                                        _chat.RemoveSubView(_chat.Views[0]);
                                    _chatTitle.Text = ChatTitle(_sent);
                                    me.Text = "";
                                })),
                    },
                },
            }.Fill(),
        };

        PaintSingle();
    }

    private static string PasswordTitle(int chars) =>
        $"IsPassword — {chars} chars hidden behind bullets, KeyboardType Numeric / Email (no native keyboard on desktop, the mode is kept for shared code)";

    private static string ChatTitle(int sent) =>
        $"Chat input — MaxLines=3 ReturnType=Send: Enter submits and keeps focus, Shift+Enter breaks the line · {sent} sent";

    private void Describe() => _state = $"cursor {_editor.CursorPosition} · selection {_editor.SelectionLength} · {_editor.Text?.Length ?? 0} chars";

    private void PaintSingle() =>
        _singleTitle.Text = $"Single line — Text=\"{_editor.Text}\" · IsFocused={_focused} · {_state} · submitted: \"{_submitted}\"";

    private static SkiaLabel Title(string text) => new(text)
    {
        FontSize = 12, TextColor = Color.Parse("#6EA8FE"), FontAttributes = FontAttributes.Bold, TextTransform = TextTransform.Uppercase, HorizontalOptions = LayoutOptions.Fill,
    };

    private static SkiaControl Card(SkiaLabel title, params SkiaControl[] content) => new SkiaShape
    {
        Type = ShapeType.Rectangle,
        CornerRadius = 8,
        BackgroundColor = Color.Parse("#2B3035"),
        HorizontalOptions = LayoutOptions.Fill,
        Children = new List<SkiaControl>
        {
            new SkiaStack
            {
                Spacing = 10,
                Padding = new Thickness(16, 12),
                HorizontalOptions = LayoutOptions.Fill,
                Children = new SkiaControl[] { title }.Concat(content).ToList(),
            },
        },
    };
}
