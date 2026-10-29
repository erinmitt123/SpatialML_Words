#ifndef _ENGINE_LIGHT_INDIRECT_FS
#define _ENGINE_LIGHT_INDIRECT_FS

#include "engine/shading/common_light_indirect.glsl"

half3 specularDFG(const PixelParams pixel) {
#if defined(SHADING_MODEL_CLOTH)
    return pixel.f0 * pixel.dfg.z;
#else
    return mix(pixel.dfg.xxx, pixel.dfg.yyy, pixel.f0);
#endif
}

half3 getReflectedVector(const PixelParams pixel, const half3 n) {
half3 r = shading_reflected;
if (MATERIAL_HAS_ANISOTROPY) {
    r = getReflectedVector(pixel, shading_view, n);
}
    return getSpecularDominantDirection(n, r, pixel.roughness);
}

//------------------------------------------------------------------------------
// IBL evaluation
//------------------------------------------------------------------------------

void evaluateClothIndirectDiffuseBRDF(const PixelParams pixel, inout half diffuse) {
#if defined(SHADING_MODEL_CLOTH)
if (MATERIAL_HAS_SUBSURFACE_COLOR) {
    // Simulate subsurface scattering with a wrap diffuse term
    diffuse *= Fd_Wrap(shading_NoV, 0.5);
}
#endif
}

void evaluateSheenIBL(const PixelParams pixel, half diffuseAO, inout half3 Fd, inout half3 Fr) {
#if !defined(SHADING_MODEL_CLOTH) && !defined(SHADING_MODEL_SUBSURFACE)
if (MATERIAL_HAS_SHEEN_COLOR) {
    // Albedo scaling of the base layer before we layer sheen on top
    Fd *= pixel.sheenScaling;
    Fr *= pixel.sheenScaling;

    half3 reflectance = pixel.sheenDFG * pixel.sheenColor;
    half specularAO = 1.0;
    reflectance *= specularAO;

    Fr += reflectance * prefilteredRadiance(shading_reflected, pixel.sheenPerceptualRoughness);
}
#endif
}

void evaluateClearCoatIBL(const PixelParams pixel, half diffuseAO, inout half3 Fd, inout half3 Fr) {
if (MATERIAL_HAS_CLEAR_COAT) {
    if (pixel.clearCoat > 0.0) {
        half clearCoatNoV = shading_NoV;
        half3 clearCoatR = shading_reflected;

if (MATERIAL_HAS_CLEAR_COAT_NORMAL || MATERIAL_HAS_NORMAL) {
        // We want to use the geometric normal for the clear coat layer
        clearCoatNoV = clampNoV(dot(shading_clearCoatNormal, shading_view));
        clearCoatR = reflect(-shading_view, shading_clearCoatNormal);
}

        // The clear coat layer assumes an IOR of 1.5 (4% reflectance)
        half Fc = F_Schlick(0.04, 1.0, clearCoatNoV) * pixel.clearCoat;
        half attenuation = 1.0 - Fc;
        Fd *= attenuation;
        Fr *= attenuation;

        // TODO: Should we apply specularAO to the attenuation as well?
        half specularAO = 1.0;
        Fr += prefilteredRadiance(clearCoatR, pixel.clearCoatPerceptualRoughness) * (specularAO * Fc);
    }
}
}

void evaluateSubsurfaceIBL(const PixelParams pixel, const half3 diffuseIrradiance,
        inout half3 Fd, inout half3 Fr) {
#if defined(SHADING_MODEL_SUBSURFACE)
    half3 viewIndependent = diffuseIrradiance;
    half3 viewDependent = prefilteredRadiance(-shading_view, pixel.roughness, 1.0 + pixel.thickness);
    half attenuation = (1.0 - pixel.thickness) / (2.0 * PI);
    Fd += pixel.subsurfaceColor * (viewIndependent + viewDependent) * attenuation;
#elif defined(SHADING_MODEL_CLOTH)
if (MATERIAL_HAS_SUBSURFACE_COLOR) {
    Fd *= saturate(pixel.subsurfaceColor + shading_NoV);
}
#endif
}

