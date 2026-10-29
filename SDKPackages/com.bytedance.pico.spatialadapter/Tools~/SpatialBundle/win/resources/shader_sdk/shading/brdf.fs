#ifndef _ENGINE_BRDF_FS
#define _ENGINE_BRDF_FS

//------------------------------------------------------------------------------
// BRDF configuration
//------------------------------------------------------------------------------

// Diffuse BRDFs
#define DIFFUSE_LAMBERT             0
#define DIFFUSE_BURLEY              1

// Specular BRDF
// Normal distribution functions
#define SPECULAR_D_GGX              0

// Anisotropic NDFs
#define SPECULAR_D_GGX_ANISOTROPIC  0

// Cloth NDFs
#define SPECULAR_D_CHARLIE          0

// Visibility functions
#define SPECULAR_V_SMITH_GGX        0
#define SPECULAR_V_SMITH_GGX_FAST   1
#define SPECULAR_V_GGX_ANISOTROPIC  2
#define SPECULAR_V_KELEMEN          3
#define SPECULAR_V_NEUBELT          4

// Fresnel functions
#define SPECULAR_F_SCHLICK          0

#define BRDF_DIFFUSE                DIFFUSE_LAMBERT

#if FILAMENT_QUALITY < FILAMENT_QUALITY_HIGH
#define BRDF_SPECULAR_D             SPECULAR_D_GGX
#define BRDF_SPECULAR_V             SPECULAR_V_SMITH_GGX_FAST
#define BRDF_SPECULAR_F             SPECULAR_F_SCHLICK
#else
#define BRDF_SPECULAR_D             SPECULAR_D_GGX
#define BRDF_SPECULAR_V             SPECULAR_V_SMITH_GGX
#define BRDF_SPECULAR_F             SPECULAR_F_SCHLICK
#endif

#define BRDF_CLEAR_COAT_D           SPECULAR_D_GGX
#define BRDF_CLEAR_COAT_V           SPECULAR_V_KELEMEN

#define BRDF_ANISOTROPIC_D          SPECULAR_D_GGX_ANISOTROPIC
#define BRDF_ANISOTROPIC_V          SPECULAR_V_GGX_ANISOTROPIC

#define BRDF_CLOTH_D                SPECULAR_D_CHARLIE
#define BRDF_CLOTH_V                SPECULAR_V_NEUBELT

//------------------------------------------------------------------------------
// Specular BRDF implementations
//------------------------------------------------------------------------------

half D_GGX(half roughness, half NoH, const half3 h) {
    // Walter et al. 2007, "Microfacet Models for Refraction through Rough Surfaces"
    // In TARGET_MOBILE 
    // vec3 NxH = cross(shading_normal, h);
    // float oneMinusNoHSquared = dot(NxH, NxH);
    // has accuracy issues

    highp float oneMinusNoHSquared = 1.0 - NoH * NoH;
    // Cut off the minimum roughness to avoid highlight loss
    float alpha = max(roughness, 1e-3);
    float a = NoH * alpha;
    float k = alpha / max(FLT_MIN, oneMinusNoHSquared + a * a);

    float d = k * k * (1.0 / PI);
    return saturateMediump(d);
}

half D_GGX_Anisotropic(half at, half ab, half ToH, half BoH, half NoH) {
    // Burley 2012, "Physically-Based Shading at Disney"

    // The values at and ab are perceptualRoughness^2, a2 is therefore perceptualRoughness^4
    // The dot product below computes perceptualRoughness^8. We cannot fit in fp16 without clamping
    // the roughness to too high values so we perform the dot product and the division in fp32
    float a2 = at * ab;
    highp vec3 d = vec3(ab * ToH, at * BoH, a2 * NoH);
    highp float d2 = dot(d, d);
    float b2 = (a2 * a2) / max(d2 * d2, FLT_MIN);
    return a2 * b2 * (1.0 / PI);
}

half D_Charlie(half roughness, half NoH) {
    // Estevez and Kulla 2017, "Production Friendly Microfacet Sheen BRDF"
    half invAlpha  = 1.0 / roughness;
    half cos2h = NoH * NoH;
    half sin2h = max(1.0 - cos2h, 0.0078125); // 2^(-14/2), so sin2h^2 > 0 in fp16
    return (2.0 + invAlpha) * pow(sin2h, invAlpha * 0.5) / (2.0 * PI);
}

