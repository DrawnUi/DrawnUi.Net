# Native Integration

On .NET MAUI, DrawnUI provides seamless integration with native MAUI controls through the `SkiaMauiElement` control. This allows you to embed standard MAUI controls like WebView, MediaElement, and others within your DrawnUI canvas while maintaining hardware acceleration and performance.

## SkiaMauiElement

`SkiaMauiElement` is a wrapper control that enables embedding native MAUI `VisualElement` controls within the DrawnUI rendering pipeline. This is essential for controls that require native platform implementations or when you need to integrate existing MAUI controls into your DrawnUI application.

### Key Features

- **Native Control Embedding**: Wrap any MAUI VisualElement within DrawnUI
- **Platform Optimization**: Automatic platform-specific handling (optional snapshots while moving on Android, the native view moved directly otherwise)
- **Gesture Coordination**: Proper gesture handling between DrawnUI and native controls
- **Binding Support**: Full data binding support for embedded controls
- **Performance**: Optimized rendering with minimal overhead

### Basic Usage

```xml
<draw:SkiaMauiElement
    HorizontalOptions="Fill"
    VerticalOptions="Fill">
    
    <!-- Any MAUI VisualElement can be embedded -->
    <Entry
        Placeholder="Enter text here"
        Text="{Binding UserInput}" />
        
</draw:SkiaMauiElement>
```

### WebView Integration

One of the most common use cases is embedding a WebView for displaying web content within your DrawnUI application:

```xml
<draw:SkiaLayout
    HorizontalOptions="Fill"
    VerticalOptions="Fill"
    Type="Column">

    <!-- Header -->
    <draw:SkiaLayout
        BackgroundColor="{StaticResource Gray600}"
        HorizontalOptions="Fill"
        HeightRequest="60"
        Type="Row"
        Spacing="16"
        Padding="16,0">

        <draw:SkiaButton
            Text="← Back"
            TextColor="White"
            BackgroundColor="Transparent"
            VerticalOptions="Center" />

        <draw:SkiaLabel
            x:Name="LabelTitle"
            Text="Web Browser"
            TextColor="White"
            FontSize="18"
            VerticalOptions="Center"
            HorizontalOptions="Start" />

    </draw:SkiaLayout>

    <!-- Background -->
    <draw:SkiaControl
        BackgroundColor="{StaticResource Gray600}"
        HorizontalOptions="Fill"
        VerticalOptions="Fill"
        ZIndex="-1" />

    <!-- WebView Content -->
    <draw:SkiaMauiElement
        Margin="1,0"
        HorizontalOptions="Fill"
        VerticalOptions="Fill">

        <WebView
            x:Name="ControlBrowser"
            HorizontalOptions="FillAndExpand"
            VerticalOptions="FillAndExpand" />

    </draw:SkiaMauiElement>

</draw:SkiaLayout>
```

### Code-Behind Implementation

```csharp
public partial class ScreenBrowser
{
    public ScreenBrowser(string title, string source, bool isUrl = true)
    {
        InitializeComponent();

        LabelTitle.Text = title;

        if (isUrl)
        {
            if (string.IsNullOrEmpty(source))
            {
                source = "about:blank";
            }
            var url = new UrlWebViewSource
            {
                Url = source
            };
            ControlBrowser.Source = url;
        }
        else
        {
            if (string.IsNullOrEmpty(source))
            {
                source = "";
            }
            var html = new HtmlWebViewSource
            {
                Html = source
            };
            ControlBrowser.Source = html;
        }
    }
}
```

### Platform-Specific Behavior

`SkiaMauiElement` handles platform differences automatically:

**All platforms:**
- The native view sits over the canvas and is moved/transformed directly to follow its drawn placeholder

**Android:**
- With `AnimateSnapshot="True"` (default `False`; set it when the element moves fast, for example inside a `SkiaScroll`) a bitmap snapshot of the native view is drawn while its position changes, and the real view comes back `FreezeTimeMs` (default 250) after the movement stops

### Common Integration Scenarios

#### Media Playback
```xml
<draw:SkiaMauiElement
    HorizontalOptions="Fill"
    HeightRequest="200">
    
    <MediaElement
        Source="video.mp4"
        ShowsPlaybackControls="True"
        AutoPlay="False" />
        
</draw:SkiaMauiElement>
```

#### Date/Time Pickers
```xml
<draw:SkiaLayout Type="Column" Spacing="16">
    
    <draw:SkiaMauiElement HeightRequest="50">
        <DatePicker
            Date="{Binding SelectedDate}"
            Format="dd/MM/yyyy" />
    </draw:SkiaMauiElement>
    
    <draw:SkiaMauiElement HeightRequest="50">
        <TimePicker
            Time="{Binding SelectedTime}"
            Format="HH:mm" />
    </draw:SkiaMauiElement>
    
</draw:SkiaLayout>
```

