
void mx_texturecube_vector4(samplerCube tex_sampler, int u_wrap, int v_wrap, int border_color, int magFilter, int minFilter, int mipFilter, int anisotropy, float maxLodClamp, float minLodClamp, vec3 texCoord, float mipmapBias, float dynamicMinLoadClamp, out vec4 result)
{
    result = texture(tex_sampler, texCoord, mipmapBias);
}
