#nullable disable
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using DrawnUi.Draw;
using SkiaSharp;

namespace DrawnUi.Add3d;

/// <summary>
/// The GPU side: one shader program and one copy of each model per GL context, shared by every control; each control
/// draws its objects in one pass into its own texture, which Skia then shows. GL calls go through the head's
/// <c>Gl</c> binding; the shaders are the ones of DrawnUI for Rust, so both engines draw the same.
/// </summary>
internal static unsafe class Renderer3D
{
    /// <summary>Joint matrices a skinned mesh may use (WebGL2 guarantees 256 vertex uniform vectors).</summary>
    const int MaxJoints = 64;

    const string Vs = """
#version 300 es
layout(location=0) in vec3 a_pos;
layout(location=1) in vec3 a_nrm;
layout(location=2) in vec2 a_uv;
layout(location=3) in vec4 a_joints;
layout(location=4) in vec4 a_weights;
layout(location=5) in vec3 a_outline_nrm;
uniform mat4 u_model;
uniform mat4 u_view_proj;
uniform mat4 u_joints[64];
uniform bool u_skinned;
uniform float u_outline;
uniform vec2 u_viewport;
out vec3 v_nrm;
out vec2 v_uv;
void main() {
    mat4 skin = mat4(1.0);
    if (u_skinned) {
        skin = a_weights.x * u_joints[int(a_joints.x)] + a_weights.y * u_joints[int(a_joints.y)]
             + a_weights.z * u_joints[int(a_joints.z)] + a_weights.w * u_joints[int(a_joints.w)];
    }
    mat4 m = u_model * skin;
    v_nrm = mat3(m) * a_nrm;
    v_uv = a_uv;
    gl_Position = u_view_proj * m * vec4(a_pos, 1.0);
    if (u_outline > 0.0) {
        // The outline pass: the back faces pushed out along their smoothed normals (one per
        // position, so hard edges do not split), a fixed number of pixels on screen.
        vec4 n = u_view_proj * vec4(mat3(m) * a_outline_nrm, 0.0);
        vec2 dir = normalize(n.xy + vec2(1e-6));
        gl_Position.xy += dir * u_outline * 2.0 / u_viewport * gl_Position.w;
        // A little behind: where it meets a surface at the same depth, the surface wins.
        gl_Position.z += 0.002 * gl_Position.w;
    }
}
""";

    const string Fs = """
#version 300 es
precision mediump float;
in vec3 v_nrm;
in vec2 v_uv;
uniform vec4 u_color;
uniform bool u_textured;
uniform sampler2D u_texture;
uniform vec3 u_light;
uniform int u_look;
uniform bool u_outline_pass;
uniform vec4 u_outline_color;
out vec4 o_color;
void main() {
    if (u_outline_pass) { o_color = vec4(u_outline_color.rgb * u_outline_color.a, u_outline_color.a); return; }
    vec4 c = u_color;
    // glTF colors are linear; textures are sRGB: light in linear, show in sRGB.
    if (u_textured) { vec4 t = texture(u_texture, v_uv); c *= vec4(pow(t.rgb, vec3(2.2)), t.a); }
    if (c.a < 0.02) discard;
    vec3 n = normalize(v_nrm);
    if (!gl_FrontFacing) n = -n;
    float sun = max(dot(n, u_light), 0.0);
    float sky = 0.5 + 0.5 * n.y;
    vec3 lit;
    if (u_look == 2) lit = c.rgb;                                  // flat: the color as it is
    else if (u_look == 1) lit = c.rgb * (sun > 0.35 ? 1.0 : 0.62);  // toon: lit and shade, two tones
    else lit = c.rgb * (0.30 + 0.25 * sky + 0.70 * sun);
    o_color = vec4(pow(lit, vec3(1.0 / 2.2)) * c.a, c.a);
}
""";

