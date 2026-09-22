# WpfPackageDemo

The WpfSandbox app, consuming **DrawnUI for WPF from the `DrawnUi.Wpf` NuGet package** rather than
from a project reference: XAML-declared drawn controls, WPF `{Binding}` to a view model, a WPF style.

The package is a preview and is not published yet. Build it into a local feed first:

```
dotnet pack src/Wpf/Drawnui.Wpf/DrawnUi.Wpf.csproj -c Release -p:Version=1.10.6.15-preview.1 -o <your local feed>
```

then `dotnet run` here. The package brings its own dependencies, including the ANGLE natives used
by `RenderingMode="Accelerated"`.
