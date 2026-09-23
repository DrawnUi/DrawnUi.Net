using DrawnUi.Controls;
using DrawnUi.Draw;
using DrawnUi.Views;

namespace HelloMaui.Pages;

/// <summary>
/// SkiaShaderEffect (SkSL on a control's output, generative shaders, touch ripples) and
/// SkiaShaderCarousel transitions. Ported from the React demo's ShadersPage.tsx.
/// </summary>
public class ShadersPage : SkiaLayer
{
    // every standard uniform must be declared: the engine writes iResolution, iImageResolution, iTime,
    // iOffset and iMouse to every shader and SkiaSharp throws for a name the shader does not declare
    private const string Plasma = """
uniform float2 iResolution;
uniform float2 iImageResolution;
uniform float2 iOffset;
uniform float  iTime;
uniform float4 iMouse;

half4 main(float2 fragCoord) {
    float2 uv = (fragCoord - iOffset) / iResolution.xy;
    float t = iTime * 0.6;
    float v = sin(uv.x * 6.0 + t) + sin((uv.y * 6.0 + t) * 0.8) + sin((uv.x + uv.y) * 4.0 - t) + sin(length(uv - 0.5) * 12.0 - t * 1.5);
    v *= 0.25;
    float3 col = 0.5 + 0.5 * cos(6.2831 * (v + float3(0.0, 0.33, 0.67)) + t);
    return half4(col, 1.0);
}
""";

    private const string Wave = """
uniform shader iImage1;
uniform float2 iResolution;
uniform float2 iImageResolution;
uniform float2 iOffset;
uniform float  iTime;
uniform float4 iMouse;
uniform float  strength;

half4 main(float2 fragCoord) {
    float2 uv = (fragCoord - iOffset) / iResolution.xy;
    float2 d = float2(sin(uv.y * 20.0 + iTime * 3.0), cos(uv.x * 20.0 + iTime * 2.0)) * strength;
    float2 p = (uv + d) * iImageResolution;
    return iImage1.eval(p);
}
""";

    private static readonly string[] Transitions = { "cube", "fade", "swirl", "doorway", "bounce", "waterdrop", "pixelize", "windowslice", "crosszoom", "pagecurl", "morph", "heart", "kaleidoscope", "wind" };
    private static readonly string[] Photos = { "images/hugrobot2.jpg", "images/8.jpg", "images/dungeon.jpg", "images/nebula.jpg" };
    private static readonly Color Muted = Color.Parse("#ADB5BD");

    private SkiaLabel _error;
    private SkiaLabel _carouselTitle;
    private SkiaLabel _waveTitle;
    private SkiaLabel _blitTitle;
    private SkiaShaderCarousel _carousel;
    private SkiaImage _waveHost;
    private SkiaImage _blitHost;
    private SkiaLayer _plasmaHost;
    private SkiaButton _runButton;
    private SkiaButton _blitButton;
    private readonly List<SkiaButton> _transitionButtons = new();
    private readonly List<SkiaButton> _strengthButtons = new();
    private readonly SkiaShaderEffect _wave;
    private readonly SkiaShaderEffect _plasma;
    private readonly SkiaShaderEffect _blit;
    private readonly List<SkiaValueAnimator> _animators = new();
    private string _transition = "cube";
    private string _fromTo = "";
    private double _strength = 0.01;
    private bool _running = true;
    private bool _blitOn;