    const uint ARRAY_BUFFER = 0x8892, ELEMENT_ARRAY_BUFFER = 0x8893, STATIC_DRAW = 0x88E4;
    const uint FLOAT = 0x1406, UNSIGNED_BYTE = 0x1401, UNSIGNED_INT = 0x1405;
    const uint TEXTURE_2D = 0x0DE1, RGBA = 0x1908, RGBA8 = 0x8058;
    const uint FRAMEBUFFER = 0x8D40, RENDERBUFFER = 0x8D41, COLOR_ATTACHMENT0 = 0x8CE0, DEPTH_ATTACHMENT = 0x8D00, DEPTH_COMPONENT24 = 0x81A6;
    const uint DEPTH_TEST = 0x0B71, CULL_FACE = 0x0B44, BLEND = 0x0BE2, SCISSOR_TEST = 0x0C11, STENCIL_TEST = 0x0B90;
    const uint TRIANGLES = 0x0004, LESS = 0x0201, BACK = 0x0405, FRONT = 0x0404, CCW = 0x0901;
    const uint ONE = 1, ONE_MINUS_SRC_ALPHA = 0x0303, TEXTURE0 = 0x84C0, UNPACK_ALIGNMENT = 0x0CF5;
    const uint COLOR_BUFFER_BIT = 0x4000, DEPTH_BUFFER_BIT = 0x0100;
    const uint TEXTURE_MIN_FILTER = 0x2801, TEXTURE_MAG_FILTER = 0x2800, LINEAR = 0x2601, LINEAR_MIPMAP_LINEAR = 0x2703;
    const uint VERTEX_SHADER = 0x8B31, FRAGMENT_SHADER = 0x8B30, COMPILE_STATUS = 0x8B81, LINK_STATUS = 0x8B82;

    struct Uniforms
    {
        public int Model, ViewProj, Joints, Skinned, Color, Textured, Texture, Light, Look, Outline, Viewport, OutlinePass, OutlineColor;
    }

    /// <summary>One model on the GPU: per mesh, per primitive its vertex array and index count; its textures.</summary>
    sealed class GpuModel
    {
        public uint[][] Vaos;
        public int[][] Counts;
        public uint[] Textures;
    }

    /// <summary>The GL context the program and the models below belong to.</summary>
    static object _context;
    static uint _program;
    static Uniforms _u;

    // ponytail: a model whose scene is gone keeps its GL buffers until the context goes; delete them when apps load
    // and drop many different models.
    static ConditionalWeakTable<Scene3D, GpuModel> _models = new();

    /// <summary>Targets of disposed controls, deleted at the next draw (on the GL thread, context current).</summary>
    static readonly List<RenderTarget> _released = new();

    static readonly Matrix4x4[] _joints = new Matrix4x4[MaxJoints];

    /// <summary>GL state set during one draw, 0 = not set yet (Skia's state is unknown after it ran).</summary>
    struct DrawState
    {
        /// <summary>1 = cull on, 2 = off.</summary>
        public int Cull;
        /// <summary>The bound texture + 1.</summary>
        public uint Texture;
        /// <summary>1 = skinned, 2 = not.</summary>
        public int Skinned;
    }

    static DrawState _state;

    static readonly Vector3 Light = Vector3.Normalize(new Vector3(0.4f, 0.8f, 0.6f));

    /// <summary>Frees a control's texture and buffers at the next draw.</summary>
    public static void Release(RenderTarget target)
    {
        if (target == null)
            return;
        target.Image?.Dispose();
        target.Image = null;
        lock (_released)
            _released.Add(target);
    }

