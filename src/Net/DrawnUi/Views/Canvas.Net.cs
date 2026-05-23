using AppoMobi.Gestures;
using Polly;

namespace DrawnUi.Views
{
    public partial class Canvas : DrawnView, IGestureListener
    {
        protected DrawingContext Context;

        public new object Content
        {
            get => base.Content;
            set
            {
                base.Content = value;
                if (value is IEnumerable<SkiaControl> list)
                    SetChildren(list);
                else if (value is SkiaControl single)
                    SetChildren(new[] { single });
                else
                    ClearChildren();
            }
        }

        protected override void Draw(DrawingContext context)
        {
            Context = context;

            double widthRequest = WidthRequest;
            double heightRequest = HeightRequest;

            Arrange(context.Destination, widthRequest, heightRequest, context.Scale);

            var skia = Views.Count > 0 ? Views[0] : null;

            if (!IsGhost)
            {
                if (RenderingMode == RenderingModeType.AcceleratedRetained)
                {
                    if (skia == null || skia.NeedUpdate)
                    {
                        Debug.WriteLine("PAINT");

                        PaintTintBackground(context.Context.Canvas);
                    }
                    else
                    {
                        Debug.WriteLine("RETAINED");
                    }

                    base.Draw(context.WithDestination(Destination));                    
                }
                else
                {
                    //usual immediate mode
                    if (BackgroundColor != null && BackgroundColor != Colors.Transparent)
                    {
                        PaintTintBackground(context.Context.Canvas);
                    }
                    else
                    {
                        context.Context.Canvas.Clear();
                    }

                    base.Draw(context.WithDestination(Destination));
                }
            }
        }

        public virtual bool SignalInput(ISkiaGestureListener listener, TouchActionResult gestureType)
            => SignalNetInput(listener, gestureType);

        public virtual bool Focus() => true;

        public void OnGestureEvent(TouchActionType type, TouchActionEventArgs args, TouchActionResult action)
            => HandleNetGestureEvent(type, args, action);

        public bool InputTransparent => false;
    }
}
