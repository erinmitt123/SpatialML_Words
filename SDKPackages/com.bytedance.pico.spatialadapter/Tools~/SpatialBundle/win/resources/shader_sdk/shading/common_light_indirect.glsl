#ifndef _ENGINE_COMMON_LIGHT_INDIRECT_GLSL
#define _ENGINE_COMMON_LIGHT_INDIRECT_GLSL


//------------------------------------------------------------------------------
// Image based lighting configuration
//------------------------------------------------------------------------------

//------------------------------------------------------------------------------
// IBL prefiltered DFG term implementations
//------------------------------------------------------------------------------
//
half3 PrefilteredDFG_LUT(half lod, half NoV) {
    // coord = sqrt(linear_roughness), which is the mapping used by cmgen.
    return textureLod(SHARE_SAMPLER_2D(light_iblDFG), vec2(NoV, lod), 0.0).rgb;
}

//------------------------------------------------------------------------------
// IBL environment BRDF dispatch
//------------------------------------------------------------------------------
//
half3 prefilteredDFG(half perceptualRoughness, half NoV) {
    // PrefilteredDFG_LUT() takes a LOD, which is sqrt(roughness) = perceptualRoughness
    return PrefilteredDFG_LUT(perceptualRoughness, NoV);
}

//------------------------------------------------------------------------------
// IBL irradiance implementations
//------------------------------------------------------------------------------

half3 Irradiance_SphericalHarmonics(const half3 n, vec4 iblSH[9]) {
    return max(
          iblSH[0]
        + iblSH[1] * (n.y)
        + iblSH[2] * (n.z)
        + iblSH[3] * (n.x)
        + iblSH[4] * (n.y * n.x)
        + iblSH[5] * (n.y * n.z)
        + iblSH[6] * (3.0 * n.z * n.z - 1.0)
        + iblSH[7] * (n.z * n.x)
        + iblSH[8] * (n.x * n.x - n.y * n.y)
        , 0.0).rgb;
}

float getProgressInternal(in float immersionRatio, vec3 objectSpacePosition) {
    float dis = 0.9f;
    float a = immersionRatio * 0.5 * PI * 1.1f;//crd 提前算
    objectSpacePosition = frameUniforms.vstDirection * objectSpacePosition;
    float2 dir = objectSpacePosition.xy / length(objectSpacePosition);
    float r = smoothstep(a + dis, a - dis, dir.y);
    float progress  = smoothstep(0.0, 1.0, r);
    return progress;
}

//------------------------------------------------------------------------------
// IBL irradiance dispatch
//------------------------------------------------------------------------------

half3 diffuseVSTIrradiance(const half3 n, half iblOneLevel) {
    half3 irradiance = textureLod(SHARE_SAMPLER_CUBE(light_vstIblSpecular), n, iblOneLevel).rgb;
    return irradiance;
}

half3 diffuseIrradiance(const half3 n, textureCube localIblSpecular, half iblOneLevel) {
    half3 irradiance = textureLod(SHARE_SAMPLER_CUBE(localIblSpecular), n, iblOneLevel).rgb;
    return irradiance;
}

half3 diffuseIrradiance(const half3 n, textureCube localIblSpecular, half iblOneLevel, vec4 iblSH[9]) {
    //if (iblSH[0].x == 65504.0) {
        half3 irradiance = textureLod(SHARE_SAMPLER_CUBE(localIblSpecular), n, iblOneLevel).rgb;
        return irradiance;
//     } else {
//         return Irradiance_SphericalHarmonics(n, iblSH);
//     }
}

