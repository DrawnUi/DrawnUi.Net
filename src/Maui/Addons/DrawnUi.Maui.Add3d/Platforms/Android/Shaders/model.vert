#version 450
// SharedAdd3d/Renderer3D.cs Vs for Vulkan: uniforms in blocks, depth mapped to 0..1.
layout(location=0) in vec3 a_pos;
layout(location=1) in vec3 a_nrm;
layout(location=2) in vec2 a_uv;
layout(location=3) in vec4 a_joints;
layout(location=4) in vec4 a_weights;
layout(location=5) in vec3 a_outline_nrm;
layout(set=0, binding=0) uniform Draw {
    mat4 u_model;
    mat4 u_view_proj;
    vec4 u_color;
    vec4 u_light;
    vec4 u_outline_color;
    vec4 u_params;  // x outline width (pixels), yz viewport
    ivec4 u_flags;  // x skinned, y textured, z look, w outline pass
};
layout(set=0, binding=1) uniform Joints { mat4 u_joints[64]; };
layout(location=0) out vec3 v_nrm;
layout(location=1) out vec2 v_uv;
void main() {
    mat4 skin = mat4(1.0);
    if (u_flags.x != 0) {
        skin = a_weights.x * u_joints[int(a_joints.x)] + a_weights.y * u_joints[int(a_joints.y)]
             + a_weights.z * u_joints[int(a_joints.z)] + a_weights.w * u_joints[int(a_joints.w)];
    }
    mat4 m = u_model * skin;
    v_nrm = mat3(m) * a_nrm;
    v_uv = a_uv;
    gl_Position = u_view_proj * m * vec4(a_pos, 1.0);
    if (u_params.x > 0.0) {
        vec4 n = u_view_proj * vec4(mat3(m) * a_outline_nrm, 0.0);
        vec2 dir = normalize(n.xy + vec2(1e-6));
        gl_Position.xy += dir * u_params.x * 2.0 / u_params.yz * gl_Position.w;
        gl_Position.z += 0.002 * gl_Position.w;
    }
    // GL clip depth -1..1 to Vulkan's 0..1.
    gl_Position.z = (gl_Position.z + gl_Position.w) * 0.5;
}
