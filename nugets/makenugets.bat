@echo off
 

dotnet pack  ..\src\Net\DrawnUi\DrawnUi.Net.csproj -c Release
dotnet pack  ..\src\OpenTk\DrawnUi\DrawnUi.OpenTk.csproj -c Release
pushd ..\src\Maui & dotnet pack DrawnUi\DrawnUi.Maui.csproj -c Release & popd
dotnet pack  ..\src\Blazor\DrawnUi\DrawnUi.Blazor.csproj -c Release
dotnet pack  ..\src\Blazor\DrawnUi.Server\DrawnUi.Blazor.Server.csproj -c Release
dotnet pack  ..\src\Blazor\DrawnUi.Wasm\DrawnUi.Blazor.Wasm.csproj -c Release
dotnet pack  ..\src\Wasm\DrawnUi\DrawnUi.Wasm.csproj -c Release
dotnet pack  ..\src\Wpf\Drawnui.Wpf\DrawnUi.Wpf.csproj -c Release

dotnet pack  ..\src\Net\Addons\DrawnUi.Net.Game\DrawnUi.Net.Game.csproj -c Release
dotnet pack  ..\src\Blazor\Addons\DrawnUi.Blazor.Game\DrawnUi.Blazor.Game.csproj -c Release
dotnet pack ..\src\OpenTk\Addons\DrawnUi.OpenTk.Game\DrawnUi.OpenTk.Game.csproj -c Release
pushd ..\src\Maui & dotnet pack Addons\DrawnUi.Maui.Game\DrawnUi.Maui.Game.csproj -c Release & popd
pushd ..\src\Maui & dotnet pack Addons\DrawnUi.Maui.MapsUi\DrawnUi.Maui.MapsUi.csproj -c Release & popd
pushd ..\src\Maui & dotnet pack Addons\DrawnUi.MauiGraphics\DrawnUi.MauiGraphics.csproj -c Release & popd
dotnet pack ..\src\Wasm\Addons\DrawnUi.Wasm.Game\DrawnUi.Wasm.Game.csproj -c Release
dotnet pack ..\src\Wpf\Addons\DrawnUi.Wpf.Game\DrawnUi.Wpf.Game.csproj -c Release

dotnet pack ..\src\Blazor\Addons\DrawnUi.Blazor.Add3d\DrawnUi.Blazor.Add3d.csproj -c Release
dotnet pack ..\src\OpenTk\Addons\DrawnUi.OpenTk.Add3d\DrawnUi.OpenTk.Add3d.csproj -c Release
pushd ..\src\Maui & dotnet pack Addons\DrawnUi.Maui.Add3d\DrawnUi.Maui.Add3d.csproj -c Release & popd
dotnet pack ..\src\Wasm\Addons\DrawnUi.Wasm.Add3d\DrawnUi.Wasm.Add3d.csproj -c Release
dotnet pack ..\src\Wpf\Addons\DrawnUi.Wpf.Add3d\DrawnUi.Wpf.Add3d.csproj -c Release

pause
