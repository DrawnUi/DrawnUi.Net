using System.Numerics;

namespace Add3dGallery;

/// <summary>
/// The globe: NASA's Blue Marble on a sphere, turning slowly by itself and by drag. Pins stay on their cities, hide
/// when their city turns to the far side, and show the city's name when tapped; picking a city in the list turns the
/// globe to it.
/// </summary>
public partial class GalleryPage
{
    const float GlobeHeight = 400, CityDot = 16, CityLabelHeight = 28;

    /// <summary>The pinned cities: name, latitude and longitude in degrees.</summary>
    static readonly (string Name, float Lat, float Lon)[] Cities =
    {
        ("Tokyo", 35.68f, 139.69f),
        ("Sydney", -33.87f, 151.21f),
        ("Cape Town", -33.92f, 18.42f),
        ("London", 51.51f, -0.13f),
        ("New York", 40.71f, -74.01f),
        ("Rio de Janeiro", -22.91f, -43.17f),
    };

    SkiaModel3D _globe;
    UiButton _spinButton;
    readonly SkiaShape[] _cityPins = new SkiaShape[Cities.Length];
    readonly SkiaShape[] _cityLabels = new SkiaShape[Cities.Length];
    readonly UiButton[] _cityButtons = new UiButton[Cities.Length];
    readonly bool[] _cityOpen = new bool[Cities.Length];
    bool _spinning = true;
    /// <summary>A glide to a city: 0 to 1 while it runs, and the turn it goes from and to.</summary>
    float _globeGlide = 1, _globeFromYaw, _globeFromPitch, _globeToYaw, _globeToPitch;

    /// <summary>
    /// A point on the unit sphere for a latitude and a longitude, in the sphere's own units. The texture wraps once
    /// around the sphere: u = 1 - angle / 360 around the y axis from +x, so longitude -180 (the map's left edge) is at
    /// u = 0.
    /// </summary>
    static Vector3 OnGlobe(float lat, float lon)
    {
        var u = (lon + 180) / 360;
        var angle = (1 - u) * MathF.Tau;
        var phi = lat * MathF.PI / 180;
        return new Vector3(MathF.Cos(phi) * MathF.Cos(angle), MathF.Sin(phi), MathF.Cos(phi) * MathF.Sin(angle));
    }

    /// <summary>The view's turn that puts a point of the globe straight in front: the yaw and pitch, degrees.</summary>
    static (float Yaw, float Pitch) Facing(Vector3 p) =>
        (MathF.Atan2(p.X, p.Z) * 180 / MathF.PI, Math.Clamp(MathF.Asin(Math.Clamp(p.Y, -1, 1)) * 180 / MathF.PI, -55, 75));

    SkiaControl GlobeCard() => WideCard(new SkiaRow
    {
        Spacing = 24,
        Children =
        {
            new SkiaShape
            {
                CornerRadius = 12,
                UseCache = SkiaCacheType.None,
                WidthRequest = ViewerWidth,
                HeightRequest = GlobeHeight,
                // A night sky behind the Earth.
                FillGradient = new SkiaGradient
                {
                    Type = GradientType.Circular,
                    StartXRatio = 0.5f,
                    StartYRatio = 0.5f,
                    Colors = new List<Color> { Color.FromRgb(30, 40, 78), Color.FromRgb(10, 12, 24) },
                },
                Children =
                {
                    new SkiaModel3D(new Model3D().Part("earth", Mesh3D.Sphere(1), Colors.White))
                        {
                            Yaw = 0,
                            Pitch = 15,
                            Zoom = 0.82f,
                            AutoRotate = 10,
                            Antialias = true,
                            HorizontalOptions = LayoutOptions.Fill,
                            VerticalOptions = LayoutOptions.Fill,
                        }
                        .MaterialImage("earth", Texture3D.Decode(Theme.Earth))
                        .Assign(out _globe),
                    new SkiaLayer
                    {
                        VerticalOptions = LayoutOptions.Fill,
                        Children = Cities.SelectMany((city, i) => new SkiaControl[] { CityLabel(i, city.Name), CityPin(i, city.Name) }).ToList(),
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
                        UseCache = SkiaCacheType.Image,
                        Children =
                        {
                            new SkiaLabel("Offices") { FontSize = 22, FontFamily = Theme.Title, TextColor = Theme.Ink, AccessibilityRole = Aria.RoleHeading },
                            new SkiaLabel("Pick a city to turn the globe to it, or tap a pin.") { FontSize = 13, TextColor = Theme.Muted },
                        }
                    },
                    new SkiaWrap
                    {
                        Spacing = 8,
                        AccessibilityRole = Aria.RoleRadioGroup,
                        AccessibilityLabel = "Cities",
                        Children = Cities.Select((city, i) => (SkiaControl)new UiButton(city.Name, Theme.Quiet, 158) { AccessibilityRole = Aria.RoleRadio }
                            .Assign(out _cityButtons[i])
                            .OnTapped(_ => GoToCity(i))).ToList(),
                    },
                    new UiButton("Pause spin", Theme.Quiet, 158) { Margin = new Thickness(0, 6, 0, 0), AccessibilityHint = "Stops or starts the globe turning by itself" }
                        .Assign(out _spinButton)
                        .OnTapped(_ => SetSpin(!_spinning)),
                }
            },
        }
    });

