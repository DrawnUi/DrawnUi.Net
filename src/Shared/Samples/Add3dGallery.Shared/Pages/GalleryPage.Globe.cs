using System.Numerics;

namespace Add3dGallery;

/// <summary>
/// The globe: NASA's Blue Marble on a sphere, turning slowly by itself and by drag. Map pins (a glossy teardrop with a
/// pulse ring) stay on their cities, hide when their city turns to the far side, and show a name chip when tapped;
/// picking a city in the list turns the globe to it. The same look and numbers as DrawnUI for Rust's gallery3d.
/// </summary>
public partial class GalleryPage
{
    const float GlobeHeight = 420;
    /// <summary>The pin's size, its tip (the city's point) from its top left, the chip's size and how far above the point it sits.</summary>
    const float PinW = 32, PinH = 48, PinTipX = 16, PinTipY = 43, ChipW = 130, ChipH = 30, ChipRise = 78;

    static readonly Color PinRed = Color.FromRgb(234, 67, 53);

    /// <summary>The pinned cities: name, latitude and longitude in degrees.</summary>
    static readonly (string Name, float Lat, float Lon)[] Cities =
    {
        ("Tokyo", 35.7f, 139.7f),
        ("Sydney", -33.9f, 151.2f),
        ("Cape Town", -33.9f, 18.4f),
        ("London", 51.5f, -0.1f),
        ("New York", 40.7f, -74.0f),
        ("Rio de Janeiro", -22.9f, -43.2f),
    };

    SkiaModel3D _globe;
    UiButton _spinButton;
    readonly SkiaLayer[] _cityPins = new SkiaLayer[Cities.Length];
    readonly SkiaShape[] _pulses = new SkiaShape[Cities.Length];
    readonly SkiaShape[] _cityLabels = new SkiaShape[Cities.Length];
    readonly UiButton[] _cityButtons = new UiButton[Cities.Length];
    int _openCity = -1;
    bool _spinning = true;
    /// <summary>A glide to a city: 0 to 1 while it runs, and the turn it goes from and to.</summary>
    float _globeGlide = 1, _globeFromYaw, _globeFromPitch, _globeToYaw, _globeToPitch;

    /// <summary>
    /// A point on the globe for a latitude and a longitude, a hair above the surface (the pin stands on it). The image
    /// wraps once around the sphere: its left edge, longitude -180, at angle 360 around the y axis from +x.
    /// </summary>
    static Vector3 OnGlobe(float lat, float lon)
    {
        var angle = (1 - (lon + 180) / 360) * MathF.Tau;
        var phi = lat * MathF.PI / 180;
        const float r = 1.002f;
        return new Vector3(r * MathF.Cos(phi) * MathF.Cos(angle), r * MathF.Sin(phi), r * MathF.Cos(phi) * MathF.Sin(angle));
    }

    /// <summary>The view's turn that puts a point of the globe straight in front: the yaw and pitch, degrees.</summary>
    static (float Yaw, float Pitch) Facing(Vector3 p) =>
        (MathF.Atan2(p.X, p.Z) * 180 / MathF.PI, Math.Clamp(MathF.Asin(Math.Clamp(p.Y / p.Length(), -1, 1)) * 180 / MathF.PI, -55, 75));

