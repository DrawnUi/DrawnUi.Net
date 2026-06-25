namespace DrawnUi.Draw;

public class AnimatorBase : ISkiaAnimator
{
    private bool _IsPostAnimator;
    public bool IsPostAnimator
    {
        get
        {
            return _IsPostAnimator;
        }
        set
        {
            if (_IsPostAnimator != value)
            {
                _IsPostAnimator = value;
            }
        }
    }

    public bool IsHiddenInViewTree { get; set; }

    public AnimatorBase(IDrawnBase parent)
    {
        Parent = parent;
    }

    public static double Radians(double degrees)
    {
        return degrees * 0.0174533;
    }

    protected double runDelayMs;


    protected bool Register()
    {
        IsPaused = false;

        if (Parent != null && this.IsPostAnimator)
        {
            if (this is IOverlayEffect effect)
            {
                if (!Parent.PostAnimators.Contains(effect))
                {
                    Parent.PostAnimators.Add(effect);
                    //System.Diagnostics.Debug.WriteLine($"[AnimatorBase] Added PostAnimator {Uid}");
                }
            }
            else
                throw new Exception("Post animator must implement IOverlayEffect");
        }

        var ret = Parent?.RegisterAnimator(this);

        Parent?.Update();

        return ret.GetValueOrDefault();
    }

    protected void Unregister()
    {
        if (Parent != null && this.IsPostAnimator)
        {
            if (this is IOverlayEffect effect)
            {
                if (Parent.PostAnimators.Contains(effect))
                {
                    Parent.PostAnimators.Remove(effect);
                    //System.Diagnostics.Debug.WriteLine($"[AnimatorBase] Removed PostAnimator {Uid}");
                }
            }
            else
                throw new Exception("Post animator must implement IOverlayEffect");
        }

        Parent?.UnregisterAnimator(Uid);
    }


    private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

    private readonly object _cancellationLock = new object();

    public virtual void Cancel()
    {
        lock (_cancellationLock)
        {
            if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
            {
                _cancellationTokenSource.Cancel();
            }
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();
        }
    }

    private bool isStarting;

    public virtual void Start(double delayMs = 0)
    {
        if (isStarting)
            return;

        Cancel();
        var token = _cancellationTokenSource.Token;

        isStarting = true;

        if (delayMs > 0)
        {
            runDelayMs = delayMs;
            Tasks.StartDelayed(TimeSpan.FromMilliseconds(delayMs), token, () =>
            {
                isStarting = false;
                Start();
            });
            return;
        }

        if (Register())
        {
            WasStarted = true;
            if (!IsRunning)
            {
                LastFrameTimeNanos = 0;
                StartFrameTimeNanos = 0;
                IsRunning = true;
            }
        }

        isStarting = false;
    }

    /// <summary>
    /// Time in NANOS
    /// </summary>
    /// <param name="frameTimeNanos"></param>
    /// <returns></returns>
    public virtual bool TickFrame(long frameTimeNanos)
    {
        if (!IsRunning)
            return true;

        LastFrameTimeNanos = frameTimeNanos;
        return false;
    }

    public virtual void Pause()
    {
        IsPaused = true;
    }

    public virtual void Resume()
    {
        LastFrameTimeNanos = Super.GetCurrentTimeNanos();
        IsPaused = false;
    }

    public bool IsPaused { get; set; }

    public virtual void Stop()
    {
        Unregister();

        LastFrameTimeNanos = 0;
        StartFrameTimeNanos = 0;
        IsRunning = false;

        WasStarted = false;
    }

    public Action OnStop { get; set; }
    public Action OnStart { get; set; }

    public IDrawnBase Parent { get; protected set; }

    public bool IsDeactivated
    {
        get => _isDeactivated;
        set
        {
            _isDeactivated = value;
        }
    }

    public long LastFrameTimeNanos { get; set; }

    public long StartFrameTimeNanos
    {
        get;
        set;
    }

    public Guid Uid { get; set; } = Guid.NewGuid();

    public virtual void Dispose()
    {
        Stop();
        Parent = null;

        OnStart = null;
        OnStop = null;
    }

    protected virtual void OnRunningStateChanged(bool isRunning)
    {
        if (isRunning)
        {
            OnStart?.Invoke();
        }
        else
        {
            OnStop?.Invoke();
        }
    }

    public bool IsRunning
    {
        get
        {
            return _isRunning;
        }

        set
        {
            if (_isRunning != value)
            {
                _isRunning = value;
                OnRunningStateChanged(value);
            }
        }
    }
    bool _isRunning;
    private bool _isDeactivated;

    /// <summary>
    /// Main flag to detect if animator stopped after being started (WasStarted) or stop was just called on an already stopped animator. 
    /// </summary>
    public bool WasStarted { get; protected set; }
}
