# DrawnUI for .NET

![License](https://img.shields.io/github/license/taublast/DrawnUi.svg)
![NuGet Version](https://img.shields.io/nuget/v/DrawnUi.Maui.svg)
![NuGet Downloads](https://img.shields.io/nuget/dt/AppoMobi.Maui.DrawnUi.svg)

[Source Code](https://github.com/taublast/DrawnUi.Maui) 👈

**A rendering engine for .NET, including MAUI, Blazor, and platform-agnostic hosts, built on top of SkiaSharp**

**Hardware-accelerated rendering engine** for **.NET**, with packages and hosts for **MAUI**, **Blazor**, and **DrawnUi.Net**, powered by [SkiaSharp](https://github.com/mono/SkiaSharp).

---

## 📦 Choose Your Package

**Platform and package guide:**
- [Platforms and Packages](platforms.md)

**For .NET MAUI:**
```bash
dotnet add package DrawnUi.Maui
```

**For Blazor WebAssembly:**
```bash
dotnet add package DrawnUi.Blazor.Wasm
```

**For Blazor Server:**
```bash
dotnet add package DrawnUi.Blazor.Server
```

**For OpenTK (Windows/Linux desktop games and tools):**
```xml
<ProjectReference Include="path/to/DrawnUi.OpenTk.Game/DrawnUi.OpenTk.Game.csproj" />
```

**For platform-agnostic .NET workflows:**
```bash
dotnet add package DrawnUi.Net
```

**Initialize in MauiProgram.cs when using MAUI:**
```csharp
builder.UseDrawnUi();
```

👉 Start with [Platforms and Packages](platforms.md), then jump into the runtime-specific guide.

---

## 📚 Knowledge Base

### Documentation & Guides
- **[Platforms and Packages](platforms.md)** - Choose the right package and host before you start
- **[.NET MAUI](maui/index.md)** - Native-host setup, MAUI guidance, and MAUI tutorials
- **[Installation and Setup](maui/getting-started.md)** - .NET MAUI installation and setup
- **[MAUI Tutorials](maui/tutorials.md)** - MAUI-only tutorials and example projects
- **[Porting Native to Drawn](porting-maui.md)** - MAUI-focused migration guidance from native controls to DrawnUI
- **[Blazor](blazor/index.md)** - Entry point for DrawnUI in Blazor
- **[Blazor Packages](blazor/packages.md)** - Package roles, install targets, and project reference layout
- **[Blazor WebAssembly](blazor/wasm.md)** - Browser-side `Canvas` setup and local rendering guidance
- **[Blazor Server](blazor/server.md)** - server-backed `Canvas` setup, server-rendering model, and current limits
- **[Blazor Hybrid Web App](blazor/hybrid.md)** - Mixed `InteractiveServer` and `InteractiveWebAssembly` app structure
- **[Blazor Capabilities](blazor/capabilities.md)** - Runtime fit, validated strengths, and current boundaries
- **[Blazor Migration](blazor/migration.md)** - Adoption strategy for existing Blazor apps
- **[Blazor FAQ](blazor/faq.md)** - Package choice, migration path, and support boundaries
- **[DrawnUI for OpenTK](opentk/index.md)** - OpenTK `GameWindow` host: games, GPU tools, and desktop apps on Windows/Linux
- **[DrawnUi.Net](net/index.md)** - Platform-agnostic rendering, harnesses, and headless workflows
- **[Fluent Extensions](fluent-extensions.md)** - Code-behind UI creation patterns
- **[FAQ](faq.md)** - Frequently asked questions and answers
- **[Controls Documentation](controls/index.md)** - Complete controls reference
- **[Advanced Features](advanced/index.md)** - Performance and platform topics

### Community & Support
- **[GitHub Discussions](https://github.com/taublast/DrawnUi/discussions)** - Community help and discussions
- **[GitHub Issues](https://github.com/taublast/DrawnUi.Maui/issues)** - Report bugs or ask questions

### Additional Resources
- **[Sample Apps](sample-apps.md)** - Apps built with DrawnUI
- **[How DrawnUI was created](https://taublast.github.io/posts/MauiJuly/)** - article by the creator

**Can't find what you're looking for?** → **[Ask in GitHub Discussions](https://github.com/taublast/DrawnUi/discussions)** - The community is here to help!

---

## Features

### 🎨 **Rendering & Graphics**
* **Hardware-accelerated** SkiaSharp rendering with max performance
* **Pixel-perfect controls** with complete visual customization
* **2D and 3D transforms** for advanced visual effects
* **Visual effects** for every control: filters, shaders, shadows, blur
* **Caching system** for optimized re-drawing performance

### 😍 **Development Experience**
* **Design in XAML or code-behind** - choose your preferred approach
* **Fluent C# syntax** for programmatic UI creation
* **Hot Reload compatible** for rapid development iteration
* **Virtual controls** - no native views/handlers, background thread accessible

### 🚀 **Performance & Optimization**
* **Optimized rendering** - only visible elements drawn
* **Template recycling** for efficient memory usage
* **Hardware acceleration** on all supported platforms
* **Smooth animations** targeting maximum FPS

### 👆 **Interaction & Input**
* **Advanced gesture support** - panning, scrolling, zooming, custom gestures
* **Keyboard support** - track any key combination
* **Touch and mouse** input handling
* **Multi-platform input** normalization

### 🧭 **Navigation & Layout**
* **Familiar MAUI Shell** navigation techniques on canvas
* **SkiaShell + SkiaViewSwitcher** for fully drawn app navigation
* **Modals, popups, toasts** and custom overlays
* **Enhanced layout system** with advanced positioning


---


 
