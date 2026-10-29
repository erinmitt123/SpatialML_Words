#ifndef _ENGINE_COMMON_SHADER_GRAPH_GLSL
#define _ENGINE_COMMON_SHADER_GRAPH_GLSL

#if defined(TARGET_DESKTOP)
    #define SINGLE_RENDER
#endif

//Camera Switch Type
int CameraSwitchType(int mono, int left, int right) {
#if defined(SINGLE_RENDER)
    return mono;
#else
    return left * (1 - viewID) + right * viewID;
#endif
}

float4 CameraSwitchType(float4 mono, float4 left, float4 right) {
#if defined(SINGLE_RENDER)
    return mono;
#else
    return left * float4(1 - viewID) + right * float4(viewID);
#endif
}

float3 CameraSwitchType(float3 mono, float3 left, float3 right) {
#if defined(SINGLE_RENDER)
    return mono;
#else
    return left * float3(1 - viewID) + right * float3(viewID);
#endif
}

float2 CameraSwitchType(float2 mono, float2 left, float2 right) {
#if defined(SINGLE_RENDER)
    return mono;
#else
    return left * float2(1 - viewID) + right * float2(viewID);
#endif
}

float CameraSwitchType(float mono, float left, float right) {
#if defined(SINGLE_RENDER)
    return mono;
#else
    return left * float(1 - viewID) + right * float(viewID);
#endif
}

mat4 getWorldToRootMatrix() {
    // Performance optimize: we're applying offset in CPU to enable fast access uniform.
    if (USE_FAST_ACCESS_UNIFORM) {
        return renderGroupUniforms.renderGroups[0].rootTransformInverse;
    } else {
        return renderGroupUniforms.renderGroups[object_uniforms.renderGroupId].rootTransformInverse;
    }
}

float3 transform4x4Float3WorldToRoot(float3 position) {
    if (SPATIAL_PIPELINE) {
        return mulMat4x4Float3(getWorldToRootMatrix(), position).xyz;
    }

    return position;
}

float3 transform3x3Float3WorldToRoot(float3 direction) {
    if (SPATIAL_PIPELINE) {
        return mulMat3x3Float3(getWorldToRootMatrix(), direction);
    }

    return direction;
}

//Camera Position
float3 getCameraPositionWorldSpaceRaw() {
    return mulMat4x4Float3(getWorldToUserWorldMatrix(), getWorldCameraPosition()).xyz;
}

float3 getCameraPositionWorldSpace() {
    return transform4x4Float3WorldToRoot(getCameraPositionWorldSpaceRaw());
}

#if defined(MATERIAL_STAGE_FRAGMENT)

#include "engine/shading/common_light_indirect.glsl"

half3 specularDFG(half3 dfg, half3 f0) {
    return mix(dfg.xxx, dfg.yyy, f0);
}

half3 getReflectedVector(const float roughness, const half3 n) {
    half3 r = reflect(-shading_view, n);
    return getSpecularDominantDirection(n, r, roughness);
}

void getEnvironmentRadiance(in half3 baseColor, in float roughness, in float specular, in float metallic, in float3 normal, out float3 diffuseRadiance, out float3 specularRadiance) {
    // specular layer
    half3 Fr = vec3(0.0);
    normal = normalize(normal);
    half4 baseColorIn = vec4(baseColor, 1.0);
    half3 diffuseColor = computeDiffuseColor(baseColorIn, metallic);
    half reflectance = computeDielectricF0(specular);
    half3 f0 = computeF0(baseColorIn, metallic, reflectance);
    float perceptualRoughness = clamp(roughness, 0.0, 1.0);
    float NoV = max(dot(normal, shading_view), 1e-4);

    half3 dfg = prefilteredDFG(perceptualRoughness, NoV);
    half3 energyCompensation = 1.0 + f0 * (1.0 / dfg.y - 1.0);

    half3 E = specularDFG(dfg, f0);
    half3 r = getReflectedVector(perceptualRoughness, normal);
    Fr = E * prefilteredRadiance(r, perceptualRoughness);

    half3 specularSingleBounceAO = energyCompensation;
    Fr *= specularSingleBounceAO;

    half3 diffuseNormal = normal;

    half3 diffuseIrradiance = diffuseIrradiance(diffuseNormal);
    half3 Fd = diffuseColor * diffuseIrradiance * (1.0 - E);

    diffuseRadiance = Fd;
    specularRadiance = Fr;
}

