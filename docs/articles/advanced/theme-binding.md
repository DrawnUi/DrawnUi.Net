# Theme Bindings

Theme bindings ship in the MAUI package (`DrawnUi.Maui`). The default provider is `MauiThemeProvider`, which follows `Application.UserAppTheme` and `Application.RequestedTheme`.

### 📊 **Usage Examples**

#### XAML:
```xml
<draw:SkiaLabel TextColor="{draw:ThemeBinding Light=Red, Dark=Blue}" />
```

#### Code-Behind:
```csharp
// Method 1: Direct binding
ThemeBindings.SetThemeBinding(
    myLabel, SkiaLabel.TextColorProperty, Colors.Red, Colors.Blue);

// Method 2: Fluent syntax
myLabel.WithThemeBinding(SkiaLabel.TextColorProperty, Colors.Red, Colors.Blue)
       .WithThemeBinding(SkiaLabel.FontSizeProperty, 16.0, 18.0);

// Method 3: Get value without binding
var color = ThemeBindings.GetThemeValue(Colors.Red, Colors.Blue);
```

#### Custom Theme Provider:
```csharp
// Replace the default MauiThemeProvider with your own IThemeProvider
ThemeBindingManager.SetThemeProvider(new MyThemeProvider());
```

### 🔧 **Diagnostics & Monitoring**

```csharp
// Check active binding count
var activeBindings = ThemeBindingManager.ActiveBindingCount;

// Force cleanup
ThemeBindingManager.Cleanup();

// Force update all bindings
ThemeBindingManager.UpdateAllBindings();
```


 
 