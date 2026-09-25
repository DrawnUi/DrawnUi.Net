# WpfPackageDemo

The WpfSandbox app, consuming **DrawnUI for WPF from the `DrawnUi.Wpf` NuGet package** rather than
from a project reference: XAML-declared drawn controls, WPF `{Binding}` to a view model, a WPF style.

`dotnet run` here. The referenced version is the library version from `src/Directory.Build.targets`,
the one published on nuget.org. To try a build that is not published yet, pack the library into a
local feed first, then restore from that feed:

```
dotnet pack src/Wpf/Drawnui.Wpf/DrawnUi.Wpf.csproj -c Release -o <your local feed>
```

The package brings its own dependencies, including the ANGLE natives used by
`RenderingMode="Accelerated"`.
