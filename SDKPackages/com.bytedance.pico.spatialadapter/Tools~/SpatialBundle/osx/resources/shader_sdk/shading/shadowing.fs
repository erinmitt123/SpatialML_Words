#ifndef _ENGINE_SHADOWING_FS
#define _ENGINE_SHADOWING_FS

//------------------------------------------------------------------------------
// Shadow Sampling Types
//------------------------------------------------------------------------------

// Keep this in sync with PerViewUniforms.h
#define SHADOW_SAMPLING_RUNTIME_HARD    0
#define SHADOW_SAMPLING_RUNTIME_PCF3X3  1
#define SHADOW_SAMPLING_RUNTIME_PCF5X5  2
#define SHADOW_SAMPLING_RUNTIME_PCF7X7  3

/**
 * Returns the cascade index for this fragment (between 0 and CONFIG_MAX_SHADOW_CASCADES - 1).
 */
int getShadowCascade(const int lightIndex) {
    highp float z = mulMat4x4Float3(getWorldToViewMatrix(), getWorldPosition()).z;
    ivec4 greaterZ = ivec4(greaterThan(dirLightsUniforms.lights[lightIndex].cascadeSplits, vec4(z)));
    int cascadeCount = dirLightsUniforms.lights[lightIndex].cascadeCount;
    return clamp(greaterZ.x + greaterZ.y + greaterZ.z + greaterZ.w, 0, cascadeCount - 1);
}

// get texture coordinate for directional and spot shadow maps
highp vec4 getDirectionalLightShadowPosition(const int shadowIndex, const highp vec3 dir) {
    return computeLightSpacePosition(getWorldPosition(), getWorldNormalVector(),
        dir,
        shadowUniforms.shadows[shadowIndex].normalBias,
        shadowUniforms.shadows[shadowIndex].lightFromWorldMatrix);
}

highp vec4 getGroundingShadowPosition(const int shadowIndex) {
    return mulMat4x4Float3(shadowUniforms.shadows[shadowIndex].lightFromWorldMatrix, getWorldPosition());
}

highp vec4 getSpotLightShadowPosition(const int shadowIndex,  const highp vec3 dir, const highp float zLight) {
    highp mat4 lightFromWorldMatrix = shadowUniforms.shadows[shadowIndex].lightFromWorldMatrix;

    // for spotlights, the bias depends on z
    float bias = shadowUniforms.shadows[shadowIndex].normalBias * zLight;

    return computeLightSpacePosition(getWorldPosition(), getWorldNormalVector(),
            dir, bias, lightFromWorldMatrix);
}

//------------------------------------------------------------------------------
// PCF Shadow Sampling
//------------------------------------------------------------------------------

float sampleDepth(const mediump texture2DArray map,
        const highp vec4 scissorNormalized,
        const uint layer,  highp vec2 uv, float depth) {

    if (clamp(uv, 0.0, 1.0) != uv) {
        return 1.0;
    }

    // clamp needed for directional lights and/or large kernels
    uv = clamp(uv, scissorNormalized.xy, scissorNormalized.zw);

    // depth must be clamped to support floating-point depth formats which are always in
    // the range [0, 1].
    return texture(SHARE_SAMPLER_2D_ARRAY_SHADOW(map), vec4(uv, layer, saturate(depth)));
}

float sampleGroundingShadow(const highp vec4 scissorNormalized, highp vec2 uv) {
    if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0) {
        return 1.0;
    }

    // clamp needed for directional lights and/or large kernels
    uv = clamp(uv, scissorNormalized.xy, scissorNormalized.zw);

    // depth must be clamped to support floating-point depth formats which are always in
    // the range [0, 1].
    return texture(SHARE_SAMPLER_2D(light_groundingShadowMap), uv).r;
}

// use hardware assisted PCF
float shadowSampleHard(const mediump texture2DArray map,
        const highp vec4 scissorNormalized,
        const uint layer, const highp vec4 shadowPosition) {
    highp vec3 position = shadowPosition.xyz * (1.0 / shadowPosition.w);
    // note: shadowPosition.z is in the [1, 0] range (reversed Z)
    return sampleDepth(map, scissorNormalized, layer, position.xy, position.z);
}

