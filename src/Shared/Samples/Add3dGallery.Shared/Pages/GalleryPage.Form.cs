namespace Add3dGallery;

/// <summary>
/// 3D next to normal UI: a plain sign-up form with the avatar beside it as one more control. It looks at the field
/// being typed in (and along the text as it grows), shuts its eyes behind its hands while the password field has the keyboard, waves
/// when the form is sent and shakes its head when something is missing.
/// </summary>
public partial class GalleryPage
{
    const float FormWidth = 440;

    // The avatar's bones and moves (glTF names of avatar.glb).
    const string HeadBone = "Head", ArmL = "UpperArm.L", ArmR = "UpperArm.R", ForearmL = "LowerArm.L", ForearmR = "LowerArm.R";

    SkiaModel3D _mascot;
    SkiaEditor _nameField, _emailField, _passwordField;
    SkiaLabel _formStatus;
    /// <summary>The field with the keyboard: 0 name, 1 email, 2 password, -1 none.</summary>
    int _formFocus = -1;
    /// <summary>The head's turn and tilt now and wanted, and how far the hands are up (0 down, 1 over the eyes).</summary>
    float _headYaw, _headPitch, _headYawTo, _headPitchTo, _hands, _handsTo;
    bool _headSet, _handsSet;

    SkiaControl FormCard() => WideCard(new SkiaRow
    {
        Spacing = 24,
        Children =
        {
            new SkiaStack
            {
                Spacing = 8,
                // The form beside a mascot that moves every frame: blitted, drawn again when a field changes.
                UseCache = SkiaCacheType.Image,
                WidthRequest = FormWidth,
                VerticalOptions = LayoutOptions.Center,
                Children =
                {
                    new SkiaLabel("Create your account") { FontSize = 22, FontFamily = Theme.Title, TextColor = Theme.Ink, AccessibilityRole = Aria.RoleHeading },
                    new SkiaLabel("It takes a minute. Our mascot will not peek at your password.") { FontSize = 13, TextColor = Theme.Muted, Margin = new Thickness(0, 0, 0, 8) },
                    FieldLabel("Name"),
                    Field("Your name", false, 0).Assign(out _nameField),
                    FieldLabel("Email"),
                    Field("you@example.com", false, 1).Assign(out _emailField),
                    FieldLabel("Password"),
                    Field("At least 6 characters", true, 2).Assign(out _passwordField),
                    new UiButton("Sign up", Theme.Accent, FormWidth) { Margin = new Thickness(0, 12, 0, 0), HeightRequest = 44 }
                        .OnTapped(_ => SignUp()),
                    new SkiaLabel { FontSize = 13, TextColor = Theme.Muted, HeightRequest = 20, AccessibilityRole = Aria.RoleText }
                        .WithAccessibilityLive()
                        .Assign(out _formStatus),
                }
            },
            Stage(new SkiaModel3D(Theme.Avatar)
                    {
                        Animation = "CharacterArmature|Idle",
                        Yaw = -28,
                        Pitch = 6,
                        Orbit = false,
                        HorizontalOptions = LayoutOptions.Fill,
                        VerticalOptions = LayoutOptions.Fill,
                    }
                    .Assign(out _mascot)
                    .OnTapped3D((me, _) => me.PlayOnce("CharacterArmature|Wave")))
                .WithWidth(WideWidth - FormWidth - 24)
                .WithHeight(400),
        }
    });

    static SkiaLabel FieldLabel(string text) => new(text) { FontSize = 13, FontFamily = Theme.Title, TextColor = Theme.Muted, Margin = new Thickness(0, 4, 0, 0) };

    /// <summary>A one-line text field in the app's style; the mascot follows its focus and its text.</summary>
    SkiaEditor Field(string placeholder, bool password, int index)
    {
        var hovered = false;
        var editor = new SkiaEditor
        {
            MaxLines = 1,
            IsPassword = password,
            PlaceholderText = placeholder,
            PlaceholderColor = Theme.Muted,
            TextColor = Theme.Ink,
            CursorColor = Theme.Accent,
            FontSize = 15,
            FontFamily = "FontText",
            HorizontalOptions = LayoutOptions.Fill,
            Padding = new Thickness(12, 11),
            CornerRadius = 10,
            BackgroundColor = Theme.Field,
            StrokeColor = Theme.Line,
            StrokeWidth = 1.55,
            AccessibilityLabel = index switch { 0 => "Name", 1 => "Email", _ => "Password" },
        };
        void Frame(SkiaEditor me) =>
            me.StrokeColor = me.IsFocused ? Theme.Accent : hovered ? Theme.Lighter(Theme.Line, 0.25f) : Theme.Line;
        return editor
            .OnHovered((me, on) =>
            {
                hovered = on;
                Frame(me);
            })
            .OnFocusChanged((me, focused) =>
            {
                Frame(me);
                if (focused)
                    _formFocus = index;
                else if (_formFocus == index)
                    _formFocus = -1;
                Watch();
            })
            .OnTextChanged(_ => Watch());
    }