    static uint Shader(uint kind, string source)
    {
        var s = Gl.glCreateShader(kind);
        var bytes = Encoding.UTF8.GetBytes(source);
        fixed (byte* p = bytes)
        {
            var ptr = p;
            var len = bytes.Length;
            Gl.glShaderSource(s, 1, &ptr, &len);
        }
        Gl.glCompileShader(s);
        int ok;
        Gl.glGetShaderiv(s, COMPILE_STATUS, &ok);
        if (ok == 0)
        {
            var log = new byte[1024];
            int n;
            fixed (byte* p = log)
                Gl.glGetShaderInfoLog(s, log.Length, &n, p);
            Super.Log($"[SkiaModel3D] shader: {Encoding.UTF8.GetString(log, 0, Math.Max(0, n))}");
        }
        return s;
    }

    static int Location(string name)
    {
        var bytes = Encoding.UTF8.GetBytes(name + "\0");
        fixed (byte* p = bytes)
            return Gl.glGetUniformLocation(_program, p);
    }

    static void MakeProgram()
    {
        _program = Gl.glCreateProgram();
        Gl.glAttachShader(_program, Shader(VERTEX_SHADER, Vs));
        Gl.glAttachShader(_program, Shader(FRAGMENT_SHADER, Fs));
        Gl.glLinkProgram(_program);
        int ok;
        Gl.glGetProgramiv(_program, LINK_STATUS, &ok);
        if (ok == 0)
            Super.Log("[SkiaModel3D] the shader program did not link");
        _u = new Uniforms
        {
            Model = Location("u_model"),
            ViewProj = Location("u_view_proj"),
            Joints = Location("u_joints"),
            Skinned = Location("u_skinned"),
            Color = Location("u_color"),
            Textured = Location("u_textured"),
            Texture = Location("u_texture"),
            Light = Location("u_light"),
            Look = Location("u_look"),
            Outline = Location("u_outline"),
            Viewport = Location("u_viewport"),
            OutlinePass = Location("u_outline_pass"),
            OutlineColor = Location("u_outline_color"),
        };
    }

    static uint Buffer<T>(uint target, T[] data) where T : unmanaged
    {
        uint b;
        Gl.glGenBuffers(1, &b);
        Gl.glBindBuffer(target, b);
        fixed (T* p = data)
            Gl.glBufferData(target, data.Length * sizeof(T), p, STATIC_DRAW);
        return b;
    }

    static void Attribute<T>(uint index, T[] data, int size) where T : unmanaged
    {
        if (data == null || data.Length == 0)
            return;
        Buffer(ARRAY_BUFFER, data);
        Gl.glEnableVertexAttribArray(index);
        Gl.glVertexAttribPointer(index, size, FLOAT, 0, 0, 0);
    }

    /// <summary>
    /// One normal per position, the average of the normals there: an outline pushed along them stays closed at hard
    /// edges (a coin's rim, a box's corner).
    /// </summary>
    internal static Vector3[] OutlineNormals(Primitive3D p)
    {
        if (p.OutlineNormals != null)
            return p.OutlineNormals;
        static (int, int, int) Key(Vector3 v) => ((int)MathF.Round(v.X * 10_000), (int)MathF.Round(v.Y * 10_000), (int)MathF.Round(v.Z * 10_000));
        var sums = new Dictionary<(int, int, int), Vector3>();
        for (var i = 0; i < p.Positions.Length; i++)
        {
            var key = Key(p.Positions[i]);
            sums[key] = sums.GetValueOrDefault(key) + p.Normals[i];
        }
        var result = new Vector3[p.Positions.Length];
        for (var i = 0; i < result.Length; i++)
        {
            var s = sums[Key(p.Positions[i])];
            var l = s.Length();
            result[i] = l > 0 ? s / l : Vector3.Zero;
        }
        return result;
    }

