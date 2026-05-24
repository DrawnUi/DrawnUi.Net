using DrawnUi;
using DrawnUi.Draw;
using DrawnUi.Infrastructure.Enums;
using DrawnUi.OpenTk;
using DrawnUi.Views;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;

/// <summary>
/// GameWindow that renders a rotating colored cube with raw OpenGL,
/// then overlays a DrawnUI canvas on top via CanvasHost.
/// The 3D scene renders first (GL.Clear → DrawCube → GL.Finish),
/// then DrawnUI composites the transparent overlay on the existing framebuffer.
/// </summary>
internal sealed class CubeWindow : GameWindow
{
    private CanvasHost? _host;
    private OverlayPanel? _overlayPanel;
    private float _angle;
    private bool _firstFrameDone;

    // GL resources
    private int _vao, _vbo, _program;
    private int _wallVao, _wallVbo;
    private int _locModel, _locView, _locProj, _locLightPos, _locViewPos;

    private static readonly float[] CubeVertices = BuildCube();
    private static readonly float[] WallVertices  = BuildWall();

    public CubeWindow(GameWindowSettings gs, NativeWindowSettings ns)
        : base(gs, ns) { }

    protected override void OnLoad()
    {
        base.OnLoad();

        Super.Init();

        VSync = VSyncMode.On;
        GL.Enable(EnableCap.DepthTest);
        InitGl();

        _overlayPanel = new OverlayPanel(OnResetAngle);

        _host = new CanvasHost(new Canvas
        {
            // AcceleratedRetained: Skia does not clear before rendering.
            // GL.Clear runs each frame before DrawCube, so the framebuffer is
            // fresh every frame — no alpha accumulation.
            BackgroundColor = Colors.Transparent,
            RenderingMode = RenderingModeType.AcceleratedRetained,
            UpdateMode = UpdateModeType.Constant,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Content = new List<SkiaControl>
            {
                new SkiaLayer
                {
                    HorizontalOptions = LayoutOptions.Fill,
                    VerticalOptions = LayoutOptions.Fill,
                    Children = { _overlayPanel }
                }
            }
        });

        _host.Initialize();
        _host.Resize(ClientSize.X, ClientSize.Y);

        CenterOnScreen();
    }

    private void OnResetAngle() => _angle = 0f;

