#ifndef _ENGINE_SURFACE_MAIN_FS
#define _ENGINE_SURFACE_MAIN_FS

// Extensions
${extensions}

// Defines
${defines}

// Spec-constant
${specConstants}

// PushConstants
${pushConstants}

// User samplers & buffers
${userSamplersAndBuffers}

// Variables
${variables}

// Subpass
${subpass}

// Includes begin
#include "engine/common/common_types.glsl"
#include "engine/common/common_attribute_keywords.glsl"
#include "engine/common/common_descriptor_set.glsl"
#include "engine/common/common_samplers.glsl"
#include "engine/surface/surface_samplers.glsl"
#include "engine/common/common_uniforms.glsl"
#include "engine/surface/surface_uniforms.glsl"

#include "engine/common/common_defines.glsl"
#include "engine/common/common_math.glsl"
#include "engine/common/common_colorspace.glsl"
#include "engine/common/common_instancing.glsl"
#include "engine/common/common_shadowing.glsl"
#include "engine/common/common_shading.fs"
#include "engine/common/common_graphics.fs"
#include "engine/common/common_material.fs"
#include "engine/common/common_getters.glsl"
#include "engine/utils/getters.fs"
#include "engine/common/common_xr.glsl"
#include "engine/surface/surface_inputs.fs"
#include "engine/shading/shading_parameters.fs"
#include "engine/effects/toneMapping.glsl"
#include "engine/effects/fog.fs"
#include "engine/effects/hoverEffect.fs"
// Includes end

// User implement block begin
${userImplementBlock}
// User implement block end

#include "engine/shading/shading_includes.fs"
#include "engine/surface/surface_custom_function.fs"
#include "engine/surface/surface_debug.fs"

#if __VERSION__ == 100
vec4 fragColor;
#else
layout(location = 0) out vec4 fragColor;
#endif

void blendPostLightingColor(const MaterialInputs material, inout vec4 color) {
#if defined(POST_LIGHTING_BLEND_MODE_OPAQUE)
    color = material.postLightingColor;
#elif defined(POST_LIGHTING_BLEND_MODE_TRANSPARENT)
    color = material.postLightingColor + color * (1.0 - material.postLightingColor.a);
#elif defined(POST_LIGHTING_BLEND_MODE_ADD)
    color += material.postLightingColor;
#elif defined(POST_LIGHTING_BLEND_MODE_MULTIPLY)
    color *= material.postLightingColor;
#endif
}

void applyAlphaMask(inout vec4 baseColor) {
      if (BLEND_MODE_MASKED) {
            baseColor.a = (baseColor.a - materialParams._maskThreshold) / max(fwidth(baseColor.a), 1e-3) + 0.5;
      }

#if !defined(FORCE_NO_DISCARD)
      if (BLEND_MODE_MASKED || ALPHA_TEST_ON) {
            if (baseColor.a <= materialParams._maskThreshold) {
                  discard;
            }
      }
#endif
}

void allpyOpacityEffect(inout vec4 baseColor){
    baseColor.a *= getOpacity();
}

float filament_lodBias;
void surface_main() {
    filament_lodBias = frameUniforms.lodBias;

    initObjectUniforms(object_uniforms);

    // See shading_parameters.fs
    // Computes global variables we need to evaluate material and lighting
    computeShadingParams();

    float clipDistance = MEDIUMP_FLT_MAX;
    if(!WORLD_ENTITY_PASS){
        crossingEntityClip(object_uniforms.portalPosition, object_uniforms.portalNormal);
    }
    if(PORTAL_CLIPPING_PASS){
        clippingEntityClip(object_uniforms.portalPosition, object_uniforms.portalNormal);
    }
    if (SPATIAL_PIPELINE) {
        clipDistance = spatialSpaceFalloffClip(getUserWorldPosition());
    }

    // Initialize the inputs to sensible default values, see material_inputs.fs
    MaterialInputs inputs;
    initMaterial(inputs);



    if(SPATIAL_PIPELINE){
        // for spatial space app
        beforeMaterial(inputs);
    }

    // Invoke user code
    material(inputs);

#if !defined(SHADING_MODEL_UNLIT)
    inputs.baseColor = clamp(inputs.baseColor, 0.0, 1.0);
    inputs.sheenColor = clamp(inputs.sheenColor, 0.0, 1.0);
#endif

    if(SPATIAL_PIPELINE){
        afterMaterial(inputs);
    }
    applyAlphaMask(inputs.baseColor);

    allpyOpacityEffect(inputs.baseColor);

if (DEBUG_VIEW_ON){
    fragColor = debugMaterial(inputs);
    #if __VERSION__ == 100
        gl_FragData[0] = fragColor;
    #endif
    return;
} else {
    fragColor = evaluateMaterial(inputs);
}

#if !defined(MATERIAL_HAS_REFLECTIONS)
    //if (MATERIAL_HAS_POST_LIGHTING_COLOR) {
    //    blendPostLightingColor(inputs, fragColor);
    //}
#endif

    //if (GLOBAL_HAS_FOG && HAS_FOG) { ffr + constant crash, open this when fixed it
//     if (false) {
//         highp vec3 view = getWorldPosition() - getWorldCameraPosition();
//         view = frameUniforms.fogFromWorldMatrix * view;
//         fragColor = fog(fragColor, view);
//     }

    if(SPATIAL_PIPELINE){
        afterShading(fragColor);
    }

    fragColor.rgb = toneMapping(fragColor.rgb);

    if(SPATIAL_SPACE_CLIP) {
        float clampedAlpha = clamp(clipDistance, 0, 1);
        fragColor.a *= clampedAlpha;
    }

    if (USE_IMMERSIVE_TEX) {
        fragColor.rgba = mixImmersiveAlpha(fragColor.rgba);
    }

    if (HOVER_ACTIVE) {
        fragColor = blendHoverEffectForInteractor1(perViewOverrideUniforms.leftHandHoverRadius,perViewOverrideUniforms.leftHandHoverInnerOffset,perViewOverrideUniforms.leftHandHoverPosition,perViewOverrideUniforms.leftHandHoverHighlightCenter,perViewOverrideUniforms.leftHandSelectIsEnter,perViewOverrideUniforms.leftHandHoverDistanceScale,perViewOverrideUniforms.leftHandHoverIntensity,fragColor,perViewOverrideUniforms.leftHandHoverColor);
        fragColor = blendHoverEffectForInteractor1(perViewOverrideUniforms.rightHandHoverRadius,perViewOverrideUniforms.rightHandHoverInnerOffset,perViewOverrideUniforms.rightHandHoverPosition,perViewOverrideUniforms.rightHandHoverHighlightCenter,perViewOverrideUniforms.rightHandSelectIsEnter,perViewOverrideUniforms.rightHandHoverDistanceScale,perViewOverrideUniforms.rightHandHoverIntensity,fragColor,perViewOverrideUniforms.rightHandHoverColor);
        fragColor = blendHoverEffectForInteractor(perViewOverrideUniforms.gazeHoverRadius,perViewOverrideUniforms.gazeHoverInnerOffset,perViewOverrideUniforms.gazeHoverPosition,perViewOverrideUniforms.gazeHoverDistanceScale,perViewOverrideUniforms.gazeHoverIntensity,fragColor,perViewOverrideUniforms.gazeHoverColor);
    }

#if __VERSION__ == 100
    gl_FragData[0] = fragColor;
#endif
}



void main() {
    surface_main();
}

#endif
