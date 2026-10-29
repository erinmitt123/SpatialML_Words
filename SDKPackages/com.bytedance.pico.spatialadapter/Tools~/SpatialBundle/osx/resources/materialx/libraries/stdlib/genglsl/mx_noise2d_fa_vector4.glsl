#include "lib/mx_noise.glsl"

void mx_noise2d_fa_vector4(float amplitude, float pivot, vec2 texcoord, out vec4 result)
{
    float value = mx_perlin_noise_float(texcoord);
    vec3 xyz = vec3(value, value, value);
    float w = mx_perlin_noise_float(texcoord + vec2(19, 73));
    result = vec4(xyz, w) * amplitude + pivot;
}
