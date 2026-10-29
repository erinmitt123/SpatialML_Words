#ifndef _ENGINE_SHADING_MODEL_STANDARD_FS
#define _ENGINE_SHADING_MODEL_STANDARD_FS

half3 sheenLobe(const PixelParams pixel, half NoV, half NoL, half NoH) {
    half D = distributionCloth(pixel.sheenRoughness, NoH);
    half V = visibilityCloth(NoV, NoL);

    return (D * V) * pixel.sheenColor;
}

// #if defined(MATERIAL_HAS_CLEAR_COAT)
half clearCoatLobe(const PixelParams pixel, const half3 h, half NoH, half LoH, out half Fcc) {
    half clearCoatNoH = NoH;
if (MATERIAL_HAS_CLEAR_COAT_NORMAL || MATERIAL_HAS_NORMAL) {
    // If the material has a normal map, we want to use the geometric normal
    // instead to avoid applying the normal map details to the clear coat layer
    clearCoatNoH = saturate(dot(shading_clearCoatNormal, h));
}

    // clear coat specular lobe
    half D = distributionClearCoat(pixel.clearCoatRoughness, clearCoatNoH, h);
    half V = visibilityClearCoat(LoH);
    half F = F_Schlick(0.04, 1.0, LoH) * pixel.clearCoat; // fix IOR to 1.5

    Fcc = F;
    return D * V * F;
}
// #endif

half3 anisotropicLobe(const PixelParams pixel, const Light light, const half3 h,
        half NoV, half NoL, half NoH, half LoH) {

    half3 l = light.l;
    half3 t = pixel.anisotropicT;
    half3 b = pixel.anisotropicB;
    half3 v = shading_view;

    half ToV = dot(t, v);
    half BoV = dot(b, v);
    half ToL = dot(t, l);
    half BoL = dot(b, l);
    half ToH = dot(t, h);
    half BoH = dot(b, h);

    // Anisotropic parameters: at and ab are the roughness along the tangent and bitangent
    // to simplify materials, we derive them from a single roughness parameter
    // Kulla 2017, "Revisiting Physically Based Shading at Imageworks"
    half at = max(pixel.roughness * (1.0 + pixel.anisotropy), MIN_ROUGHNESS);
    half ab = max(pixel.roughness * (1.0 - pixel.anisotropy), MIN_ROUGHNESS);

    // specular anisotropic BRDF
    half D = distributionAnisotropic(at, ab, ToH, BoH, NoH);
    half V = visibilityAnisotropic(pixel.roughness, at, ab, ToV, BoV, ToL, BoL, NoV, NoL);
    half3  F = fresnel(pixel.f0, LoH);

    return (D * V) * F;
}

half3 isotropicLobe(const PixelParams pixel, const Light light, const half3 h,
        half NoV, half NoL, half NoH, half LoH) {
    if(USE_SIMPLE_LIT)
    {
        half specular = BlinnPhong(pixel.roughness, NoH);
        return specular * pixel.f0;
    }
    else if(SHADER_LOD_1)
    {
        half specular = DirectBRDFSpecular(NoH, LoH, pixel.roughness);
        return specular * pixel.f0;
    }
    else// if(SHADER_LOD_0)
    {
        half D = distribution(pixel.roughness, NoH, h);
        half V = visibility(pixel.roughness, NoV, NoL);
        half3  F = fresnel(pixel.f0, LoH);
        return (D * V) * F;
    }
}

half3 specularLobe(const PixelParams pixel, const Light light, const half3 h,
        half NoV, half NoL, half NoH, half LoH) {
if (MATERIAL_HAS_ANISOTROPY) {
    return anisotropicLobe(pixel, light, h, NoV, NoL, NoH, LoH);
} else {
    return isotropicLobe(pixel, light, h, NoV, NoL, NoH, LoH);
}
}

half3 diffuseLobe(const PixelParams pixel, half NoV, half NoL, half LoH) {
    return pixel.diffuseColor * diffuse(pixel.roughness, NoV, NoL, LoH);
}

/**
 * Evaluates lit materials with the standard shading model. This model comprises
 * of 2 BRDFs: an optional clear coat BRDF, and a regular surface BRDF.
 *
 * Surface BRDF
 * The surface BRDF uses a diffuse lobe and a specular lobe to render both
 * dielectrics and conductors. The specular lobe is based on the Cook-Torrance
 * micro-facet model (see brdf.fs for more details). In addition, the specular
 * can be either isotropic or anisotropic.
 *
 * Clear coat BRDF
 * The clear coat BRDF simulates a transparent, absorbing dielectric layer on
 * top of the surface. Its IOR is set to 1.5 (polyutherane) to simplify
 * our computations. This BRDF only contains a specular lobe and while based
 * on the Cook-Torrance microfacet model, it uses cheaper terms than the surface
 * BRDF's specular lobe (see brdf.fs).
 */
half3 surfaceShading(const PixelParams pixel, const Light light, half occlusion) {
    half3 h = normalize(shading_view + light.l);

    half NoV = shading_NoV;
    half NoL = saturate(light.NoL);
    half NoH = saturate(dot(shading_normal, h));
    half LoH = saturate(dot(light.l, h));

    half3 Fr = specularLobe(pixel, light, h, NoV, NoL, NoH, LoH);
    half3 Fd = diffuseLobe(pixel, NoV, NoL, LoH);
#if defined(MATERIAL_HAS_REFRACTION)
    Fd *= (1.0 - pixel.transmission);
#endif

    // TODO: attenuate the diffuse lobe to avoid energy gain

    // The energy compensation term is used to counteract the darkening effect
    // at high roughness
    half3 color = Fd + Fr * pixel.energyCompensation;

if (MATERIAL_HAS_SHEEN_COLOR) {
    color *= pixel.sheenScaling;
    color += sheenLobe(pixel, NoV, NoL, NoH);
}

if (MATERIAL_HAS_CLEAR_COAT) {
    half Fcc;
    half clearCoat = clearCoatLobe(pixel, h, NoH, LoH, Fcc);
    half attenuation = 1.0 - Fcc;

if (MATERIAL_HAS_CLEAR_COAT_NORMAL || MATERIAL_HAS_NORMAL) {
    color *= attenuation * NoL;

    // If the material has a normal map, we want to use the geometric normal
    // instead to avoid applying the normal map details to the clear coat layer
    half clearCoatNoL = saturate(dot(shading_clearCoatNormal, light.l));
    color += clearCoat * clearCoatNoL;

    // Early exit to avoid the extra multiplication by NoL
    return (color * light.colorIntensity.rgb) *
            (light.colorIntensity.w * light.attenuation * occlusion);
} else {
    color *= attenuation;
    color += clearCoat;
}
}

    return (color * light.colorIntensity.rgb) *
            (light.colorIntensity.w * light.attenuation * NoL * occlusion);
}

#endif
