# SkiaShell

SkiaShell is a powerful navigation framework for DrawnUi applications that provides full navigation capabilities similar to MAUI's Shell, but with the performance and customization benefits of direct SkiaSharp rendering.

## Overview

SkiaShell acts as a replacement for the standard MAUI Shell, allowing for fully drawn UI with SkiaSharp while maintaining compatibility with MAUI's routing capabilities. It provides complete navigation stack management, modal presentations, popups, and toast notifications. On MAUI the shell is a page: it derives from `DrawnUiBasePage` and hosts a DrawnUI `Canvas`.

On WPF and OpenTK, `SkiaShell` is a drawn control (a `SkiaLayer`) that goes inside any canvas. It has the same verbs (routes, `GoToAsync`, popups, modals, toasts and tabs) with its own API, see the `HelloWpf` and `HelloOpenTk` samples. The rest of this page shows the MAUI shell.

### Key Features

- **MAUI-compatible navigation**: Use familiar navigation patterns with `GoToAsync`
- **Navigation stack management**: Handle screen, modal, popup, and toast stacks
- **Routing with parameters**: Support for query parameters in navigation routes
- **Modal and popup systems**: Present overlays with customizable animations
- **Background freezing**: Capture and display screenshots of current views as backgrounds
- **Toast notifications**: Show temporary messages with automatic dismissal
- **Back button handling**: Handle hardware back button with customizable behavior

## Setup

### Basic Configuration

To use SkiaShell in your application, you need to:

1. Create a page that derives from `SkiaShell`. It is a `DrawnUiBasePage`, which tracks the native keyboard to adapt the layout.
2. Set its content to a Canvas
3. Set up the required layout structure on the canvas
4. Register routes, then call `Initialize(route)`: it imports the tagged elements from the canvas and navigates to the start route

Here's a basic example:

```xml
<draw:SkiaShell
    x:Class="MyApp.MainShellPage"
    xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
    xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
    xmlns:draw="http://schemas.appomobi.com/drawnUi/2023/draw">

    <draw:Canvas
        x:Name="MainCanvas"
        RenderingMode="Accelerated"
        Gestures="Enabled"
        HorizontalOptions="Fill"
        VerticalOptions="Fill">
        
        <!-- Main content goes here -->
        <draw:SkiaLayout
            Tag="ShellLayout"
            HorizontalOptions="Fill"
            VerticalOptions="Fill">
            
            <!-- Placeholder, replaced by the start route -->
            <draw:SkiaControl Tag="RootLayout" />
            
        </draw:SkiaLayout>
    </draw:Canvas>
    
</draw:SkiaShell>
```

In your code-behind:

```csharp
public partial class MainShellPage : SkiaShell
{
    public MainShellPage()
    {
        InitializeComponent();
        
        // Register navigation routes
        RegisterRoute("main", typeof(MainScreen));
        RegisterRoute("details", typeof(DetailsPage));
        
        // Import the tagged layout and navigate to the start route
        Initialize("main");
    }
}

// The start route replaces RootLayout. It holds the SkiaViewSwitcher that pages are pushed into.
public class MainScreen : SkiaLayer
{
    public MainScreen()
    {
        Children = new List<SkiaControl>
        {
            new SkiaViewSwitcher
            {
                Tag = "NavigationLayout",
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill,
                Children = new List<SkiaControl>
                {
                    new HomePage(),
                },
            },
        };
    }
}
```

### Required Layout Tags

SkiaShell relies on specific tags to identify key components in your layout:

- `ShellLayout`: The outer container for all navigation elements (typically directly inside the Canvas). Without it the canvas content is used.
- `RootLayout`: The main layout container (inside ShellLayout), required. The start route, and any route starting with `//`, replaces it.
- `NavigationLayout`: A `SkiaViewSwitcher` inside the root that pages are pushed into. The shell logs a warning when it is missing.

## Navigation

### Basic Navigation

In the samples below, `Shell` is your `SkiaShell` instance; inside the shell page itself, call the methods directly.

```csharp
// Navigate to a registered route
await Shell.GoToAsync("details");

// Navigate with parameters
await Shell.GoToAsync("details?id=123&name=Product");

// Navigate back
bool handled = Shell.GoBack(true); // true to animate, false when there was nothing to go back to

// Replace the root with another route
await Shell.GoToAsync("//main");
```

### Push and Pop Pages

```csharp
// Push a page instance
var detailsPage = new DetailsPage();
await Shell.PushAsync(detailsPage, animated: true);

// Pop the current page
var poppedPage = await Shell.PopAsync(animated: true);

// Pop to the root page
await Shell.PopToRootAsync();
```

