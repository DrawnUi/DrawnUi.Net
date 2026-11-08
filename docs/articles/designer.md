# DrawnUI Designer

[DrawnUI Designer](https://github.com/DrawnUi/DrawnUi.Designer) attaches to your running DrawnUI app: it shows its
screens, the live control tree and the properties of every control. Change a value and the app shows it at once;
write it back and it lands in your C# code. It runs as a desktop app (Windows, macOS), inside VS Code, and for AI
agents through MCP. Download it from [Releases](https://github.com/DrawnUi/DrawnUi.Designer/releases).

Debug builds of apps on DrawnUi.Maui, DrawnUi.Wpf and DrawnUi.OpenTk (1.10.8.24 or newer, apps on `DrawnUiWindow`) carry the designer agent: run your app in Debug with the designer
open and it shows up. Release builds carry nothing. See the
[designer docs](https://github.com/DrawnUi/DrawnUi.Designer/blob/master/docs/designer.md) for phones (USB cable,
Wi-Fi) and the other platforms.

## Actions: jump the app to a state

Some screens are hard to reach by hand: level 12 of a game, the "game lost" dialog, an empty cart. Declare them as
actions and the designer lists them under the app's screens; one click puts the running app there. AI agents run the
same actions through the designer's MCP tools `list_actions` and `run_action`.

```csharp
using DrawnUi.Draw;

// once, after the app's UI exists
for (var i = 1; i <= 20; i++)
{
    var level = i;
    DesignerActions.Add($"Levels/Level {level}", () => Game.StartLevel(level));
}
DesignerActions.Add("Game/Paused", () => Game.Pause());
DesignerActions.Add("Dialogs/Options", () => Game.ShowOptions());
```

- The path names the action and its groups: `"Levels/Level 3"` shows as **Level 3** under **Levels**. The designer
  keeps the order you add them in.
- The action runs on the app's UI thread when the designer runs it.
- `Add` returns an `IDisposable`: dispose it to remove the action again, for one a screen offers only while it is
  shown. `DesignerActions.Remove(path)` removes one by its path.
- Nothing runs unless a designer is attached: the list only keeps the names and their callbacks, and Release builds
  never read it.
- Apps with [Hot Preview](https://github.com/BretJohnson/hot-preview) commands (static `[Preview]` methods without
  parameters) see them too, under **Previews**, with no change.
