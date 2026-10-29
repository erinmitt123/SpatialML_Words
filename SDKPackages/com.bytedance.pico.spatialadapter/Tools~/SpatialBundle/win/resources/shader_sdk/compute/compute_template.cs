#ifndef _ENGINE_COMPUTE_TEMPLATE_CS
#define _ENGINE_COMPUTE_TEMPLATE_CS

${extensions}

${defines}

${specConstants}

${pushConstants}

#include "engine/common/common_types.glsl"
#include "engine/common/common_uniforms.glsl"
#include "engine/common/common_defines.glsl"
#include "engine/common/common_math.glsl"
#include "engine/common/common_colorspace.glsl"
#include "engine/utils/getters.cs"

layout(local_size_x = ${groupSizeX}, local_size_y = ${groupSizeY}, local_size_z = ${groupSizeZ}) in;

${userSamplersAndBuffers}

${userImplementBlock}

void main() {
    compute();
}

#endif
