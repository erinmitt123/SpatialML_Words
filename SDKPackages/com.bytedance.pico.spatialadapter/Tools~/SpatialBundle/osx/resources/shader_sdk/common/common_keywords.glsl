#ifndef _ENGINE_PIPELINE_LAYOUT_GLOBAL_KEYWORDS_GLSL
#define _ENGINE_PIPELINE_LAYOUT_GLOBAL_KEYWORDS_GLSL

layout(auto_constant_id, domain = kDomainSurface | kDomainSpatial, stage = kFragment)
const bool BLEND_MODE_OPAQUE[[ default ]], BLEND_MODE_TRANSPARENT, BLEND_MODE_MASKED, BLEND_MODE_FADE, BLEND_MODE_PREMULTIPLY;

layout(auto_constant_id, domain = kDomainSurface | kDomainSpatial, stage = kVertex | kFragment)
const bool MATERIAL_PERFORMANCE_LEVEL_0 [[ default ]], MATERIAL_PERFORMANCE_LEVEL_1;

layout(auto_constant_id, domain = kDomainSurface | kDomainSpatial, stage = kFragment)
const bool SHADER_LOD_0 [[ default ]], SHADER_LOD_1;

// depth-only pass related
layout(auto_constant_id, domain = kDomainSurface | kDomainSpatial, stage = kFragment)
const bool VARIANT_HAS_PICKING;

//======================================================================================================================
// use for replace builtin variant
layout(auto_constant_id)
const bool VARIANT_HAS_SKINNING_OR_MORPHING;

layout(auto_constant_id)
const bool POST_PROCESS_OPAQUE;

//======================================================================================================================

//Properties

layout(auto_constant_id, domain = kDomainSurface | kDomainSpatial, stage = kFragment)
const bool MATERIAL_HAS_NORMAL [[ default ]];

layout(auto_constant_id, domain = kDomainSurface | kDomainSpatial, stage = kFragment)
const bool MATERIAL_HAS_EMISSIVE [[ default ]];

layout(auto_constant_id, domain = kDomainSurface | kDomainSpatial, stage = kFragment)
const bool MATERIAL_HAS_AMBIENT_OCCLUSION;

layout(auto_constant_id, domain = kDomainSurface | kDomainSpatial, stage = kFragment)
const bool MATERIAL_HAS_CLEAR_COAT;

layout(auto_constant_id, domain = kDomainSurface | kDomainSpatial, stage = kFragment)
const bool MATERIAL_HAS_CLEAR_COAT_NORMAL;

layout(auto_constant_id, domain = kDomainSurface | kDomainSpatial, stage = kFragment)
const bool MATERIAL_HAS_ANISOTROPY;

layout(auto_constant_id, domain = kDomainSurface | kDomainSpatial, stage = kFragment)
const bool MATERIAL_HAS_SUBSURFACE_COLOR;

layout(auto_constant_id, domain = kDomainSurface | kDomainSpatial, stage = kFragment)
const bool MATERIAL_HAS_SHEEN_COLOR;

layout(auto_constant_id, domain = kDomainSurface | kDomainSpatial, stage = kFragment)
const bool MATERIAL_HAS_REFLECTANCE;

layout(auto_constant_id, domain = kDomainSurface | kDomainSpatial, stage = kVertex)
const bool MATERIAL_HAS_CLIP_SPACE_TRANSFORM;

//======================================================================================================================

layout(auto_constant_id, domain = kDomainSurface | kDomainSpatial, stage = kFragment)
const bool ALPHA_TEST_ON;

layout(auto_constant_id, domain = kDomainSurface | kDomainSpatial, stage = kVertex | kFragment)
const bool INSTANCING_ON;

layout(auto_constant_id, domain = kDomainSurface | kDomainSpatial, stage = kFragment)
const bool USE_SIMPLE_LIT;

layout(auto_constant_id, domain = kDomainSurface | kDomainSpatial, stage = kFragment)
const bool USE_IMMERSIVE_TEX;

//======================================================================================================================

layout(auto_constant_id, domain = kDomainSurface | kDomainSpatial, stage = kFragment)
const bool SPATIAL_PIPELINE;

layout(auto_constant_id, domain = kDomainSurface | kDomainSpatial, stage = kFragment)
const bool FEATHERING_ON, FEATHERING_SOLO, FEATHERING_DOUBLE;

layout(auto_constant_id, domain = kDomainSurface | kDomainSpatial, stage = kFragment)
const bool SPATIAL_SPACE_CLIP;

layout(auto_constant_id, domain = kDomainSurface | kDomainSpatial, stage = kFragment)
const bool PORTAL_CLIPPING_PASS, CROSSING_ENTITY;

layout(auto_constant_id, domain = kDomainSurface | kDomainSpatial, stage = kFragment)
const bool WORLD_ENTITY_PASS;

layout(auto_constant_id, domain = kDomainSurface | kDomainSpatial, stage = kVertex | kFragment)
const bool USE_FAST_ACCESS_UNIFORM;

//======================================================================================================================

// debug view
layout(auto_constant_id, domain = kDomainSurface | kDomainSpatial, stage = kVertex | kFragment)
const bool DEBUG_VIEW_ON;

//======================================================================================================================

// ibl
layout(auto_constant_id, domain = kDomainSurface | kDomainSpatial, stage = kFragment)
const bool LOCAL_IBL_BLEND;

layout(auto_constant_id, domain = kDomainSurface | kDomainSpatial, stage = kFragment)
const bool HAS_LOCAL_IBL;

layout(auto_constant_id, domain = kDomainSurface | kDomainSpatial, stage = kFragment)
const bool HAS_VALID_LOCAL_BLEND_PROPORTION;

//======================================================================================================================

// Shadowing
layout(auto_constant_id, domain = kDomainSurface | kDomainSpatial, stage = kFragment)
const bool POINT_LIGHT_SHADOW;

layout(auto_constant_id, domain = kDomainSurface | kDomainSpatial, stage = kFragment)
const bool HAS_SHADOWING;

layout(auto_constant_id, domain = kDomainSurface | kDomainSpatial, stage = kFragment)
const bool CAST_SOFT_GROUNDING_SHADOW;

layout(auto_constant_id, domain = kDomainSurface | kDomainSpatial, stage = kFragment)
const bool ENABLE_SOFT_GROUNDING_SHADOW, ENABLE_HARD_GROUNDING_SHADOW;

//======================================================================================================================

// Lighting
layout(auto_constant_id, domain = kDomainSurface | kDomainSpatial, stage = kFragment)
const bool GLOBAL_HAS_DIRECTIONAL_LIGHTING;

layout(auto_constant_id, domain = kDomainSurface | kDomainSpatial, stage = kFragment)
const bool GLOBAL_HAS_PUNCTUAL_LIGHTING;

//======================================================================================================================

// Interaction
layout(auto_constant_id, domain = kDomainSurface, stage = kVertex | kFragment)
const bool HOVER_ACTIVE;

#endif