void computeTexelWeightOneDimension3X3(float offset, out float4 sampleWeights){
    //because we use round to get the center, the offset must be [-0.5, 0.5]
    //here we need to calculate the area of [-1.5 + offset, -1], [-1, 0], [0, 1], [1, 1.5 + offset]
    //for details, see here https://zhuanlan.zhihu.com/p/369761748

    //we call the four areas areaA, areaB, areaC, and areaD
    float sideA = 0.5 - offset; //  -1 - (-1.5 + offset)
    float areaA = sideA * sideA * 0.5;

    float c = max(0.0, -offset);
    float d = max(0.0, offset);

    float areaB = (1.0 + sideA) * (1.0 + sideA) * 0.5 - areaA - c * c;   // c * c is also an area of triangle, it will be positive if offset < 0

    float sideD = 0.5 + offset;
    float areaD = sideD * sideD * 0.5;

    float areaC = (1.0 + sideD) * (1.0 + sideD) * 0.5 - areaD - d * d;

    sampleWeights = float4(areaA, areaB, areaC, areaD);
    sampleWeights *= 0.44444; // the tent area is 2.25, 0.44444 = 1 / 2.25
}

void getSampleWeightsAndUvsPCF3X3(vec4 shadowMapTexture_TexelSize, vec2 coord, out float sampleWeights[4], out vec2 sampleUVs[4])
{
    vec2 tentCenterInTexelSpace = coord.xy * shadowMapTexture_TexelSize.zw;
    vec2 roundTentCenter = round(tentCenterInTexelSpace);
    vec2 centerOffset = tentCenterInTexelSpace - roundTentCenter;

    vec4 texelsWeightsU, texelsWeightsV;
    computeTexelWeightOneDimension3X3(centerOffset.x, texelsWeightsU);
    computeTexelWeightOneDimension3X3(centerOffset.y, texelsWeightsV);

    vec2 sampleWeightsU = vec2(texelsWeightsU.x + texelsWeightsU.y, texelsWeightsU.z + texelsWeightsU.w);
    vec2 sampleWeightsV = vec2(texelsWeightsV.x + texelsWeightsV.y, texelsWeightsV.z + texelsWeightsV.w);
    // - 1.5 is left side of left sample, 0.5 is left side of right sample
    vec2 sampleOffsetU = texelsWeightsU.yw / sampleWeightsU.xy + vec2(-1.5, 0.5);
    vec2 sampleOffsetV = texelsWeightsV.yw / sampleWeightsV.xy + vec2(-1.5, 0.5);
    sampleOffsetU *= shadowMapTexture_TexelSize.xx;
    sampleOffsetV *= shadowMapTexture_TexelSize.yy;

    vec2 roundTentCenterInTextureCoord = roundTentCenter * shadowMapTexture_TexelSize.xy;
    sampleUVs[0] = roundTentCenterInTextureCoord + vec2(sampleOffsetU.x, sampleOffsetV.x);
    sampleUVs[1] = roundTentCenterInTextureCoord + vec2(sampleOffsetU.y, sampleOffsetV.x);
    sampleUVs[2] = roundTentCenterInTextureCoord + vec2(sampleOffsetU.x, sampleOffsetV.y);
    sampleUVs[3] = roundTentCenterInTextureCoord + vec2(sampleOffsetU.y, sampleOffsetV.y);

    sampleWeights[0] = sampleWeightsU.x * sampleWeightsV.x;
    sampleWeights[1] = sampleWeightsU.y * sampleWeightsV.x;
    sampleWeights[2] = sampleWeightsU.x * sampleWeightsV.y;
    sampleWeights[3] = sampleWeightsU.y * sampleWeightsV.y;
}


void computeTexelWeightOneDimension5X5(float offset, out float3 texelsWeightsLeft, out float3 texelsWeightsRight)
{
    //here we need six area, we can compute them as in PCF 3X3
    float sideA = 0.5 - offset; //  -1 - (-1.5 + offset)
    float areaA = sideA * sideA * 0.5;

    float sideB = 1.5 - offset;
    float areaB = sideB * sideB * 0.5 - areaA;

    float sideC = 2.5 - offset;
    float c = max(0.0, -offset);
    float areaC = sideC * sideC * 0.5 - areaA - areaB - c * c;

    float sideF = 0.5 + offset;
    float areaF = sideF * sideF * 0.5;

    float sideE = 1.5 + offset;
    float areaE = sideE * sideE * 0.5 - areaF;

    float sideD = 2.5 + offset;
    float d = max(0.0, offset);
    float areaD = sideD * sideD * 0.5 - areaF - areaE - d * d;

    texelsWeightsLeft = float3(areaA, areaB, areaC);
    texelsWeightsRight = float3(areaD, areaE, areaF);
    texelsWeightsLeft *= 0.16;
    texelsWeightsRight *= 0.16;
}