### Route Registration

Routes need to be registered before navigation:

```csharp
// Register a route with a page type
Shell.RegisterRoute("details", typeof(DetailsPage));
```

The shell creates the page when the route is used: from the app's services when the type is registered there, else with its parameterless constructor.

### Route Parameters

The query of the last route part, or the arguments dictionary passed to `GoToAsync`, goes to the `BindingContext` of the created page: through `IQueryAttributable`, or through a `[QueryProperty]` attribute on the view model.

```csharp
public class DetailsViewModel : IQueryAttributable
{
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        // Get query parameters from shell route
        var id = query.TryGetValue("id", out var idValue) ? idValue?.ToString() : null;
        var name = query.TryGetValue("name", out var nameValue) ? nameValue?.ToString() : null;
        
        // Use the parameters
        LoadDetails(id, name);
    }
}
```

## Modals and Popups

### Modal Presentation

```csharp
// Show a modal from a registered route
await Shell.PushModalAsync("details", useGestures: true, animated: true);

// Show a modal from a page instance
await Shell.PushModalAsync(new DetailsPage(), useGestures: true, animated: true);

// Dismiss the modal
await Shell.PopModalAsync(animated: true);
```

### Popup Presentation

```csharp
// Show popup
await Shell.OpenPopupAsync(
    content: new SkiaShape
    {
        WidthRequest = 300,
        HeightRequest = 200,
        BackgroundColor = Colors.White,
        CornerRadius = 10,
        Children = new List<SkiaControl>
        {
            new SkiaLabel("This is a popup").Center(),
        }
    },
    animated: true,
    closeWhenBackgroundTapped: true,
    showOverlay: true
);

// Close popup
await Shell.ClosePopupAsync(animated: true);
```

### Toast Notifications

```csharp
// Show a simple text toast
Shell.ShowToast("Operation completed successfully", msShowTime: 3000);

// Show a custom toast
Shell.ShowToast(new SkiaRichLabel
{
    Text = "**Important:** Your data has been saved.",
    TextColor = Colors.White
}, msShowTime: 3000);
```

## Customization

### Visual Customization

```csharp
// Set global appearance properties
SkiaShell.PopupBackgroundColor = Color.FromArgb("#80000000"); // 50% transparent black
SkiaShell.PopupsBackgroundBlur = 10; // Blur amount
SkiaShell.PopupsAnimationSpeed = 350; // Animation duration in ms
SkiaShell.ToastBackgroundColor = Color.FromArgb("#E6323232");
SkiaShell.ToastTextColor = Colors.White;
```

### Animation Control

Every navigation call can skip its animation. Durations are set on the `SkiaViewSwitcher` (`PagesAnimationSpeed`, `TabsAnimationSpeed`, in ms) and by `SkiaShell.PopupsAnimationSpeed`:

```csharp
// Navigate without animation
await Shell.GoToAsync("details", false);

// Pass arguments to the page's BindingContext
await Shell.GoToAsync("details", true, new Dictionary<string, object> { ["id"] = 123 });

// Modal without animation
await Shell.PushModalAsync("settings", useGestures: true, animated: false);
```

### Navigation Events

```csharp
// Subscribe to navigation events
Shell.Navigated += OnNavigated;
Shell.Navigating += OnNavigating;
Shell.RouteChanged += OnRouteChanged;

// Handle the events
private void OnNavigating(object sender, SkiaShellNavigatingArgs e)
{
    // Access navigation details
    NavigationSource source = e.Source; // Push, Pop...
    string route = e.Route;
    SkiaControl view = e.View;          // the control that will navigate
    SkiaControl current = e.Previous;   // the control upfront now
    
    // Optionally cancel navigation
    if (HasUnsavedChanges)
    {
        e.Cancel = true;
        ShowSavePrompt();
    }
}

private void OnNavigated(object sender, SkiaShellNavigatedArgs e)
{
    // Navigation completed
    Debug.WriteLine($"{e.Source} navigated to {e.Route}");
}
```

### Custom Back Navigation

Implement the `SkiaShell.IHandleGoBack` interface to handle back navigation. `GoBack` asks the static `SkiaShell.OnShellGoBack` handler first, and the `BindingContext` of the top modal when it implements the interface:

```csharp
public class EditViewModel : SkiaShell.IHandleGoBack
{
    public bool OnShellGoBack(bool animate)
    {
        // Check for unsaved changes
        if (HasUnsavedChanges)
        {
            // Show confirmation dialog
            ShowConfirmationDialog();
            
            // Return true to indicate we're handling the back navigation
            return true;
        }
        
        // Return false to let the default back navigation occur
        return false;
    }
}
```

