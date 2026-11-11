using System.Numerics;

namespace Add3dGallery;

/// <summary>
/// 3D next to normal UI: a plain sign-up form with the avatar beside it as one more control. It looks at the field
/// being typed in (and along the text as it grows), folds its ears over its eyes while the password field has the keyboard, waves
/// when the form is sent and shakes its head when something is missing.
/// </summary>
public partial class GalleryPage
{
    const float FormWidth = 420;

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

    SkiaControl FormCard() => WideCard("3D next to normal UI", "A plain sign-up form with a 3D mascot beside it: it watches what you type and looks away from your password.", new SkiaRow
        {
            Spacing = 24,
            Children =
            {
                new SkiaShape
                {
                    CornerRadius = 14,
                    BackgroundColor = Theme.Page,
                    Padding = new Thickness(24),
                    WidthRequest = FormWidth,
                    VerticalOptions = LayoutOptions.Start,
                    // The form beside a mascot that moves every frame: blitted, drawn again when a field changes.
                    UseCache = SkiaCacheType.Image,
                    Children =
                    {
                        new SkiaStack
                        {
                            Spacing = 12,
                            Children =
                            {
                                new SkiaLabel("Create your account") { FontSize = 22, FontFamily = Theme.Title, TextColor = Theme.Ink, AccessibilityRole = Aria.RoleHeading },
                                new SkiaLabel("It takes a minute. Our mascot will not peek at your password.") { FontSize = 13, TextColor = Theme.Muted },
                                Field("Name", false, 0).Assign(out _nameField),
                                Field("Email", false, 1).Assign(out _emailField),
                                Field("Password", true, 2).Assign(out _passwordField),
                                new UiButton("Sign up", Theme.Accent) { HorizontalOptions = LayoutOptions.Fill, WidthRequest = -1 }
                                    .OnTapped(_ => SignUp()),
                                new SkiaLabel { FontSize = 14, TextColor = Theme.Gold, HeightRequest = 20, AccessibilityRole = Aria.RoleText }
                                    .WithAccessibilityLive()
                                    .Assign(out _formStatus),
                            }
                        },
                    }
                },
                Stage(new SkiaModel3D(Theme.Avatar)
                        {
                            Animation = "CharacterArmature|Idle",
                            // The camera on the mascot's left: it faces the form at screen left (a view turn, no mirror).
                            Yaw = 32,
                            Pitch = 8,
                            Orbit = false,
                            HorizontalOptions = LayoutOptions.Fill,
                            VerticalOptions = LayoutOptions.Fill,
                        }
                        .Assign(out _mascot)
                        .OnTapped3D((me, _) => me.PlayOnce("CharacterArmature|Wave")))
                    .WithWidth(WideWidth - FormWidth - 24)
                    .WithHeight(ViewerHeight),
            }
        }
        // Side by side on a wide card; narrow, the form on top at full width and the mascot under it.
        .Adapt(row => _adapt.Add(width =>
        {
            var wide = TwoColumns(width);
            row.Type = wide ? LayoutType.Row : LayoutType.Column;
            row.HorizontalOptions = LayoutOptions.Fill;
            var form = row.Children[0];
            var stage = row.Children[1];
            form.WidthRequest = wide ? FormWidth : -1;
            form.HorizontalOptions = wide ? LayoutOptions.Start : LayoutOptions.Fill;
            stage.WidthRequest = wide ? CardInner(width) - FormWidth - 24 : -1;
            stage.HorizontalOptions = wide ? LayoutOptions.Start : LayoutOptions.Fill;
            stage.HeightRequest = wide ? ViewerHeight : StackedView;
        })));


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
            Padding = new Thickness(12, 10),
            HeightRequest = 44,
            CornerRadius = 10,
            BackgroundColor = Theme.Stage,
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
        if (_formFocus is 0 or 1)
        {
            var field = _formFocus == 0 ? _nameField : _emailField;
            // Along the text: from the field's start toward its end as the text grows (DrawnUI for Rust's numbers).
            _headYawTo = -30 + 20 * MathF.Min((field.Text?.Length ?? 0) / 30f, 1);
            _headPitchTo = _formFocus == 0 ? 10 : 22;
            _lookTo = 1;
        }
        else
            _lookTo = 0;
    }

    void SignUp()
    {
        var name = _nameField.Text?.Trim() ?? "";
        var email = _emailField.Text?.Trim() ?? "";
        var password = _passwordField.Text ?? "";
        string problem = null;
        if (name.Length == 0)
            problem = "Please tell us your name.";
        else if (!email.Contains('@') || !email.Contains('.'))
            problem = "That email does not look right.";
        else if (password.Length < 8)
            problem = "Use at least 8 characters for the password.";
        if (problem != null)
        {
            _formStatus.Text = problem;
            _formStatus.TextColor = Theme.Bad;
            _mascot.PlayOnce("CharacterArmature|No");
            return;
        }
        _formStatus.Text = $"Welcome aboard, {name}!";
        _formStatus.TextColor = Theme.Gold;
        _formFocus = -1;
        Watch();
        _mascot.PlayOnce("CharacterArmature|Wave");
    }

    // The bones' own rest turns in avatar.glb: the poses are made on top of them (rest * turn), as in DrawnUI for Rust.
    static readonly Quaternion HeadRest = new(-0.0561773f, -0.00027504f, 0.0000154335f, 0.998420775f);
    static readonly Quaternion Ear1LRest = new(0.0947101936f, -0.0489338711f, -0.193506643f, 0.97529f);
    static readonly Quaternion Ear1RRest = new(0.0948166773f, 0.0494704843f, 0.193454459f, 0.97526294f);
    static readonly Quaternion Ear2LRest = new(-0.237358987f, -0.0338113457f, -0.0107185887f, 0.970774233f);
    static readonly Quaternion Ear2RRest = new(-0.223375544f, 0.0275211222f, 0.0118607497f, 0.974271655f);
    static readonly Quaternion ArmLRest = new(0.526785612f, 0.503608823f, 0.47415754f, -0.494013935f);
    static readonly Quaternion ArmRRest = new(0.526785672f, -0.503608882f, -0.474157482f, -0.494013816f);
    static readonly Quaternion ForearmLRest = new(-0.02938167f, 0.261505067f, -0.280782908f, 0.922991216f);
    static readonly Quaternion ForearmRRest = new(-0.0293817f, -0.261505097f, 0.280782908f, 0.922991216f);

    /// <summary>How far the head is turned toward a field, 0 (its own animation) to 1.</summary>
    float _look, _lookTo;

    /// <summary>Each frame: the head and the hiding pose ease toward where the form wants them.</summary>
    void FormFrame(float delta)
    {
        if (!_mascot.WasInLastFrame())
            return;
        var k = MathF.Min(1, delta * 8);
        _headYaw += (_headYawTo - _headYaw) * k;
        _headPitch += (_headPitchTo - _headPitch) * k;
        _look += (_lookTo - _look) * k;
        _hands += (_handsTo - _hands) * k;
        if (_look > 0.01f)
        {
            Turn(HeadBone, HeadRest, _headPitch, _headYaw, 0, _look);
            _headSet = true;
        }
        else if (_headSet)
        {
            _mascot.ResetNode(HeadBone);
            _headSet = false;
        }
        if (_hands > 0.01f)
        {
            // Hiding from the password: the ears fold down over the eyes, the hands go to the cheeks.
            _handsSet = true;
            Turn("Ear1.L", Ear1LRest, 150, 0, 0, _hands);
            Turn("Ear1.R", Ear1RRest, 150, 0, 0, _hands);
            Turn("Ear2.L", Ear2LRest, -30, 0, 0, _hands);
            Turn("Ear2.R", Ear2RRest, -30, 0, 0, _hands);
            Turn(ArmL, ArmLRest, -150, -150, 60, _hands);
            Turn(ForearmL, ForearmLRest, 0, 0, 60, _hands);
            Turn(ArmR, ArmRRest, 30, 30, 120, _hands);
            Turn(ForearmR, ForearmRRest, 0, 0, -60, _hands);
        }
        else if (_handsSet)
        {
            _handsSet = false;
            foreach (var bone in new[] { "Ear1.L", "Ear1.R", "Ear2.L", "Ear2.R", ArmL, ArmR, ForearmL, ForearmR })
                _mascot.ResetNode(bone);
        }
    }

    /// <summary>A bone turned to rest * (x, y, z) degrees (XYZ), the way there by <paramref name="t"/> (0 = rest).</summary>
    void Turn(string bone, Quaternion rest, float x, float y, float z, float t)
    {
        var q = Quaternion.Slerp(rest, rest * Euler(x, y, z), t);
        var (ex, ey, ez) = ToEuler(q);
        _mascot.SetNodeRotation(bone, ex, ey, ez);
    }

    /// <summary>Rotations around x, then y, then z, in degrees (glTF / glam XYZ: x * y * z).</summary>
    static Quaternion Euler(float x, float y, float z)
    {
        const float r = MathF.PI / 180;
        return Quaternion.CreateFromAxisAngle(Vector3.UnitX, x * r)
               * Quaternion.CreateFromAxisAngle(Vector3.UnitY, y * r)
               * Quaternion.CreateFromAxisAngle(Vector3.UnitZ, z * r);
    }

    /// <summary>The XYZ angles, in degrees, of a turn: the inverse of <see cref="Euler"/>.</summary>
    static (float X, float Y, float Z) ToEuler(Quaternion q)
    {
        float x = q.X, y = q.Y, z = q.Z, w = q.W;
        const float d = 180 / MathF.PI;
        return (MathF.Atan2(-2 * (y * z - w * x), 1 - 2 * (x * x + y * y)) * d,
            MathF.Asin(Math.Clamp(2 * (x * z + w * y), -1, 1)) * d,
            MathF.Atan2(-2 * (x * y - w * z), 1 - 2 * (y * y + z * z)) * d);
    }
}