vec2 getTextureCoordinate0() {
#ifdef HAS_ATTRIBUTE_UV0
    #ifdef FLIP_UV_ATTRIBUTE
        return vec2(getUV0().x, 1 - getUV0().y);
    #else
        return getUV0();
    #endif
#else
    return vec2(0, 0);
#endif
}

vec2 getTextureCoordinate1() {
#ifdef HAS_ATTRIBUTE_UV1
    #ifdef FLIP_UV_ATTRIBUTE
        return vec2(getUV1().x, 1 - getUV1().y);
    #else
        return getUV1();
    #endif
#else
    return vec2(0, 0);
#endif
}

mat3 getTBN() {
    return getWorldTangentFrame();
}
mat3 getTBNInverse() {
    return inverse(getWorldTangentFrame());
}

mat4 getWorldFromModelMatrix() {
    return object_uniforms.worldFromModelMatrix;
}

float3 getViewDirectionWorldSpaceRaw()
{
    return getWorldCameraPosition() - getWorldPosition();
}

float3 getViewDirectionWorldSpace()
{
    return normalize(transform3x3Float3WorldToRoot(getViewDirectionWorldSpaceRaw()));
}

float3 getViewDirectionTangentSpace()
{
    return normalize(getTBNInverse() * getViewDirectionWorldSpaceRaw());
}

float3 getCameraPositionObjectSpace() {
    return mulMat4x4Float3(inverse(getWorldFromModelMatrix()), getWorldCameraPosition()).xyz;
}

float3 getViewDirectionObjectSpace()
{
    return normalize(mulMat3x3Float3(inverse(getWorldFromModelMatrix()), getViewDirectionWorldSpaceRaw()).xyz);
}

float3 getBitangentWorldSpace() {
    return transform3x3Float3WorldToRoot(getTBN()[1]);
}

float3 getBitangentTangentSpace() {
    //return getTBNInverse() * getTBN()[1];
    return vec3(0,1,0);
}

float3 getBitangentObjectSpace() {
    return mulMat3x3Float3(inverse(getWorldFromModelMatrix()), getTBN()[1]).xyz;
}

float3 getNormalWorldSpace() {
    return transform3x3Float3WorldToRoot(getTBN()[2]);
}

float3 getNormalTangentSpace() {
    //return getTBNInverse() * getTBN()[2];
    return vec3(0,0,1);
}

float3 getNormalObjectSpace() {
    return mulMat3x3Float3(inverse(getWorldFromModelMatrix()), getTBN()[2]).xyz;
}

float3 getTangentWorldSpace() {
    return transform3x3Float3WorldToRoot(getTBN()[0]);
}

float3 getTangentTangentSpace() {
    //return getTBNInverse() * getTBN()[0];
    return vec3(1,0,0);
}

float3 getTangentObjectSpace() {
    return mulMat3x3Float3(inverse(getWorldFromModelMatrix()), getTBN()[0]).xyz;
}

float3 getCameraPositionTangentSpace() {
    return getTBNInverse() * (getCameraPositionWorldSpaceRaw() - getUserWorldPosition());
}

float3 getUpDirectionTangentSpace() {
    return getTBNInverse() * float3(0,1,0);
}

float4 getScreenPosition() {
    vec3 positionViewport = getNormalizedViewportCoord();
    return vec4(positionViewport.x * getResolution().x, positionViewport.y * getResolution().y, positionViewport.z, gl_FragCoord.w);
}

float3 getPositionWorldSpaceRaw() {
    return getUserWorldPosition().xyz;
}

float3 getPositionWorldSpace() {
    return transform4x4Float3WorldToRoot(getUserWorldPosition().xyz);
}

float3 getPositionTangentSpace() {
    //return getTBNInverse() * (getUserWorldPosition() - getUserWorldPosition());
    return float3(0,0,0);
}

float3 getPositionObjectSpace() {
    return mulMat4x4Float3(inverse(getWorldFromModelMatrix()), getWorldPosition().xyz).xyz;
}

