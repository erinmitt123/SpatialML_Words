#ifndef _ENGINE_PIPELINE_LAYOUT_GLOBAL_UNIFROMS_GLSL
#define _ENGINE_PIPELINE_LAYOUT_GLOBAL_UNIFROMS_GLSL


const int CONFIG_MAX_PUNCTUAL_LIGHT_COUNT = 256;
layout(binding = 2, std140, set = 1) uniform PunctualLightsUniforms {
    PerPunctualLightData lights[CONFIG_MAX_PUNCTUAL_LIGHT_COUNT];
} punctualLightsUniforms;

layout(binding = 3, std140, set = 1) uniform ShadowUniforms {
    ShadowData shadows[64];
} shadowUniforms;

layout(binding = 4, std140, set = 1) uniform FroxelRecordUniforms {
    uvec4 records[1024];
} froxelRecordUniforms;

const int CONFIG_FROXEL_BUFFER_HEIGHT = 1024;
layout(binding = 5, std140, set = 1) uniform FroxelsUniforms {
    uvec4 records[CONFIG_FROXEL_BUFFER_HEIGHT];
} froxelsUniforms;

const int CONFIG_MAX_AUTO_INSTANCES = 256;
layout(binding = 6, std140, set = 1) uniform ObjectUniforms {
    PerRenderableData data[CONFIG_MAX_AUTO_INSTANCES];
} objectUniforms;

#if defined(MATERIAL_STAGE_VERTEX)
layout(binding = 7, std140, set = 1) uniform BonesUniforms {
    BoneData bones[512];
} bonesUniforms;

layout(binding = 8, std140, set = 1) uniform MorphingUniforms {
    vec4 weights[256];
} morphingUniforms;
#endif

const int CONFIG_MAX_INSTANCES = 512;
layout(binding = 9, std140, set = 1) uniform InstancesUniforms {
    InstanceData instances[CONFIG_MAX_INSTANCES];
} instancesUniforms;

// binding = 10: material instance uniforms

layout(binding = 11, std140, set = 1) uniform PerViewOverrideUniforms {
    mat3 iblRotation[[builtin]];
    mediump vec2 iblRoughnessOneLevel[[default(vec2(1, 1)), builtin]];
    float iblIntensity[[builtin]];
    mediump float iblBlendingProportion[[default(0.5f), builtin]];
    vec3 reserved0[[builtin]];
    mediump float iblLocalGlobalBlendingProportion[[default(0.5f), builtin]];
    vec4 iblSH[9][[default(vec4(65504.0f, 65504.0f, 65504.0f, 65504.0f)), builtin]];
    mat3 iblToWorkingColorSpaceMatrixOne[[builtin]];
    mat3 iblToWorkingColorSpaceMatrixTwo[[builtin]];
    vec3 leftHandHoverPosition[[builtin]];
    float leftHandHoverIntensity[[builtin]];
    float leftHandHoverDistanceScale[[builtin]];
    float leftHandHoverRadius[[builtin]];
    float leftHandHoverInnerOffset[[builtin]];
    float reserved1[[builtin]];
    vec4 leftHandHoverColor[[builtin]];
    vec3 rightHandHoverPosition[[builtin]];
    float rightHandHoverIntensity[[builtin]];
    float rightHandHoverDistanceScale[[builtin]];
    float rightHandHoverRadius[[builtin]];
    float rightHandHoverInnerOffset[[builtin]];
    float reserved2[[builtin]];
    vec4 rightHandHoverColor[[builtin]];
    vec3 gazeHoverPosition[[builtin]];
    float gazeHoverIntensity[[builtin]];
    float gazeHoverDistanceScale[[builtin]];
    float gazeHoverRadius[[builtin]];
    float gazeHoverInnerOffset[[builtin]];
    float reserved3[[builtin]];
    vec4 gazeHoverColor[[builtin]];
    vec3 reserved4[[builtin]];
    highp uint renderDebugMode[[builtin]];
    vec3 leftHandHoverHighlightCenter[[builtin]];
    bool leftHandSelectIsEnter[[builtin]];
    vec3 rightHandHoverHighlightCenter[[builtin]];
    bool rightHandSelectIsEnter[[builtin]];
} perViewOverrideUniforms;

// binding = 12: custom buffer 0, do NOT override this binding

layout(binding = 13, std140, set = 1) uniform RenderGroupUniforms {
    RenderGroupData renderGroups[64];
} renderGroupUniforms;

layout(binding = 14, std140, set = 1) uniform BoundaryUniforms {
    BoundaryData boundary[256];
} boundaryUniforms;

// binding = 15: custom buffer 1, do NOT override this binding

// binding = 16: custom buffer 2, do NOT override this binding

// binding = 17: custom buffer 3, do NOT override this binding

#endif