half3 localDiffuseIrradiance(half3 n, textureCube iblSpecular, textureCube iblSpecularTwo,
    mat3 iblToWorkingColorSpaceMatrixOne, mat3 iblToWorkingColorSpaceMatrixTwo,
    vec4 iblSH[9], float iblBlendingProportion, vec2 iblRoughnessOneLevel) {
    half3 irradiance = diffuseIrradiance(n, iblSpecular, iblRoughnessOneLevel.x, iblSH);

    if (getGamutMappingRuntime()){
        irradiance = iblToWorkingColorSpaceMatrixOne * irradiance;
    }

    if (LOCAL_IBL_BLEND) {
        half3 irradianceTwo = diffuseIrradiance(n, iblSpecularTwo, iblRoughnessOneLevel.y, iblSH);

    if (getGamutMappingRuntime()){
        irradianceTwo = iblToWorkingColorSpaceMatrixTwo * irradianceTwo;
    }

        irradiance = mix(irradiance, irradianceTwo, iblBlendingProportion);
    }
    return irradiance;
}

half3 diffuseIrradiance(half3 n) {
    half3 resIrradiance = vec3(0);

    if(HAS_VALID_LOCAL_IBL) {
        if(!HAS_VALID_LOCAL_BLEND_PROPORTION) {
            resIrradiance = diffuseIrradiance(n, light_globalIblSpecular, GLOBAL_IBL_ONE_LEVEL_ONE, frameUniforms.iblSH);
            resIrradiance *= object_uniforms.globalIblScale;
        }
        half3 localRotation = LOCAL_IBL_DIRECTION(n);
        half3 localIrradiance = localDiffuseIrradiance(localRotation, perViewOverride_localIblSpecular, perViewOverride_localIblSpecularTwo,
                perViewOverrideUniforms.iblToWorkingColorSpaceMatrixOne, perViewOverrideUniforms.iblToWorkingColorSpaceMatrixTwo, perViewOverrideUniforms.iblSH,
                perViewOverrideUniforms.iblBlendingProportion, vec2(LOCAL_IBL_ONE_LEVEL_ONE, LOCAL_IBL_ONE_LEVEL_TWO)) * LOCAL_IBL_LUMINANCE;
        resIrradiance += localIrradiance;
    }
    else {
        if(!HAS_VALID_LOCAL_BLEND_PROPORTION) {
            resIrradiance = diffuseIrradiance(n, light_systemIblSpecular, SYSTEM_IBL_LEVEL, frameUniforms.iblSH);
            resIrradiance *= object_uniforms.globalIblScale;
        }
    }

    return resIrradiance;
}

//------------------------------------------------------------------------------
// IBL specular
//------------------------------------------------------------------------------

half perceptualRoughnessToLod(half perceptualRoughness, half iblOneLevel) {
    // The mapping below is a quadratic fit for log2(perceptualRoughness)+iblRoughnessOneLevel when
    // iblRoughnessOneLevel is 4. We found empirically that this mapping works very well for
    // a 256 cubemap with 5 levels used. But also scales well for other iblRoughnessOneLevel values.
    return iblOneLevel * perceptualRoughness * (2.0 - perceptualRoughness);
}

half3 prefilteredVSTRadiance(half3 r, half perceptualRoughness) {
    //GLOBAL_IBL_ONE_LEVEL_ONE
    half3 radiance = textureLod(SHARE_SAMPLER_CUBE(light_vstIblSpecular), r, perceptualRoughnessToLod(perceptualRoughness, VST_IBL_ONE_LEVEL)).rgb;
    return radiance;
}

half3 localPrefilteredRadiance(half3 r, textureCube iblSpecular, textureCube iblSpecularTwo,
    mat3 iblToWorkingColorSpaceMatrixOne, mat3 iblToWorkingColorSpaceMatrixTwo,
    float iblLevelOne, float iblLevelTwo,
    float iblBlendingProportion) {
    half3 radiance = textureLod(SHARE_SAMPLER_CUBE(iblSpecular), r, iblLevelOne).rgb;

    if (getGamutMappingRuntime()){
        radiance = iblToWorkingColorSpaceMatrixOne * radiance;
    }

    if (LOCAL_IBL_BLEND) {
        half3 radianceTwo = textureLod(SHARE_SAMPLER_CUBE(iblSpecularTwo), r, iblLevelTwo).rgb;

    if (getGamutMappingRuntime()){
        radianceTwo = iblToWorkingColorSpaceMatrixTwo * radianceTwo;
    }

        radiance = mix(radiance, radianceTwo, iblBlendingProportion);
    }
    return radiance;
}

