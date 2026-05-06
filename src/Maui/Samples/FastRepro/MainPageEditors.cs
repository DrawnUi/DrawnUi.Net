using DrawnUi.Views;
using DrawnUi.Draw;
using DrawnUi.Controls;
using DrawnUi.Infrastructure.Enums;
using Sandbox.Resources.Strings;
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
                Content = new SkiaStack()
                {
                    VerticalOptions = LayoutOptions.Fill,
                    HorizontalOptions = LayoutOptions.Fill,
                    Padding = new Thickness(16),
                    Spacing = 16,
                    Children =
                    {
                        new SkiaLabel()
                        {
                            Text = "Plain editor",
                            FontSize = 18,
                            TextColor = Colors.Black,
                            HorizontalOptions = LayoutOptions.Fill,
                        },

                        new SkiaEditor()
                        {
                            HorizontalOptions = LayoutOptions.Fill,
                            HeightRequest = 120,
                            MaxLines = 8,
                            BackgroundColor = Color.Parse("#FEFEFE"),
                            Padding = new Thickness(8),
                            FontSize = 16,
                            TextColor = Colors.Black,
                            CursorColor = Colors.Red,
                            Text = "Plain editor text\nSecond line",
                        },

                        new SkiaLabel()
                        {
                            Text = "Markdown editor",
                            FontSize = 18,
                            TextColor = Colors.Black,
                            HorizontalOptions = LayoutOptions.Fill,
                        },

                        new SkiaEditor()
                        {
                            HorizontalOptions = LayoutOptions.Fill,
                            HeightRequest = 260,
                            MaxLines = 16,
                            BackgroundColor = Color.Parse("#FEFEFE"),
                            Padding = new Thickness(8),
                            FontSize = 16,
                            TextColor = Colors.Black,
                            CursorColor = Colors.Blue,
                            UseMarkdown = true,
                            Text = ResStrings.MarkdownTest,
                        }
                    }
                }
            };

            Content = new Grid()
            {
                Children = { Canvas }
            };
        }
    }
}
