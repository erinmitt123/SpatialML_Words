#ifndef _ENGINE_VIDEO
#define _ENGINE_VIDEO

/**
 * Assumed display gamma 2.2.
 *
 * Transfer characteristic curve:
 *  E = L ^ (1/2.2)
 *      L - luminance of image 0 <= L <= 1 for conventional colorimetry
 *      E - corresponding electrical signal
 */

float3 eotfGamma2_2(float3 E) {
    E = E.rgb * (E.rgb * (E.rgb * 0.34159177 + 0.63539077) + 0.02607818);
    return E;
}

/**
 *  display gamma 2.6.
 *
 * Transfer characteristic curve:
 *  E = L ^ (1/2.6)
 *      L - luminance of image 0 <= L <= 1 for conventional colorimetry
 *      E - corresponding electrical signal
 */
float eotfGamma2_6(float E) {
    return pow(E,2.6f);
}

float3 eotfGamma2_6(float3 E) {
    return float3(
    eotfGamma2_6(E.r),
    eotfGamma2_6(E.g),
    eotfGamma2_6(E.b)
    );
}
/**
 *  display gamma 2.8.
 *
 * Transfer characteristic curve:
 *  E = L ^ (1/2.8)
 *      L - luminance of image 0 <= L <= 1 for conventional colorimetry
 *      E - corresponding electrical signal
 */
float eotfGamma2_8(float E) {
    return pow(E,2.8f);
}

float3 eotfGamma2_8(float3 E) {
    return float3(
    eotfGamma2_8(E.r),
    eotfGamma2_8(E.g),
    eotfGamma2_8(E.b)
    );
}


/**
 * BT.601 525, BT.601 625, BT.709, BT.2020
 *
 * Transfer characteristic curve:
 *  E = 1.099 * L ^ 0.45 - 0.099  for 0.018 <= L <= 1
 *    = 4.500 * L                 for 0 <= L < 0.018
 *      L - luminance of image 0 <= L <= 1 for conventional colorimetry
 *      E - corresponding electrical signal
 */
float3 eotfSMPTE170M(float3 E) {
    float3 lowPart = E * 4.5f;
    float3 highPartInput = (E + 0.099) / 1.099;
    float3 highPart = eotfGamma2_2(highPartInput);
    float3 mask = step(0.099, E); // E >= 0.099 为1，反之为0
    return mix(lowPart, highPart, mask);
}

/**
 * SMPTE ST 2084 (Dolby Perceptual Quantizer)
 *
 * Transfer characteristic curve:
 *  E = ((c1 + c2 * L^n) / (1 + c3 * L^n)) ^ m
 *  c1 = c3 - c2 + 1 = 3424 / 4096 = 0.8359375
 *  c2 = 32 * 2413 / 4096 = 18.8515625
 *  c3 = 32 * 2392 / 4096 = 18.6875
 *  m = 128 * 2523 / 4096 = 78.84375
 *  n = 0.25 * 2610 / 4096 = 0.1593017578125
 *      L - luminance of image 0 <= L <= 1 for HDR colorimetry.
 *          L = 1 corresponds to 10000 cd/m2
 *      E - corresponding electrical signal
 */
float eotfPQ(float E) {
    const float c1 = 0.8359375f;
    const float c2 = 18.8515625f;
    const float c3 = 18.6875f;
    const float m_inv = 1.0f / 78.84375f;
    const float n_inv = 1.0f / 0.1593017578125f;

    float Em = pow(E, m_inv);
    float numerator = max(Em - c1, 0.0f);
    float denominator = max(c2 - c3 * Em, 1e-6f);
    float linearOutput = pow(numerator / denominator, n_inv);

    // 1 corresponds to 10000 cd/m2, but we need 1000 cd/m2
    // linear_output = linear_output * coeff = linear_output * 10000/display_brightness, display_brightness这里按1000算
    return clamp(linearOutput * 10.0, 0.0, 1.0);
}

float3 eotfPQ(float3 E) {
    return float3(
    eotfPQ(E.r),
    eotfPQ(E.g),
    eotfPQ(E.b)
    );
}

