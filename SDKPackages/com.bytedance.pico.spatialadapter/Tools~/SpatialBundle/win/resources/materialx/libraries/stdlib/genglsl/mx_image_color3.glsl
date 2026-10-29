#include "lib/$fileTransformUv"

void mx_image_color3(sampler2D tex_sampler, int layer, vec3 defaultval, vec2 texcoord, int uaddressmode, int vaddressmode, int filtertype, int framerange, int frameoffset, int frameendaction, vec2 uv_scale, vec2 uv_offset, out vec3 result)
{
 #ifdef FLIP_UV_ATTRIBUTE
        vec2 uvFlip = vec2(texcoord.x, 1 - texcoord.y);
 #else
        vec2 uvFlip = texcoord;
 #endif
        vec2 uv = mx_transform_uv(uvFlip, uv_scale, uv_offset);
        result = texture(tex_sampler, uv).rgb;

}
