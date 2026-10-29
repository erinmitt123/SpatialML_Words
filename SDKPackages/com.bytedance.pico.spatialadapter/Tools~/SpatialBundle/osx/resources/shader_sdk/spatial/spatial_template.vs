#ifndef _ENGINE_SPATIAL_MAIN_VS
#define _ENGINE_SPATIAL_MAIN_VS

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

// User implement block begin
${userImplementBlock}
// User implement block end

// main function in vertex shader
void main() {
    #if defined(TARGET_METAL_ENVIRONMENT) || defined(TARGET_VULKAN_ENVIRONMENT)
        instance_index = gl_InstanceIndex;
    #else
        instance_index = gl_InstanceID;
    #endif

    initObjectUniforms(object_uniforms);

    appdata i;
    v2f o;
    initVertexInput(i);
    o = vert(i);
    applyVertexOutput(o);

#if defined(TARGET_VULKAN_ENVIRONMENT)
    // In Vulkan, clip space is Y-down. In OpenGL and Metal, clip space is Y-up.
    gl_Position.y = -gl_Position.y;
#endif

#if !defined(TARGET_VULKAN_ENVIRONMENT) && !defined(TARGET_METAL_ENVIRONMENT)
    // This is not needed in Vulkan or Metal because clipControl is always (1, 0)
    gl_Position.z = dot(gl_Position.zw, frameUniforms.clipControl);
#endif
}

#endif
