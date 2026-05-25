using DrawnUi.Views;
using DrawnUi.Draw;
using DrawnUi.Controls;
using DrawnUi.Infrastructure.Enums;
using DrawnUi.Models;
using Canvas = DrawnUi.Views.Canvas;

namespace Sandbox
{


    public class MainPageEditors : BasePageReloadable, IDisposable
    {
        Canvas Canvas;
        SkiaLabel _liveDisplayLabel;
        int _tapCount;

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
                RenderingMode = RenderingModeType.Accelerated,
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
                        UseCache = SkiaCacheType.Operations,

                        Children =
                        {
                            new SkiaLabel()
                            {
                                Text = "Single-line",
                                UseCache = SkiaCacheType.Operations,
                                FontSize = 18,
                                TextColor = Colors.Black,
                                HorizontalOptions = LayoutOptions.Fill,
                            }.WithAccessibilityText(),

                            new SkiaEditor()
                            {
                                UseCache = SkiaCacheType.Operations,
                                HorizontalOptions = LayoutOptions.Fill,
                                MaxLines = 1,
                                BackgroundColor = Color.Parse("#FFCCCC"),
                                Padding = new Thickness(8),
                                FontSize = 16,
                                TextColor = Colors.Black,
                                CursorColor = Colors.Red,
                                Text = "Single line text",
                                PlaceholderText = "Write your message…",
                                PlaceholderColor = Color.Parse("#60FFFFFF"),
                            }
                            .WithAccessibility(Aria.RoleTextBox, "Single-line, editor",
                                "Write your message…", canInteract: true),

                            new SkiaLabel()
                            {
                                Text = "Multiline ",
                                UseCache = SkiaCacheType.Operations,
                                FontSize = 18,
                                TextColor = Colors.Black,
                                HorizontalOptions = LayoutOptions.Fill,
                            }.WithAccessibilityText(),

                            new SkiaEditor()
                            {
                                UseCache = SkiaCacheType.Operations,
                                HorizontalOptions = LayoutOptions.Fill,
                                MaxLines = 6,
                                BackgroundColor = Colors.DarkBlue,
                                Padding = new Thickness(8),
                                FontSize = 16,
                                TextColor = Colors.Yellow,
                                CursorColor = Colors.White,
                                Text = "First line\nSecond line\nThird line",
                            },

                            
                            new SkiaLabel()
                            {
                                Text = "Centered",
                                UseCache = SkiaCacheType.Operations,
                                FontSize = 18,
                                TextColor = Colors.Black,
                                HorizontalOptions = LayoutOptions.Fill,
                            }.WithAccessibilityText(),


                            new SkiaEditor()
                            {
                                UseCache = SkiaCacheType.Operations,
                                HorizontalOptions = LayoutOptions.Fill,
                                MaxLines = 1,
                                BackgroundColor = Color.Parse("#1E1E2E"),
                                Padding = new Thickness(8),
                                FontSize = 16,
                                TextColor = Colors.White,
                                CursorColor = Colors.OrangeRed,
                                PlaceholderText = "Search…",
                                PlaceholderColor = Color.Parse("#B0B0B0"),
                                PlaceholderHorizontalAlignment = DrawTextAlignment.Center,
                                HorizontalTextAlignment = DrawTextAlignment.Center
                            },

                            new SkiaLabel()
                            {
                                Text = "Password with placeholder",
                                UseCache = SkiaCacheType.Operations,
                                FontSize = 18,
                                TextColor = Colors.Black,
                                HorizontalOptions = LayoutOptions.Fill,
                            }.WithAccessibilityText(),


                            new SkiaEditor()
                            {
                                UseCache = SkiaCacheType.Operations,
                                HorizontalOptions = LayoutOptions.Fill,
                                MaxLines = 1,
                                IsPassword = true,
                                BackgroundColor = Color.Parse("#F0F0F0"),
                                Padding = new Thickness(8),
                                FontSize = 16,
                                TextColor = Colors.Black,
                                CursorColor = Colors.Black,
                                PlaceholderText = "Password",
                                PlaceholderColor = Color.Parse("#A0A0A0"),
                            },

#if xDEBUG
                            new SkiaLabel()
                            {
                                Text = "Rich editor (DEBUG)",
                                UseCache = SkiaCacheType.Operations,
                                FontSize = 18,
                                TextColor = Colors.DarkSlateBlue,
                                HorizontalOptions = LayoutOptions.Fill,
                            },

                            BuildRichEditorPanel(),
#endif

                            new SkiaButton("Test Accessibility")
                            {
                                HorizontalOptions = LayoutOptions.Start,
                                HeightRequest = 46,
                                BackgroundColor = Color.Parse("#2E86DE"),
                                TextColor = Colors.White,
                                Padding = new Thickness(18, 10),
                                Margin = new Thickness(0, 8, 0, 0),
                            }
                            .WithAccessibilityButton("Test Accessibility", "Tap to verify UIA is working")
                            .OnTapped(me =>
                            {
                                System.Diagnostics.Debug.WriteLine("[A11y] Button tapped via UIA");
                                _tapCount++;
                                if (_liveDisplayLabel != null)
                                    _liveDisplayLabel.Text = $"Tapped {_tapCount} time{(_tapCount == 1 ? "" : "s")}";
                            }),

                            new SkiaLabel()
                            {
                                Text = "Tapped 0 times",
                                FontSize = 15,
                                TextColor = Colors.DarkGreen,
                                HorizontalOptions = LayoutOptions.Fill,
                            }
                            .WithAccessibilityText()
                            .WithAccessibilityLive()
                            .Assign(out _liveDisplayLabel),

                            new SkiaControl()
                            {
                                HeightRequest = 0,
                                HorizontalOptions = LayoutOptions.Fill
                            }.Observe(this, (me, s) =>
                            {
                                if (s == nameof(this.KeyboardSize))
                                {
                                    me.HeightRequest = KeyboardSize;
                                }
                            })

                        }
                    }
                }.Observe(this,
                    (me, s) =>
                    {
                        if (s == nameof(this.KeyboardSize))
                        {
                            me.AdaptToKeyboardFor = Canvas.FocusedChild as SkiaControl;
                            me.AdaptToKeyboardSize = KeyboardSize;
                        }
                    })
            };

            Content = new Grid()
            {
                Children =
                {
                    Canvas,
                    //new Label()
                    //{
                    //    Text = "MAUI Label",
                    //    TextColor = Colors.Red,
                    //    HorizontalOptions = LayoutOptions.Center,
                    //    VerticalOptions = LayoutOptions.Center,
                    //},
                    //new Entry()
                    //{
                    //    Margin = new(40),
                    //    Text = "MAUI Entry",
                    //    BackgroundColor = Colors.Brown,
                    //    HorizontalOptions = LayoutOptions.Center,
                    //    VerticalOptions = LayoutOptions.Center,
                    //}
                }
            };
        }

#if notyetDEBUG
        private SkiaRichEditor _richEditor;

        private SkiaControl BuildRichEditorPanel()
        {
            _richEditor = new SkiaRichEditor
            {
                Tag = "rich",
                HorizontalOptions = LayoutOptions.Fill,
                HeightRequest = 200,
                MaxLines = 8,
                BackgroundColor = Color.Parse("#EFEFEF"),
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
                UseCache = SkiaCacheType.Image,
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
