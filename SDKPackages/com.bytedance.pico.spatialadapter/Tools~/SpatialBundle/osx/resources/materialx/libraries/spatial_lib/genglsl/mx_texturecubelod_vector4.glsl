
void mx_texturecubelod_vector4(samplerCube tex_sampler, int u_wrap, int v_wrap, int border_color, int magFilter, int minFilter, int mipFilter, int anisotropy, float maxLodClamp, float minLodClamp, vec3 texCoord, float lod, out vec4 result)
{
    result = textureLod(tex_sampler, texCoord, lod);
}
