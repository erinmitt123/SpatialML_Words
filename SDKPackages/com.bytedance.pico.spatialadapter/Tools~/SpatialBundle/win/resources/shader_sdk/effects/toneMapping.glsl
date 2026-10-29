#ifndef _ENGINE_TONEMAPPING_FS
#define _ENGINE_TONEMAPPING_FS

// ToneMapping

half3 sampleLutHDR(mediump texture3D lut, half2 size, half3 x) {
    half3 logc;

    // Alexa LogC EI 1000
    const half a = 5.555556;
    const half b = 0.047996;
    const half c = 0.244161 / log2(10.0);
    const half d = 0.386036;
    logc = c * log2(a * x + b) + d;

    // Remap to sample pixel centers
    logc = size.x + logc * size.y;

    return textureLod(sampler3D(lut, s_linear_clamp_sampler), logc, 0.0).rgb;
}

half3 sampleUserLut(mediump texture2D lut, half2 scaleOffset, half3 x) {
    float3 uv = x;
    uv.z *= scaleOffset.y;
    float2 textureSize = vec2(scaleOffset.x * scaleOffset.x, scaleOffset.x);
    float shift = floor(uv.z);
    uv.xy = uv.xy * scaleOffset.y * textureSize.xy + textureSize.xy * 0.5;
    uv.x += shift * textureSize.y;

    uv.xyz = mix(
    textureLod(sampler2D(lut, s_linear_clamp_sampler), uv.xy, 0.0).rgb,
    textureLod(sampler2D(lut, s_linear_clamp_sampler), uv.xy + float2(textureSize.y, 0.0), 0.0).rgb,
    uv.z - shift
    );
    return uv;
}

half3 sampleLut(mediump texture3D lut, half2 size, half3 x) {
    half3 logc = x;
    // Remap to sample pixel centers
    logc = size.x + logc * size.y;

    return textureLod(sampler3D(lut, s_linear_clamp_sampler), logc, 0.0).rgb;
}

half3 toneMapping(half3 color) {
    if (!getToneMappingOff()) {
        color = max(color, 0);
        half3 result = sampleLutHDR(perViewOverride_toneMappingLut, frameUniforms.toneMappingLutSize, color);
        return result;
    }
    else if(getGamutMappingOutput()){
        return workingSpace_To_OutputSpace(color);
    }

    return color;
}

#endif
