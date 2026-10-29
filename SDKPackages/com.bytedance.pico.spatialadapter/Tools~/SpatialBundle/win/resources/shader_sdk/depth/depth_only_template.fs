#ifndef _ENGINE_DEPTH_ONLY_FS
#define _ENGINE_DEPTH_ONLY_FS

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

// instancing 放在 UBO 与 varyings 定义之后
#include "engine/common/common_instancing.glsl"
#include "engine/common/common_shadowing.glsl"
#include "engine/common/common_shading.fs"
#include "engine/common/common_graphics.fs"
#include "engine/common/common_material.fs"
#include "engine/common/common_getters.glsl"
#include "engine/utils/getters.fs"
#include "engine/common/common_xr.glsl"
#include "engine/surface/surface_inputs.fs"
// 注意：shading_parameters.fs 必须在所有 varyings 与 UBO 定义之后包含，
// 否则 computeShadingParams 中的依赖（如 vertex_worldPosition、object_uniforms、instance_index）将未声明或类型不匹配
#include "engine/shading/shading_parameters.fs"
#include "engine/surface/surface_custom_function.fs"
// Includes end

// User implement block begin
${userImplementBlock}
// User implement block end

layout(location = 1) out highp vec2 outPicking;

float filament_lodBias;

// Apply alpha mask and alpha-test semantics consistent with color passes.
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

void main() {
    filament_lodBias = frameUniforms.lodBias;

    initObjectUniforms(object_uniforms);

    computeShadingParams();
    if (SPATIAL_SPACE_CLIP) {
        // shading_position depends on computeShadingParams; getUserWorldPosition is safe
        spatialSpaceClip(getUserWorldPosition());
    }

    if (needCustomDepth()) {
        MaterialInputs inputs;
        initMaterial(inputs);

        beforeMaterial(inputs);

        material(inputs);

        afterMaterial(inputs);
        float alpha = inputs.baseColor.a;
        applyAlphaMask(inputs.baseColor);
        float4 color = vec4(0.0,0.0,0.0,0.0);
        afterShading(color);
        #if defined(MATERIAL_HAS_TRANSPARENT_SHADOW)
            #if !defined(FORCE_NO_DISCARD)
            // Interleaved gradient noise, see dithering.fs
            float noise = interleavedGradientNoise(gl_FragCoord.xy);
            if (noise >= alpha) {
                discard;
            }
            #endif
        #endif
    }

    if (VARIANT_HAS_PICKING){
        outPicking.x = float(object_uniforms.objectId);
        outPicking.y = vertex_position.z / vertex_position.w;
    }
}

#endif