half V_SmithGGXCorrelated(half roughness, half NoV, half NoL) {
    // Heitz 2014, "Understanding the Masking-Shadowing Function in Microfacet-Based BRDFs"
    half a2 = roughness * roughness;
    // TODO: lambdaV can be pre-computed for all the lights, it should be moved out of this function
    half lambdaV = NoL * sqrt((NoV - a2 * NoV) * NoV + a2);
    half lambdaL = NoV * sqrt((NoL - a2 * NoL) * NoL + a2);
    half v = 0.5 / (lambdaV + lambdaL);
    // a2=0 => v = 1 / 4*NoL*NoV   => min=1/4, max=+inf
    // a2=1 => v = 1 / 2*(NoL+NoV) => min=1/4, max=+inf
    // clamp to the maximum value representable in mediump
    return saturateMediump(v);
}

half V_SmithGGXCorrelated_Fast(half roughness, half NoV, half NoL) {
    // Hammon 2017, "PBR Diffuse Lighting for GGX+Smith Microsurfaces"
    half v = 0.5 / mix(2.0 * NoL * NoV, NoL + NoV, roughness);
    return saturateMediump(v);
}

half V_SmithGGXCorrelated_Anisotropic(half at, half ab, half ToV, half BoV,
        half ToL, half BoL, half NoV, half NoL) {
    // Heitz 2014, "Understanding the Masking-Shadowing Function in Microfacet-Based BRDFs"
    // TODO: lambdaV can be pre-computed for all the lights, it should be moved out of this function
    half lambdaV = NoL * length(vec3(at * ToV, ab * BoV, NoV));
    half lambdaL = NoV * length(vec3(at * ToL, ab * BoL, NoL));
    half v = 0.5 / (lambdaV + lambdaL);
    return saturateMediump(v);
}

half V_Kelemen(half LoH) {
    // Kelemen 2001, "A Microfacet Based Coupled Specular-Matte BRDF Model with Importance Sampling"
    return saturateMediump(0.25 / max(FLT_EPS, LoH * LoH));
}

half V_Neubelt(half NoV, half NoL) {
    // Neubelt and Pettineo 2013, "Crafting a Next-gen Material Pipeline for The Order: 1886"
    return saturateMediump(1.0 / (4.0 * (NoL + NoV - NoL * NoV)));
}

half3 F_Schlick(const half3 f0, half f90, half VoH) {
    // Schlick 1994, "An Inexpensive BRDF Model for Physically-Based Rendering"
    return f0 + (f90 - f0) * pow5(1.0 - VoH);
}

half3 F_Schlick(const half3 f0, half VoH) {
    half f = pow(1.0 - VoH, 5.0);
    return f + f0 * (1.0 - f);
}

half F_Schlick(half f0, half f90, half VoH) {
    return f0 + (f90 - f0) * pow5(1.0 - VoH);
}

half GGX_D(half roughness, half NdotH){
    return (roughness*roughness)/ pow((NdotH * NdotH * (roughness * roughness- 1.0) + 1.00001f), 2.0);
}

half GGX_VF(half LdotH, half roughness){
    return 1.0/ ( LdotH * LdotH * (roughness + 0.5));
}

//https://community.arm.com/cfs-file/__key/communityserver-blogs-components-weblogfiles/00-00-00-20-66/siggraph2015_2D00_mmg_2D00_renaldas_2D00_slides.pdf
half DirectBRDFSpecular(half NoH, half LoH, half roughness)
{
    half roughness2 = roughness * roughness;
    float d = NoH * NoH * (roughness2 - 1.0) + 1.00001f;

    half LoH2 = LoH * LoH;
    half specularTerm = roughness2 / ((d * d) * max(0.1, LoH2) * (roughness * 4.0 + 2.0));
    return specularTerm;
}

//close to URP SimpleLit
float BlinnPhong(float roughness, float NoH)
{
    half glossiness = 1.0 - roughness;
    half smoothness = exp2(10.0 * glossiness + 1.0);
    half modifier = pow(NoH, smoothness);
    return modifier;
}

