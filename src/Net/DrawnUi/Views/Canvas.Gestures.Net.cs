using AppoMobi.Gestures;

namespace DrawnUi.Views;

public partial class Canvas
{
    public HashSet<ISkiaGestureListener> ReceivedInput { get; } = new();

    public Dictionary<Guid, ISkiaGestureListener> HadInput { get; } = new();

    public event EventHandler? Tapped;

    protected bool IsSavedGesture(TouchActionResult type)
    {
        return type == TouchActionResult.Panning || type == TouchActionResult.Wheel || type == TouchActionResult.Up;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AddHadInput(ISkiaGestureListener consumed)
    {
        HadInput.TryAdd(consumed.Uid, consumed);
    }

    protected virtual void ProcessNetGestures(SkiaGesturesParameters args)
    {
        lock (LockIterateListeners)
        {
            if (args.Type == TouchActionResult.Down && HadInput.Count > 0)
            {
                HadInput.Clear();
            }

            _checkHover = args.Type == TouchActionResult.Pointer;
            _hadHover = false;
            ISkiaGestureListener? consumed = null;
            ISkiaGestureListener? alreadyConsumed = null;

            IsHiddenInViewTree = false;
            var manageChildFocus = false;
            var touchLocation = new SKPoint(args.Event.Location.X, args.Event.Location.Y);
            var secondPass = true;

            if (HadInput.Count > 0 && IsSavedGesture(args.Type))
            {
                var adjust = new GestureEventProcessingInfo(touchLocation, SKPoint.Empty, SKPoint.Empty, null);

                foreach (var hadInput in HadInput.Values)
                {
                    if (!hadInput.CanDraw || hadInput.InputTransparent || hadInput.GestureListenerRegistrationTime == null)
                    {
                        continue;
                    }

                    consumed = hadInput.OnSkiaGestureEvent(args, adjust);
                    if (consumed != null)
                    {
                        alreadyConsumed ??= consumed;
                        if (args.Type != TouchActionResult.Up)
                        {
                            secondPass = false;
                            AddHadInput(consumed);
                            break;
                        }
                    }
                }
            }

            if (secondPass)
            {
                var adjust = new GestureEventProcessingInfo(touchLocation, SKPoint.Empty, SKPoint.Empty, alreadyConsumed);

                foreach (var listener in GestureListeners.GetListeners())
                {
                    if (listener == null || !listener.CanDraw || listener.InputTransparent)
                    {
                        continue;
                    }

                    if (HadInput.ContainsKey(listener.Uid) && IsSavedGesture(args.Type))
                    {
                        continue;
                    }

                    if (listener == FocusedChild)
                    {
                        manageChildFocus = true;
                    }

                    var forChild = true;
                    if (args.Type != TouchActionResult.Up)
                    {
                        var hitPoint = args.Type == TouchActionResult.Pointer
                            ? args.Event.Location
                            : args.Event.StartingLocation;

                        forChild = ((SkiaControl)listener).HitIsInside(hitPoint.X, hitPoint.Y) ||
                                   listener == FocusedChild;
                        }

                    if (!forChild)
                    {
                        continue;
                    }

                    if (manageChildFocus && listener == FocusedChild)
                    {
                        manageChildFocus = false;
                    }

                    var maybeConsumed = listener.OnSkiaGestureEvent(args, adjust);
                    if (maybeConsumed != null)
                    {
                        consumed = maybeConsumed;
                    }

                    if (consumed != null)
                    {
                        if (args.Type != TouchActionResult.Up)
                        {
                            AddHadInput(consumed);
                        }

                        break;
                    }
                }
            }

            if (args.Type == TouchActionResult.Up && HadInput.Count > 0)
            {
                HadInput.Clear();
            }

            if (args.Type != TouchActionResult.Pointer && (args.Type == TouchActionResult.Up || FocusedChild != null))
            {
                if (manageChildFocus || (FocusedChild != null && consumed != FocusedChild && !FocusedChild.LockFocus))
                {
                    FocusedChild = consumed;
                }
            }

            if (ReceivedInput.Count > 0)
            {
                ReceivedInput.Clear();
            }

            if (_checkHover && !_hadHover)
            {
                HasHover = null;
            }
        }
    }

    private bool SignalNetInput(ISkiaGestureListener listener, TouchActionResult gestureType)
    {
        if (ReceivedInput.Contains(listener))
            return false;

        if (IsSavedGesture(gestureType))
        {
            ReceivedInput.Add(listener);
        }

        return true;
    }

    private void HandleNetGestureEvent(TouchActionType type, TouchActionEventArgs args1, TouchActionResult touchAction)
    {
        if (!CanDraw)
        {
            NeedCheckParentVisibility = true;
            Repaint();
            return;
        }

        if (touchAction == TouchActionResult.Tapped)
        {
            Tapped?.Invoke(this, EventArgs.Empty);
        }

        var args = SkiaGesturesParameters.Create(touchAction, args1, RenderingScale);

        try
        {
            ProcessNetGestures(args);
        }
        catch (Exception e)
        {
            Super.Log(e);
        }

        Repaint();
    }
}