    /// <summary>Builds the page.</summary>
    public ShadersPage()
    {
        HorizontalOptions = LayoutOptions.Fill;
        VerticalOptions = LayoutOptions.Fill;

        // a relative "package" path: the head's AppPackageServices serves it from beside the executable
        var ripple = new MultiRippleWithTouchEffect { SecondarySource = "images/nebula.jpg" };
        ripple.OnCompilationError += (_, e) => ShowError(e);
        _wave = new SkiaShaderEffect { ShaderCode = Wave };
        _wave.OnCompilationError += (_, e) => ShowError(e);
        _plasma = new SkiaShaderEffect { ShaderCode = Plasma, UseBackground = PostRendererEffectUseBackgroud.Never, AutoCreateInputTexture = false };
        _plasma.OnCompilationError += (_, e) => ShowError(e);
        _blit = new SkiaShaderEffect { ShaderSource = "shaders/blit.sksl" };
        _blit.OnCompilationError += (_, e) => ShowError(e);

        Children = new List<SkiaControl>
        {
            new SkiaScroll
            {
                Orientation = ScrollOrientation.Vertical,
                Content = new SkiaStack
                {
                    Spacing = 16,
                    Padding = new Thickness(16),
                    HorizontalOptions = LayoutOptions.Center,
                    MaximumWidthRequest = 720,
                    Children = new List<SkiaControl>
                    {
                        new SkiaLabel("Shaders") { FontSize = 24, TextColor = Colors.White, HorizontalOptions = LayoutOptions.Fill, HorizontalTextAlignment = DrawTextAlignment.Center },
                        new SkiaLabel("") { FontSize = 12, TextColor = Color.Parse("#FF6B6B"), HorizontalOptions = LayoutOptions.Fill, IsVisible = false }.Assign(out _error),

                        Card(Title(CarouselTitle()).Assign(out _carouselTitle),
                            new SkiaShaderCarousel
                            {
                                HeightRequest = 280,
                                HorizontalOptions = LayoutOptions.Fill,
                                IsLooped = true,
                                LinearSpeedMs = 750,
                                TransitionShader = "shaders/transitions/cube.sksl",
                                ItemsSource = Enumerable.Range(0, Photos.Length).ToList(),
                                ItemTemplate = new DataTemplate(() => new PhotoSlide(Photos)),
                            }
                            .Assign(out _carousel)
                            .Adapt(me => me.FromToChanged += (_, _) =>
                            {
                                _fromTo = $"· {me.TransitionFromIndex} to {me.TransitionToIndex}";
                                _carouselTitle.Text = CarouselTitle();
                            }),
                            new SkiaWrap
                            {
                                Spacing = 6,
                                Children = new List<SkiaControl>
                                {
                                    new SkiaButton("Prev") { BackgroundColor = Color.Parse("#0F3460"), FontSize = 13 }.OnTapped(me => _carousel.GoPrev()),
                                    new SkiaButton("Next") { BackgroundColor = Color.Parse("#0F3460"), FontSize = 13 }.OnTapped(me => _carousel.GoNext()),
                                }
                                .Concat(Transitions.Select(t => (SkiaControl)new SkiaButton(t) { FontSize = 12 }
                                    .Adapt(b => _transitionButtons.Add(b))
                                    .OnTapped(me => SetTransition(t))))
                                .ToList(),
                            },
                            new SkiaLabel("Slides never move: a ShaderTransitionEffect blends the Image caches of the outgoing and incoming slides (iImage1 / iImage2, progress, ratio) through a gl-transitions style transition(uv) wrapped by the adapter template. Swipe, or drag slowly to scrub the transition; a swipe during a transition wraps it up first (InterruptedTransitionMs).")
                            {
                                FontSize = 12, TextColor = Muted, HorizontalOptions = LayoutOptions.Fill,
                            }),

                        Card(Title("SkiaShaderEffect on a SkiaImage — ShaderSource=\"shaders/ripples.sksl\" (Sandbox MultiRippleWithTouchEffect) · tap to ripple"),
                            new SkiaImage { Source = "images/hugrobot2.jpg", Aspect = TransformAspect.AspectCover, HorizontalOptions = LayoutOptions.Fill, HeightRequest = 260, UseCache = SkiaCacheType.Image, VisualEffects = { ripple } },
                            new SkiaLabel("The effect is an ISkiaGestureProcessor: every Down starts a ripple at the touch point, animated 0 to 1 over 4.5 s through Parent.AnimateRangeAsync and passed as the origins[10] / progresses[10] array uniforms; iImage1 is the image's own cache, iImage2 (SecondarySource) the reflection texture.")
                            {
                                FontSize = 12, TextColor = Muted, HorizontalOptions = LayoutOptions.Fill,
                            }),

                        Card(Title(WaveTitle()).Assign(out _waveTitle),
                            new SkiaImage { Source = "images/8.jpg", Aspect = TransformAspect.AspectCover, HorizontalOptions = LayoutOptions.Fill, HeightRequest = 200, UseCache = SkiaCacheType.Image, VisualEffects = { _wave } }.Assign(out _waveHost),
                            new SkiaWrap
                            {
                                Spacing = 6,
                                Children = new[] { 0, 0.005, 0.01, 0.03 }.Select(v => (SkiaControl)new SkiaButton($"strength {v}") { FontSize = 12 }
                                    .Adapt(b => _strengthButtons.Add(b))
                                    .OnTapped(me => SetStrength(v)))
                                .Concat(new[]
                                {
                                    (SkiaControl)new SkiaButton("Pause") { BackgroundColor = Color.Parse("#0D6EFD"), FontSize = 12 }.Assign(out _runButton).OnTapped(me => ToggleRunning()),
                                })
                                .ToList(),
                            }),

                        Card(Title("Generative shader — UseBackground=Never on a SkiaLayer (no input texture)"),
                            new SkiaLayer { HorizontalOptions = LayoutOptions.Fill, HeightRequest = 140, VisualEffects = { _plasma } }.Assign(out _plasmaHost)),

                        Card(Title(BlitTitle()).Assign(out _blitTitle),
                            new SkiaImage { Source = "images/dungeon.jpg", Aspect = TransformAspect.AspectCover, HorizontalOptions = LayoutOptions.Fill, HeightRequest = 160, UseCache = SkiaCacheType.Image }.Assign(out _blitHost),
                            new SkiaButton("Add effect") { BackgroundColor = Color.Parse("#0D6EFD"), FontSize = 12 }.Assign(out _blitButton).OnTapped(me => ToggleBlit())),
                    },
                },
            }.Fill(),
        };

        SetTransition("cube");
        SetStrength(0.01);
        StartAnimators();
    }