//------------------------------------------------------------------------------
// Specular BRDF dispatch
//------------------------------------------------------------------------------

half distribution(half roughness, half NoH, const half3 h) {
#if BRDF_SPECULAR_D == SPECULAR_D_GGX
    return D_GGX(roughness, NoH, h);
#endif
}

half visibility(half roughness, half NoV, half NoL) {
#if BRDF_SPECULAR_V == SPECULAR_V_SMITH_GGX
    return V_SmithGGXCorrelated(roughness, NoV, NoL);
#elif BRDF_SPECULAR_V == SPECULAR_V_SMITH_GGX_FAST
    return V_SmithGGXCorrelated_Fast(roughness, NoV, NoL);
#endif
}

half3 fresnel(const half3 f0, half LoH) {
#if BRDF_SPECULAR_F == SPECULAR_F_SCHLICK
#if FILAMENT_QUALITY == FILAMENT_QUALITY_LOW
    return F_Schlick(f0, LoH); // f90 = 1.0
#else
    half f90 = saturate(dot(f0, vec3(50.0 * 0.33)));
    return F_Schlick(f0, f90, LoH);
#endif
#endif
}

half distributionAnisotropic(half at, half ab, half ToH, half BoH, half NoH) {
#if BRDF_ANISOTROPIC_D == SPECULAR_D_GGX_ANISOTROPIC
    return D_GGX_Anisotropic(at, ab, ToH, BoH, NoH);
#endif
}

half visibilityAnisotropic(half roughness, half at, half ab,
        half ToV, half BoV, half ToL, half BoL, half NoV, half NoL) {
#if BRDF_ANISOTROPIC_V == SPECULAR_V_SMITH_GGX
    return V_SmithGGXCorrelated(roughness, NoV, NoL);
#elif BRDF_ANISOTROPIC_V == SPECULAR_V_GGX_ANISOTROPIC
    return V_SmithGGXCorrelated_Anisotropic(at, ab, ToV, BoV, ToL, BoL, NoV, NoL);
#endif
}

half distributionClearCoat(half roughness, half NoH, const half3 h) {
#if BRDF_CLEAR_COAT_D == SPECULAR_D_GGX
    return D_GGX(roughness, NoH, h);
#endif
}

half visibilityClearCoat(half LoH) {
#if BRDF_CLEAR_COAT_V == SPECULAR_V_KELEMEN
    return V_Kelemen(LoH);
#endif
}

half distributionCloth(half roughness, half NoH) {
#if BRDF_CLOTH_D == SPECULAR_D_CHARLIE
    return D_Charlie(roughness, NoH);
#endif
}

half visibilityCloth(half NoV, half NoL) {
#if BRDF_CLOTH_V == SPECULAR_V_NEUBELT
    return V_Neubelt(NoV, NoL);
#endif
}

//------------------------------------------------------------------------------
// Diffuse BRDF implementations
//------------------------------------------------------------------------------

half Fd_Lambert() {
    return 1.0 / PI;
}

half Fd_Burley(half roughness, half NoV, half NoL, half LoH) {
    // Burley 2012, "Physically-Based Shading at Disney"
    half f90 = 0.5 + 2.0 * roughness * LoH * LoH;
    half lightScatter = F_Schlick(1.0, f90, NoL);
    half viewScatter  = F_Schlick(1.0, f90, NoV);
    return lightScatter * viewScatter * (1.0 / PI);
}

// Energy conserving wrap diffuse term, does *not* include the divide by pi
half Fd_Wrap(half NoL, half w) {
    return saturate((NoL + w) / sq(1.0 + w));
}

//------------------------------------------------------------------------------
// Diffuse BRDF dispatch
//------------------------------------------------------------------------------

half diffuse(half roughness, half NoV, half NoL, half LoH) {
    if (SHADER_LOD_0 && !USE_SIMPLE_LIT) {
        #if BRDF_DIFFUSE == DIFFUSE_LAMBERT
            return Fd_Lambert();
        #else
            return Fd_Burley(roughness, NoV, NoL, LoH);
        #endif
    }
    else{
        // return Fd_Lambert(NoL);
        return Fd_Lambert();
    }
}

#endif