#### Native Picker
```xml
<draw:SkiaMauiElement HeightRequest="50">
    <Picker
        Title="Select an option"
        ItemsSource="{Binding Options}"
        SelectedItem="{Binding SelectedOption}" />
</draw:SkiaMauiElement>
```

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Content` | VisualElement | The native MAUI control to embed |
| `AnimateSnapshot` | bool | Android: draw a snapshot instead of the native view while it moves (default `False`) |
| `FreezeTimeMs` | int | Android: how long after the last movement the snapshot stays (default 250) |

### Important Notes

- **Content Property**: Use the `Content` property to set the embedded control, not child elements
- **Sizing**: The SkiaMauiElement will size itself based on the embedded control's requirements
- **Gestures**: Native controls handle their own gestures; DrawnUI gestures work outside the embedded area
- **Performance**: Consider the platform-specific rendering approach when designing your layout
- **Binding Context**: The embedded control automatically inherits the binding context
- **No cached ancestors**: the native view is shown only while the element is drawn as part of the live render tree. An ancestor that draws from its cache (`UseCache` other than `None`) hides it. Mind the defaults: `SkiaShape` uses `UseCache="Operations"`, and `SkiaScroll` caches itself at rest (`AutoCache="True"`), so set `UseCache="None"` / `AutoCache="False"` on them

### Limitations

- Cannot have SkiaControl subviews (use Content property instead)
- Platform-specific rendering differences may affect visual consistency
- Some complex native controls may have gesture conflicts

### Best Practices

1. **Use Sparingly**: Only embed native controls when necessary (e.g., WebView, MediaElement)
2. **Size Appropriately**: Set explicit sizes when possible to avoid layout issues
3. **Test on All Platforms**: Verify behavior across iOS, Android, and Windows
4. **Consider Alternatives**: Check if DrawnUI has a native equivalent before embedding
5. **Performance**: Monitor performance impact, especially with multiple embedded controls

## SkiaCamera

SkiaCamera is a specialized control that provides camera functionality directly within the DrawnUI canvas. It allows you to capture photos and video while maintaining the performance and visual consistency of the DrawnUI rendering pipeline.

It ships in the separate `DrawnUi.Maui.Camera` package for .NET MAUI, namespace `DrawnUi.Camera` (`xmlns:camera="clr-namespace:DrawnUi.Camera;assembly=DrawnUi.Maui.Camera"`). Its full documentation lives in [its repository](https://github.com/taublast/DrawnUi.Camera).

### Basic Usage

```xml
<camera:SkiaCamera
    x:Name="CameraControl"
    IsOn="True"
    Facing="Default"
    CameraIndex="-1"
    FlashMode="Off"
    CaptureFlashMode="Auto"
    PhotoQuality="Medium"
    PhotoFormatIndex="0"
    WidthRequest="300"
    HeightRequest="400" />
```

### Key Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `IsOn` | bool | false | Camera power state - use this to start/stop camera |
| `Facing` | CameraPosition | Default | Camera selection: Default (back), Selfie (front), Manual |
| `CameraIndex` | int | -1 | Manual camera selection index (when Facing = Manual) |
| `State` | HardwareState | Off | Current camera state (read-only) |
| `IsBusy` | bool | - | Processing state (read-only) |
| `FlashMode` | FlashMode | Off | Preview torch mode: Off, On, Strobe |
| `CaptureFlashMode` | CaptureFlashMode | Off | Flash mode for capture: Off, Auto, On |
| `PhotoQuality` | CaptureQuality | High | Photo quality: Max, High, Medium, Low, Preview, Manual |
| `PhotoFormatIndex` | int | -1 | Format index for manual capture (when PhotoQuality = Manual) |
| `CurrentStillCaptureFormat` | CaptureFormat | - | Currently selected capture format (read-only) |
| `IsFlashSupported` | bool | - | Whether flash is available (read-only) |
| `IsAutoFlashSupported` | bool | - | Whether auto flash is supported (read-only) |
| `Zoom` | double | 1.0 | Camera zoom level |
| `ZoomLimitMin` | double | 1.0 | Minimum zoom level |
| `ZoomLimitMax` | double | 10.0 | Maximum zoom level |
| `Effect` | SkiaImageEffect | None | Real-time color filters for the preview only (not applied to captured photos) |

### Examples

```xml
<!-- Basic camera with controls -->
<draw:SkiaLayout Type="Column" Spacing="10">
    <camera:SkiaCamera
        x:Name="Camera"
        IsOn="True"
        Facing="Default"
        FlashMode="Off"
        CaptureFlashMode="Auto"
        PhotoQuality="Medium"
        WidthRequest="300"
        HeightRequest="400" />

    <draw:SkiaLayout Type="Row" Spacing="10">
        <draw:SkiaButton
            Text="Capture"
            Tapped="OnCaptureClicked" />
        <draw:SkiaButton
            Text="Toggle Torch"
            Tapped="OnToggleTorchClicked" />
        <draw:SkiaButton
            Text="Switch Camera"
            Tapped="OnSwitchCameraClicked" />
    </draw:SkiaLayout>