float4 getGeometryColor() {
#if defined(HAS_ATTRIBUTE_COLOR)
    return getColor();
#else
    return vec4(1.0);
#endif
}

highp vec4 getInstanceCustomData(uint index) {
    highp vec4 customData;
    if (INSTANCING_ON) {
        if (USE_FAST_ACCESS_UNIFORM) {
            customData = instancesUniforms.instances[0].customData[index];
        } else {
            customData = instancesUniforms.instances[instance_index].customData[index];
        }
    }
    return customData;
}

#endif

// Vertex only
#if defined(MATERIAL_STAGE_VERTEX)
vec2 getTextureCoordinate0(MaterialVertexInputs m) {
#ifdef HAS_ATTRIBUTE_UV0
    #ifdef FLIP_UV_ATTRIBUTE
        return vec2(m.uv0.x, 1 - m.uv0.y);
    #else
        return m.uv0;
    #endif
#else
    return vec2(0, 0);
#endif
}

vec2 getTextureCoordinate1(MaterialVertexInputs m) {
#ifdef HAS_ATTRIBUTE_UV1
    #ifdef FLIP_UV_ATTRIBUTE
        return vec2(m.uv1.x, 1 - m.uv1.y);
    #else
        return m.uv1;
    #endif
#else
    return vec2(0, 0);
#endif
}

mat3 getTBNVertex(MaterialVertexInputs m) {
    mediump vec4 t = vec4(1,0,0,1);
    vec3 n = vec3(0,0,1);
#if defined(HAS_ATTRIBUTE_NORMAL)
if (MATERIAL_NEEDS_TBN) {
    t = vertex_worldTangent;
} else {
    t.xyz = transformObjectToWorldNormal(t.xyz);
}
    n = m.worldNormal;
    t.w = mesh_tangent.w;
#else
    n = transformObjectToWorldNormal(n);
#endif
    vec3 b = cross(n, t.xyz) * sign(t.w);

    mat3 TBN = mat3(t.xyz, b, n);
    return TBN;
}

float3 getCameraPositionObjectSpace() {
    return mulMat4x4Float3(inverse(getWorldFromModelMatrix()), getWorldCameraPosition()).xyz;
}

float3 getBitangentWorldSpace(MaterialVertexInputs m, mat3 TBN) {
    return transform3x3Float3WorldToRoot(TBN[1]);
}

float3 getBitangentTangentSpace(MaterialVertexInputs m) {
    return vec3(0,1,0);
}

float3 getBitangentObjectSpace(MaterialVertexInputs m, mat3 TBN) {
    return mulMat4x4Float3(inverse(getWorldFromModelMatrix()),TBN[1]).xyz;
}

float3 getNormalWorldSpace(MaterialVertexInputs m) {
    vec3 normal = vec3(0,0,1);
#if defined(HAS_ATTRIBUTE_NORMAL)
    normal = transform3x3Float3WorldToRoot(m.worldNormal);
#endif
    return normal;
}

float3 getNormalTangentSpace(MaterialVertexInputs m) {
//  Use for verify
//    vec3 normal = vec3(0,0,1);
//#if defined(HAS_ATTRIBUTE_NORMAL)
//    normal = m.worldNormal;
//#endif
//    return (getTBNInverse(m) * normal.xyz).xyz;
    return vec3(0,0,1);
}

float3 getNormalObjectSpace(MaterialVertexInputs m) {
    vec3 normal = vec3(0,0,1);
#if defined(HAS_ATTRIBUTE_NORMAL)
    normal = m.worldNormal;
#endif
    return (inverse(getWorldFromModelMatrix()) * vec4(normal.xyz,0)).xyz;
}

float3 getTangentWorldSpace(MaterialVertexInputs m) {
    vec3 tangent = vec3(1,0,0);
#if defined(HAS_ATTRIBUTE_NORMAL)
if (MATERIAL_NEEDS_TBN){
    tangent = vertex_worldTangent.xyz;
}
#endif
    return tangent;
}

float3 getTangentObjectSpace(MaterialVertexInputs m) {
    vec3 tangent = vec3(1,0,0);
#if defined(HAS_ATTRIBUTE_NORMAL)
if (MATERIAL_NEEDS_TBN){
    tangent = vertex_worldTangent.xyz;
}
#endif
    return mulMat4x4Float3(inverse(getWorldFromModelMatrix()),tangent).xyz;
}

