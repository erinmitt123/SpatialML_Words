#ifndef _ENGINE_LIGHT_DIRECTIONAL_FS
#define _ENGINE_LIGHT_DIRECTIONAL_FS

//------------------------------------------------------------------------------
// Directional light evaluation
//------------------------------------------------------------------------------
#if FILAMENT_QUALITY >= FILAMENT_QUALITY_HIGH
#define SUN_AS_AREA_LIGHT
#endif

half3 sampleSunAreaLight(const half3 lightDirection) {
#if defined(SUN_AS_AREA_LIGHT)
    if (frameUniforms.sun.w >= 0.0) {
        // simulate sun as disc area light
        half LoR = dot(lightDirection, shading_reflected);
        half d = frameUniforms.sun.x;
        highp vec3 s = shading_reflected - LoR * lightDirection;
        return LoR < d ? normalize(lightDirection * d + normalize(s) * frameUniforms.sun.y) : shading_reflected;
    }
#endif
    return lightDirection;
}

Light getDirectionalLight(const PerDirLightData lightData) {
    Light light;
    // note: lightColorIntensity.w is always premultiplied by the exposure
    light.colorIntensity = lightData.lightColorIntensity;
    light.l = sampleSunAreaLight(lightData.lightDirection);
    light.attenuation = 1.0;
    light.direction = lightData.lightDirection;
    light.NoL = saturate(dot(shading_normal, light.l));
    light.channels = lightData.lightChannels;
    light.id = lightData.lightId;
    light.worldId = lightData.worldId;
    light.castsShadows = bool(lightData.castsShadows > 0);
    light.shadowIntensity = lightData.shadowIntensity;
    light.isGroudingShadow = bool(lightData.isGroudingShadow > 0);
    return light;
}

void evaluateDirectionalLight(const MaterialInputs material, const PixelParams pixel,
    const int index, inout half3 color, inout half totalVisibility) {

    PerDirLightData lightData = dirLightsUniforms.lights[index];

    Light light = getDirectionalLight(lightData);

    if(WORLD_ENTITY_PASS){
        if(light.worldId == 0u || light.worldId != object_uniforms.worldId){
            return;
        }
    }else{
        if(light.worldId != 0u){
            return;
        }
    }

    if ((light.channels & object_uniforms.lightChannels) == 0u) {
        return;
    }

    if (light.id != 0u && light.id != object_uniforms.lightId) {
        return;
    }

    if (light.isGroudingShadow &&
        ((object_uniforms.flags & FILAMENT_OBJECT_GROUNDING_SHADOWS_BIT) == 0 || !ENABLE_HARD_GROUNDING_SHADOW)) {
        return;
    }

    half visibility = 1.0;
    if (HAS_SHADOWING) {
        if (light.NoL > 0.0 && (light.castsShadows || light.isGroudingShadow) && lightData.cascadeHasVisibleShadows > 0) {
            int cascade = lightData.cascadeCount == 1 ? 0 : getShadowCascade(index);
            int shadowIndex = lightData.startCascadeIndex + cascade;

            highp vec4 shadowPosition = getDirectionalLightShadowPosition(shadowIndex, light.direction);
            visibility = shadow(true, light_shadowMap, shadowIndex, shadowPosition, 0.0);
        }
    }

    visibility = mix(1.0, visibility, light.shadowIntensity);
    if(!light.isGroudingShadow) {
        if (MATERIAL_HAS_AMBIENT_OCCLUSION) {
            visibility *= computeMicroShadowing(light.NoL, material.ambientOcclusion);
        }
#if defined(MATERIAL_HAS_CUSTOM_SURFACE_SHADING)
        color.rgb += customSurfaceShading(material, pixel, light, visibility);
#else
        color.rgb += surfaceShading(pixel, light, visibility);
#endif
    } else {
        totalVisibility = min(totalVisibility, visibility);
    }
}

void evaluateDirectionalLights(const MaterialInputs material,
        const PixelParams pixel, inout half3 color) {
    half totalVisibility = 1.0;
    
    // Use FAU to improve performance
    if (frameUniforms.directionalLightCount == 1) {
        evaluateDirectionalLight(material, pixel, 0, color, totalVisibility);
    } else {
        for (int i = 0; i < frameUniforms.directionalLightCount; ++i) {
            evaluateDirectionalLight(material, pixel, i, color, totalVisibility);
        }
    }

    if(ENABLE_SOFT_GROUNDING_SHADOW) {
        int shadowIndex = frameUniforms.groundingShadowMapIndex;
        highp vec4 shadowPosition = getGroundingShadowPosition(shadowIndex);
        totalVisibility *= groundingShadow(shadowPosition, shadowIndex);
    }

    color *= totalVisibility;
}

#endif
