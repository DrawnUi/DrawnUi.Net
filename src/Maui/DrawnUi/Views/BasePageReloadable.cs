namespace DrawnUi.Views;

/// <summary>
/// Base class for a page with canvas, supports C# HotReload for building UI with code (not XAML).
/// Override `Build()`, see examples.
/// 
/// </summary>
public class BasePageReloadable : DrawnUiBasePage, IDisposable
{

    public BasePageReloadable()
    {
        if (HotReloadPossible)
        {
            Super.HotReload -= ReloadUi; // never twice: OnHandlerChanged subscribes too
            Super.HotReload += ReloadUi;
        }
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();

        if (Handler != null)
        {
            Build();

            if (HotReloadPossible)
            {
                Super.HotReload -= ReloadUi; // never twice: the constructor subscribes too
                Super.HotReload += ReloadUi;
            }
        }
        else
        {
            Super.HotReload -= ReloadUi;
        }
    }

    /// <summary>
    /// Hot reload can reach this app: under a debugger, when the runtime reports code updates as supported, or when the
    /// app is a Debug build (dotnet watch on Mac Catalyst and iOS applies updates while MetadataUpdater.IsSupported is
    /// false). False in Release builds, so their pages never subscribe.
    /// </summary>
    protected static bool HotReloadPossible =>
        Debugger.IsAttached || System.Reflection.Metadata.MetadataUpdater.IsSupported || AppIsDebugBuild;

    private static bool? _appIsDebugBuild;

    /// <summary>The app's own assembly was compiled without optimizations (a Debug build). Read once.</summary>
    private static bool AppIsDebugBuild
    {
        get
        {
            if (_appIsDebugBuild.HasValue)
                return _appIsDebugBuild.Value;

            var assembly = Application.Current?.GetType().Assembly ?? System.Reflection.Assembly.GetEntryAssembly();
            if (assembly == null)
                return false; // asked again later, once the app is known

            var debuggable = (DebuggableAttribute)Attribute.GetCustomAttribute(assembly, typeof(DebuggableAttribute));
            _appIsDebugBuild = debuggable != null && debuggable.IsJITOptimizerDisabled;
            return _appIsDebugBuild.Value;
        }
    }

    private bool wasBuilt;

    public int CountReloads { get; protected set; }

    /// <summary>
    /// Reload code-behind constructed page
    /// </summary>
    /// <param name="obj"></param>
    protected virtual void ReloadUi(Type[] obj)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            CountReloads++;
            Build();
        });
    }

    /// <summary>
    /// Build code-behind constructed page
    /// </summary>
    public virtual void Build()
    {
        wasBuilt = true;
    }

    public void Dispose()
    {
        Dispose(true);
	    GC.SuppressFinalize(this);
	}

    public bool IsDisposed { get; protected  set; }

    protected virtual void Dispose(bool isDisposing)
    {
        Super.HotReload -= ReloadUi;

        if (!IsDisposed && isDisposing)
	    {
		    IsDisposed = true;

            Super.HotReload -= ReloadUi;

            foreach (var child in InternalChildren)
		    {
			    if (child is IDisposable disposable)
			    {
				    disposable?.Dispose();
			    }
			    else
			    if (child is Grid grid)
			    {
				    foreach (var gridChild in grid.Children)
				    {
					    if (gridChild is IDisposable disposableChild)
					    {
						    disposableChild?.Dispose();
					    }
				    }
			    }
			    else
			    if (child is StackLayout stack)
			    {
				    foreach (var stackChild in stack.Children)
				    {
					    if (stackChild is IDisposable disposableChild)
					    {
						    disposableChild?.Dispose();
					    }
				    }
			    }
		    }
		}

    }
}
