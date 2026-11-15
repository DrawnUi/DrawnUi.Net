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
    // A pin is a round badge centered on its city, its halo around it; the name chip stands above the badge.
    const float PinW = 44, PinH = 44, PinTipX = 22, PinTipY = 22, ChipW = 168, ChipH = 46, ChipRise = 62;


    /// <summary>The pinned cities: name, country, latitude and longitude in degrees.</summary>
    static readonly (string Name, string Country, float Lat, float Lon)[] Cities =
    {
        ("Tokyo", "Japan", 35.7f, 139.7f),
        ("Sydney", "Australia", -33.9f, 151.2f),
        ("Cape Town", "South Africa", -33.9f, 18.4f),
        ("London", "United Kingdom", 51.5f, -0.1f),
        ("New York", "United States", 40.7f, -74.0f),
        ("Rio de Janeiro", "Brazil", -22.9f, -43.2f),
    };

    SkiaModel3D _globe;
    UiButton _spinButton;
    readonly SkiaLayer[] _cityPins = new SkiaLayer[Cities.Length];
    readonly SkiaLayer[] _pulses = new SkiaLayer[Cities.Length];
    readonly SkiaLayer[] _cityLabels = new SkiaLayer[Cities.Length];
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
                            .Concat(Cities.Select((city, i) => (SkiaControl)CityLabel(i, city.Name, city.Country))).ToList(),
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

    static readonly Color StarGold = Color.FromRgb(246, 184, 32);
    /// <summary>The callouts' dark glass.</summary>
    static readonly Color CalloutFill = Color.FromRgba(30, 32, 46, 240);

    /// <summary>
    /// A city pin: a golden badge with a white ring and a white star, a soft shadow, and a warm halo that breathes
    /// (it swells and fades slowly, each city a little out of step), as a live marker on a map does.
    /// </summary>
    /// <summary>A thin ray of the flare: bright in the middle, fading to nothing at both ends.</summary>
    static SkiaShape Ray(float length, float thickness, float degrees) => new()
    {
        WidthRequest = length,
        HeightRequest = thickness,
        HorizontalOptions = LayoutOptions.Center,
        VerticalOptions = LayoutOptions.Center,
        Rotation = degrees,
        BackgroundColor = Colors.White,
        FillBlendMode = SKBlendMode.Screen,
        FillGradient = new SkiaGradient
        {
            StartXRatio = 0, StartYRatio = 0.5f, EndXRatio = 1, EndYRatio = 0.5f,
            Colors = new List<Color> { Colors.White.WithAlpha(0f), Color.FromRgba(255, 248, 220, 240), Colors.White.WithAlpha(0f) },
            ColorPositions = new List<double> { 0, 0.5, 1 },
        },
    };

    static readonly Color SparkGold = Color.FromRgb(255, 200, 70);

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
            // a flare of light on the city: a soft gold glow, a white-hot core and four thin rays
            Children =
            {
                new SkiaLayer
                {
                    WidthRequest = PinW,
                    HeightRequest = PinH,
                    Children =
                    {
                        new SkiaShape
                        {
                            Type = ShapeType.Circle,
                            WidthRequest = PinW,
                            HeightRequest = PinH,
                            BackgroundColor = SparkGold,
                            // light only brightens what is under it: no dark disc over the ocean
                            FillBlendMode = SKBlendMode.Screen,
                            FillGradient = new SkiaGradient
                            {
                                Type = GradientType.Circular,
                                StartXRatio = 0.5f, StartYRatio = 0.5f,
                                // a tight bloom around the core only: a wide gold haze over the deep blue ocean
                                // reads as a grey smudge, not as light
                                Colors = new List<Color> { Color.FromRgba(255, 250, 230, 255), Color.FromRgba(255, 226, 150, 200), SparkGold.WithAlpha(0f), SparkGold.WithAlpha(0f) },
                                ColorPositions = new List<double> { 0, 0.1, 0.28, 1 },
                            },
                        },
                        Ray(PinW, 2, 0),
                        Ray(PinW, 2, 90),
                        Ray(PinW * 0.55f, 1.5f, 45),
                        Ray(PinW * 0.55f, 1.5f, 135),
                        new SkiaShape
                        {
                            Type = ShapeType.Circle,
                            WidthRequest = 5,
                            HeightRequest = 5,
                            HorizontalOptions = LayoutOptions.Center,
                            VerticalOptions = LayoutOptions.Center,
                            BackgroundColor = Colors.White,
                            FillBlendMode = SKBlendMode.Screen,
                        },
                    }
                }
                .Assign(out _pulses[index]),
            }
        }
        .Assign(out _cityPins[index])
        .OnHovered((me, on) => me.Scale = on ? 1.15 : 1)
        .OnTapped(_ => ToggleCity(index));

    /// <summary>
    /// A city's callout over its pin: a dark glass card with a hairline edge and a soft shadow, a gold star, the city in
    /// semibold and its country under it. The globe under it draws every frame, the
    /// callout never changes: one image, made once and blitted where the city shows.
    /// </summary>
    SkiaLayer CityLabel(int index, string name, string country) => new SkiaLayer
    {
        WidthRequest = ChipW,
        HeightRequest = ChipH,
        UseCache = SkiaCacheType.Image,
        IsVisible = false,
        InputTransparent = true,
        Children =
        {
            new SkiaShape
            {
                WidthRequest = ChipW,
                HeightRequest = ChipH,
                CornerRadius = 12,
                BackgroundColor = CalloutFill,
                StrokeColor = Color.FromRgba(255, 255, 255, 30),
                StrokeWidth = 1,
                Shadows = new List<SkiaShadow> { new() { Blur = 10, Y = 4, Opacity = 0.5, Color = Colors.Black } },
                Children =
                {
                    new SkiaRow
                    {
                        Spacing = 9,
                        Padding = new Thickness(12, 0),
                        VerticalOptions = LayoutOptions.Center,
                        Children =
                        {
                            new SkiaShape
                            {
                                Type = ShapeType.Path,
                                PathData = "M7.00 0.40 L8.70 5.05 L13.66 5.24 L9.76 8.30 L11.11 13.06 L7.00 10.30 L2.89 13.06 L4.24 8.30 L0.34 5.24 L5.30 5.05 Z",
                                WidthRequest = 14,
                                HeightRequest = 14,
                                VerticalOptions = LayoutOptions.Center,
                                BackgroundColor = StarGold,
                            },
                            new SkiaStack
                            {
                                Spacing = 1,
                                VerticalOptions = LayoutOptions.Center,
                                Children =
                                {
                                    new SkiaLabel(name) { FontSize = 14, FontFamily = Theme.Title, TextColor = Theme.Ink, MaxLines = 1 },
                                    new SkiaLabel(country) { FontSize = 11, TextColor = Theme.Muted, MaxLines = 1 },
                                }
                            },
                        }
                    },
                }
            },
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
        WakeLoop();
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
            // Each light flashes now and then: a short bright swell, then calm, every city at its own moment.
            var phase = (_time / 2.6f + i * 0.37f) % 1;
            var flash = MathF.Pow(MathF.Max(0, MathF.Sin(phase * MathF.PI)), 8);
            var size = 0.45f + 0.75f * flash;
            var flare = _pulses[i];
            flare.Scale = size;
            flare.Rotation = 20 * flash;
        }
    }
}
