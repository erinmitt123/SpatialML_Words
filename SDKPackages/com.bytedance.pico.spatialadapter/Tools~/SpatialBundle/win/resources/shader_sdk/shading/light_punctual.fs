#ifndef _ENGINE_LIGHT_PUNCTUAL_FS
#define _ENGINE_LIGHT_PUNCTUAL_FS

//------------------------------------------------------------------------------
// Punctual lights evaluation
//------------------------------------------------------------------------------

// Make sure this matches the same constants in Froxel.cpp
#define FROXEL_BUFFER_WIDTH_SHIFT   6u
#define FROXEL_BUFFER_WIDTH         (1u << FROXEL_BUFFER_WIDTH_SHIFT)
#define FROXEL_BUFFER_WIDTH_MASK    (FROXEL_BUFFER_WIDTH - 1u)

#define LIGHT_TYPE_POINT            0u
#define LIGHT_TYPE_SPOT             1u


struct FroxelParams {
    uint recordOffset; // offset at which the list of lights for this froxel starts
    uint count;   // number lights in this froxel
};

/**
 * Returns the coordinates of the froxel at the specified fragment coordinates.
 * The coordinates are a 3D position in the froxel grid.
 */
uvec3 getFroxelCoords(const highp vec3 fragCoords) {
    uvec3 froxelCoord;

    froxelCoord.xy = uvec2(fragCoords.xy * frameUniforms.froxelCountXY);

    // go from screen-space to reciprocal of normalized view-space Z (i.e. scaled by 1/zLightFar)
    // we get away with the reciprocal because 1/z is handled by the log2() below.
    // see Froxelizer.cpp
    highp float viewSpaceNormalizedZ = frameUniforms.zParams.x * fragCoords.z + frameUniforms.zParams.y;

    // frameUniforms.zParams.w is actually the number of z-slices, make sure it's mediump
    float zSliceCount = frameUniforms.zParams.w;

    // compute the sliceZ mapping in highp, store in mediump
    float sliceZWithoutOffset = log2(viewSpaceNormalizedZ) * frameUniforms.zParams.z;

    // finally discretize the mapping into slices
    // We need to clamp because the far plane (z=1) is out of bounds, any smaller z is not.
    froxelCoord.z = uint(clamp(sliceZWithoutOffset + zSliceCount, 0.0, zSliceCount - 1.0));

    return froxelCoord;
}

/**
 * Computes the froxel index of the fragment at the specified coordinates.
 * The froxel index is computed from the 3D coordinates of the froxel in the
 * froxel grid and later used to fetch from the froxel buffer.
 */
uint getFroxelIndex(const highp vec3 fragCoords) {
    uvec3 froxelCoord = getFroxelCoords(fragCoords);
    return froxelCoord.x * frameUniforms.fParams.x +
           froxelCoord.y * frameUniforms.fParams.y +
           froxelCoord.z * frameUniforms.fParams.z;
}

/**
 * Computes the texture coordinates of the froxel data given a froxel index.
 */
ivec2 getFroxelTexCoord(uint froxelIndex) {
    return ivec2(froxelIndex & FROXEL_BUFFER_WIDTH_MASK, froxelIndex >> FROXEL_BUFFER_WIDTH_SHIFT);
}

/**
 * Returns the froxel data for the given froxel index. The data is fetched
 * from FroxelsUniforms UBO.
 */
FroxelParams getFroxelParams(const uint froxelIndex) {
    uint w = froxelIndex >> 2u;
    uint c = froxelIndex & 0x3u;

    highp uvec4 d;
    d = froxelsUniforms.records[w];
    highp uint f = d[c];
    FroxelParams froxel;
    froxel.recordOffset = f >> 16u;
    froxel.count = f & 0xFFu;
    return froxel;
}

/**
 * Return the light index from the record index
 * A light record is a single uint index into the lights data buffer (punctualLightsUniforms UBO).
 */
uint getLightIndex(const uint index) {
    uint v = index >> 4u;
    uint c = (index >> 2u) & 0x3u;
    uint s = (index & 0x3u) * 8u;
    // this intermediate is needed to workaround a bug on qualcomm h/w
    highp uvec4 d;
    d = froxelRecordUniforms.records[v];
    return (d[c] >> s) & 0xFFu;
}

float getSquareFalloffAttenuation(float distanceSquare, float falloff) {
    float factor = distanceSquare * falloff;
    float smoothFactor = saturate(1.0 - factor * factor);
    // We would normally divide by the square distance here
    // but we do it at the call site
    return smoothFactor * smoothFactor;
}

float getDistanceAttenuation(const highp vec3 posToLight, float falloff, float falloffExponent) {
    float distanceSquare = pow(length(posToLight), falloffExponent);
    float attenuation = getSquareFalloffAttenuation(distanceSquare, falloff);
    // light far attenuation
    highp vec3 v = getWorldPosition() - getWorldCameraPosition();
    float d = dot(v, v);
    attenuation *= saturate(frameUniforms.lightFarAttenuationParams.x - d * frameUniforms.lightFarAttenuationParams.y);
    // Assume a punctual light occupies a volume of 1cm to avoid a division by 0
    return attenuation / max(distanceSquare, 1e-4);
}

float getAngleAttenuation(const highp vec3 lightDir, const highp vec3 l, const highp vec2 scaleOffset) {
    float cd = dot(lightDir, l);
    float attenuation = saturate(cd * scaleOffset.x + scaleOffset.y);
    return attenuation * attenuation;
}

/**
 * Returns a Light structure (see common_lighting.fs) describing a point or spot light.
 * The colorIntensity field will store the *pre-exposed* intensity of the light
 * in the w component.
 *
 * The light parameters used to compute the Light structure are fetched from the
 * punctualLightsUniforms uniform buffer.
 */

