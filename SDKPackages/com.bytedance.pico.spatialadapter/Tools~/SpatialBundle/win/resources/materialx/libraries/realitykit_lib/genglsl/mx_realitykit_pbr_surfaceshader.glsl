
void mx_realitykit_pbr_surfaceshader(float3 baseColor, float3 emissiveColor, float3 normal, float roughness, float metallic, float ambientOcclusion, float specular, float opacity, float opacityThreshold, float clearCoat, float clearCoatRoughness, float3 clearCoatNormal, bool hasPremultipliedAlpha, inout MaterialInputs material)
{
    material.baseColor.rgb = baseColor;
    material.baseColor.a = opacity;
    material.metallic = clamp(metallic,0.0,1.0);
    material.roughness = clamp(roughness,0.0,1.0);
    material.normal = normal;
    material.clearCoat = clearCoat;
    //material.clearCoatNormal = clearCoatNormal;
    material.clearCoatRoughness = clearCoatRoughness;
    material.emissive.rgb = emissiveColor;
    material.ambientOcclusion = ambientOcclusion;
    material.reflectance = computeDielectricF0(specular);
    material.emissive.w = 0.0;
}
