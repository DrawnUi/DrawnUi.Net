using GraphicsApi = global::Slint.GraphicsApi;

namespace DrawnUi.Slint;

/// <summary>The OpenGL functions that <see cref="SlintGpuRenderer"/> calls itself, from Slint's context.</summary>
internal sealed unsafe class SlintGl
{
    public const uint Texture2D = 0x0DE1, Rgba = 0x1908, Rgba8 = 0x8058, UnsignedByte = 0x1401;
    public const uint TextureMinFilter = 0x2801, TextureMagFilter = 0x2800, Linear = 0x2601;
    public const uint TextureWrapS = 0x2802, TextureWrapT = 0x2803, ClampToEdge = 0x812F;

    private const uint FramebufferBinding = 0x8CA6, Framebuffer = 0x8D40, ViewportParam = 0x0BA2;
    private const uint ScissorTest = 0x0C11, ScissorBox = 0x0C10, Blend = 0x0BE2, DepthTest = 0x0B71;
    private const uint StencilTest = 0x0B90, CullFace = 0x0B44;
    private const uint BlendSrcRgb = 0x80C9, BlendDstRgb = 0x80C8, BlendSrcAlpha = 0x80CB, BlendDstAlpha = 0x80CA;
    private const uint BlendEquationRgb = 0x8009, BlendEquationAlpha = 0x883D;
    private const uint CurrentProgram = 0x8B8D, VertexArrayBinding = 0x85B5, ArrayBuffer = 0x8892, ArrayBufferBinding = 0x8894;
    private const uint ActiveTextureParam = 0x84E0, Texture0 = 0x84C0, TextureBinding2D = 0x8069, UnpackAlignment = 0x0CF5;

    private readonly delegate* unmanaged<int, uint*, void> _genTextures;
    private readonly delegate* unmanaged<int, uint*, void> _deleteTextures;
    private readonly delegate* unmanaged<uint, uint, void> _bindTexture;
    private readonly delegate* unmanaged<uint, int, int, int, int, int, uint, uint, void*, void> _texImage2D;
    private readonly delegate* unmanaged<uint, uint, int, void> _texParameteri;
    private readonly delegate* unmanaged<uint, int*, void> _getIntegerv;
    private readonly delegate* unmanaged<uint, byte> _isEnabled;
    private readonly delegate* unmanaged<uint, void> _enable;
    private readonly delegate* unmanaged<uint, void> _disable;
    private readonly delegate* unmanaged<uint, uint, void> _bindFramebuffer;
    private readonly delegate* unmanaged<int, int, int, int, void> _viewport;
    private readonly delegate* unmanaged<int, int, int, int, void> _scissor;
    private readonly delegate* unmanaged<uint, uint, uint, uint, void> _blendFuncSeparate;
    private readonly delegate* unmanaged<uint, uint, void> _blendEquationSeparate;
    private readonly delegate* unmanaged<uint, void> _useProgram;
    private readonly delegate* unmanaged<uint, void> _bindVertexArray;
    private readonly delegate* unmanaged<uint, uint, void> _bindBuffer;
    private readonly delegate* unmanaged<uint, void> _activeTexture;
    private readonly delegate* unmanaged<uint, int, void> _pixelStorei;
    private readonly delegate* unmanaged<int, int, int, int, uint, uint, void*, void> _readPixels;

    public SlintGl(GraphicsApi api)
    {
        _genTextures = (delegate* unmanaged<int, uint*, void>)Load(api, "glGenTextures");
        _deleteTextures = (delegate* unmanaged<int, uint*, void>)Load(api, "glDeleteTextures");
        _bindTexture = (delegate* unmanaged<uint, uint, void>)Load(api, "glBindTexture");
        _texImage2D = (delegate* unmanaged<uint, int, int, int, int, int, uint, uint, void*, void>)Load(api, "glTexImage2D");
        _texParameteri = (delegate* unmanaged<uint, uint, int, void>)Load(api, "glTexParameteri");
        _getIntegerv = (delegate* unmanaged<uint, int*, void>)Load(api, "glGetIntegerv");
        _isEnabled = (delegate* unmanaged<uint, byte>)Load(api, "glIsEnabled");
        _enable = (delegate* unmanaged<uint, void>)Load(api, "glEnable");
        _disable = (delegate* unmanaged<uint, void>)Load(api, "glDisable");
        _bindFramebuffer = (delegate* unmanaged<uint, uint, void>)Load(api, "glBindFramebuffer");
        _viewport = (delegate* unmanaged<int, int, int, int, void>)Load(api, "glViewport");
        _scissor = (delegate* unmanaged<int, int, int, int, void>)Load(api, "glScissor");
        _blendFuncSeparate = (delegate* unmanaged<uint, uint, uint, uint, void>)Load(api, "glBlendFuncSeparate");
        _blendEquationSeparate = (delegate* unmanaged<uint, uint, void>)Load(api, "glBlendEquationSeparate");
        _useProgram = (delegate* unmanaged<uint, void>)Load(api, "glUseProgram");
        _bindVertexArray = (delegate* unmanaged<uint, void>)Load(api, "glBindVertexArray");
        _bindBuffer = (delegate* unmanaged<uint, uint, void>)Load(api, "glBindBuffer");
        _activeTexture = (delegate* unmanaged<uint, void>)Load(api, "glActiveTexture");
        _pixelStorei = (delegate* unmanaged<uint, int, void>)Load(api, "glPixelStorei");
        _readPixels = (delegate* unmanaged<int, int, int, int, uint, uint, void*, void>)Load(api, "glReadPixels");
    }

    private static nint Load(GraphicsApi api, string name)
    {
        var address = api.GetProcAddress(name);
        return address != 0 ? address : throw new NotSupportedException($"OpenGL has no {name}.");
    }