    /// <summary>A pin on a city: a dot with a ring; tap it to show or hide the city's name.</summary>
    SkiaShape CityPin(int index, string name) => new SkiaShape
        {
            Type = ShapeType.Circle,
            WidthRequest = CityDot,
            HeightRequest = CityDot,
            BackgroundColor = Color.FromRgb(255, 92, 92),
            StrokeColor = Colors.White,
            StrokeWidth = 2.5,
            // Shown while its city faces the eye: hidden, it is out of taps and screen readers too.
            IsVisible = false,
            AccessibilityRole = Aria.RoleButton,
            AccessibilityLabel = $"{name}, pin",
            AccessibilityHint = "Shows the city's name",
            Shadows = new List<SkiaShadow> { new() { Blur = 3, Y = 1, Opacity = 0.5, Color = Colors.Black } },
        }
        .Assign(out _cityPins[index])
        .OnHovered((me, on) => me.BackgroundColor = on ? Color.FromRgb(255, 150, 150) : Color.FromRgb(255, 92, 92))
        .OnTapped(_ => ToggleCity(index));

    SkiaShape CityLabel(int index, string name) => new SkiaShape
    {
        CornerRadius = 8,
        HeightRequest = CityLabelHeight,
        Padding = new Thickness(10, 0),
        BackgroundColor = Color.FromRgba(14, 16, 24, 220),
        StrokeColor = Theme.Line,
        StrokeWidth = 1,
        IsVisible = false,
        InputTransparent = true,
        Children =
        {
            new SkiaLabel(name) { FontSize = 13, FontFamily = Theme.Title, TextColor = Theme.Ink, VerticalOptions = LayoutOptions.Center },
        }
    }.Assign(out _cityLabels[index]);

    void ToggleCity(int index)
    {
        _cityOpen[index] = !_cityOpen[index];
        _cityPins[index].AccessibilityIsPressed = _cityOpen[index];
    }

    /// <summary>Turns the globe to a city in half a second, stops its spin and shows the city's name.</summary>
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
        _cityOpen[index] = true;
        _cityPins[index].AccessibilityIsPressed = true;
    }

    void SetSpin(bool on)
    {
        _spinning = on;
        _globe.AutoRotate = on ? 10 : 0;
        _spinButton.Text = on ? "Pause spin" : "Spin";
        _spinButton.AccessibilityIsPressed = !on;
    }

    /// <summary>Each frame: a glide to a city goes on; pins and names follow their cities, hidden on the far side.</summary>
    void GlobeFrame(float delta)
    {
        if (!_globe.WasInLastFrame())
            return;
        if (_globeGlide < 1)
        {
            _globeGlide = MathF.Min(1, _globeGlide + delta / 0.6f);
            var e = 1 - MathF.Pow(1 - _globeGlide, 3);
            var turn = ((_globeToYaw - _globeFromYaw) % 360 + 540) % 360 - 180;
            _globe.Yaw = _globeFromYaw + turn * e;
            _globe.Pitch = _globeFromPitch + (_globeToPitch - _globeFromPitch) * e;
        }
        for (var i = 0; i < Cities.Length; i++)
        {
            var at = _globe.ScreenPosition(OnGlobe(Cities[i].Lat, Cities[i].Lon));
            var shown = at is { Occluded: false };
            var pin = _cityPins[i];
            if (pin.IsVisible != shown)
                pin.IsVisible = shown;
            var label = _cityLabels[i];
            var labelShown = shown && _cityOpen[i];
            if (label.IsVisible != labelShown)
                label.IsVisible = labelShown;
            if (!shown)
                continue;
            Move(pin, at.Value.X - CityDot / 2, at.Value.Y - CityDot / 2);
            if (labelShown)
                Move(label, at.Value.X - (float)label.MeasuredSize.Units.Width / 2, at.Value.Y - CityDot / 2 - CityLabelHeight - 6);
        }
    }
}
