#ifndef _ENGINE_CUSTOM_FUNCTION_FS
#define _ENGINE_CUSTOM_FUNCTION_FS

// 该文件用于在fragment各个阶段插入自定义功能

// 该结构用于在各个阶段之间传输数据
struct CustomData {
    float fragFade;
}customData;

uint getFeatheringCasterData(uvec4 data, uint offset){
    uint index = offset >> 2u; // 得到0到3之间的索引，指示uvec4内部的哪个uint
    uint shift = (offset & 3u) << 3u; // 决定需要右移多少位来调整为最低8位

    // 抽取和返回值
    return (data[index] >> shift) & 0xFFu;
}

// 位于fragment中material()用户定制材质函数之前
void beforeMaterial(inout MaterialInputs material) {
    customData.fragFade = 1.0;
    uvec4 featheringCasterData = object_uniforms.featheringCaster;
    uvec4 featheringAlphaData = object_uniforms.featheringAlpha;
    // 1:receiver 0:other
    if(FEATHERING_ON) {
        vec3 fragPosition = getWorldPosition();
        vec4 _screenUV = frameUniforms.centerClipFromWorldMatrix * float4(fragPosition, 1.0);
        vec2 _texUV = _screenUV.xy / _screenUV.w;
        _texUV = _texUV * 0.5 + vec2(0.5);
        _texUV.y = 1.0 - _texUV.y;
        float _fade = 1.f;
        uint casterCount = getFeatheringCasterData(featheringCasterData, 0u);
        for(uint i = 0u; i < casterCount; ++i) {
            float _fade2 = texture(sampler2DArray(light_featheringMap, s_linear_clamp_sampler), vec3(_texUV, getFeatheringCasterData(featheringCasterData, i + 1u))).r;
            _fade2 = 1.0 - _fade2 * float(getFeatheringCasterData(featheringAlphaData, i + 1u)) / 255.f;
            //_fade = min(_fade2, _fade);
            _fade *= _fade2;
        }
        customData.fragFade *= _fade;
    }

    //对FEATHERING_ON中的casterCount为1、2的情况进行for循环展开以优化GPU性能开销
    if(FEATHERING_SOLO){
        vec3 fragPosition = getWorldPosition();
        vec4 _screenUV = frameUniforms.centerClipFromWorldMatrix * float4(fragPosition, 1.0);
        vec2 _texUV = _screenUV.xy / _screenUV.w;
        _texUV = _texUV * 0.5 + vec2(0.5);
        _texUV.y = 1.0 - _texUV.y;
        float _fade2 = texture(sampler2DArray(light_featheringMap, s_linear_clamp_sampler), vec3(_texUV, getFeatheringCasterData(featheringCasterData,   1u))).r;
        _fade2 = 1.0 - _fade2 * float(getFeatheringCasterData(featheringAlphaData, 1u)) / 255.f;
        customData.fragFade *= _fade2;
    }

    if(FEATHERING_DOUBLE){
        vec3 fragPosition = getWorldPosition();
        vec4 _screenUV = frameUniforms.centerClipFromWorldMatrix * float4(fragPosition, 1.0);
        vec2 _texUV = _screenUV.xy / _screenUV.w;
        _texUV = _texUV * 0.5 + vec2(0.5);
        _texUV.y = 1.0 - _texUV.y;
        float _fade2 = texture(sampler2DArray(light_featheringMap, s_linear_clamp_sampler), vec3(_texUV, getFeatheringCasterData(featheringCasterData,  1u))).r;
        _fade2 = 1.0 - _fade2 * float(getFeatheringCasterData(featheringAlphaData, 1u)) / 255.f;
        float _fade3 = texture(sampler2DArray(light_featheringMap, s_linear_clamp_sampler), vec3(_texUV, getFeatheringCasterData(featheringCasterData, 2u))).r;
        _fade3 = 1.0 - _fade3 * float(getFeatheringCasterData(featheringAlphaData, 2u)) / 255.f;
        _fade2 *= _fade3;
        customData.fragFade *= _fade2;
    }
    customData.fragFade *= object_uniforms.renderOpacity;
}

void afterMaterial(inout MaterialInputs material) {

}

// 位于fragment中evaluateMaterial()光照模型函数之后
// depth变体没有光照函数，此函数位置在afterMaterial()之后，且此时fragColor无意义
void afterShading(inout float4 fragColor) {
    if(BLEND_MODE_PREMULTIPLY) {
        fragColor *= customData.fragFade;
    } else {
    #if !defined(SHADING_MODEL_UNLIT)
        fragColor *= customData.fragFade;
    #else
        fragColor.a *= customData.fragFade;
    #endif
    }

}

#endif
