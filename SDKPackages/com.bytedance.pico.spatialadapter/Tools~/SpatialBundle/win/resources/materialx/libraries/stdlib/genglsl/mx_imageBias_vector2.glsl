#include "lib/$fileTransformUv"

void mx_imageBias_vector2(sampler2D tex_sampler, vec2 texcoord, int uaddressmode, int vaddressmode, int filtertype, int anisolevel, float mipmapBias, out vec2 result)
{
#ifdef FLIP_UV_ATTRIBUTE
        vec2 uvFlip = vec2(texcoord.x, 1 - texcoord.y);
#else
        vec2 uvFlip = texcoord;
#endif
        result = texture(tex_sampler, uvFlip, mipmapBias).rg;
}