half3 prefilteredRadiance(half3 r, half roughness, half offset) {
    half3 resRadiance = vec3(0);

    if(HAS_VALID_LOCAL_IBL) {
        if(!HAS_VALID_LOCAL_BLEND_PROPORTION) {
            resRadiance = textureLod(SHARE_SAMPLER_CUBE(light_globalIblSpecular), r, GLOBAL_IBL_ONE_LEVEL_ONE * roughness + offset).rgb;
            resRadiance *= object_uniforms.globalIblScale;
        }
        half3 localRotation = LOCAL_IBL_DIRECTION(r);
        half3 localRadiance = localPrefilteredRadiance(localRotation, perViewOverride_localIblSpecular, perViewOverride_localIblSpecularTwo,
            perViewOverrideUniforms.iblToWorkingColorSpaceMatrixOne, perViewOverrideUniforms.iblToWorkingColorSpaceMatrixTwo,
            LOCAL_IBL_ONE_LEVEL_ONE * roughness + offset,
            LOCAL_IBL_ONE_LEVEL_TWO * roughness + offset,
            perViewOverrideUniforms.iblBlendingProportion) * LOCAL_IBL_LUMINANCE;
        resRadiance += localRadiance;
    }
    else {
        if(!HAS_VALID_LOCAL_BLEND_PROPORTION) {
            resRadiance = textureLod(SHARE_SAMPLER_CUBE(light_systemIblSpecular), r, SYSTEM_IBL_LEVEL * roughness + offset).rgb;
            resRadiance *= object_uniforms.globalIblScale;
        }
    }

    return resRadiance;
}

half3 prefilteredRadiance(half3 r, half perceptualRoughness) {
    half3 resRadiance = vec3(0);

    if(HAS_VALID_LOCAL_IBL) {
        if(!HAS_VALID_LOCAL_BLEND_PROPORTION) {
            resRadiance = textureLod(SHARE_SAMPLER_CUBE(light_globalIblSpecular), r, perceptualRoughnessToLod(perceptualRoughness, GLOBAL_IBL_ONE_LEVEL_ONE)).rgb;
            resRadiance *= object_uniforms.globalIblScale;
        }
        half3 localRotation = LOCAL_IBL_DIRECTION(r);
        half3 localRadiance = localPrefilteredRadiance(localRotation, perViewOverride_localIblSpecular, perViewOverride_localIblSpecularTwo,
            perViewOverrideUniforms.iblToWorkingColorSpaceMatrixOne, perViewOverrideUniforms.iblToWorkingColorSpaceMatrixTwo,
            perceptualRoughnessToLod(perceptualRoughness, LOCAL_IBL_ONE_LEVEL_ONE),
            perceptualRoughnessToLod(perceptualRoughness, LOCAL_IBL_ONE_LEVEL_TWO),
            perViewOverrideUniforms.iblBlendingProportion) * LOCAL_IBL_LUMINANCE;
        resRadiance += localRadiance;
    }
    else {
        if(!HAS_VALID_LOCAL_BLEND_PROPORTION) {
            resRadiance = textureLod(SHARE_SAMPLER_CUBE(light_systemIblSpecular), r, perceptualRoughnessToLod(perceptualRoughness, SYSTEM_IBL_LEVEL)).rgb;
            resRadiance *= object_uniforms.globalIblScale;
        }
    }

    return resRadiance;
}

half3 getSpecularDominantDirection(const half3 n, const half3 r, half roughness) {
    return mix(r, n, roughness * roughness);
}


#endif