void getSampleWeightsAndUvsPCF5X5(vec4 shadowMapTexture_TexelSize, vec2 coord, out float sampleWeights[9], out vec2 samplesUV[9])
{
    // first, just like 3x3, we get the tent center, and texel weights
    vec2 tentCenterInTexelSpace = coord.xy * shadowMapTexture_TexelSize.zw;
    vec2 roundTentCenter = round(tentCenterInTexelSpace);
    vec2 centerOffset = tentCenterInTexelSpace - roundTentCenter;

    vec3 texelsWeightsULeft, texelsWeightsURight;
    vec3 texelsWeightsVLeft, texelsWeightsVRight;
    computeTexelWeightOneDimension5X5(centerOffset.x, texelsWeightsULeft, texelsWeightsURight);
    computeTexelWeightOneDimension5X5(centerOffset.y, texelsWeightsVLeft, texelsWeightsVRight);

    // sampleWeights = the sum of texelsWeights
    vec3 sampleWeightsU = float3(texelsWeightsULeft.x + texelsWeightsULeft.y,
                                texelsWeightsULeft.z + texelsWeightsURight.x,
                                texelsWeightsURight.y + texelsWeightsURight.z);
    vec3 sampleWeightsV = float3(texelsWeightsVLeft.x + texelsWeightsVLeft.y,
                                texelsWeightsVLeft.z + texelsWeightsVRight.x,
                                texelsWeightsVRight.y + texelsWeightsVRight.z);

    // how much we should right offset the sample point
    // and -2.5, 0.5, 1.5 are respectively the left side of three sample point
    vec3 sampleOffsetU = vec3(texelsWeightsULeft.y, texelsWeightsURight.x, texelsWeightsURight.z) / sampleWeightsU.xyz + vec3(-2.5, -0.5, 1.5);
    vec3 sampleOffsetV = vec3(texelsWeightsVLeft.y, texelsWeightsVRight.x, texelsWeightsVRight.z) / sampleWeightsV.xyz + vec3(-2.5, -0.5, 1.5);
    sampleOffsetU *= shadowMapTexture_TexelSize.xxx;
    sampleOffsetV *= shadowMapTexture_TexelSize.yyy;

    vec2 roundTentCenterInTextureCoord = roundTentCenter * shadowMapTexture_TexelSize.xy;
    samplesUV[0] = roundTentCenterInTextureCoord + vec2(sampleOffsetU.x, sampleOffsetV.x);
    samplesUV[1] = roundTentCenterInTextureCoord + vec2(sampleOffsetU.y, sampleOffsetV.x);
    samplesUV[2] = roundTentCenterInTextureCoord + vec2(sampleOffsetU.z, sampleOffsetV.x);
    samplesUV[3] = roundTentCenterInTextureCoord + vec2(sampleOffsetU.x, sampleOffsetV.y);
    samplesUV[4] = roundTentCenterInTextureCoord + vec2(sampleOffsetU.y, sampleOffsetV.y);
    samplesUV[5] = roundTentCenterInTextureCoord + vec2(sampleOffsetU.z, sampleOffsetV.y);
    samplesUV[6] = roundTentCenterInTextureCoord + vec2(sampleOffsetU.x, sampleOffsetV.z);
    samplesUV[7] = roundTentCenterInTextureCoord + vec2(sampleOffsetU.y, sampleOffsetV.z);
    samplesUV[8] = roundTentCenterInTextureCoord + vec2(sampleOffsetU.z, sampleOffsetV.z);

    sampleWeights[0] = sampleWeightsU.x * sampleWeightsV.x;
    sampleWeights[1] = sampleWeightsU.y * sampleWeightsV.x;
    sampleWeights[2] = sampleWeightsU.z * sampleWeightsV.x;
    sampleWeights[3] = sampleWeightsU.x * sampleWeightsV.y;
    sampleWeights[4] = sampleWeightsU.y * sampleWeightsV.y;
    sampleWeights[5] = sampleWeightsU.z * sampleWeightsV.y;
    sampleWeights[6] = sampleWeightsU.x * sampleWeightsV.z;
    sampleWeights[7] = sampleWeightsU.y * sampleWeightsV.z;
    sampleWeights[8] = sampleWeightsU.z * sampleWeightsV.z;
}