float oetfHLG(float L) {
    const float a = 0.17883277;
    const float b = 0.28466892;
    const float c = 0.55991073;
    const float r = 0.5;

    if (L <= 1.0) {
        return r * sqrt(L);
    } else {
        return a * log(L - b) + c;
    }
}

vec3 oetfHLG(vec3 rgb) {
    return vec3(
    eotfPQ(rgb.r),
    eotfPQ(rgb.g),
    eotfPQ(rgb.b)
    );
}

/**
 * ARIB STD-B67 Hybrid Log Gamma
 *
 * Transfer characteristic curve:
 *  E = r * L^0.5                 for 0 <= L <= 1
 *    = a * ln(L - b) + c         for 1 < L
 *  a = 0.17883277
 *  b = 0.28466892
 *  c = 0.55991073
 *  r = 0.5
 *      L - luminance of image 0 <= L for HDR colorimetry. L = 1 corresponds
 *          to reference white level of 100 cd/m2
 *      E - corresponding electrical signal
 */
float eotfHLG(float E) {
    const float a = 0.17883277;
    const float b = 0.28466892;// 1-4a
    const float c = 0.55991073;//0.5-a*ln(4a)

    float ret = 0.0;
    if (E <= 0.5) {
        ret = E * E / 3.0;
    } else {
        ret = (exp((E - c) / a) + b) / 12;
    }

    ret = clamp(ret, 0.0, 1.0);
    return ret;
}

float3 eotfHLG(float3 E) {
    return float3(
    eotfHLG(E.r),
    eotfHLG(E.g),
    eotfHLG(E.b)
    );
}



float4 toneMapping_v0_1_deprecated(highp float4 color, sampler2D lutTexture, lowp float intensity) {
    //获取原始图层颜色
    highp vec4 textureColor = color;

    //获取蓝色通道颜色，textureColor.b 的范围为(0,1)，blueColor 范围为(0,63)
    highp float blueColor = textureColor.b * 63.0;

    //quad1为查找颜色所在左边位置的小正方形
    highp vec2 quad1;
    quad1.y = floor(floor(blueColor) / 8.0);
    quad1.x = floor(blueColor) - (quad1.y * 8.0);

    //quad2为查找颜色所在右边位置的小正方形
    highp vec2 quad2;
    quad2.y = floor(ceil(blueColor) / 8.0);
    quad2.x = ceil(blueColor) - (quad2.y * 8.0);

    //获取到左边小方形里面的颜色值
    highp vec2 texPos1;
    texPos1.x = (quad1.x * 0.125) + 0.5/512.0 + ((0.125 - 1.0/512.0) * textureColor.r);
    texPos1.y = 1.0f - (quad1.y * 0.125) + 0.5/512.0 + ((0.125 - 1.0/512.0) * textureColor.g);

    //获取到右边小方形里面的颜色值
    highp vec2 texPos2;
    texPos2.x = (quad2.x * 0.125) + 0.5/512.0 + ((0.125 - 1.0/512.0) * textureColor.r);
    texPos2.y = 1.0f - (quad2.y * 0.125) + 0.5/512.0 + ((0.125 - 1.0/512.0) * textureColor.g);

    //获取对应位置纹理的颜色 RGBA 值
    lowp vec4 newColor1 = TEXTURE(lutTexture, texPos1);
    lowp vec4 newColor2 = TEXTURE(lutTexture, texPos2);

    //真正的颜色是 newColor1 和 newColor2 的混合
    lowp vec4 newColor = mix(newColor1, newColor2, fract(blueColor));

    return mix(textureColor, vec4(newColor.rgb, textureColor.w), intensity);
}

