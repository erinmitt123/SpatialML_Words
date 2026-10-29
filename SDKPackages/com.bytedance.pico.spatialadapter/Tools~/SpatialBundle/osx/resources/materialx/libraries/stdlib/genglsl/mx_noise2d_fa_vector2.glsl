#include "lib/mx_noise.glsl"

void mx_noise2d_fa_vector2(float amplitude, float pivot, vec2 texcoord, out vec2 result)
{
    float value = mx_perlin_noise_float(texcoord);
    value = value * amplitude + pivot;
    result = vec2(value, value);
}
