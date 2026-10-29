#ifndef _ENGINE_PIPELINE_LAYOUT_GLOBAL_SAMPLERS_GLSL
#define _ENGINE_PIPELINE_LAYOUT_GLOBAL_SAMPLERS_GLSL

// ========================================================
// = kDSetSampledImage

[[perview, global]]
layout(binding = 0, set = kDSetSampledImage) uniform mediump texture2DArray light_shadowMap;

[[perview, global]]
layout(binding = 1, set = kDSetSampledImage) uniform mediump texture2D light_iblDFG;

[[perview, global]]
layout(binding = 2, set = kDSetSampledImage) uniform highp texture2DArray light_structure;

[[perview, global]]
layout(binding = 3, set = kDSetSampledImage) uniform mediump texture2DArray light_featheringMap;

[[perview, global]]
layout(binding = 4, set = kDSetSampledImage) uniform mediump texture2D light_immersiveTexture;

[[perview, global]]
layout(binding = 5, set = kDSetSampledImage) uniform mediump texture2DArray light_immersiveTextureArray;

[[builtin, perview, global]]
layout(binding = 6, set = kDSetSampledImage) uniform lowp textureCube light_globalIblSpecular;

[[builtin, perview, global]]
layout(binding = 7, set = kDSetSampledImage) uniform lowp textureCube light_globalIblSpecularTwo;

[[builtin, perview, global]]
layout(binding = 8, set = kDSetSampledImage) uniform lowp textureCube light_vstIblSpecular;

[[builtin, perview, global]]
layout(binding = 9, set = kDSetSampledImage) uniform lowp textureCube light_vstIblSpecularTwo;

[[perview, global]]
layout(binding = 10, set = kDSetSampledImage) uniform mediump texture2D light_groundingShadowMap;

[[perview, global]]
layout(binding = 11, set = kDSetSampledImage) uniform mediump texture2DArray light_worldColorTexture;

[[perview, global]]
layout(binding = 12, set = kDSetSampledImage) uniform mediump texture2DArray light_worldDepthTexture;

[[perview, global]]
layout(binding = 13, set = kDSetSampledImage) uniform mediump texture2DArray light_portalDepthTexture;

[[perview, global]]
layout(binding = 14, set = kDSetSampledImage) uniform mediump texture2DArray light_groundGlassMap;

[[perview, global]]
layout(binding = 15, set = kDSetSampledImage) uniform mediump texture2D light_vstMap;

[[builtin, perview, global]]
layout(binding = 16, set = kDSetSampledImage) uniform lowp textureCube light_systemIblSpecular;

[[builtin, perviewOverride, global, colorSpace(kSrgb)]]
layout(binding = 17, set = kDSetSampledImage) uniform lowp textureCube perViewOverride_localIblSpecular;

[[builtin, perviewOverride, global, colorSpace(kSrgb)]]
layout(binding = 18, set = kDSetSampledImage) uniform lowp textureCube perViewOverride_localIblSpecularTwo;

[[builtin, perviewOverride, global, colorSpace(kSrgb)]]
layout(binding = 19, set = kDSetSampledImage) uniform mediump texture3D perViewOverride_toneMappingLut;

[[builtin, perviewOverride, global, colorSpace(kSrgb)]]
layout(binding = 20, set = kDSetSampledImage) uniform mediump texture2D perViewOverride_userLut;

[[builtin, morph, global]]
layout(binding = 21, set = kDSetSampledImage) uniform highp texture2DArray morphTargetBuffer_positions;

[[builtin, morph, global]]
layout(binding = 22, set = kDSetSampledImage) uniform highp itexture2DArray morphTargetBuffer_tangents;

// -----------------------------------------------------------------------
// kDSetStorageImage

[[builtin, perviewOverride, global]]
layout(binding = 0, set = kDSetStorageImage, rg8i) uniform mediump iimage2DArray perViewStorageImage_padding;

[[builtin, perviewOverride, global]]
layout(binding = 1, set = kDSetStorageImage, rg8i) uniform mediump iimage2DArray perViewStorageImage_portalColorTexture;

#endif