float3 toneMapping_v0_2_deprecated(highp float3 color, sampler2D lutTexture, lowp float intensity) {
    //获取原始图层颜色
    highp float3 textureColor = color;

    //获取蓝色通道颜色，textureColor.b 的范围为(0,1)，blueColor 范围为(0,63)
    highp float blueColor = textureColor.b * 63.0;

    //quad1为查找颜色所在左边位置的小正方形
    highp float2 quad1;
    quad1.y = floor(floor(blueColor) / 8.0);
    quad1.x = floor(blueColor) - (quad1.y * 8.0);

    //quad2为查找颜色所在右边位置的小正方形
    highp float2 quad2;
    quad2.y = floor(ceil(blueColor) / 8.0);
    quad2.x = ceil(blueColor) - (quad2.y * 8.0);

    //获取到左边小方形里面的颜色值
    highp float2 texPos1;
    texPos1.x = (quad1.x * 0.125) + 0.5/512.0 + ((0.125 - 1.0/512.0) * textureColor.r);
    texPos1.y = (quad1.y * 0.125) + 0.5/512.0 + ((0.125 - 1.0/512.0) * textureColor.g);

    //获取到右边小方形里面的颜色值
    highp float2 texPos2;
    texPos2.x = (quad2.x * 0.125) + 0.5/512.0 + ((0.125 - 1.0/512.0) * textureColor.r);
    texPos2.y = (quad2.y * 0.125) + 0.5/512.0 + ((0.125 - 1.0/512.0) * textureColor.g);

    //获取对应位置纹理的颜色 RGBA 值
    lowp float3 newColor1 = TEXTURE(lutTexture, texPos1).rgb;
    lowp float3 newColor2 = TEXTURE(lutTexture, texPos2).rgb;

    //真正的颜色是 newColor1 和 newColor2 的混合
    lowp float3 newColor = mix(newColor1, newColor2, fract(blueColor));

    return mix(textureColor, newColor.rgb, intensity);
}

// 三线性插值（更快但精度较低）
float3 InterpolateTrilinear(sampler3D clut, float3 color, float clut_size) {
    float clut_size_minus_1 = clut_size - 1.0f;
    float r_in = (color.r * clut_size_minus_1 + 0.5f) / clut_size;
    float g_in = (color.g * clut_size_minus_1 + 0.5f) / clut_size;
    float b_in = (color.b * clut_size_minus_1 + 0.5f) / clut_size;
    return texture(clut, float3(r_in, g_in, b_in)).rgb;  // 保持 RGB 顺序
}

// 精确采样（无插值）
float3 SampleColor(sampler3D clut, int r, int g, int b) {
    return texelFetch(clut, int3(r, g, b), 0).rgb;  // 保持 RGB 顺序
}

// 四面体插值（更精确但计算量更大）
float3 InterpolateTetrahedral(sampler3D clut, float3 color, float clut_size) {
    int clut_size_minus_1 = int(clut_size - 1.0f);
    float r = color.r * float(clut_size_minus_1);
    float g = color.g * float(clut_size_minus_1);
    float b = color.b * float(clut_size_minus_1);

    int r_prev = int(floor(r));
    int g_prev = int(floor(g));
    int b_prev = int(floor(b));

    int r_next = min(r_prev + 1, clut_size_minus_1);
    int g_next = min(g_prev + 1, clut_size_minus_1);
    int b_next = min(b_prev + 1, clut_size_minus_1);

    // 获取八个顶点的颜色（简化为只获取四个关键顶点）
    float3 c000 = SampleColor(clut, r_prev, g_prev, b_prev);
    float3 c111 = SampleColor(clut, r_next, g_next, b_next);

    // 计算小数部分
    float r_fract = r - float(r_prev);
    float g_fract = g - float(g_prev);
    float b_fract = b - float(b_prev);

    // 根据小数部分选择四面体并插值
    if (r_fract > g_fract) {
        if (g_fract > b_fract) {
            float3 c100 = SampleColor(clut, r_next, g_prev, b_prev);
            float3 c110 = SampleColor(clut, r_next, g_next, b_prev);
            return (1.0f - r_fract) * c000 + (r_fract - g_fract) * c100 + (g_fract - b_fract) * c110 + b_fract * c111;
        } else if (r_fract > b_fract) {
            float3 c100 = SampleColor(clut, r_next, g_prev, b_prev);
            float3 c101 = SampleColor(clut, r_next, g_prev, b_next);
            return (1.0f - r_fract) * c000 + (r_fract - b_fract) * c100 + (b_fract - g_fract) * c101 + g_fract * c111;
        } else {
            float3 c001 = SampleColor(clut, r_prev, g_prev, b_next);
            float3 c101 = SampleColor(clut, r_next, g_prev, b_next);
            return (1.0f - b_fract) * c000 + (b_fract - r_fract) * c001 + (r_fract - g_fract) * c101 + g_fract * c111;
        }
    } else {
        if (b_fract > g_fract) {
            float3 c001 = SampleColor(clut, r_prev, g_prev, b_next);
            float3 c011 = SampleColor(clut, r_prev, g_next, b_next);
            return (1.0f - b_fract) * c000 + (b_fract - g_fract) * c001 + (g_fract - r_fract) * c011 + r_fract * c111;
        } else if (b_fract > r_fract) {
            float3 c010 = SampleColor(clut, r_prev, g_next, b_prev);
            float3 c011 = SampleColor(clut, r_prev, g_next, b_next);
            return (1.0f - g_fract) * c000 + (g_fract - b_fract) * c010 + (b_fract - r_fract) * c011 + r_fract * c111;
        } else {
            float3 c010 = SampleColor(clut, r_prev, g_next, b_prev);
            float3 c110 = SampleColor(clut, r_next, g_next, b_prev);
            return (1.0f - g_fract) * c000 + (g_fract - r_fract) * c010 + (r_fract - b_fract) * c110 + b_fract * c111;
        }
    }
}