    SkiaControl GlobeCard() => WideCard("Globe with pins", "Drag the Earth to turn it. Pins stay on their cities and hide behind the globe; tap one for its name.", TwoColumnCard(new SkiaRow
    {
        Spacing = 24,
        Children =
        {
            new SkiaShape
            {
                CornerRadius = 14,
                UseCache = SkiaCacheType.None,
                WidthRequest = ViewerWidth,
                HeightRequest = GlobeHeight,
                BackgroundColor = Color.FromRgb(8, 10, 22),
                Children =
                {
                    new SkiaModel3D(new Model3D().Part("earth", Mesh3D.Sphere(1), Colors.White))
                        {
                            Yaw = -20,
                            Pitch = 15,
                            Zoom = 0.9f,
                            AutoRotate = 10,
                            PinchZoom = true,
                            MinZoom = 0.6f,
                            MaxZoom = 1.6f,
                            ResetOnDoubleTap = true,
                            Antialias = true,
                            HorizontalOptions = LayoutOptions.Fill,
                            VerticalOptions = LayoutOptions.Fill,
                        }
                        .MaterialImage("earth", Texture3D.Decode(Theme.Earth))
                        .Assign(out _globe)
                        .Adapt(me => me.ViewChanged += (_, _) => PlacePins()),
                    // The pins, then their name chips over every pin.
                    new SkiaLayer
                    {
                        VerticalOptions = LayoutOptions.Fill,
                        Children = Cities.Select((city, i) => (SkiaControl)CityPin(i, city.Name))
                            .Concat(Cities.Select((city, i) => (SkiaControl)CityLabel(i, city.Name))).ToList(),
                    },
                }
            },
            new SkiaStack
            {
                Spacing = 12,
                // Still beside a globe that draws every frame: blitted, drawn again only when a choice changes.
                UseCache = SkiaCacheType.Image,
                WidthRequest = WideWidth - ViewerWidth - 24,
                Children =
                {
                    new SkiaStack
                    {
                        Spacing = 4,
                        Children =
                        {
                            new SkiaLabel("Offices") { FontSize = 24, FontFamily = Theme.Title, TextColor = Theme.Ink, AccessibilityRole = Aria.RoleHeading },
                            new SkiaLabel("Pick a city to turn the globe to it, or tap a pin.") { FontSize = 13, TextColor = Theme.Muted },
                        }
                    },
                    new SkiaWrap
                    {
                        Spacing = 8,
                        AccessibilityRole = Aria.RoleRadioGroup,
                        AccessibilityLabel = "Cities",
                        Children = Cities.Select((city, i) => (SkiaControl)new UiButton(city.Name, Theme.Quiet, -1) { AccessibilityRole = Aria.RoleRadio }
                            .Assign(out _cityButtons[i])
                            .OnTapped(_ => GoToCity(i))).ToList(),
                    },
                    new UiButton("Pause spin", Theme.Quiet, 158) { Margin = new Thickness(0, 6, 0, 0), AccessibilityHint = "Stops or starts the globe turning by itself" }
                        .Assign(out _spinButton)
                        .OnTapped(_ => SetSpin(!_spinning)),
                    new SkiaLabel("Image: NASA Blue Marble") { FontSize = 12, TextColor = Theme.Muted },
                }
            },
        }
    }, ViewerWidth, GlobeHeight));

    /// <summary>A map pin: a glossy teardrop with a soft shadow, a white dot inside and a ring pulsing where it stands.</summary>
    SkiaLayer CityPin(int index, string name) => new SkiaLayer
        {
            WidthRequest = PinW,
            HeightRequest = PinH,
            HorizontalOptions = LayoutOptions.Start,
            VerticalOptions = LayoutOptions.Start,
            UseCache = SkiaCacheType.None,
            // Shown while its city faces the eye: hidden, it is out of taps and screen readers too.
            IsVisible = false,
            AccessibilityRole = Aria.RoleButton,
            AccessibilityLabel = name,
            AccessibilityHint = "Shows the city's name",
            Children =
            {
                new SkiaShape { Type = ShapeType.Ellipse, WidthRequest = 30, HeightRequest = 10, Margin = new Thickness(1, 38, 0, 0), StrokeColor = PinRed, StrokeWidth = 2 }
                    .Assign(out _pulses[index]),
                new SkiaShape
                {
                    Type = ShapeType.Path,
                    PathData = "M16 0 C7.2 0 0 7 0 15.6 C0 27 16 43 16 43 C16 43 32 27 32 15.6 C32 7 24.8 0 16 0 Z",
                    WidthRequest = 32,
                    HeightRequest = 43,
                    BackgroundColor = PinRed,
                    FillGradient = new SkiaGradient
                    {
                        StartXRatio = 0.3f, StartYRatio = 0, EndXRatio = 0.7f, EndYRatio = 1,
                        Colors = new List<Color> { Color.FromRgb(255, 112, 96), Color.FromRgb(200, 40, 32) },
                    },
                    Shadows = new List<SkiaShadow> { new() { Blur = 5, Y = 3, Opacity = 0.45, Color = Colors.Black } },
                },
                new SkiaShape { Type = ShapeType.Circle, WidthRequest = 12, HeightRequest = 12, Margin = new Thickness(10, 10, 0, 0), BackgroundColor = Colors.White },
                new SkiaShape { Type = ShapeType.Ellipse, WidthRequest = 10, HeightRequest = 5, Margin = new Thickness(8, 4, 0, 0), BackgroundColor = Color.FromRgba(255, 255, 255, 90) },
            }
        }
        .Assign(out _cityPins[index])
        .OnHovered((me, on) => me.Scale = on ? 1.15 : 1)
        .OnTapped(_ => ToggleCity(index));