    static GpuModel Upload(Scene3D scene)
    {
        Gl.glPixelStorei(UNPACK_ALIGNMENT, 4);
        var model = new GpuModel { Textures = new uint[scene.Images.Length], Vaos = new uint[scene.Meshes.Length][], Counts = new int[scene.Meshes.Length][] };
        for (var i = 0; i < scene.Images.Length; i++)
        {
            var image = scene.Images[i];
            uint t;
            Gl.glGenTextures(1, &t);
            Gl.glBindTexture(TEXTURE_2D, t);
            fixed (byte* p = image.Rgba)
                Gl.glTexImage2D(TEXTURE_2D, 0, (int)RGBA8, image.Width, image.Height, 0, RGBA, UNSIGNED_BYTE, p);
            Gl.glGenerateMipmap(TEXTURE_2D);
            Gl.glTexParameteri(TEXTURE_2D, TEXTURE_MIN_FILTER, (int)LINEAR_MIPMAP_LINEAR);
            Gl.glTexParameteri(TEXTURE_2D, TEXTURE_MAG_FILTER, (int)LINEAR);
            model.Textures[i] = t;
        }
        for (var m = 0; m < scene.Meshes.Length; m++)
        {
            var primitives = scene.Meshes[m];
            model.Vaos[m] = new uint[primitives.Length];
            model.Counts[m] = new int[primitives.Length];
            for (var k = 0; k < primitives.Length; k++)
            {
                var p = primitives[k];
                uint vao;
                Gl.glGenVertexArrays(1, &vao);
                Gl.glBindVertexArray(vao);
                Attribute(0, p.Positions, 3);
                Attribute(1, p.Normals, 3);
                Attribute(2, p.Uvs, 2);
                Attribute(3, p.Joints, 4);
                Attribute(4, p.Weights, 4);
                Attribute(5, OutlineNormals(p), 3);
                Buffer(ELEMENT_ARRAY_BUFFER, p.Indices);
                Gl.glBindVertexArray(0);
                model.Vaos[m][k] = vao;
                model.Counts[m][k] = p.Indices.Length;
            }
        }
        return model;
    }

    static GpuModel Model(Scene3D scene)
    {
        if (!_models.TryGetValue(scene, out var model))
        {
            model = Upload(scene);
            _models.AddOrUpdate(scene, model);
            // The upload bound its own textures.
            _state.Texture = 0;
        }
        return model;
    }

    static void EnsureTarget(GRContext gr, RenderTarget target, int w, int h)
    {
        if (target.Context != gr)
        {
            // A new context: the old objects died with the old one.
            target.Image?.Dispose();
            target.Image = null;
            target.Context = gr;
            uint id;
            Gl.glGenFramebuffers(1, &id);
            target.Frame = id;
            Gl.glGenTextures(1, &id);
            target.Color = id;
            Gl.glGenRenderbuffers(1, &id);
            target.Depth = id;
            target.Width = target.Height = 0;
        }
        if (target.Width == w && target.Height == h && target.Image != null)
            return;
        target.Image?.Dispose();
        Gl.glBindTexture(TEXTURE_2D, target.Color);
        Gl.glTexImage2D(TEXTURE_2D, 0, (int)RGBA8, w, h, 0, RGBA, UNSIGNED_BYTE, null);
        Gl.glTexParameteri(TEXTURE_2D, TEXTURE_MIN_FILTER, (int)LINEAR);
        Gl.glTexParameteri(TEXTURE_2D, TEXTURE_MAG_FILTER, (int)LINEAR);
        Gl.glBindRenderbuffer(RENDERBUFFER, target.Depth);
        Gl.glRenderbufferStorage(RENDERBUFFER, DEPTH_COMPONENT24, w, h);
        Gl.glBindFramebuffer(FRAMEBUFFER, target.Frame);
        Gl.glFramebufferTexture2D(FRAMEBUFFER, COLOR_ATTACHMENT0, TEXTURE_2D, target.Color, 0);
        Gl.glFramebufferRenderbuffer(FRAMEBUFFER, DEPTH_ATTACHMENT, RENDERBUFFER, target.Depth);
        target.Width = w;
        target.Height = h;
        // The texture keeps its identity: one image wraps it until the next resize. The frame is imported bottom-left.
        using var backend = new GRBackendTexture(w, h, false, new GRGlTextureInfo(TEXTURE_2D, target.Color, RGBA8));
        target.Image = SKImage.FromTexture(gr, backend, GRSurfaceOrigin.BottomLeft, SKColorType.Rgba8888, SKAlphaType.Premul);
    }

