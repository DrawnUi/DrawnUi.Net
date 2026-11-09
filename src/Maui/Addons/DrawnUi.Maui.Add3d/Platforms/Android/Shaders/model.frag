#version 450
// SharedAdd3d/Renderer3D.cs Fs for Vulkan.
layout(location=0) in vec3 v_nrm;
layout(location=1) in vec2 v_uv;
layout(set=0, binding=0) uniform Draw {
    mat4 u_model;
    mat4 u_view_proj;
    vec4 u_color;
    vec4 u_light;
    vec4 u_outline_color;
    vec4 u_params;
    ivec4 u_flags;
};
layout(set=1, binding=0) uniform sampler2D u_texture;
layout(location=0) out vec4 o_color;
void main() {
    if (u_flags.w != 0) { o_color = vec4(u_outline_color.rgb * u_outline_color.a, u_outline_color.a); return; }
    vec4 c = u_color;
    if (u_flags.y != 0) { vec4 t = texture(u_texture, v_uv); c *= vec4(pow(t.rgb, vec3(2.2)), t.a); }
    if (c.a < 0.02) discard;
    vec3 n = normalize(v_nrm);
    if (!gl_FrontFacing) n = -n;
    float sun = max(dot(n, u_light.xyz), 0.0);
    float sky = 0.5 + 0.5 * n.y;
    vec3 lit;
    if (u_flags.z == 2) lit = c.rgb;
    else if (u_flags.z == 1) lit = c.rgb * (sun > 0.35 ? 1.0 : 0.62);
    else lit = c.rgb * (0.30 + 0.25 * sky + 0.70 * sun);
    o_color = vec4(pow(lit, vec3(1.0 / 2.2)) * c.a, c.a);
}
