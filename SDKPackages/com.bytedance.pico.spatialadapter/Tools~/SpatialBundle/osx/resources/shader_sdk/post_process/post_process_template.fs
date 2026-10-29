#ifndef _ENGINE_POST_PROCESS_TEMPLATE_FS
#define _ENGINE_POST_PROCESS_TEMPLATE_FS

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
#include "engine/common/common_descriptor_set.glsl"
#include "engine/common/common_samplers.glsl"
#include "engine/common/common_types.glsl"
#include "engine/common/common_uniforms.glsl"
#include "engine/common/common_defines.glsl"
#include "engine/common/common_math.glsl"
#include "engine/common/common_colorspace.glsl"
#include "engine/common/common_shading.fs"
#include "engine/common/common_graphics.fs"
#include "engine/common/common_getters.glsl"
#include "engine/post_process/post_process_inputs.fs"
// Includes end

// User implement block begin
${userImplementBlock}
// User implement block end

void main() {
    PostProcessInputs inputs;

    // Invoke user code
    postProcess(inputs);

#if defined(FRAG_OUTPUT0)
    FRAG_OUTPUT_AT0 FRAG_OUTPUT_SWIZZLE0 = inputs.FRAG_OUTPUT0;
#endif
#if defined(FRAG_OUTPUT1)
    FRAG_OUTPUT_AT1 FRAG_OUTPUT_SWIZZLE1 = inputs.FRAG_OUTPUT1;
#endif
#if defined(FRAG_OUTPUT2)
    FRAG_OUTPUT_AT2 FRAG_OUTPUT_SWIZZLE2 = inputs.FRAG_OUTPUT2;
#endif
#if defined(FRAG_OUTPUT3)
    FRAG_OUTPUT_AT3 FRAG_OUTPUT_SWIZZLE3 = inputs.FRAG_OUTPUT3;
#endif
#if defined(FRAG_OUTPUT4)
    FRAG_OUTPUT_AT4 FRAG_OUTPUT_SWIZZLE4 = inputs.FRAG_OUTPUT4;
#endif
#if defined(FRAG_OUTPUT5)
    FRAG_OUTPUT_AT5 FRAG_OUTPUT_SWIZZLE5 = inputs.FRAG_OUTPUT5;
#endif
#if defined(FRAG_OUTPUT6)
    FRAG_OUTPUT_AT6 FRAG_OUTPUT_SWIZZLE6 = inputs.FRAG_OUTPUT6;
#endif
#if defined(FRAG_OUTPUT7)
    FRAG_OUTPUT_AT7 FRAG_OUTPUT_SWIZZLE7 = inputs.FRAG_OUTPUT7;
#endif
#if defined(FRAG_OUTPUT_DEPTH)
    gl_FragDepth = inputs.depth;
#endif
}

#endif

