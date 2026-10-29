#ifndef _ENGINE_SURFACE_MAIN_VS
#define _ENGINE_SURFACE_MAIN_VS

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
#include "engine/common/common_getters.glsl"
#include "engine/utils/getters.vs"
#include "engine/surface/surface_inputs.vs"
#include "engine/surface/surface_custom_function.vs"
// Includes end

// User implement block begin
${userImplementBlock}
// User implement block end

/*
 * This is the main vertex shader of surface materials. It can be invoked with
 * USE_OPTIMIZED_DEPTH_VERTEX_SHADER defined, and in this case we are guaranteed that the
 * DEPTH variant is active *AND* there is no custom vertex shader (i.e.: materialVertex() is
 * empty).
 * We can use this to remove all code that doesn't participate in the depth computation.
 */

void main() {
    #if defined(TARGET_METAL_ENVIRONMENT) || defined(TARGET_VULKAN_ENVIRONMENT)
        instance_index = gl_InstanceIndex;
    #else
        instance_index = gl_InstanceID;
    #endif

    initObjectUniforms(object_uniforms);

    // Initialize the inputs to sensible default values, see material_inputs.vs
#if defined(USE_OPTIMIZED_DEPTH_VERTEX_SHADER)

    // In USE_OPTIMIZED_DEPTH_VERTEX_SHADER mode, we can even skip this if we're already in
#if !defined(VERTEX_DOMAIN_DEVICE)
    // Run initMaterialVertex to compute material.worldPosition.
    MaterialVertexInputs material;
    initMaterialVertex(material);
    // materialVertex() is guaranteed to be empty here, but we keep it to workaround some problem
    // in NVIDA drivers related to depth invariance.
    materialVertex(material);
#endif

#else // defined(USE_OPTIMIZED_DEPTH_VERTEX_SHADER)

    MaterialVertexInputs material;
    initMaterialVertex(material);

#if defined(HAS_ATTRIBUTE_NORMAL)
    // If the material defines a value for the "normal" property, we need to output
    // the full orthonormal basis to apply normal mapping
    #if defined(MATERIAL_NEEDS_TBN)
        material.worldNormal = mesh_normal.xyz;
        vertex_worldTangent = mesh_tangent;

        if (VARIANT_HAS_SKINNING_OR_MORPHING) {
            if ((object_uniforms.flags & FILAMENT_OBJECT_MORPHING_ENABLED_BIT) != 0) {
                morphNormal(material.worldNormal);
                material.worldNormal = normalize(material.worldNormal);
            }

            if ((object_uniforms.flags & FILAMENT_OBJECT_SKINNING_ENABLED_BIT) != 0) {
                skinNormal(material.worldNormal, mesh_bone_indices, mesh_bone_weights);
                skinNormal(vertex_worldTangent.xyz, mesh_bone_indices, mesh_bone_weights);
            }
        }

        // We don't need to normalize here, even if there's a scale in the matrix
        // because we ensure the worldFromModelNormalMatrix pre-scales the normal such that
        // all its components are < 1.0. This prevents the bitangent to exceed the range of fp16
        // in the fragment shader, where we renormalize after interpolation
        vertex_worldTangent.xyz = transformObjectToWorldNormal(vertex_worldTangent.xyz);
        material.worldNormal = transformObjectToWorldNormal(material.worldNormal);
    #else
        material.worldNormal = mesh_normal.xyz;

        if (VARIANT_HAS_SKINNING_OR_MORPHING) {
            if ((object_uniforms.flags & FILAMENT_OBJECT_MORPHING_ENABLED_BIT) != 0) {
                morphNormal(material.worldNormal);
                material.worldNormal = normalize(material.worldNormal);
            }

            if ((object_uniforms.flags & FILAMENT_OBJECT_SKINNING_ENABLED_BIT) != 0) {
                skinNormal(material.worldNormal, mesh_bone_indices, mesh_bone_weights);
            }
        }

        material.worldNormal = transformObjectToWorldNormal(material.worldNormal);

    #endif // MATERIAL_HAS_ANISOTROPY || MATERIAL_HAS_NORMAL || MATERIAL_HAS_CLEAR_COAT_NORMAL
#endif // HAS_ATTRIBUTE_NORMAL

    beforeMaterialVertex(material);

    // Invoke user code
    materialVertex(material);

    afterMaterialVertex(material);

    // Handle built-in interpolated attributes
#if defined(HAS_ATTRIBUTE_COLOR)
    vertex_color = material.color;
#endif
#if defined(HAS_ATTRIBUTE_UV0)
    vertex_uv01.xy = material.uv0;
#endif
#if defined(HAS_ATTRIBUTE_UV1)
    vertex_uv01.zw = material.uv1;
#endif

    // Handle user-defined interpolated attributes
#if defined(VARIABLE_CUSTOM0)
    VARIABLE_CUSTOM_AT0 = material.VARIABLE_CUSTOM0;
#endif
#if defined(VARIABLE_CUSTOM1)
    VARIABLE_CUSTOM_AT1 = material.VARIABLE_CUSTOM1;
#endif
#if defined(VARIABLE_CUSTOM2)
    VARIABLE_CUSTOM_AT2 = material.VARIABLE_CUSTOM2;
#endif
#if defined(VARIABLE_CUSTOM3)
    VARIABLE_CUSTOM_AT3 = material.VARIABLE_CUSTOM3;
#endif

    // The world position can be changed by the user in materialVertex()
    vertex_worldPosition.xyz = material.worldPosition.xyz;

#ifdef HAS_ATTRIBUTE_NORMAL
    vertex_worldNormal = material.worldNormal;
#endif

#endif // !defined(USE_OPTIMIZED_DEPTH_VERTEX_SHADER)


#if defined(VERTEX_DOMAIN_DEVICE)
    // The other vertex domains are handled in initMaterialVertex()->computeWorldPosition()
    gl_Position = getPosition();

#if !defined(USE_OPTIMIZED_DEPTH_VERTEX_SHADER)
if (MATERIAL_HAS_CLIP_SPACE_TRANSFORM) {
    gl_Position = getMaterialClipSpaceTransform(material) * gl_Position;
}
#endif // !USE_OPTIMIZED_DEPTH_VERTEX_SHADER

#if defined(MATERIAL_HAS_VERTEX_DOMAIN_DEVICE_JITTERED)
    // Apply the clip-space transform which is normally part of the projection
    gl_Position.xy = gl_Position.xy * frameUniforms.clipTransform.xy + (gl_Position.w * frameUniforms.clipTransform.zw);
#endif
#else
    gl_Position = getWorldToClipMatrix() * getWorldPosition(material);
#endif

#if defined(VERTEX_DOMAIN_DEVICE)
    // GL convention to inverted DX convention (must happen after clipSpaceTransform)
    gl_Position.z = gl_Position.z * -0.5 + 0.5;
#endif

    // this must happen before we compensate for vulkan below
    vertex_position = gl_Position;

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
