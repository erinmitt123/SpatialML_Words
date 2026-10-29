
void mx_texturecubegradient_vector4(samplerCube tex_sampler, int u_wrap, int v_wrap, int border_color, int magFilter, int minFilter, int mipFilter, int anisotropy, float maxLodClamp, float minLodClamp, vec3 texCoord, float dynamicMinLoadClamp, vec3 dPdx, vec3 dPdy, out vec4 result)
{
    result = textureGrad(tex_sampler, texCoord, dPdx, dPdy);
}
