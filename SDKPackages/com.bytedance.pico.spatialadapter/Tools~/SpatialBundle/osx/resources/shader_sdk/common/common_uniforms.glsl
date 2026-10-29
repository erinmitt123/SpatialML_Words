#ifndef _ENGINE_COMMON_UNIFROMS_GLSL
#define _ENGINE_COMMON_UNIFROMS_GLSL

layout(binding = 0, std140, set = 1) uniform ViewUniforms {
    PerViewData viewData;
} viewUniforms;

const int CONFIG_MAX_DIRECTIONAL_LIGHT_COUNT = 128;
layout(binding = 1, std140, set = 1) uniform DirLightsUniforms {
    PerDirLightData lights[CONFIG_MAX_DIRECTIONAL_LIGHT_COUNT];
} dirLightsUniforms;

layout(binding = 18, std140, set = 1) uniform KeywordFallbackUniforms {
   //bool keywords[256];
   uvec4 bitsets[2]; // 0: (x,y,z,w) = bits 0-127; 1: (x,y,z,w) = bits 128-255
} keywordFallbackUniforms;
#endif