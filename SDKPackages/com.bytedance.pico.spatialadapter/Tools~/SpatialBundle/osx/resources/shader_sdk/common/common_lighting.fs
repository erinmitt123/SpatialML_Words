#ifndef _ENGINE_COMMON_LIGHTING_FS
#define _ENGINE_COMMON_LIGHTING_FS

struct Light {
    uint channels;
    uint id;
    uint worldId;
    half4 colorIntensity;  // rgb, pre-exposed intensity
    half3 l;
    half attenuation;
    highp vec3 worldPosition;
    bool castsLights;
    bool castsShadows;
    half NoL;
    half3 direction;
    half zLight;
    uint type;
    int shadowIndex;
    half shadowIntensity;
    bool isGroudingShadow;
};

struct PixelParams {
    half3  diffuseColor;
    half perceptualRoughness;
    half perceptualRoughnessUnclamped;
    half3  f0;
    half roughness;
    half3  dfg;
    half3  energyCompensation;

    half clearCoat;
    half clearCoatPerceptualRoughness;
    half clearCoatRoughness;

    half3  sheenColor;
#if !defined(SHADING_MODEL_CLOTH)
    half sheenRoughness;
    half sheenPerceptualRoughness;
    half sheenScaling;
    half sheenDFG;
#endif

    half3  anisotropicT;
    half3  anisotropicB;
    half anisotropy;

#if defined(SHADING_MODEL_SUBSURFACE) || defined(MATERIAL_HAS_REFRACTION)
    half thickness;
#endif
#if defined(SHADING_MODEL_SUBSURFACE)
    half3  subsurfaceColor;
    half subsurfacePower;
#endif

#if defined(SHADING_MODEL_CLOTH)
    half3  subsurfaceColor;
#endif

#if defined(MATERIAL_HAS_REFRACTION)
    half etaRI;
    half etaIR;
    half transmission;
    half uThickness;
    half3  absorption;
#endif
};

half computeMicroShadowing(half NoL, half visibility) {
    // Chan 2018, "Material Advances in Call of Duty: WWII"
    half aperture = inversesqrt(1.0 - min(visibility, 0.9999));
    half microShadow = saturate(NoL * aperture);
    return microShadow * microShadow;
}


/**
 * Returns the reflected vector at the current shading point. The reflected vector
 * return by this function might be different from shading_reflected:
 * - For anisotropic material, we bend the reflection vector to simulate
 *   anisotropic indirect lighting
 * - The reflected vector may be modified to point towards the dominant specular
 *   direction to match reference renderings when the roughness increases
 */

half3 getReflectedVector(const PixelParams pixel, const half3 v, const half3 n) {
if (MATERIAL_HAS_ANISOTROPY) {
    half3  anisotropyDirection = pixel.anisotropy >= 0.0 ? pixel.anisotropicB : pixel.anisotropicT;
    half3  anisotropicTangent  = cross(anisotropyDirection, v);
    half3  anisotropicNormal   = cross(anisotropicTangent, anisotropyDirection);
    half   bendFactor          = abs(pixel.anisotropy) * saturate(5.0 * pixel.perceptualRoughness);
    half3  bentNormal          = normalize(mix(n, anisotropicNormal, bendFactor));

    return reflect(-v, bentNormal);
} else {
    return reflect(-v, n);
}
}

void getAnisotropyPixelParams(const MaterialInputs material, inout PixelParams pixel) {
if (MATERIAL_HAS_ANISOTROPY) {
    vec3 direction = material.anisotropyDirection;
    pixel.anisotropy = material.anisotropy;
    pixel.anisotropicT = normalize(shading_tangentToWorld * direction);
    pixel.anisotropicB = normalize(cross(getWorldGeometricNormalVector(), pixel.anisotropicT));
}
}

#endif