float3 getTangentTangentSpace(MaterialVertexInputs m) {
// Use for verify
//    vec3 tangent = vec3(1,0,0);
//#if defined(HAS_ATTRIBUTE_NORMAL)
//if (MATERIAL_NEEDS_TBN){
//    tangent = vertex_worldTangent.xyz;
//}
//#endif
//    return getTBNInverse(m) * tangent;
    return vec3(1,0,0);
}

float3 getPositionWorldSpaceRaw(MaterialVertexInputs m) {
    return getWorldPosition(m).xyz + getWorldOffset().xyz;
}

float3 getPositionWorldSpace(MaterialVertexInputs m) {
    return transform4x4Float3WorldToRoot(getPositionWorldSpaceRaw(m));
}

float3 getPositionTangentSpace(MaterialVertexInputs m, mat3 TBNInverse) {
    //return TBNInverse * (getPositionWorldSpace(m).xyz - getPositionWorldSpace(m).xyz);;
    return float3(0,0,0);
}

float3 getCameraPositionTangentSpace(MaterialVertexInputs m, mat3 TBNInverse) {
    return TBNInverse * (getCameraPositionWorldSpaceRaw() - getPositionWorldSpaceRaw(m));
}

float3 getUpDirectionTangentSpace(MaterialVertexInputs m, mat3 TBNInverse) {
    return TBNInverse * float3(0,1,0);
}

float3 getPositionObjectSpace(MaterialVertexInputs m) {
    return mulMat4x4Float3(inverse(getWorldFromModelMatrix()),getWorldPosition(m).xyz).xyz;
}

float4 getGeometryColor() {
#if defined(HAS_ATTRIBUTE_COLOR)
    return mesh_color;
#else
    return vec4(1.0);
#endif
}

highp vec4 getInstanceCustomData(uint index) {
    highp vec4 customData;
    if (INSTANCING_ON) {
        if (USE_FAST_ACCESS_UNIFORM) {
            customData = instancesUniforms.instances[0].customData[index];
        } else {
            customData = instancesUniforms.instances[instance_index].customData[index];
        }
    }
    return customData;
}

#endif /// end vertex only

mat4 getModelToWorld() {
    if (SPATIAL_PIPELINE) {
        return getWorldToRootMatrix() * getWorldToUserWorldMatrix() * getWorldFromModelMatrix();
    }

    return getWorldToUserWorldMatrix() * getWorldFromModelMatrix();
}

mat4 getWorldToView() {
    if (SPATIAL_PIPELINE) {
        return getWorldToViewMatrix() * inverse(getWorldToUserWorldMatrix()) * inverse(getWorldToRootMatrix());
    }

    return getWorldToViewMatrix() * inverse(getWorldToUserWorldMatrix());
}

mat4 getModelToView() {
    return getWorldToViewMatrix() * getWorldFromModelMatrix();
}

mat4 getProjectionToView() {
    return getClipToViewMatrix();
}

mat4 getViewToProject() {
    return getViewToClipMatrix();
}

float getTimeSecond() {
    return getUserTime().x;
}

float3 getUpDirectionWorldSpace() {
    return float3(0,1,0);
}

float3 getUpDirectionObjectSpace() {
    return mulMat3x3Float3(getWorldFromModelMatrix(), float3(0,1,0)).xyz;
}

