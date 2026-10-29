#ifndef _ENGINE_COMMON_DEFINES_GLSL
#define _ENGINE_COMMON_DEFINES_GLSL

#define bool2    bvec2
#define bool3    bvec3
#define bool4    bvec4

#define int2     ivec2
#define int3     ivec3
#define int4     ivec4

#define uint2    uvec2
#define uint3    uvec3
#define uint4    uvec4

#define float2   vec2
#define float3   vec3
#define float4   vec4

#define float3x3 mat3
#define float4x4 mat4

// To workaround an adreno crash (#5294), we need ensure that a method with
// parameter 'const mat4' does not call another method also with a 'const mat4'
// parameter (i.e. mulMat4x4Float3). So we remove the const modifier for
// materials compiled for vulkan+mobile.
#if defined(TARGET_VULKAN_ENVIRONMENT) && defined(TARGET_MOBILE)
   #define highp_mat4 highp mat4
#else
   #define highp_mat4 const highp mat4
#endif

#if defined(TARGET_MOBILE)
   #define half mediump float
   #define half2 mediump vec2
   #define half3 mediump vec3
   #define half4 mediump vec4
   #define half3x3 mediump mat3
   #define half4x4 mediump mat4
#else
   #define half highp float
   #define half2 highp vec2
   #define half3 highp vec3
   #define half4 highp vec4
   #define half3x3 highp mat3
   #define half4x4 highp mat4
#endif

//------------------------------------------------------------------------------
// static defines begin
//------------------------------------------------------------------------------
#if defined(HAS_ATTRIBUTE_NORMAL)
    #define MATERIAL_NEEDS_TBN true
#else
    #define MATERIAL_NEEDS_TBN false
#endif

//------------------------------------------------------------------------------
// static defines end
//------------------------------------------------------------------------------


//------------------------------------------------------------------------------
// variants begin
//------------------------------------------------------------------------------

//ibl
#define LOCAL_IBL_DIRECTION(R) (perViewOverrideUniforms.iblRotation * R)
#define GLOBAL_IBL_DIRECTION(R) (frameUniforms.iblRotation * R)

#define LOCAL_IBL_LUMINANCE (frameUniforms.exposure * perViewOverrideUniforms.iblIntensity)
#define GLOBAL_IBL_LUMINANCE (frameUniforms.exposure * frameUniforms.iblIntensity)

#define LOCAL_IBL_ONE_LEVEL_ONE (perViewOverrideUniforms.iblRoughnessOneLevel[0])
#define LOCAL_IBL_ONE_LEVEL_TWO (perViewOverrideUniforms.iblRoughnessOneLevel[1])
#define GLOBAL_IBL_ONE_LEVEL_ONE (frameUniforms.iblRoughnessOneLevel[0])
#define GLOBAL_IBL_ONE_LEVEL_TWO (frameUniforms.iblRoughnessOneLevel[1])
#define VST_IBL_ONE_LEVEL frameUniforms.vstRoughnessOneLevel
#define SYSTEM_IBL_LEVEL frameUniforms.systemRoughnessLevel

#define HAS_VALID_LOCAL_IBL (HAS_LOCAL_IBL)
#define HAS_LOCAL_IBL_ONLY (HAS_VALID_LOCAL_BLEND_PROPORTION && HAS_VALID_LOCAL_IBL)

#define MAX_CAST_SHADOW_SCALE 1.4f

//------------------------------------------------------------------------------
// variants end
//------------------------------------------------------------------------------


//------------------------------------------------------------------------------
//func
//------------------------------------------------------------------------------

//glsl es do not support ##, so need to write _ST and _R
#define TRANSFORM_TEX(uv, name) \
    (MATERIAL_PERFORMANCE_LEVEL_1 ? (uv) : \
    (MATERIAL_PERFORMANCE_LEVEL_0 ? ( mat3( \
        materialParams.name##_R.y * materialParams.name##_ST.x, -materialParams.name##_R.x * materialParams.name##_ST.y, 0.0, \
        materialParams.name##_R.x * materialParams.name##_ST.x,  materialParams.name##_R.y * materialParams.name##_ST.y, 0.0, \
        materialParams.name##_ST.z,                              materialParams.name##_ST.w,                             1.0) \
    * vec3(uv.xy, 1.0)).xy : (uv)))

#define SHARE_SAMPLER_2D(map) sampler2D(map, s_linear_clamp_sampler)
#define SHARE_SAMPLER_2D_POINT(map) sampler2D(map, s_point_clamp_sampler)
#define SHARE_SAMPLER_2D_LINEAR(map) sampler2D(map, s_linear_clamp_sampler)
#define SHARE_SAMPLER_CUBE(map) samplerCube(map, s_trilinear_clamp_sampler)
#define SHARE_SAMPLER_2D_ARRAY(map) sampler2DArray(map, s_linear_clamp_sampler)
#define SHARE_SAMPLER_2D_ARRAY_POINT(map) sampler2DArray(map, s_point_clamp_sampler)
#define SHARE_SAMPLER_2D_ARRAY_LINEAR(map) sampler2DArray(map, s_linear_clamp_sampler)
#define SHARE_SAMPLER_2D_ARRAY_SHADOW(map) sampler2DArrayShadow(map, s_linear_clamp_compare_ge_sampler)

#define TEXTURE(name, coord) texture(name, coord)
#define TEXEL_FETCH(name, coord, lod) texelFetch(name, coord, lod)
#define TEXEL_FETCH_OFFSET(name, coord, lod, offset) texelFetchOffset(name, coord, lod, offset)
#define TEXTURE_GATHER(name, coord, comp) textureGather(name, coord, comp)
#define TEXTURE_GATHER_OFFSET(name, coord, comp, offset) textureGatherOffset(name, coord, comp, offset)
#define TEXTURE_GATHER_OFFSETS(name, coord, comp, offsets) textureGatherOffsets(name, coord, comp, offsets)
#define TEXTURE_GRAD(name, coord, dPdx, dPdy) textureGrad(name, coord, dPdx, dPdy)
#define TEXTURE_GRAD_OFFSET(name, coord, dPdx, dPdy, offset) textureGradOffset(name, coord, dPdx, dPdy, offset)
#define TEXTURE_LOD(name, coord, lod) textureLod(name, coord, lod)
#define TEXTURE_LOD_OFFSET(name, coord, lod, offset) textureLodOffset(name, coord, lod, offset)
#define TEXTURE_OFFSET(name, coord, offset) textureOffset(name, coord, offset)
#define TEXTURE_PROJ(name, coord) textureProj(name, coord)
#define TEXTURE_PROJ_GRAD(name, coord, dPdx, dPdy) textureProjGrad(name, coord, dPdx, dPdy)
#define TEXTURE_PROJ_GRAD_OFFSET(name, coord, dPdx, dPdy, offset) textureProjGradOffset(name, coord, dPdx, dPdy, offset)
#define TEXTURE_PROJ_LOD(name, coord, lod) textureProjLod(name, coord, lod)
#define TEXTURE_PROJ_LOD_OFFSET(name, coord, lod, offset) textureProjLodOffset(name, coord, lod, offset)
#define TEXTURE_PROJ_OFFSET(name, coord, offset) textureProjOffset(name, coord, offset)

//------------------------------------------------------------------------------
//func end
//------------------------------------------------------------------------------

#endif