// 色调映射主函数
float3 toneMapping_deprecated(float3 color, sampler3D clut, float intensity) {
#if 1
    return InterpolateTetrahedral(clut, color, 32.0f);
#else
    return InterpolateTrilinear(clut, color, 32.0f);
#endif
}


#define SAMPLE_COLOR(clut, r, g, b) (texelFetch(clut, int3(r, g, b), 0).rgb)

#define INTERPOLATE_TRILINEAR(clut, color, clut_size, result) \
{ \
    float clut_size_minus_1 = clut_size - 1.0f; \
    float r_in = (color.r * clut_size_minus_1 + 0.5f) / clut_size; \
    float g_in = (color.g * clut_size_minus_1 + 0.5f) / clut_size; \
    float b_in = (color.b * clut_size_minus_1 + 0.5f) / clut_size; \
    result = texture(clut, float3(r_in, g_in, b_in)).rgb; \
}

#define INTERPOLATE_TETRAHEDRAL(clut, color, clut_size, result) \
{ \
    int clut_size_minus_1 = int(clut_size - 1.0f); \
    float r = color.r * float(clut_size_minus_1); \
    float g = color.g * float(clut_size_minus_1); \
    float b = color.b * float(clut_size_minus_1); \
    \
    int r_prev = int(floor(r)); \
    int g_prev = int(floor(g)); \
    int b_prev = int(floor(b)); \
    \
    int r_next = min(r_prev + 1, clut_size_minus_1); \
    int g_next = min(g_prev + 1, clut_size_minus_1); \
    int b_next = min(b_prev + 1, clut_size_minus_1); \
    \
    float3 c000 = SAMPLE_COLOR(clut, r_prev, g_prev, b_prev); \
    float3 c111 = SAMPLE_COLOR(clut, r_next, g_next, b_next); \
    \
    float r_fract = r - float(r_prev); \
    float g_fract = g - float(g_prev); \
    float b_fract = b - float(b_prev); \
    \
    if (r_fract > g_fract) { \
        if (g_fract > b_fract) { \
            float3 c100 = SAMPLE_COLOR(clut, r_next, g_prev, b_prev); \
            float3 c110 = SAMPLE_COLOR(clut, r_next, g_next, b_prev); \
            result = (1.0f - r_fract) * c000 + (r_fract - g_fract) * c100 + (g_fract - b_fract) * c110 + b_fract * c111; \
        } else if (r_fract > b_fract) { \
            float3 c100 = SAMPLE_COLOR(clut, r_next, g_prev, b_prev); \
            float3 c101 = SAMPLE_COLOR(clut, r_next, g_prev, b_next); \
            result = (1.0f - r_fract) * c000 + (r_fract - b_fract) * c100 + (b_fract - g_fract) * c101 + g_fract * c111; \
        } else { \
            float3 c001 = SAMPLE_COLOR(clut, r_prev, g_prev, b_next); \
            float3 c101 = SAMPLE_COLOR(clut, r_next, g_prev, b_next); \
            result = (1.0f - b_fract) * c000 + (b_fract - r_fract) * c001 + (r_fract - g_fract) * c101 + g_fract * c111; \
        } \
    } else { \
        if (b_fract > g_fract) { \
            float3 c001 = SAMPLE_COLOR(clut, r_prev, g_prev, b_next); \
            float3 c011 = SAMPLE_COLOR(clut, r_prev, g_next, b_next); \
            result = (1.0f - b_fract) * c000 + (b_fract - g_fract) * c001 + (g_fract - r_fract) * c011 + r_fract * c111; \
        } else if (b_fract > r_fract) { \
            float3 c010 = SAMPLE_COLOR(clut, r_prev, g_next, b_prev); \
            float3 c011 = SAMPLE_COLOR(clut, r_prev, g_next, b_next); \
            result = (1.0f - g_fract) * c000 + (g_fract - b_fract) * c010 + (b_fract - r_fract) * c011 + r_fract * c111; \
        } else { \
            float3 c010 = SAMPLE_COLOR(clut, r_prev, g_next, b_prev); \
            float3 c110 = SAMPLE_COLOR(clut, r_next, g_next, b_prev); \
            result = (1.0f - g_fract) * c000 + (g_fract - r_fract) * c010 + (r_fract - b_fract) * c110 + b_fract * c111; \
        } \
    } \
}

