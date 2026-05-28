/*
Blazor-specific SkiaControl partial.
Styles code lives in SharedNet/Draw/SkiaControl.NetShared.cs (shared with Net/OpenTK).
*/

using System.Runtime.CompilerServices;
using Color = DrawnUi.Color;


namespace DrawnUi.Draw
{
    public partial class SkiaControl : View
    {
        private static void ReportHotreloadChildAdded(SkiaControl control)
        {
        }

        private static void ReportHotreloadChildRemoved(SkiaControl control)
        {
        }

        public static Color TransparentColor = Colors.Transparent;
        public static Color WhiteColor = Colors.White;
        public static Color BlackColor = Colors.Black;
        public static Color RedColor = Colors.Red;

        protected override void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            base.OnPropertyChanged(propertyName);

            TrackExplicitPropertyChange(propertyName);

            if (propertyName == nameof(ZIndex))
            {
                Parent?.InvalidateViewsList();
                Repaint();
            }
            else if (propertyName.IsEither(
                         nameof(Opacity),
                         nameof(TranslationX), nameof(TranslationY),
                         nameof(Rotation),
                         nameof(AnchorX), nameof(AnchorY),
                         nameof(RotationX), nameof(RotationY),
                         nameof(ScaleX), nameof(ScaleY)
                     ))
            {
                Repaint();
            }
            else if (propertyName.IsEither(nameof(BackgroundColor),
                         nameof(IsClippedToBounds)
                     ))
            {
                Update();
            }
            else if (propertyName == nameof(Shadow))
            {
                UpdatePlatformShadow();
            }
            else if (propertyName == nameof(Clip))
            {
                Update();
            }
            else if (propertyName == nameof(Padding))
            {
                UsePadding = OnPaddingSet(this.Padding);
                InvalidateMeasure();
            }
            else if (propertyName.IsEither(
                         nameof(HorizontalOptions), nameof(VerticalOptions)))
            {
                InvalidateMeasure();
            }
            else if (propertyName.IsEither(
                         nameof(Margin),
                         nameof(HeightRequest), nameof(WidthRequest),
                         nameof(MaximumWidthRequest), nameof(MinimumWidthRequest),
                         nameof(MaximumHeightRequest), nameof(MinimumHeightRequest)
                     ))
            {
                InvalidateMeasure();
                if (UsingCacheType != SkiaCacheType.ImageDoubleBuffered)
                {
                    UpdateSizeRequest();
                }
            }
            else if (propertyName.IsEither(nameof(IsVisible)))
            {
                OnVisibilityChanged(IsVisible);
                Repaint();
            }
        }
    }
}
