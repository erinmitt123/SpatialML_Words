#ifndef _ENGINE_COMMON_XR_GLSL
#define _ENGINE_COMMON_XR_GLSL


//------------------------------------------------------------------------------
// Spatial xr
//------------------------------------------------------------------------------

void crossingEntityClip(vec3 portalPosition, vec3 portalNormal){
#if !defined(FORCE_NO_DISCARD)
    if(CROSSING_ENTITY){
        vec3 fragPosition = getUserWorldPosition();
        vec3 p2f = fragPosition - portalPosition;
        float dotProduct = dot(portalNormal, p2f);
        if(dotProduct < 0){
            discard;
        }
    }
#endif
}

void clippingEntityClip(vec3 portalPosition, vec3 portalNormal){
#if !defined(FORCE_NO_DISCARD)
    if(!CROSSING_ENTITY){
        vec3 fragPosition = getUserWorldPosition();
        vec3 p2f = fragPosition - portalPosition;
        float dotProduct = dot(portalNormal, p2f);
        if(dotProduct >= 0){
            discard;
        }
    }
#endif
}


float getAlphaFromDistance(float distance, float maxDistance) {
    return max(1.0 - distance / maxDistance, 0.0);
}

float spatialSpaceFalloffClip(vec3 userWorldPosition) {
    mediump float distance = 0;
    mediump float maxDistance = frameUniforms.clipFalloffScale;
    if (SPATIAL_SPACE_CLIP){
        int startIndex = object_uniforms.boundaryStartIndex;
        int count = object_uniforms.boundaryCount;
        uint mask = object_uniforms.boundaryMask;
        for(uint i = 0; i < count; i++) {
            if((mask & (1u << (31 - i))) != 0u) {
                mat4 matrix = boundaryUniforms.boundary[startIndex + i].boundingBoxMatrix;
                uint blurredFacesMask = uint(matrix[3][3]);
                mediump vec3 invScalesSq = vec3(matrix[0][3], matrix[1][3], matrix[2][3]);
                mediump float dis = isInBoxGetDistance(matrix, invScalesSq, blurredFacesMask, userWorldPosition);
                distance = max(distance, dis);
                #if !defined(FORCE_NO_DISCARD)
                if(distance > maxDistance) {
                    discard;
                }
                #endif
            }
        }
    }
    return getAlphaFromDistance(distance, maxDistance);
}

void spatialSpaceClip(vec3 userWorldPosition) {
#if !defined(FORCE_NO_DISCARD)
    if (SPATIAL_SPACE_CLIP) {
        int startIndex = object_uniforms.boundaryStartIndex;
        int count = object_uniforms.boundaryCount;
        uint mask = object_uniforms.boundaryMask;
        for (uint i = 0; i < count; i++) {
            if ((mask & (1u << (31 - i))) != 0u) {
                mat4 matrix = boundaryUniforms.boundary[startIndex + i].boundingBoxMatrix;
                if (!isInBox(matrix, userWorldPosition)) {
                    discard;
                }
            }
        }
    }
#endif
}

half4 mixImmersiveAlpha(half4 color) {
    float immersiveAlpha = 1.0;
    // use shading_normalizedViewportCoord to replace gl_FragCoord
    immersiveAlpha = texture(sampler2DArray(light_immersiveTextureArray, s_linear_clamp_sampler), vec3(shading_normalizedViewportCoord.x, 1-shading_normalizedViewportCoord.y, viewID)).r;
    if(BLEND_MODE_OPAQUE || BLEND_MODE_MASKED){
        color.a = immersiveAlpha;
    }
    color.rgb = color.rgb * immersiveAlpha;
    return color;
}
#endif
