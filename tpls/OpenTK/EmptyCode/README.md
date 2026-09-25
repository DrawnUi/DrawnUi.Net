# DrawnUI OpenTK app

A desktop window (Windows, Linux) with one DrawnUI canvas. Your UI lives in `MainWindow.CreateMainContent()`.

## Run

```
dotnet run
```

## Publish

One self-contained, trimmed executable per platform (settings in `EmptyCode.csproj`):

```
dotnet publish -c Release -r win-x64
dotnet publish -c Release -r linux-x64
dotnet publish -c Release -r linux-arm64
```

Keep the fonts and images that land next to the executable: the app loads them from its own folder.

## Linux

Install the native libraries once:

```
sudo apt install libglfw3 libopenal1 libgl1
```

- `linux-x64` ships its own GLFW. `linux-arm64` does not (OpenTK has no arm64 GLFW build), so it needs the `libglfw3` package above.
- On Windows you can run the Linux build under WSL2: see "Build on Windows, run the Linux version under WSL2" in the DrawnUI OpenTK FAQ (https://drawnui.net/articles/opentk/faq.html).

## Update mode

`MainWindow` uses `UpdateModeType.Constant`: the canvas redraws on every frame, which suits games where something moves all the time. For an ordinary app switch to `UpdateModeType.Dynamic` and create the window with `new GameWindowSettings { UpdateFrequency = 0 }`: animations still run at full rate, but an idle window stops rendering.