    static void DeleteReleased(GRContext gr)
    {
        lock (_released)
        {
            foreach (var t in _released)
            {
                if (t.Context != gr)
                    continue;
                uint id = t.Frame;
                Gl.glDeleteFramebuffers(1, &id);
                id = t.Color;
                Gl.glDeleteTextures(1, &id);
                id = t.Depth;
                Gl.glDeleteRenderbuffers(1, &id);
            }
            _released.Clear();
        }
    }

    /// <summary>
    /// Draws the objects into the control's texture in one pass and returns the image Skia shows of it (null when
    /// nothing could be drawn). Skia's GL state is reset after the GL calls.
    /// </summary>
    public static SKImage Draw(GRContext gr, RenderTarget target, RenderItem[] items, int count, in RenderStyle style, in Matrix4x4 viewProj, int w, int h)
    {
        if (gr == null || w <= 0 || h <= 0)
            return null;
        if (_context != gr)
        {
            _context = gr;
            _models = new ConditionalWeakTable<Scene3D, GpuModel>();
            MakeProgram();
        }
        if (_released.Count > 0)
            DeleteReleased(gr);
        EnsureTarget(gr, target, w, h);

        Gl.glBindFramebuffer(FRAMEBUFFER, target.Frame);
        Gl.glViewport(0, 0, w, h);
        Gl.glDisable(SCISSOR_TEST);
        Gl.glDisable(STENCIL_TEST);
        Gl.glDisable(BLEND);
        Gl.glColorMask(1, 1, 1, 1);
        Gl.glDepthMask(1);
        Gl.glClearColor(0, 0, 0, 0);
        Gl.glClearDepthf(1);
        Gl.glClear(COLOR_BUFFER_BIT | DEPTH_BUFFER_BIT);
        Gl.glEnable(DEPTH_TEST);
        Gl.glDepthFunc(LESS);
        Gl.glFrontFace(CCW);
        Gl.glCullFace(BACK);
        Gl.glUseProgram(_program);
        var vp = viewProj;
        Gl.glUniformMatrix4fv(_u.ViewProj, 1, 0, (float*)&vp);
        var light = Light;
        Gl.glUniform3fv(_u.Light, 1, (float*)&light);
        Gl.glUniform1i(_u.Texture, 0);
        Gl.glUniform1i(_u.Look, (int)style.Look);
        Gl.glUniform2f(_u.Viewport, w, h);
        var outlineColor = style.OutlineColor;
        Gl.glUniform4fv(_u.OutlineColor, 1, (float*)&outlineColor);
        Gl.glActiveTexture(TEXTURE0);
        // Joints and weights of a primitive without a skin: no joint moves it (generic values, used where its vertex
        // array has no such attribute).
        Gl.glVertexAttrib4f(3, 0, 0, 0, 0);
        Gl.glVertexAttrib4f(4, 1, 0, 0, 0);
        _state = default;

        // The outline first (back faces pushed out), then opaque, then blended, each in tree order.
        var firstPass = style.Outline > 0 ? 0 : 1;
        for (var pass = firstPass; pass < 3; pass++)
        {
            var outlinePass = pass == 0;
            var blendPass = pass == 2;
            Gl.glUniform1i(_u.OutlinePass, outlinePass ? 1 : 0);
            Gl.glUniform1f(_u.Outline, outlinePass ? style.Outline : 0);
            Gl.glCullFace(outlinePass ? FRONT : BACK);
            if (blendPass)
            {
                Gl.glEnable(BLEND);
                Gl.glBlendFunc(ONE, ONE_MINUS_SRC_ALPHA);
                Gl.glDepthMask(0);
            }
            for (var o = 0; o < count; o++)
                DrawItem(items[o], outlinePass, blendPass);
        }

        Gl.glBindVertexArray(0);
        Gl.glBindTexture(TEXTURE_2D, 0);
        Gl.glUseProgram(0);
        Gl.glDisable(DEPTH_TEST);
        Gl.glDisable(CULL_FACE);
        Gl.glDisable(BLEND);
        Gl.glDepthMask(1);
        Gl.glBindFramebuffer(FRAMEBUFFER, 0);
        Gl.glBindBuffer(ARRAY_BUFFER, 0);
        gr.ResetContext();
        return target.Image;
    }

