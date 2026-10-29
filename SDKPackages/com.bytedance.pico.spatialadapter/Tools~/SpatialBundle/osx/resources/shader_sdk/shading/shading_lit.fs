#ifndef _ENGINE_SHADING_LIT_FS
#define _ENGINE_SHADING_LIT_FS

//------------------------------------------------------------------------------
// Lighting
//------------------------------------------------------------------------------
#if defined(GEOMETRIC_SPECULAR_AA)
half normalFiltering(half perceptualRoughness, const half3 worldNormal) {
    // Kaplanyan 2016, "Stable specular highlights"
    // Tokuyoshi 2017, "Error Reduction and Simplification for Shading Anti-Aliasing"
    // Tokuyoshi and Kaplanyan 2019, "Improved Geometric Specular Antialiasing"

    // This implementation is meant for deferred rendering in the original paper but
    // we use it in forward rendering as well (as discussed in Tokuyoshi and Kaplanyan
    // 2019). The main reason is that the forward version requires an expensive transform
    // of the half vector by the tangent frame for every light. This is therefore an
    // approximation but it works well enough for our needs and provides an improvement
    // over our original implementation based on Vlachos 2015, "Advanced VR Rendering".

    half3 du = dFdx(worldNormal);
    half3 dv = dFdy(worldNormal);

    half variance = materialParams._specularAntiAliasingVariance * (dot(du, du) + dot(dv, dv));

    half roughness = perceptualRoughnessToRoughness(perceptualRoughness);
    half kernelRoughness = min(2.0 * variance, materialParams._specularAntiAliasingThreshold);
    half squareRoughness = saturate(roughness * roughness + kernelRoughness);

    return roughnessToPerceptualRoughness(sqrt(squareRoughness));
}
#endif

void getCommonPixelParams(const MaterialInputs material, inout PixelParams pixel) {
    half4 baseColor = material.baseColor;

#if defined(SHADING_MODEL_SPECULAR_GLOSSINESS)
    // This is from KHR_materials_pbrSpecularGlossiness.
    half3 specularColor = material.specularColor;
    // https://kcoley.github.io/glTF/extensions/2.0/Khronos/KHR_materials_pbrSpecularGlossiness/#bestpractices
    //half metallic = computeMetallicFromSpecularColor(specularColor);
    //pixel.diffuseColor = computeDiffuseColor(baseColor, metallic);
    //Note: a diffuse map is required for the specular glossiness workflow
    pixel.diffuseColor = baseColor.rgb;
    pixel.f0 = specularColor;
#elif !defined(SHADING_MODEL_CLOTH)
    pixel.diffuseColor = computeDiffuseColor(baseColor, material.metallic);
    half reflectance;
#if !defined(SHADING_MODEL_SUBSURFACE)
//if (!MATERIAL_HAS_REFLECTANCE && MATERIAL_HAS_IOR) {
//    reflectance = iorToF0(max(1.0, material.ior), 1.0);
//} else {
    reflectance = computeDielectricF0(material.reflectance);
//}
#else
    // Assumes an interface from air to an IOR of 1.5 for dielectrics
    reflectance = computeDielectricF0(material.reflectance);
#endif
    pixel.f0 = computeF0(baseColor, material.metallic, reflectance);
#else
    pixel.diffuseColor = baseColor.rgb;
if (MATERIAL_HAS_SHEEN_COLOR) {
    pixel.f0 = material.sheenColor;
} else {
    pixel.f0 = vec3(0.0);
}
if (MATERIAL_HAS_SUBSURFACE_COLOR) {
    pixel.subsurfaceColor = material.subsurfaceColor;
}
#endif

if (BLEND_MODE_TRANSPARENT) {
    //transparent Mode only affect diffuse
    pixel.diffuseColor *= baseColor.a;
}

#if !defined(SHADING_MODEL_CLOTH) && !defined(SHADING_MODEL_SUBSURFACE)
#if defined(MATERIAL_HAS_REFRACTION)
    // Air's Index of refraction is 1.000277 at STP but everybody uses 1.0
    const half airIor = 1.0;
    half materialor;
//if (!MATERIAL_HAS_IOR) {
    // [common case] ior is not set in the material, deduce it from F0
    materialor = f0ToIor(pixel.f0.g);
//} else {
//    // if ior is set in the material, use it (can lead to unrealistic materials)
//    materialor = max(1.0, material.ior);
//}
    pixel.etaIR = airIor / materialor;  // air -> material
    pixel.etaRI = materialor / airIor;  // material -> air
//if (MATERIAL_HAS_TRANSMISSION) {
//    pixel.transmission = saturate(material.transmission);
//} else {
    pixel.transmission = 1.0;
//}
//if (MATERIAL_HAS_ABSORPTION) {
//    if (MATERIAL_HAS_THICKNESS || MATERIAL_HAS_MICRO_THICKNESS) {
//        pixel.absorption = max(vec3(0.0), material.absorption);
//    } else {
//        pixel.absorption = saturate(material.absorption);
//    }
//}else {
//    pixel.absorption = vec3(0.0);
//}
//if (MATERIAL_HAS_THICKNESS){
    pixel.thickness = max(0.0, material.thickness);
//}
#if (REFRACTION_TYPE == REFRACTION_TYPE_THIN)
//if (MATERIAL_HAS_MICRO_THICKNESS) {
//    pixel.uThickness = max(0.0, material.microThickness);
//} else {
    pixel.uThickness = 0.0;
//}
#else
    pixel.uThickness = 0.0;
#endif
#endif
#endif
}

