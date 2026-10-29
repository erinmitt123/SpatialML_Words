#ifndef _ENGINE_MATERIAL_INPUTS_FS
#define _ENGINE_MATERIAL_INPUTS_FS

// Decide if we can skip lighting when dot(n, l) <= 0.0
#if defined(SHADING_MODEL_CLOTH)
    #define MATERIAL_CAN_SKIP_LIGHTING_CLOTH
#elif defined(SHADING_MODEL_SUBSURFACE) || defined(MATERIAL_HAS_CUSTOM_SURFACE_SHADING)
    // Cannot skip lighting
#else
    #define MATERIAL_CAN_SKIP_LIGHTING
#endif

struct MaterialInputs {
    half4  baseColor;
#if !defined(SHADING_MODEL_UNLIT)
#if !defined(SHADING_MODEL_SPECULAR_GLOSSINESS)
    half roughness;
#endif
#if !defined(SHADING_MODEL_CLOTH) && !defined(SHADING_MODEL_SPECULAR_GLOSSINESS)
    half metallic;
    half reflectance;
#endif
    half ambientOcclusion;
#endif
    half4  emissive;

#if !defined(SHADING_MODEL_CLOTH) && !defined(SHADING_MODEL_SUBSURFACE) && !defined(SHADING_MODEL_UNLIT)
    half3 sheenColor;
    half sheenRoughness;
#endif

    half clearCoat;
    half clearCoatRoughness;

    half anisotropy;
    half3  anisotropyDirection;
    half anisotropyAngle;

#if defined(SHADING_MODEL_SUBSURFACE) || defined(MATERIAL_HAS_REFRACTION)
    half thickness;
#endif
#if defined(SHADING_MODEL_SUBSURFACE)
    half subsurfacePower;
    half3  subsurfaceColor;
#endif

#if defined(SHADING_MODEL_CLOTH)
    half3  sheenColor;
    half3  subsurfaceColor;
#endif

#if defined(SHADING_MODEL_SPECULAR_GLOSSINESS)
    half3  specularColor;
    half glossiness;
#endif

    half3  normal;
    half3  bentNormal;
    half3  clearCoatNormal;
    half4  postLightingColor;

#if !defined(SHADING_MODEL_CLOTH) && !defined(SHADING_MODEL_SUBSURFACE) && !defined(SHADING_MODEL_UNLIT)
#if defined(MATERIAL_HAS_REFRACTION)
    half3 absorption;

    half transmission;
    half ior;
#if (REFRACTION_TYPE == REFRACTION_TYPE_THIN)
    half microThickness;
#endif
#elif !defined(SHADING_MODEL_SPECULAR_GLOSSINESS)
    half ior;
#endif
#endif
};

void initMaterial(out MaterialInputs material) {
    material.baseColor = vec4(1.0);
#if !defined(SHADING_MODEL_UNLIT)
#if !defined(SHADING_MODEL_SPECULAR_GLOSSINESS)
    material.roughness = 1.0;
#endif
#if !defined(SHADING_MODEL_CLOTH) && !defined(SHADING_MODEL_SPECULAR_GLOSSINESS)
    material.metallic = 0.0;
    material.reflectance = 0.5;
#endif
    material.ambientOcclusion = 1.0;
#endif
    material.emissive = vec4(vec3(0.0), 1.0);

#if !defined(SHADING_MODEL_CLOTH) && !defined(SHADING_MODEL_SUBSURFACE) && !defined(SHADING_MODEL_UNLIT)
if (MATERIAL_HAS_SHEEN_COLOR) {
    material.sheenColor = vec3(0.0);
    material.sheenRoughness = 0.0;
}
#endif

if (MATERIAL_HAS_CLEAR_COAT) {
    material.clearCoat = 1.0;
    material.clearCoatRoughness = 0.0;
}

if (MATERIAL_HAS_ANISOTROPY) {
    material.anisotropy = 0.0;
    material.anisotropyDirection = vec3(1.0, 0.0, 0.0);
}

#if defined(SHADING_MODEL_SUBSURFACE) || defined(MATERIAL_HAS_REFRACTION)
    material.thickness = 0.5;
#endif
#if defined(SHADING_MODEL_SUBSURFACE)
    material.subsurfacePower = 12.234;
    material.subsurfaceColor = vec3(1.0);
#endif

#if defined(SHADING_MODEL_CLOTH)
    material.sheenColor = sqrt(material.baseColor.rgb);
    material.subsurfaceColor = vec3(0.0);
#endif

#if defined(SHADING_MODEL_SPECULAR_GLOSSINESS)
    material.glossiness = 0.0;
    material.specularColor = vec3(0.0);
#endif

if (MATERIAL_HAS_NORMAL) {
    material.normal = vec3(0.0, 0.0, 1.0);
}

//if (MATERIAL_HAS_BENT_NORMAL) {
//    material.bentNormal = vec3(0.0, 0.0, 1.0);
//}

if (MATERIAL_HAS_CLEAR_COAT_NORMAL) {
    material.clearCoatNormal = vec3(0.0, 0.0, 1.0);
}

//if (MATERIAL_HAS_POST_LIGHTING_COLOR) {
//    material.postLightingColor = vec4(0.0);
//}

#if !defined(SHADING_MODEL_CLOTH) && !defined(SHADING_MODEL_SUBSURFACE) && !defined(SHADING_MODEL_UNLIT)
#if defined(MATERIAL_HAS_REFRACTION)
//if (MATERIAL_HAS_ABSORPTION){
//    material.absorption = vec3(0.0);
//}

//if (MATERIAL_HAS_TRANSMISSION) {
//    material.transmission = 1.0;
//}

//if (MATERIAL_HAS_IOR) {
//    material.ior = 1.5;
//}

#if (REFRACTION_TYPE == REFRACTION_TYPE_THIN)
//if (MATERIAL_HAS_THICKNESS) {
//    material.microThickness = 0.0;
//}
#endif
#elif !defined(SHADING_MODEL_SPECULAR_GLOSSINESS)
    material.ior = 1.5;
#endif
#endif
}

#if defined(MATERIAL_HAS_CUSTOM_SURFACE_SHADING)
/** @public-api */
struct LightData {
    half4  colorIntensity;
    half3  l;
    half NdotL;
    vec3  worldPosition;
    half attenuation;
    half visibility;
};

/** @public-api */
struct ShadingData {
    half3  diffuseColor;
    half perceptualRoughness;
    half3  f0;
    half roughness;
};
#endif

#endif