    /// <summary>Where the mascot looks: at the field with the keyboard, along its text; hands over its eyes for the password.</summary>
    void Watch()
    {
        _handsTo = _formFocus == 2 ? 1 : 0;
        if (_formFocus == 2)
        {
            // Hiding: head down into its hands.
            _headYawTo = 0;
            _headPitchTo = 18;
            return;
        }
        if (_formFocus is 0 or 1)
        {
            var field = _formFocus == 0 ? _nameField : _emailField;
            // Along the text: from the field's start to its end as the text grows.
            var along = Math.Clamp((field.Text?.Length ?? 0) / 30f, 0, 1);
            // The form is on the mascot's right (screen left): from the field's start toward its end, down a little
            // more for the lower field.
            _headYawTo = -50 + 25 * along;
            _headPitchTo = _formFocus == 0 ? 2 : 10;
        }
        else
        {
            _headYawTo = 0;
            _headPitchTo = 0;
        }
    }

    void SignUp()
    {
        var name = _nameField.Text?.Trim() ?? "";
        var email = _emailField.Text?.Trim() ?? "";
        var password = _passwordField.Text ?? "";
        string problem = null;
        if (name.Length == 0)
            problem = "Please type your name.";
        else if (!email.Contains('@') || !email.Contains('.'))
            problem = "That email does not look right.";
        else if (password.Length < 6)
            problem = "The password needs at least 6 characters.";
        if (problem != null)
        {
            _formStatus.Text = problem;
            _formStatus.TextColor = Theme.Bad;
            _mascot.PlayOnce("CharacterArmature|No");
            return;
        }
        _formStatus.Text = $"Welcome aboard, {name}!";
        _formStatus.TextColor = Theme.Good;
        _formFocus = -1;
        Watch();
        _mascot.PlayOnce("CharacterArmature|Wave");
    }

    /// <summary>Each frame: the head and the hands ease toward where the form wants them.</summary>
    void FormFrame(float delta)
    {
        if (!_mascot.WasInLastFrame())
            return;
        var k = MathF.Min(1, delta * 8);
        _headYaw += (_headYawTo - _headYaw) * k;
        _headPitch += (_headPitchTo - _headPitch) * k;
        _hands += (_handsTo - _hands) * k;
        // The head bone rests tilted 10 degrees down (its turn in the idle animation); yaw is its own Y.
        if (MathF.Abs(_headYaw) > 0.2f || MathF.Abs(_headPitch) > 0.2f)
        {
            _mascot.SetNodeRotation(HeadBone, -10 + _headPitch, _headYaw, 0);
            _headSet = true;
        }
        else if (_headSet)
        {
            _mascot.ResetNode(HeadBone);
            _headSet = false;
        }
        if (_hands > 0.01f)
        {
            // From the arms' rest turns (the idle pose) to the fists up before the face, as high as its short arms
            // reach (found by a search over the bones); the right arm mirrors the left.
            if (!_handsSet)
                CloseEyes(true);
            _handsSet = true;
            float h = _hands;
            _mascot.SetNodeRotation(ArmL, Lerp(-94, -120, h), 0, Lerp(-88, -230, h));
            _mascot.SetNodeRotation(ArmR, Lerp(-94, -120, h), 0, Lerp(88, 230, h));
            _mascot.SetNodeRotation(ForearmL, Lerp(6, 0, h), Lerp(30, 0, h), Lerp(-30, 15, h));
            _mascot.SetNodeRotation(ForearmR, Lerp(6, 0, h), Lerp(-30, 0, h), Lerp(30, -15, h));
        }
        else if (_handsSet)
        {
            _handsSet = false;
            CloseEyes(false);
            _mascot.ResetNode(ArmL);
            _mascot.ResetNode(ArmR);
            _mascot.ResetNode(ForearmL);
            _mascot.ResetNode(ForearmR);
        }
    }

    static float Lerp(float a, float b, float t) => a + (b - a) * t;

    /// <summary>The face's color, for eyes shut: avatar.glb's Main_Light.</summary>
    static readonly Color Skin = Color.FromRgb(209, 166, 103);

    /// <summary>Shut eyes: the eyes' materials take the face's color; open: the file's own.</summary>
    void CloseEyes(bool shut)
    {
        foreach (var material in new[] { "EyeColor", "Black", "White" })
            _mascot.SetMaterialColor(material, shut ? Skin : null);
    }
}
