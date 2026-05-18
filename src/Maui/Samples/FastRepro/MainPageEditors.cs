using DrawnUi.Views;
using DrawnUi.Draw;
using DrawnUi.Controls;
using DrawnUi.Infrastructure.Enums;
using Canvas = DrawnUi.Views.Canvas;

namespace Sandbox
{
    public class MainPageEditors : BasePageReloadable, IDisposable
    {
        Canvas Canvas;

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                Content = null;
                Canvas?.Dispose();
            }

            base.Dispose(isDisposing);
        }

        public override void Build()
        {
            Canvas?.Dispose();

            Canvas = new Canvas()
            {
                RenderingMode = RenderingModeType.Default,
                Gestures = GesturesMode.Enabled,
                VerticalOptions = LayoutOptions.Fill,
                HorizontalOptions = LayoutOptions.Fill,
                Content = new SkiaScroll()
                {
                    Orientation = ScrollOrientation.Vertical,
                    VerticalOptions = LayoutOptions.Fill,
                    HorizontalOptions = LayoutOptions.Fill,

                    Content = new SkiaStack()
                    {
                        BackgroundColor = Colors.White,
                        HorizontalOptions = LayoutOptions.Fill,
                        Padding = new Thickness(16),
                        Spacing = 16,

                        Children =
                        {
                            new SkiaLabel()
                            {
                                Text = "Single-line editor",
                                FontSize = 18,
                                TextColor = Colors.Black,
                                HorizontalOptions = LayoutOptions.Fill,
                            },

                            new SkiaEditor()
                            {
                                HorizontalOptions = LayoutOptions.Fill,
                                MaxLines = 1,
                                BackgroundColor = Color.Parse("#FFCCCC"),
                                Padding = new Thickness(8),
                                FontSize = 16,
                                TextColor = Colors.Black,
                                CursorColor = Colors.Red,
                                Text = "Single line text",
                            },

                            new SkiaLabel()
                            {
                                Text = "Multiline editor",
                                FontSize = 18,
                                TextColor = Colors.Black,
                                HorizontalOptions = LayoutOptions.Fill,
                            },

                            new SkiaEditor()
                            {
                                HorizontalOptions = LayoutOptions.Fill,
                                MaxLines = 4,
                                BackgroundColor = Colors.DarkBlue,
                                Padding = new Thickness(8),
                                FontSize = 16,
                                TextColor = Colors.Yellow,
                                CursorColor = Colors.White,
                                Text = "First line\nSecond line\nThird line",
                            },

#if DEBUG
                            new SkiaLabel()
                            {
                                Text = "Rich editor (DEBUG)",
                                FontSize = 18,
                                TextColor = Colors.DarkSlateBlue,
                                HorizontalOptions = LayoutOptions.Fill,
                            },

                            BuildRichEditorPanel(),
#endif
                        }
                    }
                }
            };

            Content = new Grid()
            {
                Children = { Canvas }
            };
        }

#if DEBUG
        private SkiaRichEditor _richEditor;

        private SkiaControl BuildRichEditorPanel()
        {
            _richEditor = new SkiaRichEditor
            {
                Tag = "rich",
                HorizontalOptions = LayoutOptions.Fill,
                HeightRequest = 200,
                MaxLines = 8,
                BackgroundColor = Color.Parse("#FEFEFE"),
                Padding = new Thickness(8),
                FontSize = 16,
                TextColor = Colors.Black,
                CursorColor = Colors.DarkBlue,
                SelectionColor = Color.Parse("#5590CFFE"),
                Text = "Hello world\nSecond line",
            };

            SkiaLabel formatStatus = new SkiaLabel
            {
                Tag = "fmt-status",
                Text = "Tap B/I/U/S to toggle format on selection",
                FontSize = 13,
                TextColor = Colors.Gray,
                HorizontalOptions = LayoutOptions.Fill,
            };

            SkiaButton MakeBtn(string label, Action onClick) => new SkiaButton
            {
                Text = label,
                FontSize = 16,
                WidthRequest = 44,
                HeightRequest = 44,
                BackgroundColor = Color.Parse("#E0E0E0"),
                TextColor = Colors.Black,
                CornerRadius = 8,
                CommandTapped = new Command(() =>
                {
                    onClick();
                    var fmt = _richEditor.SelectionFormat;
                    formatStatus.Text = $"B={fmt.Bold} I={fmt.Italic} U={fmt.Underline} S={fmt.Strikethrough}  pos={_richEditor.CursorPosition} sel={_richEditor.SelectionLength}";
                })
            };

            var toolbar = new SkiaStack
            {
                Type = LayoutType.Row,
                Spacing = 8,
                Padding = new Thickness(0, 4),
                HorizontalOptions = LayoutOptions.Start,
                Children =
                {
                    MakeBtn("B", () => _richEditor.ToggleBold()),
                    MakeBtn("I", () => _richEditor.ToggleItalic()),
                    MakeBtn("U", () => _richEditor.ToggleUnderline()),
                    MakeBtn("S", () => _richEditor.ToggleStrikethrough()),
                    MakeBtn("Z", () => _richEditor.UndoRich()),
                    MakeBtn("Y", () => _richEditor.RedoRich()),
                }
            };

            return new SkiaStack
            {
                Type = LayoutType.Column,
                Spacing = 4,
                HorizontalOptions = LayoutOptions.Fill,
                Children =
                {
                    _richEditor,
                    toolbar,
                    formatStatus,
                }
            };
        }
#endif
    }
}