void getSheenPixelParams(const MaterialInputs material, inout PixelParams pixel) {
#if !defined(SHADING_MODEL_CLOTH) && !defined(SHADING_MODEL_SUBSURFACE)
if (MATERIAL_HAS_SHEEN_COLOR) {
    pixel.sheenColor = material.sheenColor;

    half sheenPerceptualRoughness = material.sheenRoughness;
    sheenPerceptualRoughness = clamp(sheenPerceptualRoughness, 0.0, 1.0);

#if defined(GEOMETRIC_SPECULAR_AA)
    sheenPerceptualRoughness =
            normalFiltering(sheenPerceptualRoughness, getWorldGeometricNormalVector());
#endif

    pixel.sheenPerceptualRoughness = sheenPerceptualRoughness;
    pixel.sheenRoughness = perceptualRoughnessToRoughness(sheenPerceptualRoughness);
} else {
    pixel.sheenColor = vec3(0.0);
}
#endif
}

void getClearCoatPixelParams(const MaterialInputs material, inout PixelParams pixel) {
if (MATERIAL_HAS_CLEAR_COAT) {
    pixel.clearCoat = material.clearCoat;

    // Clamp the clear coat roughness to avoid divisions by 0

    half clearCoatPerceptualRoughness = 0.0;
    clearCoatPerceptualRoughness = clamp(material.clearCoatRoughness, 0.0, 1.0);

#if defined(GEOMETRIC_SPECULAR_AA)
    clearCoatPerceptualRoughness = normalFiltering(clearCoatPerceptualRoughness, getWorldGeometricNormalVector());
#endif

    pixel.clearCoatPerceptualRoughness = clearCoatPerceptualRoughness;
    pixel.clearCoatRoughness = perceptualRoughnessToRoughness(clearCoatPerceptualRoughness);

#if defined(CLEAR_COAT_IOR_CHANGE)
    // The base layer's f0 is computed assuming an interface from air to an IOR
    // of 1.5, but the clear coat layer forms an interface from IOR 1.5 to IOR
    // 1.5. We recompute f0 by first computing its IOR, then reconverting to f0
    // by using the correct interface
    pixel.f0 = mix(pixel.f0, f0ClearCoatToSurface(pixel.f0), pixel.clearCoat);
#endif
}
}

void getRoughnessPixelParams(const MaterialInputs material, inout PixelParams pixel) {
#if defined(SHADING_MODEL_SPECULAR_GLOSSINESS)
    half perceptualRoughness = computeRoughnessFromGlossiness(material.glossiness);
#else
    half perceptualRoughness = material.roughness;
#endif

    // This is used by the refraction code and must be saved before we apply specular AA
    pixel.perceptualRoughnessUnclamped = perceptualRoughness;

#if defined(GEOMETRIC_SPECULAR_AA)
    perceptualRoughness = normalFiltering(perceptualRoughness, getWorldGeometricNormalVector());
#endif

if (MATERIAL_HAS_CLEAR_COAT) {
    // This is a hack but it will do: the base layer must be at least as rough
    // as the clear coat layer to take into account possible diffusion by the
    // top layer
    half basePerceptualRoughness = max(perceptualRoughness, pixel.clearCoatPerceptualRoughness);
    perceptualRoughness = mix(perceptualRoughness, basePerceptualRoughness, pixel.clearCoat);
}

    // Clamp the roughness to a minimum value to avoid divisions by 0 during lighting
    pixel.perceptualRoughness = clamp(perceptualRoughness, 0.0, 1.0);
    // Remaps the roughness to a perceptually linear roughness (roughness^2)
    pixel.roughness = perceptualRoughnessToRoughness(pixel.perceptualRoughness);
}