## Advanced Features

### Background Freezing

When showing modals or popups, SkiaShell can freeze the background content by taking a screenshot:

```csharp
// Show a modal with frozen background (true by default)
await Shell.PushModalAsync("details", useGestures: true, animated: true, freezeBackground: true);
```

The frozen screenshot is tinted with `SkiaShell.PopupBackgroundColor` and blurred by `SkiaShell.PopupsBackgroundBlur`.

### Custom Modal Presentation

A modal is a `SkiaShell.ModalWrapper` holding a `SkiaDrawer` that slides in from the bottom. Create a custom modal presentation style by returning your own wrapper:

```csharp
// Subclass SkiaShell to customize modal presentation
public class CustomShell : SkiaShell
{
    public class SideModalWrapper : ModalWrapper
    {
        public SideModalWrapper(bool useGestures, bool animated, bool willFreeze, Color backgroundColor, SkiaShell shell)
            : base(useGestures, animated, willFreeze, backgroundColor, shell)
        {
        }

        public override void WrapContent(SkiaControl content)
        {
            base.WrapContent(content);
            
            // Customize the drawer
            Drawer.Direction = DrawerDirection.FromRight;
        }
    }

    protected override ModalWrapper CreateModalDrawer(bool useGestures, bool animated, bool willFreeze, Color backgroundColor)
    {
        return new SideModalWrapper(useGestures, animated, willFreeze, backgroundColor, this)
        {
            Tag = "Modal",
            ZIndex = ZIndexModals + ModalStack.Count,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
        };
    }
}
```

### Handling Page Lifecycle

Implement `IVisibilityAware` on a page: the `SkiaViewSwitcher` calls it when the page shows and hides. `SkiaLayout` already has the four methods as virtual ones:

```csharp
public class MyPage : SkiaLayout, IVisibilityAware
{
    public override void OnAppearing()
    {
        // Page is becoming visible
        LoadData();
    }
    
    public override void OnDisappearing()
    {
        // Page is being hidden
        SaveData();
    }
}
```

## Example: Complete Shell Application

Here's a complete example of a minimal shell-based application:

```xml
<!-- MainShell.xaml -->
<draw:SkiaShell
    x:Class="MyApp.MainShell"
    xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
    xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
    xmlns:draw="http://schemas.appomobi.com/drawnUi/2023/draw">

    <draw:Canvas
        x:Name="MainCanvas"
        RenderingMode="Accelerated"
        Gestures="Enabled"
        HorizontalOptions="Fill"
        VerticalOptions="Fill">
        
        <draw:SkiaLayout
            Tag="ShellLayout"
            BackgroundColor="#F0F0F0"
            HorizontalOptions="Fill"
            VerticalOptions="Fill">
            
            <!-- Replaced by the "main" route -->
            <draw:SkiaControl Tag="RootLayout" />
            
        </draw:SkiaLayout>
    </draw:Canvas>
</draw:SkiaShell>
```

```csharp
// MainShell.xaml.cs
public partial class MainShell : SkiaShell
{
    public MainShell()
    {
        InitializeComponent();
        
        // Register routes
        RegisterRoute("main", typeof(TabsScreen));
        RegisterRoute("details", typeof(DetailsPage));
        
        // Initialize shell and navigate to initial route
        Initialize("main");
    }
    
    protected override bool OnBackButtonPressed()
    {
        // Let shell handle back button
        return GoBack(true);
    }
}

// Root screen: each child of the SkiaViewSwitcher is the root of one tab,
// pages pushed with GoToAsync("details") go into the selected tab
public class TabsScreen : SkiaLayer
{
    SkiaViewSwitcher _tabs;

    public TabsScreen()
    {
        Children = new List<SkiaControl>
        {
            new SkiaViewSwitcher
            {
                Tag = "NavigationLayout",
                Margin = new Thickness(0, 0, 0, 60),
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill,
                Children = new List<SkiaControl>
                {
                    new HomePage(),
                    new ProfilePage(),
                    new SettingsPage(),
                },
                SelectedIndex = 0,
            }.Assign(out _tabs),

            // Bottom tabs
            new SkiaGrid
            {
                HeightRequest = 60,
                BackgroundColor = Colors.White,
                VerticalOptions = LayoutOptions.End,
                Children = new List<SkiaControl>
                {
                    new SkiaLabel("Home").Center().SetGrid(0, 0).OnTapped(me => _tabs.SelectedIndex = 0),
                    new SkiaLabel("Profile").Center().SetGrid(1, 0).OnTapped(me => _tabs.SelectedIndex = 1),
                    new SkiaLabel("Settings").Center().SetGrid(2, 0).OnTapped(me => _tabs.SelectedIndex = 2),
                },
            }.WithColumnDefinitions("*,*,*"),
        };
    }
}
```

