@echo off
 

dotnet pack  ..\src\Net\DrawnUi\DrawnUi.Net.csproj -c Release
dotnet pack  ..\src\OpenTk\DrawnUi\DrawnUi.OpenTk.csproj -c Release
dotnet pack  ..\src\Maui\DrawnUi\DrawnUi.Maui.csproj -c Release
dotnet pack  ..\src\Blazor\DrawnUi\DrawnUi.Blazor.csproj -c Release
dotnet pack  ..\src\Blazor\DrawnUi.Server\DrawnUi.Blazor.Server.csproj -c Release
dotnet pack  ..\src\Blazor\DrawnUi.Wasm\DrawnUi.Blazor.Wasm.csproj -c Release

dotnet pack  ..\src\Blazor\Addons\DrawnUi.Blazor.Game\DrawnUi.Blazor.Game.csproj -c Release
dotnet pack ..\src\OpenTk\Addons\DrawnUi.OpenTk.Game\DrawnUi.OpenTk.Game.csproj -c Release
dotnet pack ..\src\Maui\Addons\DrawnUi.Maui.Game\DrawnUi.Maui.Game.csproj -c Release
dotnet pack ..\src\Maui\Addons\DrawnUi.Maui.MapsUi\DrawnUi.Maui.MapsUi.csproj -c Release
dotnet pack ..\src\Maui\Addons\DrawnUi.MauiGraphics\DrawnUi.MauiGraphics.csproj -c Release

pause
