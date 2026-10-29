#ifndef _ENGINE_POST_PROCESS_TEMPLATE_VS
#define _ENGINE_POST_PROCESS_TEMPLATE_VS

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

// Attributes
${attributes}

// Includes begin
#include "engine/common/common_types.glsl"
#include "engine/common/common_uniforms.glsl"
#include "engine/common/common_defines.glsl"
#include "engine/common/common_math.glsl"
#include "engine/common/common_colorspace.glsl"
#include "engine/common/common_getters.glsl"
#include "engine/post_process/post_process_inputs.vs"
#include "engine/post_process/post_process_getters.vs"
// Includes end

// User implement block begin
${userImplementBlock}
// User implement block end

void main() {
    // Initialize the vertex shader inputs to sensible default values.
    PostProcessVertexInputs inputs;
    initPostProcessMaterialVertex(inputs);

    inputs.normalizedUV = position.xy * 0.5 + 0.5;

    gl_Position = getPosition();
    // GL convention to inverted DX convention
    gl_Position.z = gl_Position.z * -0.5 + 0.5;

    // Adjust clip-space
#if !defined(TARGET_VULKAN_ENVIRONMENT) && !defined(TARGET_METAL_ENVIRONMENT)
    // This is not needed in Vulkan or Metal because clipControl is always (1, 0)
    gl_Position.z = dot(gl_Position.zw, frameUniforms.clipControl);
#endif

    // Invoke user code
    postProcessVertex(inputs);

    // Handle user-defined interpolated attributes
#if defined(VARIABLE_CUSTOM0)
    VARIABLE_CUSTOM_AT0 = inputs.VARIABLE_CUSTOM0;
#endif
#if defined(VARIABLE_CUSTOM1)
    VARIABLE_CUSTOM_AT1 = inputs.VARIABLE_CUSTOM1;
#endif
#if defined(VARIABLE_CUSTOM2)
    VARIABLE_CUSTOM_AT2 = inputs.VARIABLE_CUSTOM2;
#endif
#if defined(VARIABLE_CUSTOM3)
    VARIABLE_CUSTOM_AT3 = inputs.VARIABLE_CUSTOM3;
#endif
}

#endif
