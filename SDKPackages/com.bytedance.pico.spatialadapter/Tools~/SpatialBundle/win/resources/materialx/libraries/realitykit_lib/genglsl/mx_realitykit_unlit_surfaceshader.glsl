
void mx_realitykit_unlit_surfaceshader(float3 baseColor, float opacity, float opacityThreshold, bool applyPostProcessToneMap, bool hasPremultipliedAlpha, inout MaterialInputs material)
{
    material.baseColor.rgb = baseColor;
    material.baseColor.a = opacity;
}
