#ifndef _ENGINE_GETTERS_FS
#define _ENGINE_GETTERS_FS

//------------------------------------------------------------------------------
// Uniforms access
//------------------------------------------------------------------------------

/** sort-of public */
float getObjectUserData() {
    return object_uniforms.userData;
}

//------------------------------------------------------------------------------
// Attributes access
//------------------------------------------------------------------------------

#if !defined(MATERIAL_DOMAIN_SPATIAL)
#if defined(HAS_ATTRIBUTE_COLOR)
/** @public-api */
half4 getColor() {
    return vertex_color;
}
#endif

#if defined(HAS_ATTRIBUTE_UV0)
/** @public-api */
highp vec2 getUV0() {
    return vertex_uv01.xy;
}
#endif

#if defined(HAS_ATTRIBUTE_UV1)
/** @public-api */
highp vec2 getUV1() {
    return vertex_uv01.zw;
}
#endif
#endif // !MATERIAL_DOMAIN_SPATIAL

/** @public-api */
half getMaskThreshold() {
    return materialParams._maskThreshold;
}

/** @public-api */
highp mat3 getWorldTangentFrame() {
    return shading_tangentToWorld;
}

/** @public-api */
highp vec3 getWorldPosition() {
    return shading_position;
}

/** @public-api */
highp vec3 getUserWorldPosition() {
    return mulMat4x4Float3(getWorldToUserWorldMatrix(), getWorldPosition()).xyz;
}

/** @public-api */
half3 getWorldViewVector() {
    return shading_view;
}

bool isPerspectiveProjection() {
    return frameUniforms.clipFromViewMatrix[viewID][2].w != 0.0;
}

/** @public-api */
half3 getWorldNormalVector() {
    return shading_normal;
}

/** @public-api */
half3 getWorldGeometricNormalVector() {
    return shading_geometricNormal;
}

/** @public-api */
half3 getWorldReflectedVector() {
    return shading_reflected;
}

/** @public-api */
half getNdotV() {
    return shading_NoV;
}

highp vec3 getNormalizedPhysicalViewportCoord() {
    // make sure to handle our reversed-z
    return vec3(shading_normalizedViewportCoord, gl_FragCoord.z);
}

highp vec3 getNormalizedPhysicalViewportCoordVirtual() {
    // make sure to handle our reversed-z
    return vec3(shading_normalizedViewportCoordVirtual, gl_FragCoord.z);
}

/**
 * Returns the normalized [0, 1] logical viewport coordinates with the origin at the viewport's
 * bottom-left. Z coordinate is in the [1, 0] range (reversed).
 *
 * @public-api
 */
highp vec3 getNormalizedViewportCoord() {
    // make sure to handle our reversed-z
    highp vec2 scale = frameUniforms.logicalViewportScale;
    highp vec2 offset = frameUniforms.logicalViewportOffset;
    highp vec2 logicalUv = shading_normalizedViewportCoord * scale + offset;
    return vec3(logicalUv, gl_FragCoord.z);
}

float getIblLuminance() {
    if(HAS_LOCAL_IBL) {
        return frameUniforms.exposure * mix(perViewOverrideUniforms.iblIntensity, frameUniforms.iblIntensity, perViewOverrideUniforms.iblLocalGlobalBlendingProportion);
    }
    return frameUniforms.exposure * frameUniforms.iblIntensity;
}

float getOpacity() {
    return object_uniforms.opacity;
}

float getDimScale() {
    return object_uniforms.dimScale;
}

uint getRenderDebugMode() {
    return perViewOverrideUniforms.renderDebugMode;
}
#endif