## SkiaTabsSelector

`SkiaTabsSelector` is a layout for building top and bottom tab bars. Its children of type `TabType` (default `SkiaLabel`) are the tabs; any other child just draws, so you can add icons, backgrounds or an indicator. It tracks the selection; the look of the selected tab is yours.

### Basic Usage

```xml
<draw:SkiaTabsSelector
    x:Name="TabsSelector"
    Type="Row"
    Spacing="24"
    SelectedIndex="0"
    HeightRequest="50"
    BackgroundColor="White"
    CommandTabSelected="{Binding SelectTabCommand}">

    <draw:SkiaLabel Text="Home" />
    <draw:SkiaLabel Text="Search" />
    <draw:SkiaLabel Text="Profile" />
    <draw:SkiaLabel Text="Settings" />
</draw:SkiaTabsSelector>
```

Tapping a tab does not select it by itself: set `SelectedIndex` from your tap handler, or execute `CommandTappedTab` with the tab index.

### Code-Behind Example

```csharp
// Subclass to style the selected tab
public class MyTabs : SkiaTabsSelector
{
    public override async Task ApplySelectedIndex(bool tabsChanged, int selectedIndex)
    {
        await base.ApplySelectedIndex(tabsChanged, selectedIndex);

        foreach (var tab in SelectableTabs)
        {
            if (tab.VIew is SkiaLabel label)
                label.TextColor = tab.IsSelected ? Colors.Blue : Colors.Gray;
        }
    }
}

new MyTabs
{
    Type = LayoutType.Row,
    Spacing = 24,
    HeightRequest = 60,
    CommandTabSelected = new Command(index => Console.WriteLine($"Selected tab: {index}")),
    Children = new List<SkiaControl>
    {
        new SkiaLabel("Tab 1").OnTapped(me => _tabs.SelectedIndex = 0),
        new SkiaLabel("Tab 2").OnTapped(me => _tabs.SelectedIndex = 1),
        new SkiaLabel("Tab 3").OnTapped(me => _tabs.SelectedIndex = 2),
    }
}.Assign(out _tabs)
```

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `SelectedIndex` | int | Index of the currently selected tab, default -1 |
| `TabType` | Type | Type of the children treated as tabs, default `SkiaLabel` |
| `LastSelectedIndex` | int | Index selected before the current one |
| `CommandTabSelected` | ICommand | Runs with the new index when the selection changes |
| `CommandTabReselected` | ICommand | Runs with the index when the selected tab is tapped again through `CommandTappedTab` |
| `CommandTappedTab` | ICommand | Read-only. Execute it with a tab index: selects that tab, or reselects it |

### Events

`SkiaTabsSelector` raises no events: use `CommandTabSelected` and `CommandTabReselected`, or override `OnTabSelectionChanged` and `OnTabReselected`.

## SkiaViewSwitcher

`SkiaViewSwitcher` allows you to switch your views with animations like pop, push, and slide transitions. Each child is the root of one tab, `SelectedIndex` picks the visible tab, and every tab keeps its own stack of pushed views.

### Basic Usage

```xml
<draw:SkiaViewSwitcher
    x:Name="ViewSwitcher"
    SelectedIndex="0"
    PagesAnimationSpeed="300"
    HorizontalOptions="Fill"
    VerticalOptions="Fill">

    <!-- Each child is the root of one tab -->
    <local:HomeView />
    <local:SearchView />
</draw:SkiaViewSwitcher>
```

`PagesAnimationSpeed` (default 200) and `TabsAnimationSpeed` (default 150) are in milliseconds. `AnimatePages` (default true) and `AnimateTabs` (default false) turn the animations on or off.

### Code-Behind Example

```csharp
// Switch to another tab
ViewSwitcher.SelectedIndex = 1;

// Push a view on top of the current tab (adds to stack)
ViewSwitcher.PushView(new MyCustomView(), animated: true);

// Pop the current view
await ViewSwitcher.PopPage();

// Pop the current tab back to its root
await ViewSwitcher.PopTabToRoot();
```

## Performance Considerations

- **Layer Management**: SkiaShell maintains separate navigation stacks for better organization and performance
- **Z-Index Control**: Different types of content (modals, popups, toasts) have different Z-index ranges
- **Animation Control**: Customize animations or disable them for better performance
- **Background Freezing**: Uses screenshots to avoid continuously rendering background content
- **Locking Mechanism**: Uses semaphores to prevent multiple simultaneous navigation operations