#if defined(MATERIAL_STAGE_FRAGMENT)
#if defined(USE_VIDEO_LOD_NODE)
#include "engine/effects/video.glsl"
#define VIDEO_TEX(n) materialParams_videoTex_##n
#define VIDEO_TEX_2(n) materialParams_videoTex2_##n
#define VIDEO_TEX_LOD(n) materialParams_videoTexLod_##n
#define VIDEO_SCALE_OFFSET_U(n) materialParams.videoTex_scaleOffsetU_##n
#define VIDEO_SCALE_OFFSET_V(n) materialParams.videoTex_scaleOffsetV_##n
#define VIDEO_ENABLE_MULTI_VIEW(n) materialParams.videoTex_enableMultiView_##n
#define VIDEO_CROP_MATRIX(n) materialParams.videoTex_cropMatrix_##n
#define VIDEO_ENABLE_HDR_PQ_SG(n) materialParams.videoTex_enableHDRPQ_##n
#define VIDEO_ENABLE_HDR_HLG_SG(n) materialParams.videoTex_enableHDRHLG_##n
#define VIDEO_ENABLE_BASE_COLOR(n) materialParams.videoTex_enableBaseColor_##n
#define VIDEO_ENABLE_GAMMA_TO_LINEAR(n) materialParams.videoTex_enableGammaToLinear_##n
#define VIDEO_BASE_COLOR(n) materialParams.videoTex_baseColor_##n
#define VIDEO_COLOR_SPACE_MAT(n) materialParams.videoTex_ColorSpaceMat_##n
SAMPLER3D(videoTex_HdrLut);

// video reflection sampler
//eyeIndex left right
//   0      0     1
//   1      0     0
//   2      1     1
#define VIDEO_REFLECTION_FUNC(name) void getVideoReflectionTexture_##name##_Raw(vec2 uv, uint eyeIndex, float lod, out vec4 outColor, out ivec2 outTextureSize) {     \
    vec4 color;                                                                                                     \
    uint eye = 0;                                                                                                   \
    uint eyeIndexValue = clamp(eyeIndex, 0, 2);                                                                     \
    uint useViewID = clamp(1 - eyeIndexValue, 0, 1);                                                                \
    uint viewIndex = viewID * useViewID + (1 - useViewID) * (eyeIndexValue - 1);                                    \
    eye = viewIndex * 2;                                                                                            \
    if (VIDEO_ENABLE_BASE_COLOR(name)) {                                                                            \
        color = VIDEO_BASE_COLOR(name);                                                                             \
    } else {                                                                                                        \
        vec2 videoUV;                                                                                               \
        videoUV.x = uv.x * VIDEO_SCALE_OFFSET_U(name)[eye] + VIDEO_SCALE_OFFSET_U(name)[eye+1];                     \
        videoUV.y = uv.y * VIDEO_SCALE_OFFSET_V(name)[eye] + VIDEO_SCALE_OFFSET_V(name)[eye+1];                     \
        videoUV = (VIDEO_CROP_MATRIX(name) * float4(videoUV, 0.0, 1.0)).xy;                                         \
        if (VIDEO_ENABLE_MULTI_VIEW(name) && viewID == 1) {                                                         \
            color = texture(VIDEO_TEX_2(name), videoUV);                                                            \
        } else {                                                                                                    \
            color = texture(VIDEO_TEX(name), videoUV);                                                              \
        }                                                                                                           \
    }                                                                                                               \
                                                                                                                    \
    if (VIDEO_ENABLE_HDR_PQ_SG(name) || VIDEO_ENABLE_BASE_COLOR(name)) {                                            \
        if (VIDEO_ENABLE_HDR_PQ_SG(name)) {                                                                         \
            color.rgb = eotfPQ(color.rgb);                                                                          \
        } else if (VIDEO_ENABLE_BASE_COLOR(name)) {                                                                 \
            color.rgb = eotfHLG(color.rgb);                                                                         \
        }                                                                                                           \
        TONE_MAPPING(color.rgb,videoTex_HdrLut,1.0,color.rgb);                                                     \
        color.a = 1.0f;                                                                                             \
        color.rgb = linearRec2020_To_LinearP3(color.rgb);                                                           \
    } else {                                                                                                        \
        if(VIDEO_ENABLE_GAMMA_TO_LINEAR(name)){                                                                     \
            color.rgb = eotfGamma2_2(color.rgb);                                                                    \
        }                                                                                                           \
        color.rgb = VIDEO_COLOR_SPACE_MAT(name) * color.rgb;                                                        \
    }                                                                                                               \
    outColor = color;                                                                                               \
    outTextureSize = ivec2(vec2(textureSize(VIDEO_TEX(name), 0)) * vec2(VIDEO_SCALE_OFFSET_U(name)[eye], VIDEO_SCALE_OFFSET_V(name)[eye])); \
}                                                                                                                   \
void getVideoReflectionTexture_##name##_Blurred(vec2 uv, uint eyeIndex, float lod, out vec4 outColor, out ivec2 outTextureSize) {     \
    vec4 color;                                                                                                     \
    uint eye = 0;                                                                                                   \
    uint eyeIndexValue = clamp(eyeIndex, 0, 2);                                                                     \
    uint useViewID = clamp(1 - eyeIndexValue, 0, 1);                                                                \
    uint viewIndex = viewID * useViewID + (1 - useViewID) * (eyeIndexValue - 1);                                    \
    eye = viewIndex * 2;                                                                                            \
    if (VIDEO_ENABLE_BASE_COLOR(name)) {                                                                            \
        color = VIDEO_BASE_COLOR(name);                                                                             \
    } else {                                                                                                        \
        color = textureLod(VIDEO_TEX_LOD(name), uv, lod);                                                           \
    }                                                                                                               \
    outColor = color;                                                                                               \
    outTextureSize = ivec2(vec2(textureSize(VIDEO_TEX(name), 0)) * vec2(VIDEO_SCALE_OFFSET_U(name)[eye], VIDEO_SCALE_OFFSET_V(name)[eye])); \
}

