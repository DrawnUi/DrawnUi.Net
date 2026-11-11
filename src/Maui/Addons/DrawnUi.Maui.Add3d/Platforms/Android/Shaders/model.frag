#version 450
// SharedAdd3d/Renderer3D.cs Fs for Vulkan.
layout(location=0) in vec3 v_nrm;
layout(location=1) in vec2 v_uv;
layout(location=2) in vec3 v_wpos;
layout(set=0, binding=0) uniform Draw {
    mat4 u_model;
    mat4 u_view_proj;
    vec4 u_color;
    vec4 u_light;
    vec4 u_outline_color;
    vec4 u_params;
    ivec4 u_flags;
    vec4 u_eye;
    vec4 u_material;
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
    else {
        // Lit (DrawnUI for Rust's render.rs): a key light, a soft fill from the other side, the sky, and a
        // highlight that only glossy materials get (roughness below 1).
        float fill = max(dot(n, normalize(vec3(-u_light.x, 0.3, -u_light.z))), 0.0);
        lit = c.rgb * (0.24 + 0.20 * sky + 0.68 * sun + 0.22 * fill);
        float gloss = 1.0 - clamp(u_material.x, 0.0, 1.0);
        if (gloss > 0.01 && sun > 0.0) {
            vec3 v = u_eye.w > 0.5 ? normalize(u_eye.xyz - v_wpos) : normalize(u_eye.xyz);
            float s = pow(max(dot(n, normalize(u_light.xyz + v)), 0.0), mix(6.0, 90.0, gloss * gloss));
            lit += 0.55 * gloss * gloss * s;
        }
    }
    o_color = vec4(pow(lit, vec3(1.0 / 2.2)) * c.a, c.a);
}
