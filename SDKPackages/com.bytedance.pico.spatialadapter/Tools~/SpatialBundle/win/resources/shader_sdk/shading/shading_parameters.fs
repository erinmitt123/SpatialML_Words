#ifndef _ENGINE_SHADING_PARAMETERS_FS
#define _ENGINE_SHADING_PARAMETERS_FS

//------------------------------------------------------------------------------
// Material evaluation
//------------------------------------------------------------------------------

struct ShadingParamInitInfo{
     vec4 position;
     vec4 worldPosition;
     half3 worldNormal;
     half4 worldTangent;
};

/**
 * Computes global shading parameters used to apply lighting, such as the view
 * vector in world space, the tangent frame at the shading point, etc.
 */
#if defined(MATERIAL_DOMAIN_SPATIAL)
void computeShadingParams(in ShadingParamInitInfo info) {
#else
void computeShadingParams() {
    ShadingParamInitInfo info;
    info.worldPosition = vertex_worldPosition;
    info.position = vertex_position;
    #if defined(HAS_ATTRIBUTE_NORMAL)
    info.worldNormal = vertex_worldNormal;
    if (MATERIAL_NEEDS_TBN) {
        info.worldTangent = vertex_worldTangent;
    }
    #endif
#endif
#if defined(HAS_ATTRIBUTE_NORMAL)
    highp vec3 n = info.worldNormal;
    highp vec3 t;
    highp vec3 b;
    if (MATERIAL_NEEDS_TBN) {
        t = info.worldTangent.xyz;
        b = info.worldTangent.w * cross(n, t);
    }

    n = gl_FrontFacing ? n : -n;
        if (MATERIAL_NEEDS_TBN) {
        t = gl_FrontFacing ? t : -t;
        b = gl_FrontFacing ? b : -b;
    }

    shading_geometricNormal = normalize(n);

    if (MATERIAL_NEEDS_TBN) {
        // We use unnormalized post-interpolation values, assuming mikktspace tangents
        shading_tangentToWorld = mat3(t, b, n);
    }
#endif

    shading_position = info.worldPosition.xyz;

    // With perspective camera, the view vector is cast from the fragment pos to the eye position,
    // With ortho camera, however, the view vector is the same for all fragments:
#if defined(HAS_ATTRIBUTE_NORMAL) || !defined(SHADING_MODEL_UNLIT)
    highp vec3 sv = isPerspectiveProjection() ?
        (frameUniforms.worldFromViewMatrix[viewID][3].xyz - shading_position) :
         frameUniforms.worldFromViewMatrix[viewID][2].xyz; // ortho camera backward dir
    shading_view = normalize(sv);
#else
    if(MATERIAL_PERFORMANCE_LEVEL_0){
        highp vec3 sv = isPerspectiveProjection() ?
            (frameUniforms.worldFromViewMatrix[viewID][3].xyz - shading_position) :
             frameUniforms.worldFromViewMatrix[viewID][2].xyz; // ortho camera backward dir
        shading_view = normalize(sv);
    }
#endif

    // we do this so we avoid doing (matrix multiply), but we burn 4 varyings:
    //    p = clipFromWorldMatrix * shading_position;
    //    shading_normalizedViewportCoord = p.xy * 0.5 / p.w + 0.5
    if (MATERIAL_PERFORMANCE_LEVEL_0 || WORLD_ENTITY_PASS || USE_IMMERSIVE_TEX || GLOBAL_HAS_PUNCTUAL_LIGHTING) {
        shading_normalizedViewportCoord = info.position.xy * (0.5 / info.position.w) + 0.5;
        vec4 virtualCoord = vec4(getWorldToClipMatrixVirtual() * info.worldPosition);
        shading_normalizedViewportCoordVirtual = virtualCoord.xy * (0.5 / virtualCoord.w) + 0.5;
    }
}

/**
 * Computes global shading parameters that the material might need to access
 * before lighting: N dot V, the reflected vector and the shading normal (before
 * applying the normal map). These parameters can be useful to material authors
 * to compute other material properties.
 *
 * This function must be invoked by the user's material code (guaranteed by
 * the material compiler) after setting a value for MaterialInputs.normal.
 */
void prepareMaterial(const MaterialInputs material) {
#if defined(HAS_ATTRIBUTE_NORMAL)
if (MATERIAL_HAS_NORMAL) {
    shading_normal = normalize(shading_tangentToWorld * material.normal);
} else {
    shading_normal = getWorldGeometricNormalVector();
}
    shading_NoV = clampNoV(dot(shading_normal, shading_view));
    shading_reflected = reflect(-shading_view, shading_normal);

//if (MATERIAL_HAS_BENT_NORMAL) {
//    shading_bentNormal = normalize(shading_tangentToWorld * material.bentNormal);
//}

if (MATERIAL_HAS_CLEAR_COAT) {
if (MATERIAL_HAS_CLEAR_COAT_NORMAL) {
    shading_clearCoatNormal = normalize(shading_tangentToWorld * material.clearCoatNormal);
} else {
    shading_clearCoatNormal = getWorldGeometricNormalVector();
}
}
#endif
}

#endif
