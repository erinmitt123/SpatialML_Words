#ifndef _ENGINE_SPATIAL_COMMON_GLSL
#define _ENGINE_SPATIAL_COMMON_GLSL

// spatial shader common includes

// TODO: find a better way to customize instancing function
// matc should compile error without this. if you want to use custom matrix as MVP
// you need to remove this and implement 'void initObjectUniforms(out PerRenderableData p)'
// here instead of the default version.
#include "engine/common/common_types.glsl"
#include "engine/common/common_uniforms.glsl"
#include "engine/surface/surface_uniforms.glsl"
#include "engine/common/common_defines.glsl"
#include "engine/common/common_getters.glsl"
#include "engine/common/common_instancing.glsl"
#include "engine/common/common_math.glsl"
#include "engine/common/common_colorspace.glsl"
#include "engine/common/common_attribute_keywords.glsl"
#include "engine/common/common_descriptor_set.glsl"
#include "engine/common/common_samplers.glsl"
#include "engine/surface/surface_samplers.glsl"
#include "engine/effects/toneMapping.glsl"
#include "engine/common/common_shadowing.glsl"
#include "engine/common/common_getters.glsl"

#endif
