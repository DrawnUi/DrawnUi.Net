using System.Runtime.InteropServices;

#if !(IOS || MACCATALYST) // Apple heads draw with Metal only: no GL entry points, no OpenGLES framework linked

namespace DrawnUi.Add3d;

/// <summary>
/// The GL ES 3 / WebGL2 entry points the renderer (Renderer3D) calls. The web heads (Blazor and pure WebAssembly) import
/// them from the emscripten GL library libSkiaSharp is linked with, under the module name the wasm pinvoke generator
/// knows; the MAUI and WPF heads from the system GL ES library (Android) or ANGLE (Windows), both named libGLESv2; the
/// OpenTK head from the desktop OpenGL driver (3.3 core and up), through GLFW. Called only on a canvas drawing with GL,
/// with its context current.
/// </summary>
internal static unsafe class Gl
{
#if OPENTK
    /// <summary>Desktop OpenGL: the shaders are GLSL 330 core instead of GLSL ES 300.</summary>
    public static readonly bool Desktop = true;

    // Desktop GL exports only GL 1.1 from opengl32.dll on Windows: every entry point is looked up through GLFW, once,
    // the first time the renderer runs (on the canvas's GL thread, its context current).
    static delegate* unmanaged<uint, uint> _glCreateShader;
    static delegate* unmanaged<uint, int, byte**, int*, void> _glShaderSource;
    static delegate* unmanaged<uint, void> _glCompileShader;
    static delegate* unmanaged<uint, uint, int*, void> _glGetShaderiv;
    static delegate* unmanaged<uint, int, int*, byte*, void> _glGetShaderInfoLog;
    static delegate* unmanaged<uint> _glCreateProgram;
    static delegate* unmanaged<uint, uint, void> _glAttachShader;
    static delegate* unmanaged<uint, void> _glLinkProgram;
    static delegate* unmanaged<uint, uint, int*, void> _glGetProgramiv;
    static delegate* unmanaged<uint, byte*, int> _glGetUniformLocation;
    static delegate* unmanaged<uint, void> _glUseProgram;
    static delegate* unmanaged<int, uint*, void> _glGenBuffers;
    static delegate* unmanaged<uint, uint, void> _glBindBuffer;
    static delegate* unmanaged<uint, nint, void*, uint, void> _glBufferData;
    static delegate* unmanaged<int, uint*, void> _glGenVertexArrays;
    static delegate* unmanaged<uint, void> _glBindVertexArray;
    static delegate* unmanaged<uint, void> _glEnableVertexAttribArray;
    static delegate* unmanaged<uint, int, uint, byte, int, nint, void> _glVertexAttribPointer;
    static delegate* unmanaged<uint, float, float, float, float, void> _glVertexAttrib4f;
    static delegate* unmanaged<int, uint*, void> _glGenTextures;
    static delegate* unmanaged<int, uint*, void> _glDeleteTextures;
    static delegate* unmanaged<uint, uint, void> _glBindTexture;
    static delegate* unmanaged<uint, int, int, int, int, int, uint, uint, void*, void> _glTexImage2D;
    static delegate* unmanaged<uint, uint, int, void> _glTexParameteri;
    static delegate* unmanaged<uint, void> _glGenerateMipmap;
    static delegate* unmanaged<uint, void> _glActiveTexture;
    static delegate* unmanaged<uint, int, void> _glPixelStorei;
    static delegate* unmanaged<int, uint*, void> _glGenRenderbuffers;
    static delegate* unmanaged<int, uint*, void> _glDeleteRenderbuffers;
    static delegate* unmanaged<uint, uint, void> _glBindRenderbuffer;
    static delegate* unmanaged<uint, uint, int, int, void> _glRenderbufferStorage;
    static delegate* unmanaged<uint, int, uint, int, int, void> _glRenderbufferStorageMultisample;
    static delegate* unmanaged<int, int, int, int, int, int, int, int, uint, uint, void> _glBlitFramebuffer;
    static delegate* unmanaged<uint, int*, void> _glGetIntegerv;
    static delegate* unmanaged<int, uint*, void> _glGenFramebuffers;
    static delegate* unmanaged<int, uint*, void> _glDeleteFramebuffers;
    static delegate* unmanaged<uint, uint, void> _glBindFramebuffer;
    static delegate* unmanaged<uint, uint, uint, uint, int, void> _glFramebufferTexture2D;
    static delegate* unmanaged<uint, uint, uint, uint, void> _glFramebufferRenderbuffer;
    static delegate* unmanaged<int, int, int, int, void> _glViewport;
    static delegate* unmanaged<float, float, float, float, void> _glClearColor;
    static delegate* unmanaged<double, void> _glClearDepth;
    static delegate* unmanaged<uint, void> _glClear;
    static delegate* unmanaged<uint, void> _glEnable;
    static delegate* unmanaged<uint, void> _glDisable;
    static delegate* unmanaged<byte, void> _glDepthMask;
    static delegate* unmanaged<byte, byte, byte, byte, void> _glColorMask;
    static delegate* unmanaged<uint, void> _glDepthFunc;
    static delegate* unmanaged<uint, void> _glCullFace;
    static delegate* unmanaged<uint, void> _glFrontFace;
    static delegate* unmanaged<uint, uint, void> _glBlendFunc;
    static delegate* unmanaged<int, int, byte, float*, void> _glUniformMatrix4fv;
    static delegate* unmanaged<int, int, float*, void> _glUniform4fv;
    static delegate* unmanaged<int, int, float*, void> _glUniform3fv;
    static delegate* unmanaged<int, int, void> _glUniform1i;
    static delegate* unmanaged<int, float, void> _glUniform1f;
    static delegate* unmanaged<int, float, float, void> _glUniform2f;
    static delegate* unmanaged<uint, int, uint, nint, void> _glDrawElements;