void computeTexelWeightOneDimension7X7(float offset, out vec4 texelsWeightsLeft, out vec4 texelsWeightsRight)
{
    float sideA = 0.5 - offset; //  -1 - (-1.5 + offset)
    float areaA = sideA * sideA * 0.5;

    float sideB = 1.5 - offset;
    float areaB = sideB * sideB * 0.5 - areaA;

    float sideC = 2.5 - offset;
    float areaC = sideC * sideC * 0.5 - areaA - areaB;

    float sideD = 3.5 - offset;
    float d = max(0.0, -offset);
    float areaD = sideD * sideD * 0.5 - areaA - areaB - areaC - d * d;

    float sideH = 0.5 + offset;
    float areaH = sideH * sideH * 0.5;

    float sideG = 1.5 + offset;
    float areaG = sideG * sideG * 0.5 - areaH;

    float sideF = 2.5 + offset;
    float areaF = sideF * sideF * 0.5 - areaG - areaH;

    float sideE = 3.5 + offset;
    float e = max(0.0, offset);
    float areaE = sideE * sideE * 0.5 - areaF - areaG - areaH;


    texelsWeightsLeft = float4(areaA, areaB, areaC, areaD);
    texelsWeightsRight = float4(areaE, areaF, areaG, areaH);
    texelsWeightsLeft *= 0.08163;
    texelsWeightsRight *= 0.08163;
}