void getSubsurfacePixelParams(const MaterialInputs material, inout PixelParams pixel) {
#if defined(SHADING_MODEL_SUBSURFACE)
    pixel.subsurfacePower = material.subsurfacePower;
    pixel.subsurfaceColor = material.subsurfaceColor;
    pixel.thickness = saturate(material.thickness);
#endif
}

void getEnergyCompensationPixelParams(inout PixelParams pixel) {
    // Pre-filtered DFG term used for image-based lighting
    pixel.dfg = prefilteredDFG(pixel.perceptualRoughness, shading_NoV);

#if !defined(SHADING_MODEL_CLOTH)
    // Energy compensation for multiple scattering in a microfacet model
    // See "Multiple-Scattering Microfacet BSDFs with the Smith Model"
    pixel.energyCompensation = 1.0 + pixel.f0 * (1.0 / pixel.dfg.y - 1.0);
#else
    pixel.energyCompensation = vec3(1.0);
#endif

#if !defined(SHADING_MODEL_CLOTH)
if (MATERIAL_HAS_SHEEN_COLOR) {
    pixel.sheenDFG = prefilteredDFG(pixel.sheenPerceptualRoughness, shading_NoV).z;
    pixel.sheenScaling = 1.0 - max3(pixel.sheenColor) * pixel.sheenDFG;
}
#endif
}

/**
 * Computes all the parameters required to shade the current pixel/fragment.
 * These parameters are derived from the MaterialInputs structure computed
 * by the user's material code.
 *
 * This function is also responsible for discarding the fragment when alpha
 * testing fails.
 */
void getPixelParams(const MaterialInputs material, out PixelParams pixel) {
    getCommonPixelParams(material, pixel);
    getSheenPixelParams(material, pixel);
    getClearCoatPixelParams(material, pixel);
    getRoughnessPixelParams(material, pixel);
    getSubsurfacePixelParams(material, pixel);
    getAnisotropyPixelParams(material, pixel);
    getEnergyCompensationPixelParams(pixel);
}

/**
 * This function evaluates all lights one by one:
 * - Image based lights (IBL)
 * - Directional lights
 * - Punctual lights
 *
 * Area lights are currently not supported.
 *
 * Returns a pre-exposed HDR RGBA color in linear space.
 */
half4 evaluateLights(const MaterialInputs material) {
    PixelParams pixel;
    getPixelParams(material, pixel);

    // Ideally we would keep the diffuse and specular components separate
    // until the very end but it costs more ALUs on mobile. The gains are
    // currently not worth the extra operations
    half3 color = vec3(0.0);

    // We always evaluate the IBL as not having one is going to be uncommon,
    // it also saves 1 shader variant
    if (USE_SIMPLE_LIT){
        evaluateIBLSimple(material, pixel, color);
    }
    else{
        evaluateIBL(material, pixel, color);
    }

    if (GLOBAL_HAS_DIRECTIONAL_LIGHTING) {
        evaluateDirectionalLights(material, pixel, color);
    }

    if (GLOBAL_HAS_PUNCTUAL_LIGHTING) {
        evaluatePunctualLights(material, pixel, color);
    }

    return vec4(color, computeOutputAlpha(material.baseColor.a));
}

void addEmissive(const MaterialInputs material, inout half4 color) {
    if (MATERIAL_HAS_EMISSIVE) {
        half4 emissive = material.emissive;
        half attenuation = mix(1.0, getExposure(), emissive.w);
        if(BLEND_MODE_OPAQUE || BLEND_MODE_MASKED){
            color.rgb += emissive.rgb * attenuation;
        } else {
            color.rgb += emissive.rgb * attenuation * material.baseColor.a;
        }
    }
}

/**
 * Evaluate lit materials. The actual shading model used to do so is defined
 * by the function surfaceShading() found in shading_model_*.fs.
 *
 * Returns a pre-exposed HDR RGBA color in linear space.
 */
half4 evaluateMaterial(const MaterialInputs material) {
    half4 color = evaluateLights(material);
    addEmissive(material, color);
    if (BLEND_MODE_TRANSPARENT) {
        float opacity = getOpacity();
        float steepCurve = 0.5 * (tanh(opacity * 60.0 - 3.0) + 1.0);
        float modifiedOpacity = steepCurve * 0.7 + 0.3 * opacity;
        color.rgb *= modifiedOpacity;
    }

    if(BLEND_MODE_OPAQUE || BLEND_MODE_MASKED){
        color.rgba *= getOpacity();
    }

    color.rgb *= getDimScale();

    return color;
}

#endif