    static Gl()
    {
        _glCreateShader = (delegate* unmanaged<uint, uint>)Load("glCreateShader");
        _glShaderSource = (delegate* unmanaged<uint, int, byte**, int*, void>)Load("glShaderSource");
        _glCompileShader = (delegate* unmanaged<uint, void>)Load("glCompileShader");
        _glGetShaderiv = (delegate* unmanaged<uint, uint, int*, void>)Load("glGetShaderiv");
        _glGetShaderInfoLog = (delegate* unmanaged<uint, int, int*, byte*, void>)Load("glGetShaderInfoLog");
        _glCreateProgram = (delegate* unmanaged<uint>)Load("glCreateProgram");
        _glAttachShader = (delegate* unmanaged<uint, uint, void>)Load("glAttachShader");
        _glLinkProgram = (delegate* unmanaged<uint, void>)Load("glLinkProgram");
        _glGetProgramiv = (delegate* unmanaged<uint, uint, int*, void>)Load("glGetProgramiv");
        _glGetUniformLocation = (delegate* unmanaged<uint, byte*, int>)Load("glGetUniformLocation");
        _glUseProgram = (delegate* unmanaged<uint, void>)Load("glUseProgram");
        _glGenBuffers = (delegate* unmanaged<int, uint*, void>)Load("glGenBuffers");
        _glBindBuffer = (delegate* unmanaged<uint, uint, void>)Load("glBindBuffer");
        _glBufferData = (delegate* unmanaged<uint, nint, void*, uint, void>)Load("glBufferData");
        _glGenVertexArrays = (delegate* unmanaged<int, uint*, void>)Load("glGenVertexArrays");
        _glBindVertexArray = (delegate* unmanaged<uint, void>)Load("glBindVertexArray");
        _glEnableVertexAttribArray = (delegate* unmanaged<uint, void>)Load("glEnableVertexAttribArray");
        _glVertexAttribPointer = (delegate* unmanaged<uint, int, uint, byte, int, nint, void>)Load("glVertexAttribPointer");
        _glVertexAttrib4f = (delegate* unmanaged<uint, float, float, float, float, void>)Load("glVertexAttrib4f");
        _glGenTextures = (delegate* unmanaged<int, uint*, void>)Load("glGenTextures");
        _glDeleteTextures = (delegate* unmanaged<int, uint*, void>)Load("glDeleteTextures");
        _glBindTexture = (delegate* unmanaged<uint, uint, void>)Load("glBindTexture");
        _glTexImage2D = (delegate* unmanaged<uint, int, int, int, int, int, uint, uint, void*, void>)Load("glTexImage2D");
        _glTexParameteri = (delegate* unmanaged<uint, uint, int, void>)Load("glTexParameteri");
        _glGenerateMipmap = (delegate* unmanaged<uint, void>)Load("glGenerateMipmap");
        _glActiveTexture = (delegate* unmanaged<uint, void>)Load("glActiveTexture");
        _glPixelStorei = (delegate* unmanaged<uint, int, void>)Load("glPixelStorei");
        _glGenRenderbuffers = (delegate* unmanaged<int, uint*, void>)Load("glGenRenderbuffers");
        _glDeleteRenderbuffers = (delegate* unmanaged<int, uint*, void>)Load("glDeleteRenderbuffers");
        _glBindRenderbuffer = (delegate* unmanaged<uint, uint, void>)Load("glBindRenderbuffer");
        _glRenderbufferStorage = (delegate* unmanaged<uint, uint, int, int, void>)Load("glRenderbufferStorage");
        _glRenderbufferStorageMultisample = (delegate* unmanaged<uint, int, uint, int, int, void>)Load("glRenderbufferStorageMultisample");
        _glBlitFramebuffer = (delegate* unmanaged<int, int, int, int, int, int, int, int, uint, uint, void>)Load("glBlitFramebuffer");
        _glGetIntegerv = (delegate* unmanaged<uint, int*, void>)Load("glGetIntegerv");
        _glGenFramebuffers = (delegate* unmanaged<int, uint*, void>)Load("glGenFramebuffers");
        _glDeleteFramebuffers = (delegate* unmanaged<int, uint*, void>)Load("glDeleteFramebuffers");
        _glBindFramebuffer = (delegate* unmanaged<uint, uint, void>)Load("glBindFramebuffer");
        _glFramebufferTexture2D = (delegate* unmanaged<uint, uint, uint, uint, int, void>)Load("glFramebufferTexture2D");
        _glFramebufferRenderbuffer = (delegate* unmanaged<uint, uint, uint, uint, void>)Load("glFramebufferRenderbuffer");
        _glViewport = (delegate* unmanaged<int, int, int, int, void>)Load("glViewport");
        _glClearColor = (delegate* unmanaged<float, float, float, float, void>)Load("glClearColor");
        _glClearDepth = (delegate* unmanaged<double, void>)Load("glClearDepth");
        _glClear = (delegate* unmanaged<uint, void>)Load("glClear");
        _glEnable = (delegate* unmanaged<uint, void>)Load("glEnable");
        _glDisable = (delegate* unmanaged<uint, void>)Load("glDisable");
        _glDepthMask = (delegate* unmanaged<byte, void>)Load("glDepthMask");
        _glColorMask = (delegate* unmanaged<byte, byte, byte, byte, void>)Load("glColorMask");
        _glDepthFunc = (delegate* unmanaged<uint, void>)Load("glDepthFunc");
        _glCullFace = (delegate* unmanaged<uint, void>)Load("glCullFace");
        _glFrontFace = (delegate* unmanaged<uint, void>)Load("glFrontFace");
        _glBlendFunc = (delegate* unmanaged<uint, uint, void>)Load("glBlendFunc");
        _glUniformMatrix4fv = (delegate* unmanaged<int, int, byte, float*, void>)Load("glUniformMatrix4fv");
        _glUniform4fv = (delegate* unmanaged<int, int, float*, void>)Load("glUniform4fv");
        _glUniform3fv = (delegate* unmanaged<int, int, float*, void>)Load("glUniform3fv");
        _glUniform1i = (delegate* unmanaged<int, int, void>)Load("glUniform1i");
        _glUniform1f = (delegate* unmanaged<int, float, void>)Load("glUniform1f");
        _glUniform2f = (delegate* unmanaged<int, float, float, void>)Load("glUniform2f");
        _glDrawElements = (delegate* unmanaged<uint, int, uint, nint, void>)Load("glDrawElements");
    }

