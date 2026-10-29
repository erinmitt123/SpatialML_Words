#ifndef _ENGINE_SHADING_UNLIT_FS
#define _ENGINE_SHADING_UNLIT_FS

#include "engine/shading/common_light_indirect.glsl"

/// ibl Fr begin
half3 getReflectedVector(const half3 n, half roughness) {
    return mix(shading_reflected, n, roughness * roughness);
}

//------------------------------------------------------------------------------
// IBL environment BRDF dispatch
//------------------------------------------------------------------------------

//unlit metallic is 0, so f0 is 0.04
half3 specularDFG(half3 dfg) {
    return mix(dfg.xxx, dfg.yyy, 0.04);
}

void addEmissive(const MaterialInputs material, inout half4 color) {
if (MATERIAL_HAS_EMISSIVE){
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
 * Evaluates unlit materials. In this lighting model, only the base color and
 * emissive properties are taken into account:
 *
 * finalColor = baseColor + emissive
 *
 * The emissive factor is only applied if the fragment passes the optional
 * alpha test.
 *
 * When the shadowMultiplier property is enabled on the material, the final
 * color is multiplied by the inverse light visibility to apply a shadow.
 * This is mostly useful in AR to cast shadows on unlit transparent shadow
 * receiving planes.
 */
half4 evaluateMaterial(const MaterialInputs material) {
    half4 color = material.baseColor;

    addEmissive(material, color);

    if(ENABLE_SOFT_GROUNDING_SHADOW) {
        evaluateGroundingShadow(color);
    }

    color.a = computeOutputAlpha(color.a);
    
    if(BLEND_MODE_OPAQUE || BLEND_MODE_MASKED){
        color.a *= getOpacity();
    }
    else if(BLEND_MODE_PREMULTIPLY) {
        color.rgb *= getOpacity();
    }

    color.rgb *= getDimScale();

    return color;
}

#endif