    /// <summary>
    /// A clean name chip over a pin. The globe under it draws every frame, the chip never changes: one image, its blurred
    /// shadow and text made once and blitted where the city shows.
    /// </summary>
    SkiaShape CityLabel(int index, string name) => new SkiaShape
    {
        WidthRequest = ChipW,
        HeightRequest = ChipH,
        CornerRadius = 15,
        UseCache = SkiaCacheType.Image,
        BackgroundColor = Color.FromRgb(250, 250, 252),
        Shadows = new List<SkiaShadow> { new() { Blur = 6, Y = 2, Opacity = 0.35, Color = Colors.Black } },
        IsVisible = false,
        InputTransparent = true,
        Children =
        {
            new SkiaLabel(name) { FontSize = 14, TextColor = Color.FromRgb(30, 32, 44), HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center },
        }
    }.Assign(out _cityLabels[index]);

    void ToggleCity(int index)
    {
        _openCity = _openCity == index ? -1 : index;
        for (var i = 0; i < Cities.Length; i++)
            _cityPins[i].AccessibilityIsPressed = i == _openCity;
        PlacePins();
    }

    /// <summary>Turns the globe to a city in 0.6 s, stops its spin and shows the city's name.</summary>
    void GoToCity(int index)
    {
        for (var i = 0; i < Cities.Length; i++)
        {
            _cityButtons[i].Fill = i == index ? Theme.Accent : Theme.Quiet;
            _cityButtons[i].AccessibilityIsPressed = i == index;
        }
        SetSpin(false);
        var (yaw, pitch) = Facing(OnGlobe(Cities[index].Lat, Cities[index].Lon));
        _globeFromYaw = _globe.Yaw;
        _globeFromPitch = _globe.Pitch;
        _globeToYaw = yaw;
        _globeToPitch = pitch;
        _globeGlide = 0;
        _openCity = -1;
        ToggleCity(index);
    }

    void SetSpin(bool on)
    {
        _spinning = on;
        _globe.AutoRotate = on ? 10 : 0;
        _spinButton.Text = on ? "Pause spin" : "Spin";
        _spinButton.AccessibilityIsPressed = !on;
    }

    /// <summary>Pins and the open name chip go where their cities show; a city on the far side hides its pin.</summary>
    void PlacePins()
    {
        for (var i = 0; i < Cities.Length; i++)
        {
            var at = _globe.ScreenPosition(OnGlobe(Cities[i].Lat, Cities[i].Lon));
            var shown = at is { Occluded: false };
            var pin = _cityPins[i];
            if (pin.IsVisible != shown)
                pin.IsVisible = shown;
            var label = _cityLabels[i];
            var labelShown = shown && _openCity == i;
            if (label.IsVisible != labelShown)
                label.IsVisible = labelShown;
            if (!shown)
                continue;
            Move(pin, at.Value.X - PinTipX, at.Value.Y - PinTipY);
            if (labelShown)
                Move(label, at.Value.X - ChipW / 2, at.Value.Y - ChipRise);
        }
    }

    /// <summary>Each frame: a glide to a city goes on, the pins' pulse rings grow and fade.</summary>
    void GlobeFrame(float delta)
    {
        if (!_globe.WasInLastFrame())
            return;
        if (_globeGlide < 1)
        {
            _globeGlide = MathF.Min(1, _globeGlide + delta / 0.6f);
            var t = _globeGlide;
            var e = t < 0.5f ? 4 * t * t * t : 1 - MathF.Pow(-2 * t + 2, 3) / 2;
            var turn = ((_globeToYaw - _globeFromYaw) % 360 + 540) % 360 - 180;
            _globe.Yaw = _globeFromYaw + turn * e;
            _globe.Pitch = _globeFromPitch + (_globeToPitch - _globeFromPitch) * e;
        }
        for (var i = 0; i < Cities.Length; i++)
        {
            if (!_cityPins[i].IsVisible)
                continue;
            // Each pulse grows and fades over 1.6 s, the pins out of step.
            var phase = (_time / 1.6f + i * 0.17f) % 1;
            var ring = _pulses[i];
            ring.Scale = 0.4f + phase;
            ring.Opacity = 1 - phase;
        }
    }
}