Light getLight(const uint lightIndex) {
    // retrieve the light data from the UBO

    PerPunctualLightData data = punctualLightsUniforms.lights[lightIndex];

    highp vec4 positionFalloff = data.positionFalloff;

    // poition-to-light vector
    highp vec3 worldPosition = getWorldPosition();
    highp vec3 posToLight = positionFalloff.xyz - worldPosition;

    // and populate the Light structure
    Light light;
    light.colorIntensity = data.colorIntensity;
    light.l = normalize(posToLight);
    light.attenuation = getDistanceAttenuation(posToLight, positionFalloff.w, data.falloffExponent);
    light.direction = data.direction;
    light.NoL = saturate(dot(shading_normal, light.l));
    light.worldPosition = positionFalloff.xyz;
    light.channels = data.channels;
    light.id = data.lightId;
    light.worldId = data.worldId;

    light.type = data.typeShadow;
    if (HAS_SHADOWING) {
        light.shadowIndex = data.shadowIndex;
        light.castsShadows = bool(data.castsShadows > 0);
        if (light.type == LIGHT_TYPE_SPOT) {
            light.zLight = dot(shadowUniforms.shadows[light.shadowIndex].lightFromWorldZ, vec4(worldPosition, 1.0));
        }
    }
    if (light.type == LIGHT_TYPE_SPOT) {
        light.attenuation *= getAngleAttenuation(-data.direction, light.l, data.spotScaleOffset);
    }

    light.shadowIntensity = data.shadowIntensity;
    return light;
}

float evaluatePunctualLightVisibility(Light light) {
    float visibility = 1.0;

    //point light shadow has constant variant
    if (light.type == LIGHT_TYPE_POINT && POINT_LIGHT_SHADOW == false) {
        return visibility;
    }

    if (light.NoL > 0.0 && light.castsShadows) {
        int shadowIndex = light.shadowIndex;
        if (light.type == LIGHT_TYPE_POINT) {
            // point-light shadows are sampled from a direction
            highp vec3 r = getWorldPosition() - light.worldPosition;
            int face = getPointLightFace(r);
            shadowIndex += face;
            light.zLight = dot(shadowUniforms.shadows[shadowIndex].lightFromWorldZ, vec4(getWorldPosition(), 1.0));
        }

        highp vec4 shadowPosition = getSpotLightShadowPosition(shadowIndex, light.direction, light.zLight);
        visibility = shadow(false, light_shadowMap, shadowIndex, shadowPosition, light.zLight);
        visibility = mix(1.0, visibility, light.shadowIntensity);
    }

    return visibility;
}

/**
 * Evaluates all punctual lights that my affect the current fragment.
 * The result of the lighting computations is accumulated in the color
 * parameter, as linear HDR RGB.
 */
void evaluatePunctualLights(const MaterialInputs material,
        const PixelParams pixel, inout vec3 color) {

    // Fetch the light information stored in the froxel that contains the
    // current fragment
    FroxelParams froxel;
    bool useVirtual = frameUniforms.virtualEnabled > 0;
    if(useVirtual) {
        froxel = getFroxelParams(getFroxelIndex(getNormalizedPhysicalViewportCoordVirtual()));
    } else {
        froxel = getFroxelParams(getFroxelIndex(getNormalizedPhysicalViewportCoord()));
    }

    // Each froxel contains how many lights can influence
    // the current fragment. A froxel also contains a record offset that
    // tells us where the indices of those lights are in the records
    // buffer. The records buffer contains the indices of the actual
    // light data in the punctualLightsUniforms UBO.

    uint index = froxel.recordOffset;
    uint end = index + froxel.count;
    uint channels = object_uniforms.lightChannels;
    uint lightId = object_uniforms.lightId;
    uint worldId = object_uniforms.worldId;

    // Iterate point lights
    for ( ; index < end; index++) {
        uint lightIndex = getLightIndex(index);
        Light light = getLight(lightIndex);

        if(WORLD_ENTITY_PASS){
            if(light.worldId == 0u || light.worldId != object_uniforms.worldId){
                continue;
            }
        }else{
            if(light.worldId != 0u){
                continue;
            }
        }

        if ((light.channels & channels) == 0u) {
            continue;
        }

        if (light.id != 0u && light.id != lightId) {
            continue;
        }

#if defined(MATERIAL_CAN_SKIP_LIGHTING)
        if (light.NoL <= 0.0 || light.attenuation <= 0.0) {
            continue;
        }
#elif defined(MATERIAL_CAN_SKIP_LIGHTING_CLOTH)
        if (!MATERIAL_HAS_SUBSURFACE_COLOR) {
            if (light.NoL <= 0.0 || light.attenuation <= 0.0) {
                continue;
            }
        }
#endif

    float visibility = 1.0;
    if (HAS_SHADOWING) {
        visibility = evaluatePunctualLightVisibility(light);
        #if defined(MATERIAL_CAN_SKIP_LIGHTING)
            if (visibility <= 0.0) {
                continue;
            }
        #elif defined(MATERIAL_CAN_SKIP_LIGHTING_CLOTH)
            if (!MATERIAL_HAS_SUBSURFACE_COLOR) {
                if (visibility <= 0.0) {
                        continue;
                }
            }
        #endif
    }

    if (MATERIAL_HAS_AMBIENT_OCCLUSION) {
        visibility *= computeMicroShadowing(light.NoL, material.ambientOcclusion);
    }

    #if defined(MATERIAL_HAS_CUSTOM_SURFACE_SHADING)
        color.rgb += customSurfaceShading(material, pixel, light, visibility);
    #else
        color.rgb += surfaceShading(pixel, light, visibility);
    #endif
    }
}

#endif