#define TONE_MAPPING(color, clut, intensity, result) INTERPOLATE_TETRAHEDRAL(clut, color, 32.0f, result)

float2 limitUV(float2 uv, float4 limitation){
    uv.x = clamp(uv.x,limitation.x,limitation.z);
    uv.y = clamp(uv.y,limitation.y,limitation.w);
    return uv;
}

float4 dynamicSupperSamplingDebug_deprecated(sampler2D video,float2 uv, float4 limitation,int debug){

    float2 textureSize = float2(textureSize(video,0));
    float dxu = max(abs(dFdx(uv.x)) , abs(dFdy(uv.x)));
    float dyv = max(abs(dFdx(uv.y)) , abs(dFdy(uv.y)));
    float xAnisotropy = abs(dxu * textureSize.x);
    float yAnisotropy = abs(dyv * textureSize.y);

    float scale = abs(xAnisotropy) * abs(yAnisotropy);

    float4 outputColor = float4(0.0f,0.0f,0.0f,0.0f);
    float4 foreColor = float4(0.0f,0.0f,0.0f,0.0f);
    if(xAnisotropy > 4.0f || yAnisotropy > 4.0f){
        float4 sampleColor = float4(0.0,0.0,0.0,0.0);
        sampleColor += TEXTURE(video, limitUV(uv + float2(dxu * 0.0625f, dyv * -0.1875f),limitation));
        sampleColor += TEXTURE(video, limitUV(uv + float2(dxu * -0.0625f, dyv * 0.1875f),limitation));
        sampleColor += TEXTURE(video, limitUV(uv + float2(dxu * 0.3125f, dyv * 0.0625f),limitation));
        sampleColor += TEXTURE(video, limitUV(uv + float2(dxu * -0.1875f, dyv * -0.3125f),limitation));
        sampleColor += TEXTURE(video, limitUV(uv + float2(dxu * -0.3125f, dyv * 0.3125f),limitation));
        sampleColor += TEXTURE(video, limitUV(uv + float2(dxu * -0.4375f, dyv * -0.0625f),limitation));
        sampleColor += TEXTURE(video, limitUV(uv + float2(dxu * 0.1875f, dyv * 0.4375f),limitation));
        sampleColor += TEXTURE(video, limitUV(uv + float2(dxu * 0.4375f, dyv * -0.4375f),limitation));
        outputColor = sampleColor / 8.0f;
        foreColor = float4(0.0f,0.0f,1.0f,0.0f);

    } else if(xAnisotropy > 2.5f || yAnisotropy > 2.5f){
        float4 sampleColor = float4(0.0f,0.0f,0.0f,0.0f);
        sampleColor += TEXTURE(video, limitUV(uv + float2(dxu * -0.125f, dyv * -0.375f),limitation));
        sampleColor += TEXTURE(video, limitUV(uv + float2(dxu * 0.375f, dyv * -0.125f),limitation));
        sampleColor += TEXTURE(video, limitUV(uv + float2(dxu * -0.375f, dyv * 0.125f),limitation));
        sampleColor += TEXTURE(video, limitUV(uv + float2(dxu * 0.125f, dyv * 0.375f),limitation));
        outputColor = sampleColor / 4.0f;
        foreColor = float4(0.0f,1.0f,0.0f,0.0f);
    }else{
        outputColor = TEXTURE(video, uv);
        foreColor = float4(1.0f,0.0f,0.0f,0.0f);
    }

    float a = float(debug > 1) * 0.2;
    outputColor = foreColor * a + outputColor * (1.0 - a);

    return outputColor;
}

