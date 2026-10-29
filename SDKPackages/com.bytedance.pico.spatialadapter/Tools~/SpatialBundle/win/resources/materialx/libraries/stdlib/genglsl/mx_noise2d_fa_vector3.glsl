#include "lib/mx_noise.glsl"

void mx_noise2d_fa_vector3(float amplitude, float pivot, vec2 texcoord, out vec3 result)
{
    float value = mx_perlin_noise_float(texcoord);
    value = value * amplitude + pivot;
    result = vec3(value, value, value);
}