#ifdef FLIP_UV_ATTRIBUTE
#define GET_VIDEO_REFLECTION_TEXTURE(name,mode,uv,eyeIndex,lod,outColor,outTextureSize) getVideoReflectionTexture_##name##_##mode(vec2(uv.x,1-uv.y),uint(eyeIndex),lod,outColor,outTextureSize)
#else
#define GET_VIDEO_REFLECTION_TEXTURE(name,mode,uv,eyeIndex,lod,outColor,outTextureSize) getVideoReflectionTexture_##name##_##mode(uv,uint(eyeIndex),lod,outColor,outTextureSize)
#endif

#endif //USE_VIDEO_LOD_NODE

// Custom video material
#if defined(USE_VIDEO_NODE)
#include "engine/effects/video.glsl"
vec4 getVideoTexture(vec2 uv) {
    int eye = viewID * 2;
    vec4 result = vec4(0.0);
#ifdef FLIP_UV_ATTRIBUTE
    uv = vec2(uv.x, 1 - uv.y);
#endif
    vec2 videouv;
    videouv.x = uv.x * materialParams._builtin_video_scaleOffsetU[eye] + materialParams._builtin_video_scaleOffsetU[eye+1];
    videouv.y = uv.y * materialParams._builtin_video_scaleOffsetV[eye] + materialParams._builtin_video_scaleOffsetV[eye+1];
    if(materialParams._builtin_video_enableBaseColor){
            result = materialParams._builtin_video_baseColor;
    } else {
        float4 uv4 = float4(videouv.xy,0.0,1.0);
        uv4 = materialParams._builtin_video_cropMatrix * uv4;

        #if defined(MATERIAL_ENABLE_MULTIVIEW)
            if(viewID == 0){
                DYNAMIC_SUPER_SAMPLING(materialParams__builtin_video_texture, uv4.xy, result.rgba);
            }else{
                DYNAMIC_SUPER_SAMPLING(materialParams__builtin_video_texture2, uv4.xy, result.rgba);
            }
        #else
            DYNAMIC_SUPER_SAMPLING(materialParams__builtin_video_texture, uv4.xy, result.rgba);
        #endif
    }

#if defined(VIDEO_ENABLE_HDR_PQ || VIDEO_ENABLE_HDR_HLG)
    #if defined(VIDEO_ENABLE_HDR_PQ)
        result.rgb = eotfPQ(result.rgb);
    #endif
    #if defined(VIDEO_ENABLE_HDR_HLG)
        result.rgb = eotfHLG(result.rgb);
    #endif
        TONE_MAPPING(result.rgb,materialParams__builtin_video_HdrLut,1.0,result.rgb);
    result.a = 1.0f;
    result.rgb = linearRec2020_To_LinearP3(result.rgb);
#else
    if(materialParams._builtin_video_gammaToLinear){
        result.rgb = eotfGamma2_2(result.rgb);
    }
    result.rgb = APPLY_COLORSPACE(materialParams__builtin_video_texture, result.rgb);
#endif

    return result;
}

#define GET_VIDEO_TEXTURE(uv) getVideoTexture(uv)
#endif
#endif //USE_VIDEO_NODE
#endif
