#ifndef _ENGINE_COMMON_TYPES_GLSL
#define _ENGINE_COMMON_TYPES_GLSL

struct PerViewData {
    mat4 viewFromWorldMatrix[4];
    mat4 worldFromViewMatrix[4];
    mat4 clipFromViewMatrix[4];
    mat4 viewFromClipMatrix[4];
    mat4 clipFromWorldMatrix[4];
    mat4 worldFromClipMatrix[4];
    mat4 centerClipFromWorldMatrix;
    mat4 clipFromWorldMatrixVirtual;
    mat4 userWorldFromWorldMatrix;
    vec4 clipTransform;
    vec2 clipControl;
    float time;
    float temporalNoise;
    vec4 userTime;
    vec4 resolution;
    vec2 logicalViewportScale;
    vec2 logicalViewportOffset;
    mediump float lodBias;
    mediump float userLutIntensity;
    float oneOverFarMinusNear;
    float nearOverFarMinusNear;
    float cameraFar;
    float exposure;
    mediump float ev100;
    mediump float needsAlphaChannel;
    mediump vec2 toneMappingLutSize;
    mediump vec2 froxelCountXY;
    mediump vec4 zParams;
    lowp uvec3 fParams;
    int virtualEnabled;
    mediump vec2 lightFarAttenuationParams;
    mediump vec2 userLutSize;
    mediump vec3 sunLightDirection;
    lowp int directionalLightCount;
    mediump vec4 sunLightColorIntensity;
    mediump vec4 sun;
    vec4 fogDensity[2];
    mediump vec3 fogColor;
    float fogStart;
    float fogMaxOpacity;
    uint fogMinMaxMip;
    float fogHeightFalloff;
    float fogCutOffDistance;
    mediump float fogColorFromIbl;
    float fogInscatteringStart;
    mediump float fogInscatteringSize;
    float fogOneOverFarMinusNear;
    mat3 fogFromWorldMatrix;
    int groundingShadowMapIndex;
    float groundingShadowScaleFactor;
    float groundMinY;
    int castsGroundingShadow;
    highp mat3 xyzToWorkingColorSpaceMatrix;
    highp mat3 workingColorSpaceToXYZMatrix;
    highp mat3 workingColorSpaceToOutputColorSpaceMatrix;
    highp mat3 outputColorSpaceToWorkingColorSpaceMatrix;
    mat3 iblRotation;
    vec2 iblRoughnessOneLevel;
    float iblIntensity;
    float iblBlendingProportion;
    mat3 vstDirection;
    float vstImmersionRatio;
    float vstRoughnessOneLevel;
    float vstIblBlendingProportion;
    float fogNearOverFarMinusNear;
    vec4 iblSH[9];
    mat3 iblToWorkingColorSpaceMatrixOne;
    mat3 iblToWorkingColorSpaceMatrixTwo;
    float systemIblIntensity;
    float systemRoughnessLevel;
    float clipFalloffScale;
    float reserved6;
	vec4 eyeTrackingOffset[2];
};

#define frameUniforms viewUniforms.viewData

struct PerDirLightData {
    mediump vec4 lightColorIntensity; // directional light color and intensity
    mediump vec3 lightDirection;      // directional light direction
    uint lightChannels;               // light channel bits
    uint lightId;
    uint worldId;
    int isGroudingShadow;
    int castsShadows;
    mediump vec4 cascadeSplits;
    int cascadeHasVisibleShadows;
    int cascadeCount;
    mediump float shadowIntensity;
    int startCascadeIndex;
};

struct PerPunctualLightData {
    mediump vec4 colorIntensity;       // { float3(col),  float Intensity   }
    vec4 positionFalloff;              // { float3(pos), 1/falloff^2 }
    mediump vec3 direction;            // dir
    int shadowIndex;
    mediump vec2 spotScaleOffset;      // { scale, offset }
    uint typeShadow;                   // (t: 0=point, 1=spot)
    uint channels;                     // light channels
    uint lightId;                      // lightId
    uint worldId;
    int castsShadows;                  // castShadows
    mediump float shadowIntensity;
    vec3 reserved0;
    float falloffExponent;
};


struct RenderGroupData {
    highp mat4 rootTransformInverse;
    uvec4 reserved0;
    uvec4 reserved1;
    uvec4 reserved2;
};

struct BoundaryData {
    highp mat4 boundingBoxMatrix;
};

struct GroundGlassData {
    highp mat4 groundGlassVPMatrix;
};

// Adreno drivers seem to ignore precision qualifiers in structs, unless they're used in
// UBOs, which is the case here.
struct ShadowData {
    highp mat4 lightFromWorldMatrix;
    highp vec4 lightFromWorldZ;
    highp vec4 scissorNormalized;
    mediump float texelSizeAtOneMeter;
    mediump float bulbRadiusLs;
    mediump float normalBias;
    mediump uint layer;
    vec3 reserved0;
    float nearOverFarMinusNear;
};

struct PerRenderableData {
    highp mat4 worldFromModelMatrix;
    highp mat3 worldFromModelNormalMatrix;
    highp int morphTargetCount;
    highp int flags;                           // see packFlags() below (0x00000fll)
    highp int objectId;                        // used for picking
    highp float userData;   // TODO: We need a better solution, this currently holds the average local scale for the renderable
    highp uint lightChannels;                  // channels
    highp uint lightId;                        // lightId
    highp uint renderGroupId;
    highp uint worldId;
    vec3 portalPosition;
    highp int groundGlassIndex;
    vec3 portalNormal;
	float opacity;
	float dimScale;
    highp int boundaryStartIndex;
    highp int boundaryCount;
    uint boundaryMask;
    float globalIblScale;
    float renderOpacity;
    float reserved2;
    float reserved3;
    uvec4 featheringCaster;
    uvec4 featheringAlpha;
    highp vec4 reserved4;
};

//#define FAST_SKIN

#if defined(MATERIAL_STAGE_VERTEX)
struct BoneData {
#ifdef FAST_SKIN
    highp mat4 transform;
#else
    highp mat3x4 transform;    // bone transform is mat4x3 stored in row-major (last row [0,0,0,1])
    highp uvec4 cof;           // 8 first cofactor matrix of transform's upper left
#endif
};
#endif

struct InstanceData {
    highp mat4 worldFromModelMatrix;

//#if !defined(INSTANCING_ASSUME_UNIFORM_SCALING)
    highp mat3 worldFromModelNormalMatrix;
    highp vec4 padding;
//#endif

//#if !defined(INSTANCING_CUSTOM_DATA)
    highp vec4 customData[4];
//#endif

    highp vec4 reserved[4];
};

// Bits for flags
#define FILAMENT_OBJECT_SKINNING_ENABLED_BIT   0x100
#define FILAMENT_OBJECT_MORPHING_ENABLED_BIT   0x200
#define FILAMENT_OBJECT_GROUNDING_SHADOWS_BIT  0x400
#define FILAMENT_OBJECT_INSTANCE_BUFFER_BIT    0x800

#endif