    /// <inheritdoc/>
    public override void OnDisposing()
    {
        StopAnimators();
        base.OnDisposing();
    }

    private void ShowError(string error)
    {
        _error.Text = $"Shader error: {error}";
        _error.IsVisible = true;
    }

    private string CarouselTitle() => $"SkiaShaderCarousel — TransitionShader=\"shaders/transitions/{_transition}.sksl\" · IsLooped · LinearSpeedMs=750 {_fromTo}";

    private string WaveTitle() => $"Inline ShaderCode + SetUniform(\"strength\", {_strength}) + iTime · {(_running ? "animating" : "paused")}";

    private string BlitTitle() => $"ShaderSource=\"shaders/blit.sksl\" (pass-through) toggled through VisualEffects · {(_blitOn ? "on" : "off")}";

    private void SetTransition(string transition)
    {
        _transition = transition;
        _carousel.TransitionShader = $"shaders/transitions/{transition}.sksl";
        for (var i = 0; i < _transitionButtons.Count; i++)
            _transitionButtons[i].BackgroundColor = Color.Parse(Transitions[i] == transition ? "#533483" : "#495057");
        _carouselTitle.Text = CarouselTitle();
    }

    private void SetStrength(double strength)
    {
        _strength = strength;
        _wave.SetUniform("strength", (float)strength);
        _wave.Update();
        var values = new[] { 0, 0.005, 0.01, 0.03 };
        for (var i = 0; i < _strengthButtons.Count; i++)
            _strengthButtons[i].BackgroundColor = Color.Parse(values[i] == strength ? "#533483" : "#495057");
        _waveTitle.Text = WaveTitle();
    }

    // iTime shaders repaint only when something asks for frames: a looping animator on each host ticks them.
    private void StartAnimators()
    {
        StopAnimators();
        foreach (var host in new SkiaControl[] { _waveHost, _plasmaHost })
        {
            var animator = new SkiaValueAnimator(host) { mMinValue = 0, mMaxValue = 1, Speed = 1000, Repeat = -1, OnUpdated = _ => { _wave.Update(); _plasma.Update(); } };
            animator.Start();
            _animators.Add(animator);
        }
    }

    private void StopAnimators()
    {
        foreach (var animator in _animators)
            animator.Stop();
        _animators.Clear();
    }

    private void ToggleRunning()
    {
        _running = !_running;
        if (_running)
            StartAnimators();
        else
            StopAnimators();
        _runButton.Text = _running ? "Pause" : "Run";
        _waveTitle.Text = WaveTitle();
    }

    private void ToggleBlit()
    {
        _blitOn = !_blitOn;
        if (_blitOn)
            _blitHost.VisualEffects.Add(_blit);
        else
            _blitHost.VisualEffects.Remove(_blit);
        _blitHost.Update();
        _blitButton.Text = _blitOn ? "Remove effect" : "Add effect";
        _blitTitle.Text = BlitTitle();
    }

    private static SkiaLabel Title(string text) => new(text)
    {
        FontSize = 12, TextColor = Color.Parse("#6EA8FE"), FontAttributes = FontAttributes.Bold, TextTransform = TextTransform.Uppercase, HorizontalOptions = LayoutOptions.Fill,
    };

    private static SkiaControl Card(SkiaLabel title, params SkiaControl[] content) => new SkiaShape
    {
        Type = ShapeType.Rectangle,
        CornerRadius = 8,
        BackgroundColor = Color.Parse("#2B3035"),
        HorizontalOptions = LayoutOptions.Fill,
        Children = new List<SkiaControl>
        {
            new SkiaStack
            {
                Spacing = 10,
                Padding = new Thickness(16, 12),
                HorizontalOptions = LayoutOptions.Fill,
                Children = new SkiaControl[] { title }.Concat(content).ToList(),
            },
        },
    };
}

/// <summary>A slide of the shader carousel: MUST be cached as Image, the transition effect samples the cache.</summary>
public class PhotoSlide : SkiaDynamicDrawnCell
{
    private readonly string[] _photos;
    private SkiaImage _image;
    private SkiaLabel _label;

    /// <summary>Builds the slide.</summary>
    public PhotoSlide(string[] photos)
    {
        _photos = photos;
        Type = LayoutType.Absolute;
        UseCache = SkiaCacheType.Image;
        HorizontalOptions = LayoutOptions.Fill;
        VerticalOptions = LayoutOptions.Fill;
        Children = new List<SkiaControl>
        {
            new SkiaImage { Aspect = TransformAspect.AspectCover, HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill }.Assign(out _image),
            new SkiaLabel { FontSize = 28, TextColor = Colors.White, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center, DropShadowColor = Colors.Black, DropShadowSize = 4 }.Assign(out _label),
        };
    }

    /// <inheritdoc/>
    protected override void SetContent(object ctx)
    {
        var i = ContextIndex < 0 ? 0 : ContextIndex;
        _image.Source = _photos[i % _photos.Length];
        _label.Text = $"Slide {i + 1}";
    }
}
