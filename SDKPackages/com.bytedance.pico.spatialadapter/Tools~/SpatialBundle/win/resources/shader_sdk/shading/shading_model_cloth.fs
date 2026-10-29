#ifndef _ENGINE_SHADING_MODEL_CLOTH_FS
#define _ENGINE_SHADING_MODEL_CLOTH_FS

/**
 * Evaluates lit materials with the cloth shading model. Similar to the standard
 * model, the cloth shading model is based on a Cook-Torrance microfacet model.
 * Its distribution and visibility terms are however very different to take into
 * account the softer apperance of many types of cloth. Some highly reflecting
 * fabrics like satin or leather should use the standard model instead.
 *
 * This shading model optionally models subsurface scattering events. The
 * computation of these events is not physically based but can add necessary
 * details to a material.
 */
half3 surfaceShading(const PixelParams pixel, const Light light, half occlusion) {
    half3 h = normalize(shading_view + light.l);
    half NoL = light.NoL;
    half NoH = saturate(dot(shading_normal, h));
    half LoH = saturate(dot(light.l, h));

    // specular BRDF
    half D = distributionCloth(pixel.roughness, NoH);
    half V = visibilityCloth(shading_NoV, NoL);
    half3  F = pixel.f0;
    // Ignore pixel.energyCompensation since we use a different BRDF here
    half3 Fr = (D * V) * F;

    // diffuse BRDF
    half diffuse = diffuse(pixel.roughness, shading_NoV, NoL, LoH);
if (MATERIAL_HAS_SUBSURFACE_COLOR) {
    // Energy conservative wrap diffuse to simulate subsurface scattering
    diffuse *= Fd_Wrap(dot(shading_normal, light.l), 0.5);
}

    // We do not multiply the diffuse term by the Fresnel term as discussed in
    // Neubelt and Pettineo 2013, "Crafting a Next-gen Material Pipeline for The Order: 1886"
    // The effect is fairly subtle and not deemed worth the cost for mobile
    half3 Fd = diffuse * pixel.diffuseColor;
    half3 color;
if (MATERIAL_HAS_SUBSURFACE_COLOR) {
    // Cheap subsurface scatter
    Fd *= saturate(pixel.subsurfaceColor + NoL);
    // We need to apply NoL separately to the specular lobe since we already took
    // it into account in the diffuse lobe
    color = Fd + Fr * NoL;
    color *= light.colorIntensity.rgb * (light.colorIntensity.w * light.attenuation * occlusion);
} else {
    color = Fd + Fr;
    color *= light.colorIntensity.rgb * (light.colorIntensity.w * light.attenuation * NoL * occlusion);
}

    return color;
}

#endif

