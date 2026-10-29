#ifndef _ENGINE_COMMON_GETTERS_GLSL
#define _ENGINE_COMMON_GETTERS_GLSL

//------------------------------------------------------------------------------
// Uniforms access
//------------------------------------------------------------------------------

/** @public-api */
highp mat4 getWorldToViewMatrix() {
    return frameUniforms.viewFromWorldMatrix[viewID];
}

/** @public-api */
highp mat4 getViewToWorldMatrix() {
    return frameUniforms.worldFromViewMatrix[viewID];
}

/** @public-api */
highp mat4 getViewToClipMatrix() {
    return frameUniforms.clipFromViewMatrix[viewID];
}

/** @public-api */
highp mat4 getClipToViewMatrix() {
    return frameUniforms.viewFromClipMatrix[viewID];
}

/** @public-api */
highp mat4 getWorldToClipMatrix() {
    return frameUniforms.clipFromWorldMatrix[viewID];
}

/** @public-api */
highp mat4 getClipToWorldMatrix() {
    return frameUniforms.worldFromClipMatrix[viewID];
}

/** @public-api */
highp mat4 getWorldToClipMatrixVirtual() {
    return frameUniforms.clipFromWorldMatrixVirtual;
}

/** @public-api */
highp mat4 getWorldToUserWorldMatrix() {
    return frameUniforms.userWorldFromWorldMatrix;
}

/** @public-api */
float getTime() {
    return frameUniforms.time;
}

/** @public-api */
highp vec4 getUserTime() {
    return frameUniforms.userTime;
}

/** @public-api **/
highp float getUserTimeMod(float m) {
    return mod(mod(frameUniforms.userTime.x, m) + mod(frameUniforms.userTime.y, m), m);
}

/**
 * Transforms a texture UV to make it suitable for a render target attachment.
 *
 * In Vulkan and Metal, texture coords are Y-down but in OpenGL they are Y-up. This wrapper function
 * accounts for these differences. When sampling from non-render targets (i.e. uploaded textures)
 * these differences do not matter because OpenGL has a second piece of backwardness, which is that
 * the first row of texels in glTexImage2D is interpreted as the bottom row.
 *
 * To protect users from these differences, we recommend that materials in the SURFACE domain
 * leverage this wrapper function when sampling from offscreen render targets.
 *
 * @public-api
 */
highp vec2 uvToRenderTargetUV(const highp vec2 uv) {
#if defined(TARGET_METAL_ENVIRONMENT) || defined(TARGET_VULKAN_ENVIRONMENT)
    return vec2(uv.x, 1.0 - uv.y);
#else
    return uv;
#endif
}

// TODO: below shouldn't be accessible from post-process materials

/** @public-api */
highp vec4 getResolution() {
    return frameUniforms.resolution;
}

/** @public-api */
highp vec3 getWorldCameraPosition() {
    return frameUniforms.worldFromViewMatrix[viewID][3].xyz;
}

/** @public-api, @deprecated use getUserWorldPosition() or getWorldToUserWorldMatrix() instead  */
highp vec3 getWorldOffset() {
    return getWorldToUserWorldMatrix()[3].xyz;
}

/** @public-api */
float getExposure() {
    // NOTE: this is a highp uniform only to work around #3602 (qualcomm)
    // We are intentionally casting it to mediump here, as per the Materials doc.
    return frameUniforms.exposure;
}

/** @public-api */
float getEV100() {
    return frameUniforms.ev100;
}

bool getToneMappingOff() {
    return materialParams._toneMappingOff;
}

bool getGamutMappingRuntime() {
    return materialParams._gamutMappingRuntime;
}

bool getGamutMappingOutput() {
    return materialParams._gamutMappingOutput;
}

#endif
