using System.ComponentModel;
using DrawnUi.Views;

namespace DrawnUi.Add3d;

/// <summary>
/// Frees a 3D control's GPU images the moment its canvas leaves the screen (the canvas lets its drawing view go, as when
/// its window closes), not only when the control is disposed: a closed window's 3D images do not wait for the garbage
/// collector. The next draw on a new view makes them again.
/// </summary>
internal sealed class CanvasWatch
{
    readonly RenderTarget _target;
    readonly Action _released;
    DrawnView _canvas;

    public CanvasWatch(RenderTarget target, Action released)
    {
        _target = target;
        _released = released;
    }

    /// <summary>Watches this canvas from now on (null: none).</summary>
    public void Follow(DrawnView canvas)
    {
        if (ReferenceEquals(canvas, _canvas))
            return;
        if (_canvas != null)
            _canvas.PropertyChanged -= OnCanvasChanged;
        _canvas = canvas;
        if (canvas != null)
            canvas.PropertyChanged += OnCanvasChanged;
    }

    void OnCanvasChanged(object sender, PropertyChangedEventArgs e)
    {
        // Raised while the canvas holds its draw lock: it cannot draw meanwhile, and its device is still alive.
        if (e.PropertyName != nameof(DrawnView.CanvasView) || _canvas?.CanvasView != null)
            return;
        Renderer3D.ReleaseNow(_target);
        _released?.Invoke();
    }
}