    static nint Load(string name)
    {
        var proc = OpenTK.Windowing.GraphicsLibraryFramework.GLFW.GetProcAddress(name);
        if (proc == 0)
            throw new EntryPointNotFoundException($"OpenGL entry point {name} not found (OpenGL 3.3 core or newer is needed).");
        return proc;
    }

    public static uint glCreateShader(uint type) => _glCreateShader(type);
    public static void glShaderSource(uint shader, int count, byte** strings, int* lengths) => _glShaderSource(shader, count, strings, lengths);
    public static void glCompileShader(uint shader) => _glCompileShader(shader);
    public static void glGetShaderiv(uint shader, uint pname, int* value) => _glGetShaderiv(shader, pname, value);
    public static void glGetShaderInfoLog(uint shader, int size, int* length, byte* log) => _glGetShaderInfoLog(shader, size, length, log);
    public static uint glCreateProgram() => _glCreateProgram();
    public static void glAttachShader(uint program, uint shader) => _glAttachShader(program, shader);
    public static void glLinkProgram(uint program) => _glLinkProgram(program);
    public static void glGetProgramiv(uint program, uint pname, int* value) => _glGetProgramiv(program, pname, value);
    public static int glGetUniformLocation(uint program, byte* name) => _glGetUniformLocation(program, name);
    public static void glUseProgram(uint program) => _glUseProgram(program);
    public static void glGenBuffers(int n, uint* buffers) => _glGenBuffers(n, buffers);
    public static void glBindBuffer(uint target, uint buffer) => _glBindBuffer(target, buffer);
    public static void glBufferData(uint target, nint size, void* data, uint usage) => _glBufferData(target, size, data, usage);
    public static void glGenVertexArrays(int n, uint* arrays) => _glGenVertexArrays(n, arrays);
    public static void glBindVertexArray(uint array) => _glBindVertexArray(array);
    public static void glEnableVertexAttribArray(uint index) => _glEnableVertexAttribArray(index);
    public static void glVertexAttribPointer(uint index, int size, uint type, byte normalized, int stride, nint offset) => _glVertexAttribPointer(index, size, type, normalized, stride, offset);
    public static void glVertexAttrib4f(uint index, float x, float y, float z, float w) => _glVertexAttrib4f(index, x, y, z, w);
    public static void glGenTextures(int n, uint* textures) => _glGenTextures(n, textures);
    public static void glDeleteTextures(int n, uint* textures) => _glDeleteTextures(n, textures);
    public static void glBindTexture(uint target, uint texture) => _glBindTexture(target, texture);
    public static void glTexImage2D(uint target, int level, int internalformat, int width, int height, int border, uint format, uint type, void* pixels) => _glTexImage2D(target, level, internalformat, width, height, border, format, type, pixels);
    public static void glTexParameteri(uint target, uint pname, int param) => _glTexParameteri(target, pname, param);
    public static void glGenerateMipmap(uint target) => _glGenerateMipmap(target);
    public static void glActiveTexture(uint texture) => _glActiveTexture(texture);
    public static void glPixelStorei(uint pname, int param) => _glPixelStorei(pname, param);
    public static void glGenRenderbuffers(int n, uint* renderbuffers) => _glGenRenderbuffers(n, renderbuffers);
    public static void glDeleteRenderbuffers(int n, uint* renderbuffers) => _glDeleteRenderbuffers(n, renderbuffers);
    public static void glBindRenderbuffer(uint target, uint renderbuffer) => _glBindRenderbuffer(target, renderbuffer);
    public static void glRenderbufferStorage(uint target, uint internalformat, int width, int height) => _glRenderbufferStorage(target, internalformat, width, height);
    public static void glRenderbufferStorageMultisample(uint target, int samples, uint internalformat, int width, int height) => _glRenderbufferStorageMultisample(target, samples, internalformat, width, height);
    public static void glBlitFramebuffer(int srcX0, int srcY0, int srcX1, int srcY1, int dstX0, int dstY0, int dstX1, int dstY1, uint mask, uint filter) => _glBlitFramebuffer(srcX0, srcY0, srcX1, srcY1, dstX0, dstY0, dstX1, dstY1, mask, filter);
    public static void glGetIntegerv(uint pname, int* data) => _glGetIntegerv(pname, data);
    public static void glGenFramebuffers(int n, uint* framebuffers) => _glGenFramebuffers(n, framebuffers);
    public static void glDeleteFramebuffers(int n, uint* framebuffers) => _glDeleteFramebuffers(n, framebuffers);
    public static void glBindFramebuffer(uint target, uint framebuffer) => _glBindFramebuffer(target, framebuffer);
    public static void glFramebufferTexture2D(uint target, uint attachment, uint textarget, uint texture, int level) => _glFramebufferTexture2D(target, attachment, textarget, texture, level);
    public static void glFramebufferRenderbuffer(uint target, uint attachment, uint renderbuffertarget, uint renderbuffer) => _glFramebufferRenderbuffer(target, attachment, renderbuffertarget, renderbuffer);
    public static void glViewport(int x, int y, int width, int height) => _glViewport(x, y, width, height);
    public static void glClearColor(float r, float g, float b, float a) => _glClearColor(r, g, b, a);
    public static void glClearDepthf(float d) => _glClearDepth(d);
    public static void glClear(uint mask) => _glClear(mask);
    public static void glEnable(uint cap) => _glEnable(cap);
    public static void glDisable(uint cap) => _glDisable(cap);
    public static void glDepthMask(byte flag) => _glDepthMask(flag);
    public static void glColorMask(byte r, byte g, byte b, byte a) => _glColorMask(r, g, b, a);
    public static void glDepthFunc(uint func) => _glDepthFunc(func);
    public static void glCullFace(uint mode) => _glCullFace(mode);
    public static void glFrontFace(uint mode) => _glFrontFace(mode);
    public static void glBlendFunc(uint sfactor, uint dfactor) => _glBlendFunc(sfactor, dfactor);
    public static void glUniformMatrix4fv(int location, int count, byte transpose, float* value) => _glUniformMatrix4fv(location, count, transpose, value);
    public static void glUniform4fv(int location, int count, float* value) => _glUniform4fv(location, count, value);
    public static void glUniform3fv(int location, int count, float* value) => _glUniform3fv(location, count, value);
    public static void glUniform1i(int location, int value) => _glUniform1i(location, value);
    public static void glUniform1f(int location, float value) => _glUniform1f(location, value);
    public static void glUniform2f(int location, float x, float y) => _glUniform2f(location, x, y);
    public static void glDrawElements(uint mode, int count, uint type, nint offset) => _glDrawElements(mode, count, type, offset);
#else
    /// <summary>GL ES 3 / WebGL2: the shaders are GLSL ES 300 as written.</summary>
    public static readonly bool Desktop = false;

#if BROWSER
    const string Lib = "libSkiaSharp";
#else
    const string Lib = "libGLESv2";
#endif

