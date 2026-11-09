using System.Runtime.InteropServices;

namespace DrawnUi.Add3d;

/// <summary>
/// The Blazor WebAssembly head's GL binding: the WebGL2 entry points of the emscripten GL library libSkiaSharp is
/// linked with, imported under the module name the wasm pinvoke generator knows. The renderer (Renderer3D) calls only
/// these; another head brings its own Gl class with the same methods.
/// </summary>
internal static unsafe class Gl
{
    const string Lib = "libSkiaSharp";

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
}
