#ifndef _ENGINE_MATERIAL_DEBUG_FS
#define _ENGINE_MATERIAL_DEBUG_FS

#define RENDER_DEBUG_MODE_NONE 0
#define RENDER_DEBUG_MODE_WORLD_NORMAL 1
#define RENDER_DEBUG_MODE_VERTEX_UV0 2
#define RENDER_DEBUG_MODE_BASE_COLOR 3
#define RENDER_DEBUG_MODE_OPACITY_ALPHA 4
#define RENDER_DEBUG_MODE_LIGHTING 5
#define RENDER_DEBUG_MODE_CHECKER_UV0 6
#define RENDER_DEBUG_MODE_DERIVED_METALLIC 7
#define RENDER_DEBUG_MODE_DERIVED_SPECULAR 8
#define RENDER_DEBUG_MODE_DERIVED_ROUGHNESS 9
#define RENDER_DEBUG_MODE_DERIVED_AMBIENT_OCCLUSION 10
#define RENDER_DEBUG_MODE_DERIVED_EMISSIVE 11
#define RENDER_DEBUG_MODE_DERIVED_CLEARCOAT 12
#define RENDER_DEBUG_MODE_DERIVED_CLEARCOAT_ROUGHNESS 13

void resetMaterial(out MaterialInputs material) {
    material.baseColor = vec4(1.0);
#if !defined(SHADING_MODEL_UNLIT)
#if !defined(SHADING_MODEL_SPECULAR_GLOSSINESS)
    material.roughness = 1.0;
#endif
#if !defined(SHADING_MODEL_CLOTH) && !defined(SHADING_MODEL_SPECULAR_GLOSSINESS)
    material.metallic = 0.0;
    material.reflectance = 0.5;
#endif
    material.ambientOcclusion = 1.0;
#endif
    material.emissive = vec4(vec3(0.0), 1.0);

#if !defined(SHADING_MODEL_CLOTH) && !defined(SHADING_MODEL_SUBSURFACE) && !defined(SHADING_MODEL_UNLIT)
if (MATERIAL_HAS_SHEEN_COLOR) {
    material.sheenColor = vec3(0.0);
    material.sheenRoughness = 0.0;
}
#endif

if (MATERIAL_HAS_CLEAR_COAT) {
    material.clearCoat = 0.0;
    material.clearCoatRoughness = 1.0;
}

if (MATERIAL_HAS_ANISOTROPY) {
    material.anisotropy = 0.0;
    material.anisotropyDirection = vec3(1.0, 0.0, 0.0);
}

#if defined(SHADING_MODEL_SUBSURFACE) || defined(MATERIAL_HAS_REFRACTION)
    material.thickness = 0.5;
#endif
#if defined(SHADING_MODEL_SUBSURFACE)
    material.subsurfacePower = 12.234;
    material.subsurfaceColor = vec3(1.0);
#endif

#if defined(SHADING_MODEL_CLOTH)
    material.sheenColor = sqrt(material.baseColor.rgb);
    material.subsurfaceColor = vec3(0.0);
#endif

#if defined(SHADING_MODEL_SPECULAR_GLOSSINESS)
    material.glossiness = 0.0;
    material.specularColor = vec3(0.0);
#endif

if (MATERIAL_HAS_NORMAL) {
    material.normal = vec3(0.0, 0.0, 1.0);
}

//if (MATERIAL_HAS_BENT_NORMAL) {
//    material.bentNormal = vec3(0.0, 0.0, 1.0);
//}

if (MATERIAL_HAS_CLEAR_COAT_NORMAL) {
    material.clearCoatNormal = vec3(0.0, 0.0, 1.0);
}

//if (MATERIAL_HAS_POST_LIGHTING_COLOR) {
//    material.postLightingColor = vec4(0.0);
//}

#if !defined(SHADING_MODEL_CLOTH) && !defined(SHADING_MODEL_SUBSURFACE) && !defined(SHADING_MODEL_UNLIT)
#if defined(MATERIAL_HAS_REFRACTION)
//if (MATERIAL_HAS_ABSORPTION){
//    material.absorption = vec3(0.0);
//}

//if (MATERIAL_HAS_TRANSMISSION) {
//    material.transmission = 1.0;
//}

//if (MATERIAL_HAS_IOR) {
//    material.ior = 1.5;
//}

#if(REFRACTION_TYPE == REFRACTION_TYPE_THIN)
//if (MATERIAL_HAS_THICKNESS) {
//    material.microThickness = 0.0;
//}
#endif
#elif !defined(SHADING_MODEL_SPECULAR_GLOSSINESS)
    material.ior = 1.5;
#endif
#endif
}