    protected override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);
        GL.Viewport(0, 0, e.Width, e.Height);
        _host?.Resize(e.Width, e.Height);
    }

    protected override void OnRenderFrame(FrameEventArgs e)
    {
        base.OnRenderFrame(e);

        _angle = (_angle + (float)(40.0 * e.Time)) % 360f;
        _overlayPanel?.UpdateAngle(_angle);

        // ── 1. Render the 3D scene with raw OpenGL ──────────────────────────
        // Restore GL state Skia left dirty: viewport (cube maps off-screen), stencil, depth mask.
        GL.Viewport(0, 0, ClientSize.X, ClientSize.Y);
        GL.Disable(EnableCap.StencilTest);
        GL.DepthMask(true);
        GL.ColorMask(true, true, true, true);

        GL.ClearColor(0.04f, 0.06f, 0.12f, 1f);
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);
        GL.Enable(EnableCap.DepthTest);
        DrawCube();
        GL.Finish();

        // ── 2. Composite DrawnUI overlay on top ─────────────────────────────
        _host!.ResetGrContext();   // re-sync Skia after direct GL calls
        _host!.Render();

        SwapBuffers();

        if (!_firstFrameDone)
        {
            _firstFrameDone = true;
            IsVisible = true;
        }
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        _host?.Gestures.OnMouseDown(e, MousePosition, ClientSize);
    }

    protected override void OnMouseMove(MouseMoveEventArgs e)
    {
        base.OnMouseMove(e);
        _host?.Gestures.OnMouseMove(e, MousePosition, MouseState.IsButtonDown(MouseButton.Left), ClientSize);
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);
        _host?.Gestures.OnMouseUp(e, MousePosition, ClientSize);
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        _host?.Input.OnTextInput(e);
    }

    protected override void OnKeyDown(KeyboardKeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Keys.Escape) Close();
        _host?.Input.OnKeyDown(e, KeyboardState);
    }

    protected override void OnUnload()
    {
        GL.DeleteVertexArray(_vao);
        GL.DeleteBuffer(_vbo);
        GL.DeleteVertexArray(_wallVao);
        GL.DeleteBuffer(_wallVbo);
        GL.DeleteProgram(_program);
        _host?.Dispose();
        base.OnUnload();
    }

    // ── OpenGL setup ──────────────────────────────────────────────────────────

    private void InitGl()
    {
        const string vert = """
            #version 330 core
            layout(location = 0) in vec3 aPos;
            layout(location = 1) in vec3 aColor;
            layout(location = 2) in vec3 aNormal;
            out vec3 vColor;
            out vec3 vNormal;
            out vec3 vFragPos;
            uniform mat4 uModel;
            uniform mat4 uView;
            uniform mat4 uProjection;
            void main()
            {
                vec4 worldPos = uModel * vec4(aPos, 1.0);
                vFragPos  = worldPos.xyz;
                vNormal   = mat3(transpose(inverse(uModel))) * aNormal;
                gl_Position = uProjection * uView * worldPos;
                vColor = aColor;
            }
            """;

        const string frag = """
            #version 330 core
            in vec3 vColor;
            in vec3 vNormal;
            in vec3 vFragPos;
            out vec4 FragColor;
            uniform vec3 uLightPos;
            uniform vec3 uViewPos;
            void main()
            {
                vec3 norm     = normalize(vNormal);
                vec3 lightDir = normalize(uLightPos - vFragPos);
                vec3 viewDir  = normalize(uViewPos  - vFragPos);
                vec3 halfDir  = normalize(lightDir + viewDir);

                vec3 ambient  = 0.22 * vColor;
                vec3 diffuse  = max(dot(norm, lightDir), 0.0) * vColor;
                float spec    = pow(max(dot(norm, halfDir), 0.0), 64.0);
                vec3 specular = 0.5 * spec * vec3(1.0);

                FragColor = vec4(ambient + diffuse + specular, 1.0);
            }
            """;

        int vs = CompileShader(ShaderType.VertexShader, vert);
        int fs = CompileShader(ShaderType.FragmentShader, frag);

        _program = GL.CreateProgram();
        GL.AttachShader(_program, vs);
        GL.AttachShader(_program, fs);
        GL.LinkProgram(_program);
        GL.DeleteShader(vs);
        GL.DeleteShader(fs);

        _locModel    = GL.GetUniformLocation(_program, "uModel");
        _locView     = GL.GetUniformLocation(_program, "uView");
        _locProj     = GL.GetUniformLocation(_program, "uProjection");
        _locLightPos = GL.GetUniformLocation(_program, "uLightPos");
        _locViewPos  = GL.GetUniformLocation(_program, "uViewPos");

        _vao = GL.GenVertexArray();
        _vbo = GL.GenBuffer();
        GL.BindVertexArray(_vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
        GL.BufferData(BufferTarget.ArrayBuffer,
            CubeVertices.Length * sizeof(float), CubeVertices, BufferUsageHint.StaticDraw);

        const int stride = 9 * sizeof(float);
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, 0);
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride, 3 * sizeof(float));
        GL.EnableVertexAttribArray(1);
        GL.VertexAttribPointer(2, 3, VertexAttribPointerType.Float, false, stride, 6 * sizeof(float));
        GL.EnableVertexAttribArray(2);
        GL.BindVertexArray(0);

        _wallVao = GL.GenVertexArray();
        _wallVbo = GL.GenBuffer();
        GL.BindVertexArray(_wallVao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _wallVbo);
        GL.BufferData(BufferTarget.ArrayBuffer,
            WallVertices.Length * sizeof(float), WallVertices, BufferUsageHint.StaticDraw);
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, 0);
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride, 3 * sizeof(float));
        GL.EnableVertexAttribArray(1);
        GL.VertexAttribPointer(2, 3, VertexAttribPointerType.Float, false, stride, 6 * sizeof(float));
        GL.EnableVertexAttribArray(2);
        GL.BindVertexArray(0);
    }

    private static int CompileShader(ShaderType type, string src)
    {
        int shader = GL.CreateShader(type);
        GL.ShaderSource(shader, src);
        GL.CompileShader(shader);
        GL.GetShader(shader, ShaderParameter.CompileStatus, out int ok);
        if (ok == 0)
            throw new Exception($"Shader compile error ({type}): {GL.GetShaderInfoLog(shader)}");
        return shader;
    }

    private void DrawCube()
    {
        GL.UseProgram(_program);

        var model = Matrix4.CreateScale(1.2f)
                  * Matrix4.CreateRotationX(MathHelper.DegreesToRadians(22f))
                  * Matrix4.CreateRotationY(MathHelper.DegreesToRadians(_angle));
        var view  = Matrix4.LookAt(new Vector3(0f, 0.4f, 2.8f), Vector3.Zero, Vector3.UnitY);
        var proj  = Matrix4.CreatePerspectiveFieldOfView(
                        MathHelper.DegreesToRadians(45f),
                        (float)ClientSize.X / ClientSize.Y,
                        0.1f, 100f);

        var lightPos = new Vector3(2.5f, 3.0f, 3.0f);
        var viewPos  = new Vector3(0f, 0.4f, 2.8f);

        GL.UniformMatrix4(_locView,  false, ref view);
        GL.UniformMatrix4(_locProj,  false, ref proj);
        GL.Uniform3(_locLightPos, ref lightPos);
        GL.Uniform3(_locViewPos,  ref viewPos);

        // Draw wall + floor first (identity model — they don't rotate)
        var wallModel = Matrix4.Identity;
        GL.UniformMatrix4(_locModel, false, ref wallModel);
        GL.BindVertexArray(_wallVao);
        GL.DrawArrays(PrimitiveType.Triangles, 0, WallVertices.Length / 9);
        GL.BindVertexArray(0);

        // Draw cube
        GL.UniformMatrix4(_locModel, false, ref model);
        GL.BindVertexArray(_vao);
        GL.DrawArrays(PrimitiveType.Triangles, 0, 36);
        GL.BindVertexArray(0);
    }

    private unsafe void CenterOnScreen()
    {
        try
        {
            var monitor = GLFW.GetPrimaryMonitor();
            if (monitor == null) return;
            var mode = GLFW.GetVideoMode(monitor);
            if (mode == null) return;
            Location = new Vector2i(
                (mode->Width  - ClientSize.X) / 2,
                (mode->Height - ClientSize.Y) / 2);
        }
        catch { }
    }

    // Six faces × 2 triangles × 3 verts = 36 verts; each: pos.xyz + color.rgb + normal.xyz
    private static float[] BuildCube() =>
    [
        // Front – cyan   normal ( 0, 0,+1)
        -0.5f, -0.5f,  0.5f,   0.0f, 0.85f, 0.9f,    0f,  0f,  1f,
         0.5f, -0.5f,  0.5f,   0.0f, 0.85f, 0.9f,    0f,  0f,  1f,
         0.5f,  0.5f,  0.5f,   0.0f, 0.85f, 0.9f,    0f,  0f,  1f,
        -0.5f, -0.5f,  0.5f,   0.0f, 0.85f, 0.9f,    0f,  0f,  1f,
         0.5f,  0.5f,  0.5f,   0.0f, 0.85f, 0.9f,    0f,  0f,  1f,
        -0.5f,  0.5f,  0.5f,   0.0f, 0.85f, 0.9f,    0f,  0f,  1f,
        // Back – orange  normal ( 0, 0,-1)
         0.5f, -0.5f, -0.5f,   1.0f, 0.55f, 0.0f,    0f,  0f, -1f,
        -0.5f, -0.5f, -0.5f,   1.0f, 0.55f, 0.0f,    0f,  0f, -1f,
        -0.5f,  0.5f, -0.5f,   1.0f, 0.55f, 0.0f,    0f,  0f, -1f,
         0.5f, -0.5f, -0.5f,   1.0f, 0.55f, 0.0f,    0f,  0f, -1f,
        -0.5f,  0.5f, -0.5f,   1.0f, 0.55f, 0.0f,    0f,  0f, -1f,
         0.5f,  0.5f, -0.5f,   1.0f, 0.55f, 0.0f,    0f,  0f, -1f,
        // Left – red     normal (-1, 0, 0)
        -0.5f, -0.5f, -0.5f,   0.9f, 0.2f,  0.2f,   -1f,  0f,  0f,
        -0.5f, -0.5f,  0.5f,   0.9f, 0.2f,  0.2f,   -1f,  0f,  0f,
        -0.5f,  0.5f,  0.5f,   0.9f, 0.2f,  0.2f,   -1f,  0f,  0f,
        -0.5f, -0.5f, -0.5f,   0.9f, 0.2f,  0.2f,   -1f,  0f,  0f,
        -0.5f,  0.5f,  0.5f,   0.9f, 0.2f,  0.2f,   -1f,  0f,  0f,
        -0.5f,  0.5f, -0.5f,   0.9f, 0.2f,  0.2f,   -1f,  0f,  0f,
        // Right – green  normal (+1, 0, 0)
         0.5f, -0.5f,  0.5f,   0.2f, 0.85f, 0.3f,    1f,  0f,  0f,
         0.5f, -0.5f, -0.5f,   0.2f, 0.85f, 0.3f,    1f,  0f,  0f,
         0.5f,  0.5f, -0.5f,   0.2f, 0.85f, 0.3f,    1f,  0f,  0f,
         0.5f, -0.5f,  0.5f,   0.2f, 0.85f, 0.3f,    1f,  0f,  0f,
         0.5f,  0.5f, -0.5f,   0.2f, 0.85f, 0.3f,    1f,  0f,  0f,
         0.5f,  0.5f,  0.5f,   0.2f, 0.85f, 0.3f,    1f,  0f,  0f,
        // Top – yellow   normal ( 0,+1, 0)
        -0.5f,  0.5f,  0.5f,   0.98f, 0.9f, 0.2f,    0f,  1f,  0f,
         0.5f,  0.5f,  0.5f,   0.98f, 0.9f, 0.2f,    0f,  1f,  0f,
         0.5f,  0.5f, -0.5f,   0.98f, 0.9f, 0.2f,    0f,  1f,  0f,
        -0.5f,  0.5f,  0.5f,   0.98f, 0.9f, 0.2f,    0f,  1f,  0f,
         0.5f,  0.5f, -0.5f,   0.98f, 0.9f, 0.2f,    0f,  1f,  0f,
        -0.5f,  0.5f, -0.5f,   0.98f, 0.9f, 0.2f,    0f,  1f,  0f,
        // Bottom – purple normal ( 0,-1, 0)
        -0.5f, -0.5f, -0.5f,   0.7f, 0.3f,  0.9f,    0f, -1f,  0f,
         0.5f, -0.5f, -0.5f,   0.7f, 0.3f,  0.9f,    0f, -1f,  0f,
         0.5f, -0.5f,  0.5f,   0.7f, 0.3f,  0.9f,    0f, -1f,  0f,
        -0.5f, -0.5f, -0.5f,   0.7f, 0.3f,  0.9f,    0f, -1f,  0f,
         0.5f, -0.5f,  0.5f,   0.7f, 0.3f,  0.9f,    0f, -1f,  0f,
        -0.5f, -0.5f,  0.5f,   0.7f, 0.3f,  0.9f,    0f, -1f,  0f,
    ];

    // Back wall and floor; per-vertex colors create angular gradient (warm upper-right, cool lower-left)
    private static float[] BuildWall() =>
    [
        // Back wall  normal (0,0,+1)
        -3f, -0.95f, -0.85f,   0.04f, 0.18f, 0.08f,   0f, 0f, 1f,   // bottom-left  dark green
         3f, -0.95f, -0.85f,   0.06f, 0.12f, 0.18f,   0f, 0f, 1f,   // bottom-right mid blue
         3f,  3.0f,  -0.85f,   0.10f, 0.22f, 0.38f,   0f, 0f, 1f,   // top-right    light blue
        -3f, -0.95f, -0.85f,   0.04f, 0.18f, 0.08f,   0f, 0f, 1f,   // bottom-left  dark green
         3f,  3.0f,  -0.85f,   0.10f, 0.22f, 0.38f,   0f, 0f, 1f,   // top-right    light blue
        -3f,  3.0f,  -0.85f,   0.06f, 0.20f, 0.12f,   0f, 0f, 1f,   // top-left     green-teal
        // Floor  normal (0,+1,0)
        -3f, -0.80f, -0.85f,   0.04f, 0.16f, 0.07f,   0f, 1f, 0f,   // back-left    dark green
         3f, -0.80f, -0.85f,   0.06f, 0.10f, 0.16f,   0f, 1f, 0f,   // back-right   dark blue
         3f, -0.80f,  3.5f,    0.07f, 0.14f, 0.22f,   0f, 1f, 0f,   // front-right  blue
        -3f, -0.80f, -0.85f,   0.04f, 0.16f, 0.07f,   0f, 1f, 0f,   // back-left    dark green
         3f, -0.80f,  3.5f,    0.07f, 0.14f, 0.22f,   0f, 1f, 0f,   // front-right  blue
        -3f, -0.80f,  3.5f,    0.05f, 0.18f, 0.09f,   0f, 1f, 0f,   // front-left   green
    ];
}
