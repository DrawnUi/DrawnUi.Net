namespace DrawnUi.Controls;

public partial class SkiaShell
{
    public class PopupWrapper : ContentWithBackdrop
    {
        private readonly bool _closeWhenBackgroundTapped;
        private readonly bool _animated;
        private readonly SkiaShell _shell;

        public PopupWrapper(bool closeWhenBackgroundTapped,
            bool animated,
            bool willFreeze,
            Color backgroundColor,
            SkiaShell shell)
        {
            _willFreeze = willFreeze;
            _closeWhenBackgroundTapped = closeWhenBackgroundTapped;
            _animated = animated;
            _shell = shell;
            if (backgroundColor == null)
                backgroundColor = PopupBackgroundColor;
            _color = backgroundColor;
            AccessibilityModal = true; // screen readers and Tab stay in the popup while it is open
        }

        protected override void SetContent(SkiaControl view)
        {
            base.SetContent(view);

            if (Backdrop != null)
            {
                if (_willFreeze)
                {
                    Backdrop.Blur = 0;
                    BackgroundColor = _color;
                }
                else
                {
                    Backdrop.BackgroundColor = _color;
                    Backdrop.Blur = SkiaShell.PopupsBackgroundBlur;
                }
                //Backdrop.Brightness = 1 - _shell.FrozenBackgroundDim;
            }

            if (Content != null && _animated)
            {
                SetupAppearingAnimation(Content);
            }
        }

        public virtual void SetupAppearingAnimation(SkiaControl content)
        {
            content.Scale = 0.5;
            Opacity = 0.1;
        }

        public async Task CloseAsync()
        {
            if (_animated && IsVisibleInViewTree())
            {
                var cts = new CancellationTokenSource();
                var timeoutTask = Task.Delay(TimeSpan.FromMilliseconds(PopupsCancelAnimationsAfterMs), cts.Token);

                try
                {
                    var animate = Task.WhenAll(
                        FadeToAsync(0, PopupsAnimationSpeed, null, cts),
                        Content.ScaleToAsync(0, 0, PopupsAnimationSpeed, null, cts));

                    var completedTask = await Task.WhenAny(animate, timeoutTask);
                    if (completedTask == timeoutTask)
                    {
                        await cts.CancelAsync();
                    }
                }
                catch (Exception e)
                {
                    Super.Log(e);
                }
                finally
                {
                    cts.Dispose();
                }


            }

            await _shell.Popups.Close(this, _animated);
            _shell.OnPopupsStackChanged(_shell.Popups.NavigationStack.Count);
        }

        protected override void OnLayoutReady()
        {
            base.OnLayoutReady();

            if (Content != null && _animated)
            {
                Task.WhenAll(
                    FadeToAsync(1, PopupsAnimationSpeed),
                    Content.ScaleToAsync(1, 1, PopupsAnimationSpeed));
            }


        }

        bool frozen;
        private readonly Color _color;
        private readonly bool _willFreeze;

        protected override void Draw(DrawingContext context)
        {
            base.Draw(context);

            if (_willFreeze && !frozen && LayoutReady && Backdrop != null)
            {
                Backdrop.IsVisible = false;
            }

            FinalizeDrawingWithRenderObject(context);
        }

        public override ISkiaGestureListener ProcessGestures(SkiaGesturesParameters args, GestureEventProcessingInfo apply)
        {
            if (_closeWhenBackgroundTapped
                && Content != null
                && args.Type == TouchActionResult.Tapped)
            {
                var point = TranslateInputOffsetToPixels(args.Event.Location, apply.ChildOffset);
                if (!Content.HitIsInside(point.X, point.Y))
                {
                    _shell.ClosePopupAsync(this, _animated).ConfigureAwait(false);
                }
            }

            return base.ProcessGestures(args, apply);
        }
    }
}