vec4 debugMaterial(inout MaterialInputs material) {
    vec4 color = vec4(0.0, 0.0, 0.0, 1.0);
    if (getRenderDebugMode() == RENDER_DEBUG_MODE_WORLD_NORMAL) {
    #if defined(HAS_ATTRIBUTE_NORMAL)
        color = vec4(getWorldNormalVector() * 0.5 + 0.5, 1.0);
    #endif
    }
    else if (getRenderDebugMode() == RENDER_DEBUG_MODE_VERTEX_UV0) {
    #if defined(HAS_ATTRIBUTE_UV0)
        vec2 uv = getUV0();
        color = vec4(uv.x, uv.y, 0.0, 1.0);
    #endif
    }
    else if (getRenderDebugMode() == RENDER_DEBUG_MODE_BASE_COLOR) {
        color = material.baseColor;
        color.a = computeOutputAlpha(color.a);
    }
    else if (getRenderDebugMode() == RENDER_DEBUG_MODE_OPACITY_ALPHA) {
        color = vec4(material.baseColor.a, material.baseColor.a, material.baseColor.a, 1.0);
    }
    else if (getRenderDebugMode() == RENDER_DEBUG_MODE_LIGHTING) {
        resetMaterial(material);
        color = evaluateMaterial(material);
    }
    else if (getRenderDebugMode() == RENDER_DEBUG_MODE_CHECKER_UV0) {
    #if defined(HAS_ATTRIBUTE_UV0)
        color = vec4(TEXTURE_FROM_VARIANT(materialParams_debugViewCheckerUV, getUV0().xy));
    #endif
    }
    else if (getRenderDebugMode() == RENDER_DEBUG_MODE_DERIVED_METALLIC) {
    #if !defined(SHADING_MODEL_CLOTH) && !defined(SHADING_MODEL_UNLIT)
        #if !defined(SHADING_MODEL_SPECULAR_GLOSSINESS)
        color = vec4(material.metallic, material.metallic, material.metallic, 1.0);
        #else
        float metallic = computeMetallicFromSpecularColor(material.specularColor);
        color = vec4(metallic, metallic, metallic, 1.0);
        #endif
    #endif
    }
    else if (getRenderDebugMode() == RENDER_DEBUG_MODE_DERIVED_SPECULAR) {
    #if defined(SHADING_MODEL_SPECULAR_GLOSSINESS)
        color = vec4(material.specularColor, 1.0);
    #endif
    }
    else if (getRenderDebugMode() == RENDER_DEBUG_MODE_DERIVED_ROUGHNESS) {
    #if !defined(SHADING_MODEL_UNLIT)
        #if !defined(SHADING_MODEL_SPECULAR_GLOSSINESS)
        color = vec4(material.roughness, material.roughness, material.roughness, 1.0);
        #else
        float roughness = 1 - material.glossiness;
        color = vec4(roughness, roughness, roughness, 1.0);
        #endif
    #endif
    }
    else if (getRenderDebugMode() == RENDER_DEBUG_MODE_DERIVED_AMBIENT_OCCLUSION) {
    #if !defined(SHADING_MODEL_UNLIT)
        color = vec4(material.ambientOcclusion, material.ambientOcclusion, material.ambientOcclusion, 1.0);
    #endif
    }
    else if (getRenderDebugMode() == RENDER_DEBUG_MODE_DERIVED_EMISSIVE) {
        color = vec4(material.emissive.rgb, 1.0);
    }
    else if (getRenderDebugMode() == RENDER_DEBUG_MODE_DERIVED_CLEARCOAT) {
    #if !defined(SHADING_MODEL_CLOTH) && !defined(SHADING_MODEL_SUBSURFACE) && !defined(SHADING_MODEL_UNLIT)
        color = vec4(material.clearCoat, material.clearCoat, material.clearCoat, 1.0);
    #endif
    }
    else if (getRenderDebugMode() == RENDER_DEBUG_MODE_DERIVED_CLEARCOAT_ROUGHNESS) {
    #if !defined(SHADING_MODEL_CLOTH) && !defined(SHADING_MODEL_SUBSURFACE) && !defined(SHADING_MODEL_UNLIT)
        color = vec4(material.clearCoatRoughness, material.clearCoatRoughness, material.clearCoatRoughness, 1.0);
    #endif
    }

    if(getGamutMappingOutput()){
        color = max(workingSpace_To_OutputSpace(color), vec4(0.0));
    }

    return color;
}

#endif