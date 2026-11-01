---
name: drawnui-designer
description: Work on a RUNNING DrawnUI app through the DrawnUI Designer's MCP server — list the connected apps, open screens, read the control tree and properties, find which code creates a control, change values live, write them into the app's C# code, undo, and check the result with a screenshot. Works with DrawnUi .NET apps (WPF, MAUI on Windows, Android and iPhone) in Debug builds. Trigger on "designer", "drawnui designer", "running app", "live edit", "write to code", "change this control", "what does the screen show", "designer mcp".
---

# DrawnUI Designer for AI agents

The DrawnUI Designer attaches to a running DrawnUI app and shows its screens, its control tree and every control's
properties. People use its window; agents use the same features through MCP. You see exactly what the app draws
now, change a value and watch it apply, then write it into the code that creates the control.

Load `drawnui` (engine semantics) and `drawnui-fluent` (C# composition) with this skill when you also edit code.

## Connect

- The designer serves MCP at `http://127.0.0.1:54271/mcp` while its window (or its VS Code panel) runs.
  Claude Code: `claude mcp add --transport http drawnui-designer http://127.0.0.1:54271/mcp`.
- An app connects by itself when it runs in **Debug** with the designer agent: DrawnUi packages that carry
  `DrawnUi.Designer.Agent` attach it with nothing to set up; Release builds never contain it.
- Android phones and iPhones on their USB cable connect the same way. Apps on other devices can connect over the
  network after `allow_devices`.

## The tools

| Tool | Use |
|---|---|
| `list_apps` | The connected apps and their `id` (every other tool takes it), plus recent apps you can start. |
| `run_app` / `restart_app` | Start a recent app, or stop and start one again; both wait until it connects. |
| `list_screens` / `open_screen` | The app's shell routes; open one, then read the tree about a second later. |
| `get_tree` | The controls drawn now: `id`, `parent`, `type`, `field`, `rect` in canvas pixels, `visible`, `source` (file:line of the code that creates it). |
| `get_properties` | A control's properties: name, .NET type, value, whether it was set. |
| `pick` / `select` | The control at a canvas point; draw a box around a control in the app. |
| `set_property` | Change a value in the running app only. |
| `write_property` | Change it in the app AND in the code; returns the file and an `undo` id, or why it was refused. |
| `undo_write` | Take a write back, if that spot of the file was not changed since. |
| `snapshot` | A PNG of what the app draws now. |
| `allow_devices` | Let apps on other devices connect; returns the address to build them with. |

## Workflow

1. `list_apps` → pick the `id`. `list_screens` + `open_screen` to reach the screen the user means.
2. `get_tree` → find the control by `type`, `field` (an `.Assign(out _field)` name), text or `rect`. To match what the
   user points at in a screenshot, use `pick` with the point.
3. `get_properties` → read before you change. Values travel as text: numbers, `True`/`False`, enum names,
   `#AARRGGBB` colors, thicknesses as `all`, `h,v` or `l,t,r,b`.
4. Try it live with `set_property`, check with `snapshot`. When it looks right, `write_property` it.
5. Report the file and line from `write_property`. Keep the `undo` id until the user is happy.

## When a write is refused

`write_property` changes one literal in fluent C#: an object initializer value (`FontSize = 18`), a constructor
argument (`new SkiaLabel("Text")`), `Color.Parse("#..")` / `Colors.X`, `new Thickness(..)`, fluent `.WithX(..)`
arguments, or the argument at the call of a small helper. It refuses, with the reason, for values that are computed,
bound, set in loops or templates, set in XAML, or set by the library for its own parts. Then edit the code yourself:
the tree's `source` gives the file and line.

## After a code edit

Under `dotnet watch` on Windows, a code change reaches the open screen by itself (hot reload). On phones hot
reload needs the IDE debugger; otherwise `open_screen` again (or `restart_app`) after editing, then `get_tree`
and `snapshot` to check.