    static void DrawItem(in RenderItem item, bool outlinePass, bool blendPass)
    {
        var scene = item.Scene;
        var gpu = Model(scene);
        var nodes = scene.Nodes;
        for (var i = 0; i < nodes.Length; i++)
        {
            var node = nodes[i];
            if (node.Mesh < 0 || node.Mesh >= gpu.Vaos.Length || item.Hidden[i])
                continue;
            var skinned = node.Skin >= 0 && node.Skin < scene.Skins.Length;
            var primitives = scene.Meshes[node.Mesh];
            var vaos = gpu.Vaos[node.Mesh];
            var counts = gpu.Counts[node.Mesh];
            var jointsSet = false;
            for (var k = 0; k < primitives.Length; k++)
            {
                var p = primitives[k];
                var materialIndex = p.Material;
                var material = materialIndex >= 0 && materialIndex < scene.Materials.Length ? scene.Materials[materialIndex] : null;
                var changed = materialIndex >= 0 && materialIndex < item.Materials.Length ? item.Materials[materialIndex] : default;
                if (changed.Hidden || (material?.Blend ?? false) != blendPass)
                    continue;
                if (!jointsSet)
                {
                    // A skinned mesh is placed by its joints only (glTF ignores its node's transform); the object's
                    // place is in the joints' world matrices.
                    jointsSet = true;
                    var model = skinned ? Matrix4x4.Identity : item.Pose[i];
                    if (skinned)
                    {
                        var n = scene.JointMatrices(node.Skin, item.Pose, _joints);
                        fixed (Matrix4x4* j = _joints)
                            Gl.glUniformMatrix4fv(_u.Joints, n, 0, (float*)j);
                    }
                    Gl.glUniformMatrix4fv(_u.Model, 1, 0, (float*)&model);
                }
                var color = changed.HasColor ? changed.Color : material?.Color ?? new Vector4(0.8f, 0.8f, 0.8f, 1);
                var texture = material != null && material.Texture >= 0 && material.Texture < gpu.Textures.Length ? gpu.Textures[material.Texture] : 0u;
                // GL state set only when it changes: a crowd of one model repeats the same state per object.
                var cull = !((material?.DoubleSided ?? false) && !outlinePass);
                if (_state.Cull != (cull ? 1 : 2))
                {
                    if (cull)
                        Gl.glEnable(CULL_FACE);
                    else
                        Gl.glDisable(CULL_FACE);
                    _state.Cull = cull ? 1 : 2;
                }
                Gl.glUniform4fv(_u.Color, 1, (float*)&color);
                if (_state.Texture != texture + 1)
                {
                    Gl.glUniform1i(_u.Textured, texture != 0 ? 1 : 0);
                    Gl.glBindTexture(TEXTURE_2D, texture);
                    _state.Texture = texture + 1;
                }
                var skin = skinned && p.Joints.Length > 0 ? 1 : 2;
                if (_state.Skinned != skin)
                {
                    Gl.glUniform1i(_u.Skinned, skin == 1 ? 1 : 0);
                    _state.Skinned = skin;
                }
                Gl.glBindVertexArray(vaos[k]);
                Gl.glDrawElements(TRIANGLES, counts[k], UNSIGNED_INT, 0);
            }
        }
    }
}

