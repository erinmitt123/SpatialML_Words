#ifndef _ENGINE_COMMON_SHADOWING_GLSL
#define _ENGINE_COMMON_SHADOWING_GLSL

//------------------------------------------------------------------------------
// Shadowing
//------------------------------------------------------------------------------

/**
 * Computes the light space position of the specified world space point.
 * The returned point may contain a bias to attempt to eliminate common
 * shadowing artifacts such as "acne". To achieve this, the world space
 * normal at the point must also be passed to this function.
 * Normal bias is not used for VSM.
 */

highp vec4 computeLightSpacePosition(highp vec3 p, const highp vec3 n,
        const highp vec3 dir, const float b, highp_mat4 lightFromWorldMatrix) {

    highp float cosTheta = saturate(dot(n, dir));
    highp float sinTheta = sqrt(1.0 - cosTheta * cosTheta);
    p += n * (sinTheta * b);

    return mulMat4x4Float3(lightFromWorldMatrix, p);
}

bool needCustomDepth() {
    if (BLEND_MODE_MASKED || ALPHA_TEST_ON) {
        return true;
    }

#if defined(MATERIAL_HAS_TRANSPARENT_SHADOW)
    return BLEND_MODE_TRANSPARENT || BLEND_MODE_FADE;
#endif

    return false;
}

#endif