#if defined(MATERIAL_HAS_REFRACTION)

struct Refraction {
    vec3 position;
    half3 direction;
    half d;
};

void refractionSolidSphere(const PixelParams pixel,
    const half3 n, half3 r, out Refraction ray) {
    r = refract(r, n, pixel.etaIR);
    half NoR = dot(n, r);
    half d = pixel.thickness * -NoR;
    ray.position = vec3(shading_position + r * d);
    ray.d = d;
    half3 n1 = normalize(NoR * r - n * 0.5);
    ray.direction = refract(r, n1,  pixel.etaRI);
}

void refractionSolidBox(const PixelParams pixel,
    const half3 n, half3 r, out Refraction ray) {
    half3 rr = refract(r, n, pixel.etaIR);
    half NoR = dot(n, rr);
    half d = pixel.thickness / max(-NoR, 0.001);
    ray.position = vec3(shading_position + rr * d);
    ray.direction = r;
    ray.d = d;
#if REFRACTION_MODE == REFRACTION_MODE_CUBEMAP
    // fudge direction vector, so we see the offset due to the thickness of the object
    half envDistance = 10.0; // this should come from a ubo
    ray.direction = normalize((ray.position - shading_position) + ray.direction * envDistance);
#endif
}

void refractionThinSphere(const PixelParams pixel,
    const half3 n, half3 r, out Refraction ray) {
    half d = 0.0;
//if (MATERIAL_HAS_MICRO_THICKNESS) {
//    // note: we need the refracted ray to calculate the distance traveled
//    // we could use shading_NoV, but we would lose the dependency on ior.
//    half3 rr = refract(r, n, pixel.etaIR);
//    half NoR = dot(n, rr);
//    d = pixel.uThickness / max(-NoR, 0.001);
//    ray.position = vec3(shading_position + rr * d);
//} else {
    ray.position = vec3(shading_position);
//}
    ray.direction = r;
    ray.d = d;
}

half3 evaluateRefraction(
    const PixelParams pixel,
    const half3 n0, half3 E) {

    Refraction ray;

#if REFRACTION_TYPE == REFRACTION_TYPE_SOLID
    refractionSolidSphere(pixel, n0, -shading_view, ray);
#elif REFRACTION_TYPE == REFRACTION_TYPE_THIN
    refractionThinSphere(pixel, n0, -shading_view, ray);
#else
#error invalid REFRACTION_TYPE
#endif

    // compute transmission T
half3 T;
//if (MATERIAL_HAS_ABSORPTION){
//    if (MATERIAL_HAS_THICKNESS || MATERIAL_HAS_MICRO_THICKNESS) {
//        T = min(vec3(1.0), exp(-pixel.absorption * ray.d));
//    } else {
//        T = 1.0 - pixel.absorption;
//    }
//}

    // Roughness remapping so that an IOR of 1.0 means no microfacet refraction and an IOR
    // of 1.5 has full microfacet refraction
    half perceptualRoughness = mix(pixel.perceptualRoughnessUnclamped, 0.0,
            saturate(pixel.etaIR * 3.0 - 2.0));
#if REFRACTION_TYPE == REFRACTION_TYPE_THIN
    // For thin surfaces, the light will bounce off at the second interface in the direction of
    // the reflection, effectively adding to the specular, but this process will repeat itself.
    // Each time the ray exits the surface on the front side after the first bounce,
    // it's multiplied by E^2, and we get: E + E(1-E)^2 + E^3(1-E)^2 + ...
    // This infinite series converges and is easy to simplify.
    // Note: we calculate these bounces only on a single component,
    // since it's a fairly subtle effect.
    E *= 1.0 + pixel.transmission * (1.0 - E.g) / (1.0 + E.g);
#endif

    /* sample the cubemap or screen-space */
#if REFRACTION_MODE == REFRACTION_MODE_CUBEMAP
    // when reading from the cubemap, we are not pre-exposed so we apply iblLuminance
    // which is not the case when we'll read from the screen-space buffer
    half3 Ft = prefilteredRadiance(ray.direction, perceptualRoughness);
#endif

    // base color changes the amount of light passing through the boundary
    Ft *= pixel.diffuseColor;

    // fresnel from the first interface
    Ft *= 1.0 - E;

    // apply absorption
//if (MATERIAL_HAS_ABSORPTION) {
//    Ft *= T;
//}

    return Ft;
}
#endif