float4 dynamicSupperSamplingWithLimitation_deprecated(sampler2D video,float2 uv, float4 limitation){

    float2 textureSize = float2(textureSize(video,0));
    float dxu = max(abs(dFdx(uv.x)) , abs(dFdy(uv.x)));
    float dyv = max(abs(dFdx(uv.y)) , abs(dFdy(uv.y)));
    float xAnisotropy = abs(dxu * textureSize.x);
    float yAnisotropy = abs(dyv * textureSize.y);

    float4 outputColor = float4(0.0f,0.0f,0.0f,0.0f);
    if(xAnisotropy > 4.0f || yAnisotropy > 4.0f){
        float4 sampleColor = float4(0.0,0.0,0.0,0.0);
        sampleColor += TEXTURE(video, limitUV(uv + float2(dxu * 0.0625f, dyv * -0.1875f),limitation));
        sampleColor += TEXTURE(video, limitUV(uv + float2(dxu * -0.0625f, dyv * 0.1875f),limitation));
        sampleColor += TEXTURE(video, limitUV(uv + float2(dxu * 0.3125f, dyv * 0.0625f),limitation));
        sampleColor += TEXTURE(video, limitUV(uv + float2(dxu * -0.1875f, dyv * -0.3125f),limitation));
        sampleColor += TEXTURE(video, limitUV(uv + float2(dxu * -0.3125f, dyv * 0.3125f),limitation));
        sampleColor += TEXTURE(video, limitUV(uv + float2(dxu * -0.4375f, dyv * -0.0625f),limitation));
        sampleColor += TEXTURE(video, limitUV(uv + float2(dxu * 0.1875f, dyv * 0.4375f),limitation));
        sampleColor += TEXTURE(video, limitUV(uv + float2(dxu * 0.4375f, dyv * -0.4375f),limitation));
        outputColor = sampleColor / 8.0f;
    } else if(xAnisotropy > 2.5f || yAnisotropy > 2.5f){
        float4 sampleColor = float4(0.0f,0.0f,0.0f,0.0f);
        sampleColor += TEXTURE(video, limitUV(uv + float2(dxu * -0.125f, dyv * -0.375f),limitation));
        sampleColor += TEXTURE(video, limitUV(uv + float2(dxu * 0.375f, dyv * -0.125f),limitation));
        sampleColor += TEXTURE(video, limitUV(uv + float2(dxu * -0.375f, dyv * 0.125f),limitation));
        sampleColor += TEXTURE(video, limitUV(uv + float2(dxu * 0.125f, dyv * 0.375f),limitation));
        outputColor = sampleColor / 4.0f;
    }else{
        outputColor = TEXTURE(video, uv);
    }

    return outputColor;
}

