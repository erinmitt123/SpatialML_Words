
void mx_pbr_surfaceshader(float3 baseColor, float opacity, float metallic,  float roughness, float3 normal, float clearCoat, float3 clearCoatNormal, float clearCoatRoughness, float4 emissive, float ambientOcclusion, float reflectance, float anisotropy, float3 anisotropyDirection, float opacityThreshold, inout MaterialInputs material)
{
    material.baseColor.rgb = baseColor;
    material.baseColor.a = opacity;
    material.metallic = clamp(metallic,0.0,1.0);
    material.roughness = clamp(roughness,0.0,1.0);
    material.normal = normal;
    material.clearCoat = clearCoat;
    //material.clearCoatNormal = clearCoatNormal;
    material.clearCoatRoughness = clearCoatRoughness;
    material.emissive = emissive;
    material.ambientOcclusion = ambientOcclusion;
    material.reflectance = reflectance;
    material.anisotropy = anisotropy;
    material.anisotropyDirection = anisotropyDirection;
    material.emissive.w = 0.0;
}
