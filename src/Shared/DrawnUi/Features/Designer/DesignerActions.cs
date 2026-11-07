namespace DrawnUi.Draw;

/// <summary>
/// Actions an app declares for the DrawnUI Designer: named states it can jump to without being played
/// to them (a level, a game state, a dialog). The designer lists them under the app's screens and runs
/// one on a click; AI agents run them through the designer's MCP (<c>list_actions</c>, <c>run_action</c>).
/// <para>
/// A path names the action and its groups, slash-separated: <c>"Levels/Level 3"</c> shows as
/// "Level 3" under "Levels". Nothing here runs unless a designer is attached: the list only keeps the
/// names and their callbacks, and an app built without the designer agent never reads it.
/// </para>
/// <code>
/// for (var i = 1; i &lt;= 20; i++)
/// {
///     var level = i;
///     DesignerActions.Add($"Levels/Level {level}", () =&gt; Game.StartLevel(level));
/// }
/// DesignerActions.Add("Dialogs/Options", () =&gt; Game.ShowOptions());
/// </code>
/// </summary>
public static class DesignerActions
{
    private static readonly object Lock = new();
    private static readonly List<(string Path, Action Run)> Items = new();

    /// <summary>
    /// Raised after the list changed (an action added or removed), on the thread that changed it. The
    /// designer agent sends the new list to the designer.
    /// </summary>
    public static event EventHandler Changed;

    /// <summary>
    /// Adds an action, or replaces the one with the same path. <paramref name="run"/> is called on the
    /// app's UI thread when the designer runs it. Dispose the result to remove the action again (one a
    /// screen offers while it is shown).
    /// </summary>
    /// <param name="path">The action's name with its groups before it: <c>"Levels/Level 3"</c>.</param>
    /// <param name="run">What the action does in the app.</param>
    public static IDisposable Add(string path, Action run)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("an action needs a path", nameof(path));
        ArgumentNullException.ThrowIfNull(run);
        lock (Lock)
        {
            var at = Items.FindIndex(x => x.Path == path);
            if (at >= 0)
                Items[at] = (path, run);
            else
                Items.Add((path, run));
        }
        Changed?.Invoke(null, EventArgs.Empty);
        return new Removal(path, run);
    }

    /// <summary>
    /// Removes the action with this path, if there is one.
    /// </summary>
    public static void Remove(string path)
    {
        bool removed;
        lock (Lock)
            removed = Items.RemoveAll(x => x.Path == path) > 0;
        if (removed)
            Changed?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>
    /// The declared actions' paths, in the order they were added.
    /// </summary>
    public static IReadOnlyList<string> Paths
    {
        get
        {
            lock (Lock)
                return Items.Select(x => x.Path).ToArray();
        }
    }

    /// <summary>
    /// Runs the action with this path on the calling thread (the designer agent calls it on the UI
    /// thread). False when there is no such action.
    /// </summary>
    public static bool Run(string path)
    {
        Action run;
        lock (Lock)
            run = Items.FirstOrDefault(x => x.Path == path).Run;
        if (run == null)
            return false;
        run();
        return true;
    }

    private sealed class Removal(string path, Action run) : IDisposable
    {
        private bool _done;

        public void Dispose()
        {
            if (_done)
                return;
            _done = true;
            bool removed;
            lock (Lock)
                removed = Items.RemoveAll(x => x.Path == path && ReferenceEquals(x.Run, run)) > 0;
            if (removed)
                Changed?.Invoke(null, EventArgs.Empty);
        }
    }
}
