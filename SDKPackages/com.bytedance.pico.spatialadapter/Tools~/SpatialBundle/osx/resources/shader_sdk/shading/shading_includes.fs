#ifndef _ENGINE_SHADING_MODEL_INCLUDES_FS
#define _ENGINE_SHADING_MODEL_INCLUDES_FS

#if defined(SHADING_MODEL_UNLIT)
    #include "engine/shading/shadowing.fs"
    #include "engine/shading/shading_unlit.fs"
#else
    #include "engine/shading/brdf.fs"
    #include "engine/common/common_lighting.fs"
    #include "engine/shading/shadowing.fs"
    #if defined(SHADING_MODEL_LIT) || defined(SHADING_MODEL_SPECULAR_GLOSSINESS)
        #if defined(MATERIAL_HAS_CUSTOM_SURFACE_SHADING)
            #include "engine/shading/shading_lit_custom.fs"
        #else
            #include "engine/shading/shading_model_standard.fs"
        #endif
    #elif defined(SHADING_MODEL_SUBSURFACE)
        #include "engine/shading/shading_model_subsurface.fs"
    #elif defined(SHADING_MODEL_CLOTH)
        #include "engine/shading/shading_model_cloth.fs"
    #endif
    #include "engine/shading/light_indirect.fs"
    #include "engine/shading/light_directional.fs"
    #include "engine/shading/light_punctual.fs"
    #include "engine/shading/shading_lit.fs"
#endif

#endif