void getSampleWeightsAndUvsPCF7X7(vec4 shadowMapTexture_TexelSize, vec2 coord, out float sampleWeights[16], out vec2 sampleUvs[16])
{
    vec2 tentCenterInTexelSpace = coord.xy * shadowMapTexture_TexelSize.zw;
    vec2 roundTentCenter = round(tentCenterInTexelSpace);
    vec2 centerOffset = tentCenterInTexelSpace - roundTentCenter;

    vec4 texelsWeightsULeft, texelsWeightsURight;
    vec4 texelsWeightsVLeft, texelsWeightsVRight;

    computeTexelWeightOneDimension7X7(centerOffset.x, texelsWeightsULeft, texelsWeightsURight);
    computeTexelWeightOneDimension7X7(centerOffset.y, texelsWeightsVLeft, texelsWeightsVRight);

    vec4 sampleWeightsU = vec4(texelsWeightsULeft.x + texelsWeightsULeft.y,
                                texelsWeightsULeft.z + texelsWeightsULeft.w,
                                texelsWeightsURight.x + texelsWeightsURight.y,
                                texelsWeightsURight.z + texelsWeightsURight.w);

    vec4 sampleWeightsV = vec4(texelsWeightsVLeft.x + texelsWeightsVLeft.y,
                                    texelsWeightsVLeft.z + texelsWeightsVLeft.w,
                                    texelsWeightsVRight.x + texelsWeightsVRight.y,
                                    texelsWeightsVRight.z + texelsWeightsVRight.w);


    vec4 sampleOffsetsU = vec4(texelsWeightsULeft.yw, texelsWeightsURight.yw) / sampleWeightsU.xyzw + vec4(-3.5, -1.5, 0.5, 2.5);
    vec4 sampleOffsetsV = vec4(texelsWeightsVLeft.yw, texelsWeightsVRight.yw) / sampleWeightsV.xyzw + vec4(-3.5, -1.5, 0.5, 2.5);
    sampleOffsetsU *= shadowMapTexture_TexelSize.xxxx;
    sampleOffsetsV *= shadowMapTexture_TexelSize.yyyy;

    vec2 roundTentCenterInTextureCoord = roundTentCenter * shadowMapTexture_TexelSize.xy;
    sampleUvs[0]  = roundTentCenterInTextureCoord + vec2(sampleOffsetsU.x, sampleOffsetsV.x);
    sampleUvs[1]  = roundTentCenterInTextureCoord + vec2(sampleOffsetsU.y, sampleOffsetsV.x);
    sampleUvs[2]  = roundTentCenterInTextureCoord + vec2(sampleOffsetsU.z, sampleOffsetsV.x);
    sampleUvs[3]  = roundTentCenterInTextureCoord + vec2(sampleOffsetsU.w, sampleOffsetsV.x);
    sampleUvs[4]  = roundTentCenterInTextureCoord + vec2(sampleOffsetsU.x, sampleOffsetsV.y);
    sampleUvs[5]  = roundTentCenterInTextureCoord + vec2(sampleOffsetsU.y, sampleOffsetsV.y);
    sampleUvs[6]  = roundTentCenterInTextureCoord + vec2(sampleOffsetsU.z, sampleOffsetsV.y);
    sampleUvs[7]  = roundTentCenterInTextureCoord + vec2(sampleOffsetsU.w, sampleOffsetsV.y);
    sampleUvs[8]  = roundTentCenterInTextureCoord + vec2(sampleOffsetsU.x, sampleOffsetsV.z);
    sampleUvs[9]  = roundTentCenterInTextureCoord + vec2(sampleOffsetsU.y, sampleOffsetsV.z);
    sampleUvs[10] = roundTentCenterInTextureCoord + vec2(sampleOffsetsU.z, sampleOffsetsV.z);
    sampleUvs[11] = roundTentCenterInTextureCoord + vec2(sampleOffsetsU.w, sampleOffsetsV.z);
    sampleUvs[12] = roundTentCenterInTextureCoord + vec2(sampleOffsetsU.x, sampleOffsetsV.w);
    sampleUvs[13] = roundTentCenterInTextureCoord + vec2(sampleOffsetsU.y, sampleOffsetsV.w);
    sampleUvs[14] = roundTentCenterInTextureCoord + vec2(sampleOffsetsU.z, sampleOffsetsV.w);
    sampleUvs[15] = roundTentCenterInTextureCoord + vec2(sampleOffsetsU.w, sampleOffsetsV.w);

    sampleWeights[0]  = sampleWeightsU.x * sampleWeightsV.x;
    sampleWeights[1]  = sampleWeightsU.y * sampleWeightsV.x;
    sampleWeights[2]  = sampleWeightsU.z * sampleWeightsV.x;
    sampleWeights[3]  = sampleWeightsU.w * sampleWeightsV.x;
    sampleWeights[4]  = sampleWeightsU.x * sampleWeightsV.y;
    sampleWeights[5]  = sampleWeightsU.y * sampleWeightsV.y;
    sampleWeights[6]  = sampleWeightsU.z * sampleWeightsV.y;
    sampleWeights[7]  = sampleWeightsU.w * sampleWeightsV.y;
    sampleWeights[8]  = sampleWeightsU.x * sampleWeightsV.z;
    sampleWeights[9]  = sampleWeightsU.y * sampleWeightsV.z;
    sampleWeights[10] = sampleWeightsU.z * sampleWeightsV.z;
    sampleWeights[11] = sampleWeightsU.w * sampleWeightsV.z;
    sampleWeights[12] = sampleWeightsU.x * sampleWeightsV.w;
    sampleWeights[13] = sampleWeightsU.y * sampleWeightsV.w;
    sampleWeights[14] = sampleWeightsU.z * sampleWeightsV.w;
    sampleWeights[15] = sampleWeightsU.w * sampleWeightsV.w;
}

