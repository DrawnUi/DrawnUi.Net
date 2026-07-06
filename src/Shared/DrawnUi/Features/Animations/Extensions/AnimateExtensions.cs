using System.Numerics;

namespace DrawnUi.Draw;

public static class AnimateExtensions
{

    public static Task Translate(this SkiaControl control, Vector2 translation, float seconds = 0.4f, Easing easing = null, CancellationTokenSource cancel = default)
    {
        var x = control.TranslationX + translation.X;
        var y = control.TranslationY + translation.Y;
        return control.TranslateToAsync(x, y, (uint)(seconds * 1000), easing, cancel);
    }

    public static Task FadeIn(this SkiaControl control, float seconds = 0.4f, Easing easing = null, CancellationTokenSource cancel = default)
    {
        return control.FadeToAsync(1, (uint)(seconds * 1000), easing, cancel);
    }

    public static Task FadeOut(this SkiaControl control, float seconds = 0.4f, Easing easing = null, CancellationTokenSource cancel = default)
    {
        return control.FadeToAsync(0.0001, (uint)(seconds * 1000), easing, cancel);
    }

    /// <summary>
    /// Animate several tasks at the same time with WhenAll
    /// </summary>
    /// <param name="control"></param>
    /// <param name="animations"></param>
    /// <returns></returns>
    public static Task AnimateWith(this SkiaControl control, params Func<SkiaControl, Task>[] animations)
    {
        var tasks = Array.ConvertAll(animations, animation => animation(control));
        return Task.WhenAll(tasks);
    }

    public static Task WhenCompleted(this Task task, Action continuationAction)
    {
        if (continuationAction == null)
            throw new ArgumentNullException(nameof(continuationAction));

        return task.ContinueWith(_ => continuationAction(), TaskScheduler.FromCurrentSynchronizationContext());
    }

}