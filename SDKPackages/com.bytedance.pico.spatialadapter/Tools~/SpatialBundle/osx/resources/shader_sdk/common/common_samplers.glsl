#ifndef _ENGINE_PIPELINE_LAYOUT_GLOBAL_SHARED_SAMPLERS_GLSL
#define _ENGINE_PIPELINE_LAYOUT_GLOBAL_SHARED_SAMPLERS_GLSL

layout(binding = 0, set = DESCRIPT_SET_SHARED_SAMPLER) uniform lowp sampler s_point_clamp_sampler;
layout(binding = 1, set = DESCRIPT_SET_SHARED_SAMPLER) uniform lowp sampler s_linear_clamp_sampler;
layout(binding = 2, set = DESCRIPT_SET_SHARED_SAMPLER) uniform lowp sampler s_linear_repeat_sampler;
layout(binding = 3, set = DESCRIPT_SET_SHARED_SAMPLER) uniform lowp sampler s_trilinear_clamp_sampler;
layout(binding = 4, set = DESCRIPT_SET_SHARED_SAMPLER) uniform lowp sampler s_trilinear_repeat_sampler;
layout(binding = 5, set = DESCRIPT_SET_SHARED_SAMPLER) uniform lowp sampler s_linear_clamp_compare_ge_sampler;

#endif