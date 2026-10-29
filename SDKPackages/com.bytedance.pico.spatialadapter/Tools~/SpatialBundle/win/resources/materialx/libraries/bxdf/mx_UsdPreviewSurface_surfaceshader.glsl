
void mx_UsdPreviewSurface_surfaceshader(float3 diffuseColor, float3 emissiveColor, int useSpecularWorkflow, float3 specularColor, float metallic,  float roughness, float clearCoat, float clearCoatRoughness, float opacity, float opacityThreshold, float ior, float3 normal, float displacement, float occlusion, inout MaterialInputs material)
{
    material.baseColor.rgb = diffuseColor;
    material.baseColor.a = opacity;
    material.metallic = clamp(metallic,0.0,1.0);
    material.roughness = clamp(roughness,0.0,1.0);
    material.normal = normal;
    material.clearCoat = clearCoat;
    material.clearCoatRoughness = clearCoatRoughness;
    material.emissive.rgb = emissiveColor;
    material.ambientOcclusion = occlusion;
    material.reflectance = (ior - 1.0)/(0.4 * ior + 0.4);
    material.emissive.w = 0.0;
}
