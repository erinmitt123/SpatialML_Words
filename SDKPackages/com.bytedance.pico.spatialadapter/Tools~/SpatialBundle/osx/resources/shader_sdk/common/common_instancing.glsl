#ifndef _ENGINE_COMMON_INSTANCING_GLSL
#define _ENGINE_COMMON_INSTANCING_GLSL

//------------------------------------------------------------------------------

PerRenderableData object_uniforms;

void initInstanceUniforms(out PerRenderableData p) {
    // We're copying each field separately to workaround an issue in some Adreno drivers
    // that fail on non-const array access in a UBO. Accessing the fields works however.
    // e.g.: this fails `p = objectUniforms.data[instance_index];`

    p.worldFromModelMatrix = objectUniforms.data[instance_index].worldFromModelMatrix;
    p.worldFromModelNormalMatrix = objectUniforms.data[instance_index].worldFromModelNormalMatrix;
    p.morphTargetCount = objectUniforms.data[instance_index].morphTargetCount;
    p.flags = objectUniforms.data[instance_index].flags;
    p.lightChannels = objectUniforms.data[instance_index].lightChannels;
    p.lightId = objectUniforms.data[instance_index].lightId;
    p.objectId = objectUniforms.data[instance_index].objectId;
    p.userData = objectUniforms.data[instance_index].userData;
    p.globalIblScale = objectUniforms.data[instance_index].globalIblScale;
    p.renderGroupId = objectUniforms.data[instance_index].renderGroupId;
    p.featheringCaster = objectUniforms.data[instance_index].featheringCaster;
    p.featheringAlpha = objectUniforms.data[instance_index].featheringAlpha;
    p.renderOpacity = objectUniforms.data[instance_index].renderOpacity;
    p.opacity = objectUniforms.data[instance_index].opacity;
    p.dimScale = objectUniforms.data[instance_index].dimScale;
    p.portalNormal = objectUniforms.data[instance_index].portalNormal;
    p.portalPosition = objectUniforms.data[instance_index].portalPosition;
    p.worldId = objectUniforms.data[instance_index].worldId;
    p.boundaryStartIndex = objectUniforms.data[instance_index].boundaryStartIndex;
    p.groundGlassIndex = objectUniforms.data[instance_index].groundGlassIndex;
    p.boundaryCount = objectUniforms.data[instance_index].boundaryCount;
    p.boundaryMask = objectUniforms.data[instance_index].boundaryMask;
}

void initInstanceUniformsFast(out PerRenderableData p) {
    // Performance optimize: in none-instanced-draw case, we're reading from index 0 to enable fast access uniform.

    p.morphTargetCount = objectUniforms.data[0].morphTargetCount;
    p.flags = objectUniforms.data[0].flags;
    p.lightChannels = objectUniforms.data[0].lightChannels;
    p.lightId = objectUniforms.data[0].lightId;
    p.objectId = objectUniforms.data[0].objectId;
    p.userData = objectUniforms.data[0].userData;
    p.globalIblScale = objectUniforms.data[0].globalIblScale;
    p.renderGroupId = objectUniforms.data[0].renderGroupId;
    p.featheringCaster = objectUniforms.data[0].featheringCaster;
    p.featheringAlpha = objectUniforms.data[0].featheringAlpha;
    p.renderOpacity = objectUniforms.data[0].renderOpacity;
    p.opacity = objectUniforms.data[0].opacity;
    p.dimScale = objectUniforms.data[0].dimScale;
    p.portalNormal = objectUniforms.data[0].portalNormal;
    p.portalPosition = objectUniforms.data[0].portalPosition;
    p.worldId = objectUniforms.data[0].worldId;
    p.boundaryStartIndex = objectUniforms.data[0].boundaryStartIndex;
    p.groundGlassIndex = objectUniforms.data[0].groundGlassIndex;
    p.boundaryCount = objectUniforms.data[0].boundaryCount;
    p.boundaryMask = objectUniforms.data[0].boundaryMask;

    if ((p.flags & FILAMENT_OBJECT_INSTANCE_BUFFER_BIT) == 0) {
        p.worldFromModelMatrix = objectUniforms.data[0].worldFromModelMatrix;
        p.worldFromModelNormalMatrix = objectUniforms.data[0].worldFromModelNormalMatrix;
    } else {
        if (USE_FAST_ACCESS_UNIFORM) {
            p.worldFromModelMatrix = instancesUniforms.instances[0].worldFromModelMatrix;
            p.worldFromModelNormalMatrix = instancesUniforms.instances[0].worldFromModelNormalMatrix;
        } else {
            p.worldFromModelMatrix = instancesUniforms.instances[instance_index].worldFromModelMatrix;
            p.worldFromModelNormalMatrix = instancesUniforms.instances[instance_index].worldFromModelNormalMatrix;
        }
    }
}

void initObjectUniforms(out PerRenderableData p) {
    if (USE_FAST_ACCESS_UNIFORM || INSTANCING_ON) {
        initInstanceUniformsFast(p);
    } else {
        // automatic instancing was used, each instance has its own uniform block.
        initInstanceUniforms(p);
    }

    if(CAST_SOFT_GROUNDING_SHADOW) {
        float y = p.worldFromModelMatrix[3][1] - frameUniforms.groundMinY;
        if(y > 0) {
            const float fullScaleHeight = 10;
            float t = smoothstep(0.0, fullScaleHeight, y);
            float scale = mix(1.05f, MAX_CAST_SHADOW_SCALE, t);
            mat4 scaleMatrix = mat4(
                scale, 0.0, 0.0, 0.0,
                0.0, 1, 0.0, 0.0,
                0.0, 0.0, scale, 0.0,
                0.0, 0.0, 0.0, 1.0
            );
            p.worldFromModelMatrix *= scaleMatrix;
        }
    }
}

//------------------------------------------------------------------------------
// Instance access
//------------------------------------------------------------------------------

/** @public-api */
int getInstanceIndex() {
    return instance_index;
}

#endif
