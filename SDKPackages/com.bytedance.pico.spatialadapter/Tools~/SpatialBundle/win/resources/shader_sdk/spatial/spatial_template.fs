#ifndef _ENGINE_SPATIAL_MAIN_FS
#define _ENGINE_SPATIAL_MAIN_FS

// Extensions
${extensions}

// Defines
${defines}

// Spec-constant
${specConstants}

// PushConstants
${pushConstants}

// User samplers & buffers
${userSamplersAndBuffers}

// Variables
${variables}

// Subpass
${subpass}

// User implement block begin
${userImplementBlock}
// User implement block end

float filament_lodBias;
void surface_main() {
    initObjectUniforms(object_uniforms);

    v2f i;
    initFragmentInput(i);
    frag(i);
}

void main () {
    surface_main();
}

#endif