float4 dynamicSupperSampling_deprecated(sampler2D video,float2 uv){

    float2 textureSize = float2(textureSize(video,0));
    float dxu = max(abs(dFdx(uv.x)) , abs(dFdy(uv.x)));
    float dyv = max(abs(dFdx(uv.y)) , abs(dFdy(uv.y)));
    float xAnisotropy = abs(dxu * textureSize.x);
    float yAnisotropy = abs(dyv * textureSize.y);

    float4 outputColor = float4(0.0f,0.0f,0.0f,0.0f);
    if(xAnisotropy > 4.0f || yAnisotropy > 4.0f){
        float4 sampleColor = float4(0.0,0.0,0.0,0.0);
        sampleColor += TEXTURE(video, uv + float2(dxu * 0.0625f, dyv * -0.1875f));
        sampleColor += TEXTURE(video, uv + float2(dxu * -0.0625f, dyv * 0.1875f));
        sampleColor += TEXTURE(video, uv + float2(dxu * 0.3125f, dyv * 0.0625f));
        sampleColor += TEXTURE(video, uv + float2(dxu * -0.1875f, dyv * -0.3125f));
        sampleColor += TEXTURE(video, uv + float2(dxu * -0.3125f, dyv * 0.3125f));
        sampleColor += TEXTURE(video, uv + float2(dxu * -0.4375f, dyv * -0.0625f));
        sampleColor += TEXTURE(video, uv + float2(dxu * 0.1875f, dyv * 0.4375f));
        sampleColor += TEXTURE(video, uv + float2(dxu * 0.4375f, dyv * -0.4375f));
        outputColor = sampleColor / 8.0f;
    } else if(xAnisotropy > 2.5f || yAnisotropy > 2.5f){
        float4 sampleColor = float4(0.0f,0.0f,0.0f,0.0f);
        sampleColor += TEXTURE(video, uv + float2(dxu * -0.125f, dyv * -0.375f));
        sampleColor += TEXTURE(video, uv + float2(dxu * 0.375f, dyv * -0.125f));
        sampleColor += TEXTURE(video, uv + float2(dxu * -0.375f, dyv * 0.125f));
        sampleColor += TEXTURE(video, uv + float2(dxu * 0.125f, dyv * 0.375f));
        outputColor = sampleColor / 4.0f;
    }else{
        outputColor = TEXTURE(video, uv);
    }

    return outputColor;
}

#define DYNAMIC_SUPER_SAMPLING(video, uv, result) \
{ \
    float2 textureSize = float2(textureSize(video, 0)); \
    float dxu = max(abs(dFdx(uv.x)), abs(dFdy(uv.x))); \
    float dyv = max(abs(dFdx(uv.y)), abs(dFdy(uv.y))); \
    float xAnisotropy = abs(dxu * textureSize.x); \
    float yAnisotropy = abs(dyv * textureSize.y); \
    \
    if (xAnisotropy > 4.0f || yAnisotropy > 4.0f) { \
        float4 sampleColor = float4(0.0f); \
        sampleColor += TEXTURE(video, uv + float2(dxu * 0.0625f, dyv * -0.1875f)); \
        sampleColor += TEXTURE(video, uv + float2(dxu * -0.0625f, dyv * 0.1875f)); \
        sampleColor += TEXTURE(video, uv + float2(dxu * 0.3125f, dyv * 0.0625f)); \
        sampleColor += TEXTURE(video, uv + float2(dxu * -0.1875f, dyv * -0.3125f)); \
        sampleColor += TEXTURE(video, uv + float2(dxu * -0.3125f, dyv * 0.3125f)); \
        sampleColor += TEXTURE(video, uv + float2(dxu * -0.4375f, dyv * -0.0625f)); \
        sampleColor += TEXTURE(video, uv + float2(dxu * 0.1875f, dyv * 0.4375f)); \
        sampleColor += TEXTURE(video, uv + float2(dxu * 0.4375f, dyv * -0.4375f)); \
        result = sampleColor / 8.0f; \
    } else if (xAnisotropy > 2.5f || yAnisotropy > 2.5f) { \
        float4 sampleColor = float4(0.0f); \
        sampleColor += TEXTURE(video, uv + float2(dxu * -0.125f, dyv * -0.375f)); \
        sampleColor += TEXTURE(video, uv + float2(dxu * 0.375f, dyv * -0.125f)); \
        sampleColor += TEXTURE(video, uv + float2(dxu * -0.375f, dyv * 0.125f)); \
        sampleColor += TEXTURE(video, uv + float2(dxu * 0.125f, dyv * 0.375f)); \
        result = sampleColor / 4.0f; \
    } else { \
        result = TEXTURE(video, uv); \
    } \
}

#endif