    [DllImport(Lib)] public static extern uint glCreateShader(uint type);
    [DllImport(Lib)] public static extern void glShaderSource(uint shader, int count, byte** strings, int* lengths);
    [DllImport(Lib)] public static extern void glCompileShader(uint shader);
    [DllImport(Lib)] public static extern void glGetShaderiv(uint shader, uint pname, int* value);
    [DllImport(Lib)] public static extern void glGetShaderInfoLog(uint shader, int size, int* length, byte* log);
    [DllImport(Lib)] public static extern uint glCreateProgram();
    [DllImport(Lib)] public static extern void glAttachShader(uint program, uint shader);
    [DllImport(Lib)] public static extern void glLinkProgram(uint program);
    [DllImport(Lib)] public static extern void glGetProgramiv(uint program, uint pname, int* value);
    [DllImport(Lib)] public static extern int glGetUniformLocation(uint program, byte* name);
    [DllImport(Lib)] public static extern void glUseProgram(uint program);
    [DllImport(Lib)] public static extern void glGenBuffers(int n, uint* buffers);
    [DllImport(Lib)] public static extern void glBindBuffer(uint target, uint buffer);
    [DllImport(Lib)] public static extern void glBufferData(uint target, nint size, void* data, uint usage);
    [DllImport(Lib)] public static extern void glGenVertexArrays(int n, uint* arrays);
    [DllImport(Lib)] public static extern void glBindVertexArray(uint array);
    [DllImport(Lib)] public static extern void glEnableVertexAttribArray(uint index);
    [DllImport(Lib)] public static extern void glVertexAttribPointer(uint index, int size, uint type, byte normalized, int stride, nint offset);
    [DllImport(Lib)] public static extern void glVertexAttrib4f(uint index, float x, float y, float z, float w);
    [DllImport(Lib)] public static extern void glGenTextures(int n, uint* textures);
    [DllImport(Lib)] public static extern void glDeleteTextures(int n, uint* textures);
    [DllImport(Lib)] public static extern void glBindTexture(uint target, uint texture);
    [DllImport(Lib)] public static extern void glTexImage2D(uint target, int level, int internalformat, int width, int height, int border, uint format, uint type, void* pixels);
    [DllImport(Lib)] public static extern void glTexParameteri(uint target, uint pname, int param);
    [DllImport(Lib)] public static extern void glGenerateMipmap(uint target);
    [DllImport(Lib)] public static extern void glActiveTexture(uint texture);
    [DllImport(Lib)] public static extern void glPixelStorei(uint pname, int param);
    [DllImport(Lib)] public static extern void glGenRenderbuffers(int n, uint* renderbuffers);
    [DllImport(Lib)] public static extern void glDeleteRenderbuffers(int n, uint* renderbuffers);
    [DllImport(Lib)] public static extern void glBindRenderbuffer(uint target, uint renderbuffer);
    [DllImport(Lib)] public static extern void glRenderbufferStorage(uint target, uint internalformat, int width, int height);
    [DllImport(Lib)] public static extern void glRenderbufferStorageMultisample(uint target, int samples, uint internalformat, int width, int height);
    [DllImport(Lib)] public static extern void glBlitFramebuffer(int srcX0, int srcY0, int srcX1, int srcY1, int dstX0, int dstY0, int dstX1, int dstY1, uint mask, uint filter);
    [DllImport(Lib)] public static extern void glGetIntegerv(uint pname, int* data);
    [DllImport(Lib)] public static extern void glGenFramebuffers(int n, uint* framebuffers);
    [DllImport(Lib)] public static extern void glDeleteFramebuffers(int n, uint* framebuffers);
    [DllImport(Lib)] public static extern void glBindFramebuffer(uint target, uint framebuffer);
    [DllImport(Lib)] public static extern void glFramebufferTexture2D(uint target, uint attachment, uint textarget, uint texture, int level);
    [DllImport(Lib)] public static extern void glFramebufferRenderbuffer(uint target, uint attachment, uint renderbuffertarget, uint renderbuffer);
    [DllImport(Lib)] public static extern void glViewport(int x, int y, int width, int height);
    [DllImport(Lib)] public static extern void glClearColor(float r, float g, float b, float a);
    [DllImport(Lib)] public static extern void glClearDepthf(float d);
    [DllImport(Lib)] public static extern void glClear(uint mask);
    [DllImport(Lib)] public static extern void glEnable(uint cap);
    [DllImport(Lib)] public static extern void glDisable(uint cap);
    [DllImport(Lib)] public static extern void glDepthMask(byte flag);
    [DllImport(Lib)] public static extern void glColorMask(byte r, byte g, byte b, byte a);
    [DllImport(Lib)] public static extern void glDepthFunc(uint func);
    [DllImport(Lib)] public static extern void glCullFace(uint mode);
    [DllImport(Lib)] public static extern void glFrontFace(uint mode);
    [DllImport(Lib)] public static extern void glBlendFunc(uint sfactor, uint dfactor);
    [DllImport(Lib)] public static extern void glUniformMatrix4fv(int location, int count, byte transpose, float* value);
    [DllImport(Lib)] public static extern void glUniform4fv(int location, int count, float* value);
    [DllImport(Lib)] public static extern void glUniform3fv(int location, int count, float* value);
    [DllImport(Lib)] public static extern void glUniform1i(int location, int value);
    [DllImport(Lib)] public static extern void glUniform1f(int location, float value);
    [DllImport(Lib)] public static extern void glUniform2f(int location, float x, float y);
    [DllImport(Lib)] public static extern void glDrawElements(uint mode, int count, uint type, nint offset);
#endif
}
#endif