float shadowSamplePCFSoft(const mediump texture2DArray map,
                                     const highp vec4 scissorNormalized,
                                     const uint layer, const highp vec4 shadowPosition){
     highp vec3 position = shadowPosition.xyz * (1.0 / shadowPosition.w);
     // note: shadowPosition.z is in the [1, 0] range (reversed Z)
     highp vec2 size = vec2(textureSize(SHARE_SAMPLER_2D_ARRAY_SHADOW(map), 0));
     highp vec2 texelSize = vec2(1.0) / size;
     vec4 shadowMap_TexelSize = vec4(texelSize, size);

     float depth = position.z;
     position.xy = clamp(position.xy, vec2(-1.0), vec2(2.0));
    float visibility = 0.0;
//     if(GLOBAL_SHADOW_SAMPLING_RUNTIME_PCF3X3) {
        float sampleWeights[4];
        vec2 sampleUVs[4];
        getSampleWeightsAndUvsPCF3X3(shadowMap_TexelSize, position.xy, sampleWeights, sampleUVs);
        for(int i = 0; i < 4; ++i){
            visibility += sampleWeights[i] * sampleDepth(map, scissorNormalized, layer, sampleUVs[i], depth);
        }
//     }

//     if(GLOBAL_SHADOW_SAMPLING_RUNTIME_PCF5X5){
//         float sampleWeights[9];
//         vec2 sampleUvs[9];
//         getSampleWeightsAndUvsPCF5X5(shadowMap_TexelSize, position.xy, sampleWeights, sampleUvs);
//         for(int i = 0; i < 9; ++i){
//             visibility += sampleWeights[i] * sampleDepth(map, scissorNormalized, layer, sampleUvs[i], depth);
//         }
//     }
//
//     if(GLOBAL_SHADOW_SAMPLING_RUNTIME_PCF7X7){
//         float sampleWeights[16];
//         vec2 sampleUvs[16];
//         getSampleWeightsAndUvsPCF7X7(shadowMap_TexelSize, position.xy, sampleWeights, sampleUvs);
//         for(int i = 0; i < 16; ++i){
//             visibility += sampleWeights[i] * sampleDepth(map, scissorNormalized, layer, sampleUvs[i], depth);
//         }
//     }
     return visibility;
}

int getPointLightFace(const highp vec3 r) {
    highp vec4 tc;
    highp float rx = abs(r.x);
    highp float ry = abs(r.y);
    highp float rz = abs(r.z);
    highp float d = max(rx, max(ry, rz));
    if (d == rx) {
        return (r.x >= 0.0 ? 0 : 1);
    } else if (d == ry) {
        return (r.y >= 0.0 ? 2 : 3);
    } else {
        return (r.z >= 0.0 ? 4 : 5);
    }
}

/**
 * Samples the light visibility at the specified position in light (shadow)
 * space. The output is a filtered visibility factor that can be used to multiply
 * the light intensity.
 */

float groundingShadow(highp vec4 shadowPosition, const int shadowIndex) {
    highp vec4 scissorNormalized = shadowUniforms.shadows[shadowIndex].scissorNormalized;
    highp vec3 position = shadowPosition.xyz * (1.0 / shadowPosition.w);
    position.xy = clamp(position.xy, vec2(-1.0), vec2(2.0));
    return sampleGroundingShadow(scissorNormalized, position.xy);
}

float shadow(const bool DIRECTIONAL,
        const highp texture2DArray shadowMap,
        const int shadowIndex, highp vec4 shadowPosition, highp float zLight) {
    highp vec4 scissorNormalized = shadowUniforms.shadows[shadowIndex].scissorNormalized;
    uint layer = shadowUniforms.shadows[shadowIndex].layer;

    //if(GLOBAL_SHADOW_SAMPLING_RUNTIME_HARD){
        return shadowSampleHard(shadowMap, scissorNormalized, layer, shadowPosition);
//     }
//     else {
//         return shadowSamplePCFSoft(shadowMap, scissorNormalized, layer, shadowPosition);
//     }

    return 0.0;
}

void evaluateGroundingShadow(inout float4 color) {
    float totalVisibility = 0.0;
    int shadowIndex = frameUniforms.groundingShadowMapIndex;
    highp vec4 shadowPosition = getGroundingShadowPosition(shadowIndex);
    totalVisibility = groundingShadow(shadowPosition, shadowIndex);

    if(BLEND_MODE_OPAQUE || BLEND_MODE_MASKED) {
        color = vec4(color.rgb * totalVisibility, color.a);
    }
    else {
        vec4 foregroundColor = vec4(vec3(0.0), 1 - totalVisibility);
        vec4 backgroundColor = color;

        float compositeAlpha = foregroundColor.a + backgroundColor.a - foregroundColor.a * backgroundColor.a;
        vec3 compositeColor;

        if (compositeAlpha > 0.05) {
            compositeColor = (foregroundColor.rgb * foregroundColor.a + backgroundColor.rgb * backgroundColor.a * (1.0 - foregroundColor.a)) / compositeAlpha;
        } else {
            compositeColor = color.rgb;
        }
        color = vec4(compositeColor, compositeAlpha);
    }
}

#endif
