
layout(binding = 10, std140, set = 1) uniform MaterialParams {
    mediump float _specularAntiAliasingVariance[[builtin]];
    mediump float _specularAntiAliasingThreshold[[builtin]];
    mediump float _iblRoughness[[default(0.5f), builtin]];
    mediump float _iblFdScale[[default(1.0f), builtin]];
    mediump float _iblFrScale[[default(1.0f), builtin]];
    float _maskThreshold[[builtin]];
    bool _toneMappingOff[[default(false), builtin, global]];
    bool _gamutMappingRuntime[[default(false), builtin, global]];
    bool _gamutMappingOutput[[default(false), builtin, global]];
} materialParams;

[[builtin, colorSpace(kSrgb), global, gamma]]
layout(set = kDSetSampledImage) uniform mediump sampler2D debugViewCheckerUV;
