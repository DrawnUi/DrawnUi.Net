namespace DrawnUi.Draw;

public class SkiaGesturesInfo
{
    public SkiaGesturesParameters Args { get; set; }
    public GestureEventProcessingInfo Info { get; set; }
    public bool Consumed { get; set; }

    public static SkiaGesturesInfo Create(SkiaGesturesParameters args, GestureEventProcessingInfo info)
    {
        return new ()
        {
            Args = args,
            Info = info
        };
    }
}

public class SkiaGesturesParameters
{
    //public SKPoint PanningOffset { get; set; }
    public TouchActionResult Type { get; set; }
    public TouchActionEventArgs Event { get; set; }

    /// <summary>
    /// When this gesture ARRIVED from the platform (<see cref="Super.GetCurrentTimeNanos"/>), not when it
    /// gets processed. Gestures are postponed to the next frame, so a long frame (LoadMore + window engage
    /// + measurement) drains a whole burst of them at once and every one of them would otherwise look like
    /// it happened "now": the accumulated pan deltas teleport the scroll and a seconds-old flick starts a
    /// full-speed fling into content the user never saw. Consumers must judge staleness against THIS,
    /// never against processing time. Zero when unknown (synthetic/test gestures).
    /// </summary>
    public long ArrivedTimeNanos { get; set; }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="action"></param>
    /// <param name="args"></param>
    /// <param name="scaleRatio"></param>
    /// <returns></returns>
    public static SkiaGesturesParameters Create(TouchActionResult action, TouchActionEventArgs args, float renderingScale)
    {
        // Stamped HERE because Create runs on the platform input thread, at arrival — processing can be
        // one long frame later. See ArrivedTimeNanos.
        var arrived = Super.GetCurrentTimeNanos();

        if (renderingScale != args.Scale)
        {
            return new SkiaGesturesParameters()
            {
                Type = action,
                Event = args.Rescale(renderingScale),
                ArrivedTimeNanos = arrived
            };
        }

        return new SkiaGesturesParameters()
        {
            Type = action,
            Event = args,
            ArrivedTimeNanos = arrived
        };
    }

    public static SkiaGesturesParameters Empty
    {
        get
        {
            return new SkiaGesturesParameters();
        }
    }
}

public struct GestureEventProcessingInfo
{
    /// <summary>
    /// Location of the gesture accounting for transforms. Might include all transforms from parents upper th rendering tree.
    /// </summary>
    public SKPoint MappedLocation { get; set; }

    /// <summary>
    /// Coordinate offset used to transform touch coordinates from parent's 
    /// coordinate space to this control's local space.
    /// </summary>
    public SKPoint ChildOffset { get; set; }

    /// <summary>
    /// Direct coordinate offset used for gesture processing without considering 
    /// cached transformations; useful for direct position calculations.
    /// </summary>
    public SKPoint ChildOffsetDirect { get; set; }

    /// <summary>
    /// Reference to a gesture listener that has already consumed this gesture.
    /// Used to track gesture ownership through the control hierarchy.
    /// </summary>
    public ISkiaGestureListener AlreadyConsumed { get; set; }

    
    public GestureEventProcessingInfo(SKPoint mappedLocation, SKPoint childOffset1, SKPoint childOffsetDirect, ISkiaGestureListener wasConsumed)
    {
        MappedLocation = mappedLocation;
        ChildOffset = childOffset1;
        ChildOffsetDirect = childOffsetDirect;
        AlreadyConsumed = wasConsumed;
    }

    //public GestureEventProcessingInfo()
    //{

    //}

    public static GestureEventProcessingInfo Empty
    {
        get
        {
            return new GestureEventProcessingInfo();
        }
    }
}