</draw:SkiaLayout>
```

### Code-Behind Example

```csharp
private async void OnCaptureClicked(object sender, EventArgs e)
{
    try
    {
        await Camera.TakePicture();
        // Photo will be delivered via CaptureSuccess event
    }
    catch (Exception ex)
    {
        // Handle error
        await DisplayAlert("Error", $"Failed to capture photo: {ex.Message}", "OK");
    }
}

private void OnCaptureSuccess(object sender, CapturedImage captured)
{
    // Handle captured photo
    MainThread.BeginInvokeOnMainThread(async () =>
    {
        await SavePhotoAsync(captured);
    });
}

private void OnToggleTorchClicked(object sender, EventArgs e)
{
    // Toggle preview torch using property-based approach
    Camera.FlashMode = Camera.FlashMode == FlashMode.Off
        ? FlashMode.On
        : FlashMode.Off;
}

private void OnSwitchCameraClicked(object sender, EventArgs e)
{
    Camera.Facing = Camera.Facing == CameraPosition.Default
        ? CameraPosition.Selfie
        : CameraPosition.Default;
}
```

### Flash Control

SkiaCamera provides comprehensive flash control with independent preview torch and capture flash modes:

```csharp
// Preview torch control
Camera.FlashMode = FlashMode.Off;    // Disable torch
Camera.FlashMode = FlashMode.On;     // Enable torch
Camera.FlashMode = FlashMode.Strobe; // Strobe mode (future feature)

// Capture flash control
Camera.CaptureFlashMode = CaptureFlashMode.Off;   // No flash
Camera.CaptureFlashMode = CaptureFlashMode.Auto;  // Auto flash
Camera.CaptureFlashMode = CaptureFlashMode.On;    // Always flash

// Check flash capabilities
if (Camera.IsFlashSupported)
{
    // Flash is available on this camera
    Camera.FlashMode = FlashMode.On;
}
```

**Key Features:**
- **Independent Control**: Preview torch and capture flash work separately
- **Property-Based API**: Modern, bindable properties for MVVM scenarios
- **Clean API**: Simple property-based approach without legacy methods
- **Future Extensibility**: Ready for strobe and other advanced flash modes

### Camera Management

SkiaCamera provides comprehensive camera enumeration and selection capabilities:

```csharp
// Get available cameras
var cameras = await Camera.GetAvailableCamerasAsync();
foreach (var camera in cameras)
{
    Debug.WriteLine($"Camera {camera.Index}: {camera.Name} ({camera.Position})");
    Debug.WriteLine($"  ID: {camera.Id}");
    Debug.WriteLine($"  Has Flash: {camera.HasFlash}");
}

// Manual camera selection
Camera.Facing = CameraPosition.Manual;
Camera.CameraIndex = 2; // Select third camera
Camera.IsOn = true;

// Automatic selection
Camera.Facing = CameraPosition.Default; // Back camera
Camera.Facing = CameraPosition.Selfie;  // Front camera
```

### Capture Format Management

Control capture resolution and quality with flexible format selection:

```csharp
// Quality presets
Camera.PhotoQuality = CaptureQuality.Max;     // Highest resolution
Camera.PhotoQuality = CaptureQuality.High;    // 20% below Max (default)
Camera.PhotoQuality = CaptureQuality.Medium;  // Balanced quality/size
Camera.PhotoQuality = CaptureQuality.Low;     // Small photo size
Camera.PhotoQuality = CaptureQuality.Preview; // No real still shot: the current preview frame is returned

// Manual format selection
var formats = await Camera.GetAvailableCaptureFormatsAsync();
Camera.PhotoQuality = CaptureQuality.Manual;
Camera.PhotoFormatIndex = 0; // Select first format

// Read current format
var currentFormat = Camera.CurrentStillCaptureFormat;
if (currentFormat != null)
{
    Debug.WriteLine($"Current: {currentFormat.Width}x{currentFormat.Height}");
    Debug.WriteLine($"Aspect: {currentFormat.AspectRatioString}");
    Debug.WriteLine($"Pixels: {currentFormat.TotalPixels:N0}");
}
```

### Core Methods

```csharp
// Camera Management
public virtual async Task<List<CameraInfo>> GetAvailableCamerasAsync()
public static Task<bool> RequestPermissionsAsync(NeedPermissions request)
public static void CheckPermissions(Action granted, Action notGranted, NeedPermissions request)