void evaluateIBL(const MaterialInputs material, const PixelParams pixel, inout half3 color) {
    // specular layer
    half3 Fr = vec3(0.0);

    const half4 ssrFr = vec4(0.0);

    half3 E = specularDFG(pixel);
    half3 r = getReflectedVector(pixel, shading_normal);
    Fr = E * prefilteredRadiance(r, pixel.perceptualRoughness);

    // Ambient occlusion
    half diffuseAO = 1.0;
if (MATERIAL_HAS_AMBIENT_OCCLUSION) {
    diffuseAO = min(material.ambientOcclusion, diffuseAO);
}
    half specularAO = 1.0;

    half3 specularSingleBounceAO = specularAO * pixel.energyCompensation;
    Fr *= specularSingleBounceAO;
#if defined(MATERIAL_HAS_REFLECTIONS)
    ssrFr.rgb *= specularSingleBounceAO;
#endif

    // diffuse layer
    half diffuseBRDF = diffuseAO; // Fd_Lambert() is baked in the SH below
    evaluateClothIndirectDiffuseBRDF(pixel, diffuseBRDF);

    half3 diffuseNormal = shading_normal;
//if (MATERIAL_HAS_BENT_NORMAL) {
//    diffuseNormal = shading_bentNormal;
//}

//     half3 diffuseIrradiance = diffuseIrradiance(diffuseNormal, perViewOverride_localIblSpecular, perViewOverride_localIblSpecularTwo);
    half3 diffuseIrradiance = diffuseIrradiance(diffuseNormal);
    half3 Fd = pixel.diffuseColor * diffuseIrradiance * (1.0 - E) * diffuseBRDF;

    // subsurface layer
    evaluateSubsurfaceIBL(pixel, diffuseIrradiance, Fd, Fr);

    // sheen layer
    evaluateSheenIBL(pixel, diffuseAO, Fd, Fr);

    // clear coat layer
    evaluateClearCoatIBL(pixel, diffuseAO, Fd, Fr);

    // because the ibl intensity is independent between local and global, the pre-calculation is performed
    //Fr *= getIblLuminance();
    //Fd *= getIblLuminance();

#if defined(MATERIAL_HAS_REFRACTION)
    half3 Ft = evaluateRefraction(pixel, shading_normal, E);
    Ft *= pixel.transmission;
    Fd *= (1.0 - pixel.transmission);
#endif

#if defined(MATERIAL_HAS_REFLECTIONS)
    Fr = Fr * (1.0 - ssrFr.a) + (E * ssrFr.rgb);
#endif

    // Combine all terms
    // Note: iblLuminance is already premultiplied by the exposure

    color.rgb += Fr + Fd;
#if defined(MATERIAL_HAS_REFRACTION)
    color.rgb += Ft;
#endif
}

void evaluateIBLSimple(const MaterialInputs material, const PixelParams pixel, inout half3 color) {
    half3 diffuseNormal = shading_normal;
//if (MATERIAL_HAS_BENT_NORMAL) {
//    diffuseNormal = shading_bentNormal;
//}
    half3 diffuseIrradiance = diffuseIrradiance(diffuseNormal);
    color.rgb += pixel.diffuseColor * diffuseIrradiance;
}

#endif