    /// <summary>A texture of RGBA pixels, for Skia to render into and Slint to show.</summary>
    public uint CreateTexture(int width, int height)
    {
        int oldTexture;
        _getIntegerv(TextureBinding2D, &oldTexture);
        uint texture;
        _genTextures(1, &texture);
        _bindTexture(Texture2D, texture);
        _texParameteri(Texture2D, TextureMinFilter, (int)Linear);
        _texParameteri(Texture2D, TextureMagFilter, (int)Linear);
        _texParameteri(Texture2D, TextureWrapS, (int)ClampToEdge);
        _texParameteri(Texture2D, TextureWrapT, (int)ClampToEdge);
        _texImage2D(Texture2D, 0, (int)Rgba8, width, height, 0, Rgba, UnsignedByte, null);
        _bindTexture(Texture2D, (uint)oldTexture);
        return texture;
    }

    public void DeleteTexture(uint texture) => _deleteTextures(1, &texture);

    /// <summary>The window's framebuffer as RGBA rows from the top, as Slint rendered it.</summary>
    public (int Width, int Height, byte[] Pixels) ReadWindow()
    {
        var viewport = stackalloc int[4];
        _getIntegerv(ViewportParam, viewport);
        int width = viewport[2], height = viewport[3];
        var pixels = new byte[width * height * 4];
        int oldAlignment;
        _getIntegerv(0x0D05, &oldAlignment); // GL_PACK_ALIGNMENT
        _pixelStorei(0x0D05, 1);
        fixed (byte* p = pixels)
            _readPixels(viewport[0], viewport[1], width, height, Rgba, UnsignedByte, p);
        _pixelStorei(0x0D05, oldAlignment);
        // OpenGL's rows start at the bottom.
        var flipped = new byte[pixels.Length];
        var row = width * 4;
        for (var y = 0; y < height; y++)
            Array.Copy(pixels, y * row, flipped, (height - 1 - y) * row, row);
        return (width, height, flipped);
    }

    /// <summary>The state that Skia changes and Slint's renderer relies on, since they share the context.</summary>
    public State Save()
    {
        var s = new State();
        int value;
        _getIntegerv(FramebufferBinding, &value); s.Framebuffer = (uint)value;
        fixed (int* viewport = s.Viewport)
            _getIntegerv(ViewportParam, viewport);
        fixed (int* scissorBox = s.ScissorBox)
            _getIntegerv(ScissorBox, scissorBox);
        s.Scissor = _isEnabled(ScissorTest) != 0;
        s.Blend = _isEnabled(Blend) != 0;
        s.Depth = _isEnabled(DepthTest) != 0;
        s.Stencil = _isEnabled(StencilTest) != 0;
        s.Cull = _isEnabled(CullFace) != 0;
        _getIntegerv(BlendSrcRgb, &value); s.BlendSrcRgb = (uint)value;
        _getIntegerv(BlendDstRgb, &value); s.BlendDstRgb = (uint)value;
        _getIntegerv(BlendSrcAlpha, &value); s.BlendSrcAlpha = (uint)value;
        _getIntegerv(BlendDstAlpha, &value); s.BlendDstAlpha = (uint)value;
        _getIntegerv(BlendEquationRgb, &value); s.BlendEquationRgb = (uint)value;
        _getIntegerv(BlendEquationAlpha, &value); s.BlendEquationAlpha = (uint)value;
        _getIntegerv(CurrentProgram, &value); s.Program = (uint)value;
        _getIntegerv(VertexArrayBinding, &value); s.VertexArray = (uint)value;
        _getIntegerv(ArrayBufferBinding, &value); s.ArrayBuffer = (uint)value;
        _getIntegerv(ActiveTextureParam, &value); s.ActiveTexture = (uint)value;
        _activeTexture(Texture0);
        _getIntegerv(TextureBinding2D, &value); s.Texture0 = (uint)value;
        _getIntegerv(UnpackAlignment, &value); s.UnpackAlignment = value;
        return s;
    }

    public void Restore(State s)
    {
        _bindFramebuffer(Framebuffer, s.Framebuffer);
        _viewport(s.Viewport[0], s.Viewport[1], s.Viewport[2], s.Viewport[3]);
        _scissor(s.ScissorBox[0], s.ScissorBox[1], s.ScissorBox[2], s.ScissorBox[3]);
        Set(ScissorTest, s.Scissor);
        Set(Blend, s.Blend);
        Set(DepthTest, s.Depth);
        Set(StencilTest, s.Stencil);
        Set(CullFace, s.Cull);
        _blendFuncSeparate(s.BlendSrcRgb, s.BlendDstRgb, s.BlendSrcAlpha, s.BlendDstAlpha);
        _blendEquationSeparate(s.BlendEquationRgb, s.BlendEquationAlpha);
        _useProgram(s.Program);
        _bindVertexArray(s.VertexArray);
        _bindBuffer(ArrayBuffer, s.ArrayBuffer);
        _activeTexture(Texture0);
        _bindTexture(Texture2D, s.Texture0);
        _activeTexture(s.ActiveTexture);
        _pixelStorei(UnpackAlignment, s.UnpackAlignment);
    }

    private void Set(uint capability, bool on)
    {
        if (on)
            _enable(capability);
        else
            _disable(capability);
    }

    public sealed class State
    {
        public uint Framebuffer, BlendSrcRgb, BlendDstRgb, BlendSrcAlpha, BlendDstAlpha, BlendEquationRgb, BlendEquationAlpha;
        public uint Program, VertexArray, ArrayBuffer, ActiveTexture, Texture0;
        public int UnpackAlignment;
        public bool Scissor, Blend, Depth, Stencil, Cull;
        public readonly int[] Viewport = new int[4], ScissorBox = new int[4];
    }
}