// Capture Format Management
public virtual async Task<List<CaptureFormat>> GetAvailableCaptureFormatsAsync()
public CaptureFormat CurrentStillCaptureFormat { get; }

// Capture Operations
public async Task TakePicture()
public virtual void FlashScreen(Color color)
public async Task<string> SaveToGalleryAsync(CapturedImage captured, string album = null, int quality = 100)
public static void OpenFileInGallery(string imageFilePath)

// Camera Controls
public virtual void SetZoom(double value)

// Flash Control Methods
public void SetFlashMode(FlashMode mode)
public FlashMode GetFlashMode()
public void SetCaptureFlashMode(CaptureFlashMode mode)
public CaptureFlashMode GetCaptureFlashMode()
```

### Events

```csharp
public event EventHandler<CapturedImage> CaptureSuccess;
public event EventHandler<Exception> CaptureFailed;
public event EventHandler<LoadedImageSource> NewPreviewSet;
public event EventHandler<HardwareState> StateChanged;
public event EventHandler<string> OnError;
public event EventHandler<double> Zoomed;
```

### Data Classes

```csharp
// Capture format information
public class CaptureFormat
{
    public int Index { get; set; }                    // Index for PhotoFormatIndex
    public int Width { get; set; }                    // Width in pixels
    public int Height { get; set; }                   // Height in pixels
    public int TotalPixels => Width * Height;         // Total pixel count
    public double AspectRatio => (double)Width / Height; // Decimal aspect ratio
    public string AspectRatioString { get; }          // Standard notation ("16:9", "4:3")
    public string FormatId { get; set; }              // Platform-specific identifier
    public string Description { get; }               // "1920x1080 (16:9)"
}

// Camera information
public class CameraInfo
{
    public string Id { get; set; }                    // Platform camera ID
    public string Name { get; set; }                  // Display name
    public CameraPosition Position { get; set; }      // Camera position
    public int Index { get; set; }                    // Camera index
    public bool HasFlash { get; set; }                // Flash availability
}
```

### Enums

```csharp
public enum CameraPosition { Default, Selfie, Manual }
public enum HardwareState { Off, On, Error }
public enum CaptureQuality { Max, High, Medium, Low, Preview, Manual }
public enum FlashMode { Off, On, Strobe }
public enum CaptureFlashMode { Off, Auto, On }
public enum SkiaImageEffect { None, BlackAndWhite, Pastel, Tint, Darken, Lighten, Grayscale, Sepia, InvertColors, Contrast, Saturation, Brightness, Gamma, TSL, HSL, Custom }
```

### Gallery Integration

SkiaCamera provides `OpenFileInGallery()` method to open captured photos in the system gallery:

```csharp
private async void OnCaptureClicked(object sender, EventArgs e)
{
    try
    {
        await Camera.TakePicture();
        // Photo will be delivered via CaptureSuccess event
    }
    catch (Exception ex)
    {
        await DisplayAlert("Error", $"Failed to capture photo: {ex.Message}", "OK");
    }
}

private async void OnCaptureSuccess(object sender, CapturedImage captured)
{
    try
    {
        // Save photo to file
        var fileName = $"photo_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
        var filePath = Path.Combine(FileSystem.Current.CacheDirectory, fileName);

        using var fileStream = File.Create(filePath);
        using var data = captured.Image.Encode(SKEncodedImageFormat.Jpeg, 90);
        data.SaveTo(fileStream);

        // Open in system gallery
        SkiaCamera.OpenFileInGallery(filePath);
    }
    catch (Exception ex)
    {
        await DisplayAlert("Error", $"Failed to open in gallery: {ex.Message}", "OK");
    }
}
```

**Android FileProvider Setup Required:**

For Android, you must configure a FileProvider in `AndroidManifest.xml`:

```xml
<application>
    <provider
        android:name="androidx.core.content.FileProvider"
        android:authorities="${applicationId}.fileprovider"
        android:exported="false"
        android:grantUriPermissions="true">
        <meta-data
            android:name="android.support.FILE_PROVIDER_PATHS"
            android:resource="@xml/file_paths" />
    </provider>
</application>
```

Create `Platforms/Android/Resources/xml/file_paths.xml`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<paths xmlns:android="http://schemas.android.com/apk/res/android">
    <external-files-path name="my_images" path="Pictures" />
    <cache-path name="my_cache" path="." />
</paths>
```
