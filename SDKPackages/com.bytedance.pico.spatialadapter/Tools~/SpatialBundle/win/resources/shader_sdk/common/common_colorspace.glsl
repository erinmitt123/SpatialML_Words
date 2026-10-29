#ifndef _ENGINE_COMMON_COLORSPACE_GLSL
#define _ENGINE_COMMON_COLORSPACE_GLSL

#include "engine/common/common_defines.glsl"

const half3x3 sRGB_To_XYZ = mat3(
    0.4124560f,  0.2126730f,  0.0193339f,
    0.3575760f,  0.7151520f,  0.1191920f,
    0.1804380f,  0.0721750f,  0.9503040f
);

const half3x3 XYZ_To_sRGB = mat3(
    3.2404542f, -0.9692660f,  0.0556434f,
   -1.5371385f,  1.8760108f, -0.2040259f,
   -0.4985314f,  0.0415560f,  1.0572252f
);

const half3x3 P3_To_XYZ = mat3(
    0.4865709f,  0.2289746f,  0.0000000f,
    0.2656677f,  0.6917385f,  0.0451134f,
    0.1982173f,  0.0792869f,  1.0439444f
);

const half3x3 XYZ_To_P3 = mat3(
    2.4934969f, -0.8294890f,  0.0358458f,
   -0.9313836f,  1.7626641f, -0.0761724f,
   -0.4027108f,  0.0236247f,  0.9568845f
);

const half3x3 Rec2020_To_XYZ = mat3(
    0.6369530f,  0.2626983f,  0.0000000f,
    0.1446169f,  0.6780088f,  0.0280731f,
    0.1688558f,  0.0592929f,  1.0608272f
);

const half3x3 XYZ_To_Rec2020 = mat3(
    1.7166634f, -0.6666738f,  0.0176425f,
   -0.3556733f,  1.6164557f, -0.0427770f,
   -0.2533681f,  0.0157683f,  0.9422433f
);

const half3x3 AP0_To_XYZ = mat3(
    0.9525523959f,  0.0000000000f,  0.0000936786f,
    0.3439664498f,  0.7281660966f, -0.0721325464f,
    0.0000000000f,  0.0000000000f,  1.0088251844f
);

const half3x3 XYZ_To_AP0 = mat3(
    1.0498110175f,  0.0000000000f, -0.0000974845f,
   -0.4959030231f,  1.3733130458f,  0.0982400361f,
    0.0000000000f,  0.0000000000f,  0.9912520182f
);

const half3x3 AP1_To_XYZ = mat3(
    0.6624541811f,  0.2722287168f, -0.0055746495f,
    0.1340042065f,  0.6740817658f,  0.0040607335f,
    0.1561876870f,  0.0536895174f,  1.0103391003f
);

const half3x3 XYZ_To_AP1 = mat3(
    1.6410233797f, -0.6636628587f,  0.0117218943f,
   -0.3248032942f,  1.6153315917f, -0.0082844420f,
   -0.2364246952f,  0.0167563477f,  0.9883948585f
);

const half3x3 D65_To_D60 = mat3(
    1.0130349f,  0.0076982f, -0.0028413f,
    0.0061053f,  0.9981634f,  0.0046852f,
   -0.0149709f, -0.0050320f,  0.9245061f
);

const half3x3 D60_To_D65 = mat3(
    0.9872240f, -0.0075984f,  0.0030726f,
   -0.0061132f,  1.0018615f, -0.0050960f,
    0.0159533f,  0.0053300f,  1.0816806f
);

const half3x3 D65_To_DCI = mat3(
    0.9765789f, -0.0256897f, -0.0057057f,
   -0.0154363f,  1.0285392f,  0.0110779f,
   -0.0166860f, -0.0037852f,  0.8711762f
);

const half3x3 DCI_To_D65 = mat3(
    1.0244967f,  0.0256122f,  0.0063842f,
    0.0151635f,  0.9725863f, -0.0122681f,
    0.0196885f,  0.0047164f,  1.1479424f
);

const half3x3 D60_To_DCI = mat3(
    0.9641682f, -0.0331883f, -0.0030403f,
   -0.0213500f,  1.0306301f,  0.0066939f,
   -0.0025516f,  0.0009780f,  0.9423024f
);

const half3x3 DCI_To_D60 = mat3(
    1.0379117f,  0.0334198f,  0.0031113f,
    0.0214828f,  0.9709785f, -0.0068283f,
    0.0027882f, -0.0009172f,  1.0612460f
);

const half3x3 sRGB_To_P3 = XYZ_To_P3 * sRGB_To_XYZ;
const half3x3 P3_To_sRGB = XYZ_To_sRGB * P3_To_XYZ;
const half3x3 sRGB_To_Rec2020 = XYZ_To_Rec2020 * sRGB_To_XYZ;
const half3x3 Rec2020_To_sRGB = XYZ_To_sRGB * Rec2020_To_XYZ;
const half3x3 P3_To_Rec2020 = XYZ_To_Rec2020 * P3_To_XYZ;
const half3x3 Rec2020_To_P3 = XYZ_To_P3 * Rec2020_To_XYZ;
const half3x3 sRGB_To_AP1 = XYZ_To_AP1 * D65_To_D60 * sRGB_To_XYZ;
const half3x3 AP1_To_sRGB = XYZ_To_sRGB * D60_To_D65 * AP1_To_XYZ;

const half3 LUMINANCE_sRGB = vec3(0.2126729, 0.7151522, 0.0721750);
const half3 LUMINANCE_P3 = vec3(0.2289746, 0.6917385, 0.0792869);
const half3 LUMINANCE_Rec2020 = vec3(0.2627002, 0.6779981, 0.0593017);
const half3 LUMINANCE_AP1 = vec3(0.2722287168, 0.6740817658, 0.0536895174);

const half3 ILLUMINANT_D65 = vec3(0.95047, 1.0, 1.08883);
const half3 ILLUMINANT_D60 = vec3(0.95265, 1.0, 1.00883);
const half3 ILLUMINANT_DCI = vec3(0.89459, 1.0, 0.95442);

const half MIDDLE_GRAY_ACEScg = 0.18;
const half MIDDLE_GRAY_ACEScct = 0.4135884;

half3 linearSRGB_To_ACEScg(half3 color) {
    return XYZ_To_AP1 * D65_To_D60 * sRGB_To_XYZ * color;
}

half3 ACEScg_To_LinearSRGB(half3 color) {
    return XYZ_To_sRGB * D60_To_D65 * AP1_To_XYZ * color;
}

half3 linearSRGB_To_LinearP3(half3 color) {
    return sRGB_To_P3 * color;
}

half3 linearP3_To_LinearSRGB(half3 color) {
    return P3_To_sRGB * color;
}

half3 linearSRGB_To_Rec2020(half3 color) {
    return sRGB_To_Rec2020 * color;
}

half3 linearRec2020_To_LinearSRGB(half3 color) {
    return Rec2020_To_sRGB * color;
}

half3 linearP3_To_Rec2020(half3 color) {
    return P3_To_Rec2020 * color;
}

half3 linearRec2020_To_LinearP3(half3 color) {
    return Rec2020_To_P3 * color;
}

half3 linearSRGB_To_AP1(half3 color) {
    return sRGB_To_AP1 * color;
}

half3 AP1_To_LinearSRGB(half3 color) {
    return AP1_To_sRGB * color;
}

half3 XYZ_To_xyY(half3 XYZ) {
    half sum = XYZ.x + XYZ.y + XYZ.z;
    return vec3(XYZ.x / sum, XYZ.y / sum, XYZ.y);
}

half3 xyY_To_XYZ(half3 xyY) {
    half X = (xyY.x * xyY.z) / xyY.y;
    half Z = ((1.0 - xyY.x - xyY.y) * xyY.z) / xyY.y;
    return vec3(X, xyY.z, Z);
}

highp mat3 getXYZToWorkingColorSpaceMatrix() {
    return frameUniforms.xyzToWorkingColorSpaceMatrix;
}

highp mat3 getWorkingColorSpaceToXYZMatrix() {
    return frameUniforms.workingColorSpaceToXYZMatrix;
}

highp mat3 getWorkingColorSpaceToOutputColorSpaceMatrix() {
    return frameUniforms.workingColorSpaceToOutputColorSpaceMatrix;
}

highp mat3 getOutputColorSpaceToWorkingColorSpaceMatrix() {
    return frameUniforms.outputColorSpaceToWorkingColorSpaceMatrix;
}

half linearSRGB_To_WorkingSpace(half color) {
    return color;
}

half linearDisplayP3_To_WorkingSpace(half color) {
    return color;
}

half linearDCIP3_To_WorkingSpace(half color) {
    return color;
}

half linearRec2020_To_WorkingSpace(half color) {
    return color;
}

half AP0_To_WorkingSpace(half color) {
    return color;
}

half AP1_To_WorkingSpace(half color) {
    return color;
}

half XYZ_To_WorkingSpace(half color) {
    return color;
}

half workingSpace_To_LinearSRGB(half color) {
    return color;
}

half workingSpace_To_LinearP3(half color) {
    return color;
}

half workingSpace_To_Rec2020(half color) {
    return color;
}

half workingSpace_To_AP0(half color) {
    return color;
}

half workingSpace_To_AP1(half color) {
    return color;
}

half workingSpace_To_XYZ(half color) {
    return color;
}

half workingSpace_To_OutputSpace(half color) {
    return color;
}

half outputSpace_To_WorkingSpace(half color) {
    return color;
}

half3 linearSRGB_To_WorkingSpace(half3 color) {
    return getXYZToWorkingColorSpaceMatrix() * sRGB_To_XYZ * color;
}

half3 linearDisplayP3_To_WorkingSpace(half3 color) {
    return getXYZToWorkingColorSpaceMatrix() * P3_To_XYZ * color;
}

half3 linearDCIP3_To_WorkingSpace(half3 color) {
    return getXYZToWorkingColorSpaceMatrix() * DCI_To_D65 * P3_To_XYZ * color;
}

half3 linearRec2020_To_WorkingSpace(half3 color) {
    return getXYZToWorkingColorSpaceMatrix() * Rec2020_To_XYZ * color;
}

half3 AP0_To_WorkingSpace(half3 color) {
    return getXYZToWorkingColorSpaceMatrix() * D60_To_D65 * AP0_To_XYZ * color;
}

half3 AP1_To_WorkingSpace(half3 color) {
    return getXYZToWorkingColorSpaceMatrix() * D60_To_D65 * AP1_To_XYZ * color;
}

half3 XYZ_To_WorkingSpace(half3 color) {
    return getXYZToWorkingColorSpaceMatrix() * color;
}

half3 workingSpace_To_LinearSRGB(half3 color) {
    return XYZ_To_sRGB * getWorkingColorSpaceToXYZMatrix() * color;
}

half3 workingSpace_To_LinearP3(half3 color) {
    return XYZ_To_P3 * getWorkingColorSpaceToXYZMatrix() * color;
}

half3 workingSpace_To_Rec2020(half3 color) {
    return XYZ_To_Rec2020 * getWorkingColorSpaceToXYZMatrix() * color;
}

half3 workingSpace_To_AP0(half3 color) {
    return XYZ_To_AP0 * D65_To_D60 * getWorkingColorSpaceToXYZMatrix() * color;
}

half3 workingSpace_To_AP1(half3 color) {
    return XYZ_To_AP1 * D65_To_D60 * getWorkingColorSpaceToXYZMatrix() * color;
}

half3 workingSpace_To_XYZ(half3 color) {
    return getWorkingColorSpaceToXYZMatrix() * color;
}

half3 workingSpace_To_OutputSpace(half3 color) {
    return getWorkingColorSpaceToOutputColorSpaceMatrix() * color;
}

half3 outputSpace_To_WorkingSpace(half3 color) {
    return getOutputColorSpaceToWorkingColorSpaceMatrix() * color;
}

half4 linearSRGB_To_WorkingSpace(half4 color) {
    return vec4(linearSRGB_To_WorkingSpace(color.rgb), color.a);
}

half4 linearDisplayP3_To_WorkingSpace(half4 color) {
    return vec4(linearDisplayP3_To_WorkingSpace(color.rgb), color.a);
}

half4 linearDCIP3_To_WorkingSpace(half4 color) {
    return vec4(linearDCIP3_To_WorkingSpace(color.rgb), color.a);
}

half4 linearRec2020_To_WorkingSpace(half4 color) {
    return vec4(linearRec2020_To_WorkingSpace(color.rgb), color.a);
}

half4 AP0_To_WorkingSpace(half4 color) {
    return vec4(AP0_To_WorkingSpace(color.rgb), color.a);
}

half4 AP1_To_WorkingSpace(half4 color) {
    return vec4(AP1_To_WorkingSpace(color.rgb), color.a);
}

half4 XYZ_To_WorkingSpace(half4 color) {
    return vec4(XYZ_To_WorkingSpace(color.rgb), color.a);
}

half4 workingSpace_To_LinearSRGB(half4 color) {
    return vec4(workingSpace_To_LinearSRGB(color.rgb), color.a);
}

half4 workingSpace_To_LinearP3(half4 color) {
    return vec4(workingSpace_To_LinearP3(color.rgb), color.a);
}

half4 workingSpace_To_Rec2020(half4 color) {
    return vec4(workingSpace_To_Rec2020(color.rgb), color.a);
}

half4 workingSpace_To_AP0(half4 color) {
    return vec4(workingSpace_To_AP0(color.rgb), color.a);
}

half4 workingSpace_To_AP1(half4 color) {
    return vec4(workingSpace_To_AP1(color.rgb), color.a);
}

half4 workingSpace_To_XYZ(half4 color) {
    return vec4(workingSpace_To_XYZ(color.rgb), color.a);
}

half4 workingSpace_To_OutputSpace(half4 color) {
    return vec4(workingSpace_To_OutputSpace(color.rgb), color.a);
}

half4 outputSpace_To_WorkingSpace(half4 color) {
    return vec4(outputSpace_To_WorkingSpace(color.rgb), color.a);
}

#define LINEAR_TO_WORKING_SPACE(color) (color)
#define SRGB_TO_WORKING_SPACE(color) max(linearSRGB_To_WorkingSpace(color), 0.0)
#define DISPLAY_P3_TO_WORKING_SPACE(color) max(linearDisplayP3_To_WorkingSpace(color), 0.0)
#define DCI_P3_TO_WORKING_SPACE(color) max(linearDCIP3_To_WorkingSpace(color), 0.0)
#define REC2020_TO_WORKING_SPACE(color) max(linearRec2020_To_WorkingSpace(color), 0.0)
#define ACES_CG_TO_WORKING_SPACE(color) max(AP1_To_WorkingSpace(color), 0.0)

vec4 applyColorSpaceFunc(half3x3 matrix, half4 color) {
    return vec4(matrix * color.rgb, color.a);
}
vec3 applyColorSpaceFunc(half3x3 matrix, half3 color) {
    return matrix * color.rgb;
}

#define APPLY_COLORSPACE(name, result) ((getGamutMappingRuntime()) ? applyColorSpaceFunc(materialParams._currentSpaceToWorkingSpaceMatrix_##name, result) : (result))

//sampler func with colorspace parameters
#define TEXTURE_FROM_VARIANT(name, coord) APPLY_COLORSPACE(name, TEXTURE(name, coord))
#define TEXEL_FETCH_FROM_VARIANT(name, coord, lod) APPLY_COLORSPACE(name, TEXEL_FETCH(name, coord, lod))
#define TEXEL_FETCH_OFFSET_FROM_VARIANT(name, coord, lod, offset) APPLY_COLORSPACE(name, TEXEL_FETCH_OFFSET(name, coord, lod, offset))
#define TEXTURE_GATHER_FROM_VARIANT(name, coord, comp) APPLY_COLORSPACE(name, TEXTURE_GATHER(name, coord, comp))
#define TEXTURE_GATHER_OFFSET_FROM_VARIANT(name, coord, comp, offset) APPLY_COLORSPACE(name, TEXTURE_GATHER_OFFSET(name, coord, comp, offset))
#define TEXTURE_GATHER_OFFSETS_FROM_VARIANT(name, coord, comp, offsets) APPLY_COLORSPACE(name, TEXTURE_GATHER_OFFSETS(name, coord, comp, offsets))
#define TEXTURE_GRAD_FROM_VARIANT(name, coord, dPdx, dPdy) APPLY_COLORSPACE(name, TEXTURE_GRAD(name, coord, dPdx, dPdy))
#define TEXTURE_GRAD_OFFSET_FROM_VARIANT(name, coord, dPdx, dPdy, offset) APPLY_COLORSPACE(name, TEXTURE_GRAD_OFFSET(name, coord, dPdx, dPdy, offset))
#define TEXTURE_LOD_FROM_VARIANT(name, coord, lod) APPLY_COLORSPACE(name, TEXTURE_LOD(name, coord, lod))
#define TEXTURE_LOD_OFFSET_FROM_VARIANT(name, coord, lod, offset) APPLY_COLORSPACE(name, TEXTURE_LOD_OFFSET(name, coord, lod, offset))
#define TEXTURE_OFFSET_FROM_VARIANT(name, coord, offset) APPLY_COLORSPACE(name, TEXTURE_OFFSET(name, coord, offset))
#define TEXTURE_PROJ_FROM_VARIANT(name, coord) APPLY_COLORSPACE(name, TEXTURE_PROJ(name, coord))
#define TEXTURE_PROJ_GRAD_FROM_VARIANT(name, coord, dPdx, dPdy) APPLY_COLORSPACE(name, TEXTURE_PROJ_GRAD(name, coord, dPdx, dPdy))
#define TEXTURE_PROJ_GRAD_OFFSET_FROM_VARIANT(name, coord, dPdx, dPdy, offset) APPLY_COLORSPACE(name, TEXTURE_PROJ_GRAD_OFFSET(name, coord, dPdx, dPdy, offset))
#define TEXTURE_PROJ_LOD_FROM_VARIANT(name, coord, lod) APPLY_COLORSPACE(name, TEXTURE_PROJ_LOD(name, coord, lod))
#define TEXTURE_PROJ_LOD_OFFSET_FROM_VARIANT(name, coord, lod, offset) APPLY_COLORSPACE(name, TEXTURE_PROJ_LOD_OFFSET(name, coord, lod, offset))
#define TEXTURE_PROJ_OFFSET_FROM_VARIANT(name, coord, offset) APPLY_COLORSPACE(name, TEXTURE_PROJ_OFFSET(name, coord, offset))

// TEXTURE
#define TEXTURE_FROM_LINEAR(name, coord) LINEAR_TO_WORKING_SPACE(TEXTURE(name, coord))
#define TEXTURE_FROM_SRGB(name, coord) SRGB_TO_WORKING_SPACE(TEXTURE(name, coord))
#define TEXTURE_FROM_DISPLAY_P3(name, coord) DISPLAY_P3_TO_WORKING_SPACE(TEXTURE(name, coord))
#define TEXTURE_FROM_DCI_P3(name, coord) DCI_P3_TO_WORKING_SPACE(TEXTURE(name, coord))
#define TEXTURE_FROM_REC2020(name, coord) REC2020_TO_WORKING_SPACE(TEXTURE(name, coord))
#define TEXTURE_FROM_ACES_CG(name, coord) ACES_CG_TO_WORKING_SPACE(TEXTURE(name, coord))

// TEXEL_FETCH
#define TEXEL_FETCH_FROM_LINEAR(name, coord, lod) LINEAR_TO_WORKING_SPACE(TEXEL_FETCH(name, coord, lod))
#define TEXEL_FETCH_FROM_SRGB(name, coord, lod) SRGB_TO_WORKING_SPACE(TEXEL_FETCH(name, coord, lod))
#define TEXEL_FETCH_FROM_DISPLAY_P3(name, coord, lod) DISPLAY_P3_TO_WORKING_SPACE(TEXEL_FETCH(name, coord, lod))
#define TEXEL_FETCH_FROM_DCI_P3(name, coord, lod) DCI_P3_TO_WORKING_SPACE(TEXEL_FETCH(name, coord, lod))
#define TEXEL_FETCH_FROM_REC2020(name, coord, lod) REC2020_TO_WORKING_SPACE(TEXEL_FETCH(name, coord, lod))
#define TEXEL_FETCH_FROM_ACES_CG(name, coord, lod) ACES_CG_TO_WORKING_SPACE(TEXEL_FETCH(name, coord, lod))

// TEXEL_FETCH_OFFSET
#define TEXEL_FETCH_OFFSET_FROM_LINEAR(name, coord, lod, offset) LINEAR_TO_WORKING_SPACE(TEXEL_FETCH_OFFSET(name, coord, lod, offset))
#define TEXEL_FETCH_OFFSET_FROM_SRGB(name, coord, lod, offset) SRGB_TO_WORKING_SPACE(TEXEL_FETCH_OFFSET(name, coord, lod, offset))
#define TEXEL_FETCH_OFFSET_FROM_DISPLAY_P3(name, coord, lod, offset) DISPLAY_P3_TO_WORKING_SPACE(TEXEL_FETCH_OFFSET(name, coord, lod, offset))
#define TEXEL_FETCH_OFFSET_FROM_DCI_P3(name, coord, lod, offset) DCI_P3_TO_WORKING_SPACE(TEXEL_FETCH_OFFSET(name, coord, lod, offset))
#define TEXEL_FETCH_OFFSET_FROM_REC2020(name, coord, lod, offset) REC2020_TO_WORKING_SPACE(TEXEL_FETCH_OFFSET(name, coord, lod, offset))
#define TEXEL_FETCH_OFFSET_FROM_ACES_CG(name, coord, lod, offset) ACES_CG_TO_WORKING_SPACE(TEXEL_FETCH_OFFSET(name, coord, lod, offset))

// TEXTURE_GATHER
#define TEXTURE_GATHER_FROM_LINEAR(name, coord, comp) LINEAR_TO_WORKING_SPACE(TEXTURE_GATHER(name, coord, comp))
#define TEXTURE_GATHER_FROM_SRGB(name, coord, comp) SRGB_TO_WORKING_SPACE(TEXTURE_GATHER(name, coord, comp))
#define TEXTURE_GATHER_FROM_DISPLAY_P3(name, coord, comp) DISPLAY_P3_TO_WORKING_SPACE(TEXTURE_GATHER(name, coord, comp))
#define TEXTURE_GATHER_FROM_DCI_P3(name, coord, comp) DCI_P3_TO_WORKING_SPACE(TEXTURE_GATHER(name, coord, comp))
#define TEXTURE_GATHER_FROM_REC2020(name, coord, comp) REC2020_TO_WORKING_SPACE(TEXTURE_GATHER(name, coord, comp))
#define TEXTURE_GATHER_FROM_ACES_CG(name, coord, comp) ACES_CG_TO_WORKING_SPACE(TEXTURE_GATHER(name, coord, comp))

// TEXTURE_GATHER_OFFSET
#define TEXTURE_GATHER_OFFSET_FROM_LINEAR(name, coord, comp, offset) LINEAR_TO_WORKING_SPACE(TEXTURE_GATHER_OFFSET(name, coord, comp, offset))
#define TEXTURE_GATHER_OFFSET_FROM_SRGB(name, coord, comp, offset) SRGB_TO_WORKING_SPACE(TEXTURE_GATHER_OFFSET(name, coord, comp, offset))
#define TEXTURE_GATHER_OFFSET_FROM_DISPLAY_P3(name, coord, comp, offset) DISPLAY_P3_TO_WORKING_SPACE(TEXTURE_GATHER_OFFSET(name, coord, comp, offset))
#define TEXTURE_GATHER_OFFSET_FROM_DCI_P3(name, coord, comp, offset) DCI_P3_TO_WORKING_SPACE(TEXTURE_GATHER_OFFSET(name, coord, comp, offset))
#define TEXTURE_GATHER_OFFSET_FROM_REC2020(name, coord, comp, offset) REC2020_TO_WORKING_SPACE(TEXTURE_GATHER_OFFSET(name, coord, comp, offset))
#define TEXTURE_GATHER_OFFSET_FROM_ACES_CG(name, coord, comp, offset) ACES_CG_TO_WORKING_SPACE(TEXTURE_GATHER_OFFSET(name, coord, comp, offset))

// TEXTURE_GATHER_OFFSETS
#define TEXTURE_GATHER_OFFSETS_FROM_LINEAR(name, coord, comp, offsets) LINEAR_TO_WORKING_SPACE(TEXTURE_GATHER_OFFSETS(name, coord, comp, offsets))
#define TEXTURE_GATHER_OFFSETS_FROM_SRGB(name, coord, comp, offsets) SRGB_TO_WORKING_SPACE(TEXTURE_GATHER_OFFSETS(name, coord, comp, offsets))
#define TEXTURE_GATHER_OFFSETS_FROM_DISPLAY_P3(name, coord, comp, offsets) DISPLAY_P3_TO_WORKING_SPACE(TEXTURE_GATHER_OFFSETS(name, coord, comp, offsets))
#define TEXTURE_GATHER_OFFSETS_FROM_DCI_P3(name, coord, comp, offsets) DCI_P3_TO_WORKING_SPACE(TEXTURE_GATHER_OFFSETS(name, coord, comp, offsets))
#define TEXTURE_GATHER_OFFSETS_FROM_REC2020(name, coord, comp, offsets) REC2020_TO_WORKING_SPACE(TEXTURE_GATHER_OFFSETS(name, coord, comp, offsets))
#define TEXTURE_GATHER_OFFSETS_FROM_ACES_CG(name, coord, comp, offsets) ACES_CG_TO_WORKING_SPACE(TEXTURE_GATHER_OFFSETS(name, coord, comp, offsets))

// TEXTURE_GRAD
#define TEXTURE_GRAD_FROM_LINEAR(name, coord, dPdx, dPdy) LINEAR_TO_WORKING_SPACE(TEXTURE_GRAD(name, coord, dPdx, dPdy))
#define TEXTURE_GRAD_FROM_SRGB(name, coord, dPdx, dPdy) SRGB_TO_WORKING_SPACE(TEXTURE_GRAD(name, coord, dPdx, dPdy))
#define TEXTURE_GRAD_FROM_DISPLAY_P3(name, coord, dPdx, dPdy) DISPLAY_P3_TO_WORKING_SPACE(TEXTURE_GRAD(name, coord, dPdx, dPdy))
#define TEXTURE_GRAD_FROM_DCI_P3(name, coord, dPdx, dPdy) DCI_P3_TO_WORKING_SPACE(TEXTURE_GRAD(name, coord, dPdx, dPdy))
#define TEXTURE_GRAD_FROM_REC2020(name, coord, dPdx, dPdy) REC2020_TO_WORKING_SPACE(TEXTURE_GRAD(name, coord, dPdx, dPdy))
#define TEXTURE_GRAD_FROM_ACES_CG(name, coord, dPdx, dPdy) ACES_CG_TO_WORKING_SPACE(TEXTURE_GRAD(name, coord, dPdx, dPdy))

// TEXTURE_GRAD_OFFSET
#define TEXTURE_GRAD_OFFSET_FROM_LINEAR(name, coord, dPdx, dPdy, offset) LINEAR_TO_WORKING_SPACE(TEXTURE_GRAD_OFFSET(name, coord, dPdx, dPdy, offset))
#define TEXTURE_GRAD_OFFSET_FROM_SRGB(name, coord, dPdx, dPdy, offset) SRGB_TO_WORKING_SPACE(TEXTURE_GRAD_OFFSET(name, coord, dPdx, dPdy, offset))
#define TEXTURE_GRAD_OFFSET_FROM_DISPLAY_P3(name, coord, dPdx, dPdy, offset) DISPLAY_P3_TO_WORKING_SPACE(TEXTURE_GRAD_OFFSET(name, coord, dPdx, dPdy, offset))
#define TEXTURE_GRAD_OFFSET_FROM_DCI_P3(name, coord, dPdx, dPdy, offset) DCI_P3_TO_WORKING_SPACE(TEXTURE_GRAD_OFFSET(name, coord, dPdx, dPdy, offset))
#define TEXTURE_GRAD_OFFSET_FROM_REC2020(name, coord, dPdx, dPdy, offset) REC2020_TO_WORKING_SPACE(TEXTURE_GRAD_OFFSET(name, coord, dPdx, dPdy, offset))
#define TEXTURE_GRAD_OFFSET_FROM_ACES_CG(name, coord, dPdx, dPdy, offset) ACES_CG_TO_WORKING_SPACE(TEXTURE_GRAD_OFFSET(name, coord, dPdx, dPdy, offset))

// TEXTURE_LOD
#define TEXTURE_LOD_FROM_LINEAR(name, coord, lod) LINEAR_TO_WORKING_SPACE(TEXTURE_LOD(name, coord, lod))
#define TEXTURE_LOD_FROM_SRGB(name, coord, lod) SRGB_TO_WORKING_SPACE(TEXTURE_LOD(name, coord, lod))
#define TEXTURE_LOD_FROM_DISPLAY_P3(name, coord, lod) DISPLAY_P3_TO_WORKING_SPACE(TEXTURE_LOD(name, coord, lod))
#define TEXTURE_LOD_FROM_DCI_P3(name, coord, lod) DCI_P3_TO_WORKING_SPACE(TEXTURE_LOD(name, coord, lod))
#define TEXTURE_LOD_FROM_REC2020(name, coord, lod) REC2020_TO_WORKING_SPACE(TEXTURE_LOD(name, coord, lod))
#define TEXTURE_LOD_FROM_ACES_CG(name, coord, lod) ACES_CG_TO_WORKING_SPACE(TEXTURE_LOD(name, coord, lod))

// TEXTURE_LOD_OFFSET
#define TEXTURE_LOD_OFFSET_FROM_LINEAR(name, coord, lod, offset) LINEAR_TO_WORKING_SPACE(TEXTURE_LOD_OFFSET(name, coord, lod, offset))
#define TEXTURE_LOD_OFFSET_FROM_SRGB(name, coord, lod, offset) SRGB_TO_WORKING_SPACE(TEXTURE_LOD_OFFSET(name, coord, lod, offset))
#define TEXTURE_LOD_OFFSET_FROM_DISPLAY_P3(name, coord, lod, offset) DISPLAY_P3_TO_WORKING_SPACE(TEXTURE_LOD_OFFSET(name, coord, lod, offset))
#define TEXTURE_LOD_OFFSET_FROM_DCI_P3(name, coord, lod, offset) DCI_P3_TO_WORKING_SPACE(TEXTURE_LOD_OFFSET(name, coord, lod, offset))
#define TEXTURE_LOD_OFFSET_FROM_REC2020(name, coord, lod, offset) REC2020_TO_WORKING_SPACE(TEXTURE_LOD_OFFSET(name, coord, lod, offset))
#define TEXTURE_LOD_OFFSET_FROM_ACES_CG(name, coord, lod, offset) ACES_CG_TO_WORKING_SPACE(TEXTURE_LOD_OFFSET(name, coord, lod, offset))

// TEXTURE_OFFSET
#define TEXTURE_OFFSET_FROM_LINEAR(name, coord, offset) LINEAR_TO_WORKING_SPACE(TEXTURE_OFFSET(name, coord, offset))
#define TEXTURE_OFFSET_FROM_SRGB(name, coord, offset) SRGB_TO_WORKING_SPACE(TEXTURE_OFFSET(name, coord, offset))
#define TEXTURE_OFFSET_FROM_DISPLAY_P3(name, coord, offset) DISPLAY_P3_TO_WORKING_SPACE(TEXTURE_OFFSET(name, coord, offset))
#define TEXTURE_OFFSET_FROM_DCI_P3(name, coord, offset) DCI_P3_TO_WORKING_SPACE(TEXTURE_OFFSET(name, coord, offset))
#define TEXTURE_OFFSET_FROM_REC2020(name, coord, offset) REC2020_TO_WORKING_SPACE(TEXTURE_OFFSET(name, coord, offset))
#define TEXTURE_OFFSET_FROM_ACES_CG(name, coord, offset) ACES_CG_TO_WORKING_SPACE(TEXTURE_OFFSET(name, coord, offset))

// TEXTURE_PROJ
#define TEXTURE_PROJ_FROM_LINEAR(name, coord) LINEAR_TO_WORKING_SPACE(TEXTURE_PROJ(name, coord))
#define TEXTURE_PROJ_FROM_SRGB(name, coord) SRGB_TO_WORKING_SPACE(TEXTURE_PROJ(name, coord))
#define TEXTURE_PROJ_FROM_DISPLAY_P3(name, coord) DISPLAY_P3_TO_WORKING_SPACE(TEXTURE_PROJ(name, coord))
#define TEXTURE_PROJ_FROM_DCI_P3(name, coord) DCI_P3_TO_WORKING_SPACE(TEXTURE_PROJ(name, coord))
#define TEXTURE_PROJ_FROM_REC2020(name, coord) REC2020_TO_WORKING_SPACE(TEXTURE_PROJ(name, coord))
#define TEXTURE_PROJ_FROM_ACES_CG(name, coord) ACES_CG_TO_WORKING_SPACE(TEXTURE_PROJ(name, coord))

// TEXTURE_PROJ_GRAD
#define TEXTURE_PROJ_GRAD_FROM_LINEAR(name, coord, dPdx, dPdy) LINEAR_TO_WORKING_SPACE(TEXTURE_PROJ_GRAD(name, coord, dPdx, dPdy))
#define TEXTURE_PROJ_GRAD_FROM_SRGB(name, coord, dPdx, dPdy) SRGB_TO_WORKING_SPACE(TEXTURE_PROJ_GRAD(name, coord, dPdx, dPdy))
#define TEXTURE_PROJ_GRAD_FROM_DISPLAY_P3(name, coord, dPdx, dPdy) DISPLAY_P3_TO_WORKING_SPACE(TEXTURE_PROJ_GRAD(name, coord, dPdx, dPdy))
#define TEXTURE_PROJ_GRAD_FROM_DCI_P3(name, coord, dPdx, dPdy) DCI_P3_TO_WORKING_SPACE(TEXTURE_PROJ_GRAD(name, coord, dPdx, dPdy))
#define TEXTURE_PROJ_GRAD_FROM_REC2020(name, coord, dPdx, dPdy) REC2020_TO_WORKING_SPACE(TEXTURE_PROJ_GRAD(name, coord, dPdx, dPdy))
#define TEXTURE_PROJ_GRAD_FROM_ACES_CG(name, coord, dPdx, dPdy) ACES_CG_TO_WORKING_SPACE(TEXTURE_PROJ_GRAD(name, coord, dPdx, dPdy))

// TEXTURE_PROJ_GRAD_OFFSET
#define TEXTURE_PROJ_GRAD_OFFSET_FROM_LINEAR(name, coord, dPdx, dPdy, offset) LINEAR_TO_WORKING_SPACE(TEXTURE_PROJ_GRAD_OFFSET(name, coord, dPdx, dPdy, offset))
#define TEXTURE_PROJ_GRAD_OFFSET_FROM_SRGB(name, coord, dPdx, dPdy, offset) SRGB_TO_WORKING_SPACE(TEXTURE_PROJ_GRAD_OFFSET(name, coord, dPdx, dPdy, offset))
#define TEXTURE_PROJ_GRAD_OFFSET_FROM_DISPLAY_P3(name, coord, dPdx, dPdy, offset) DISPLAY_P3_TO_WORKING_SPACE(TEXTURE_PROJ_GRAD_OFFSET(name, coord, dPdx, dPdy, offset))
#define TEXTURE_PROJ_GRAD_OFFSET_FROM_DCI_P3(name, coord, dPdx, dPdy, offset) DCI_P3_TO_WORKING_SPACE(TEXTURE_PROJ_GRAD_OFFSET(name, coord, dPdx, dPdy, offset))
#define TEXTURE_PROJ_GRAD_OFFSET_FROM_REC2020(name, coord, dPdx, dPdy, offset) REC2020_TO_WORKING_SPACE(TEXTURE_PROJ_GRAD_OFFSET(name, coord, dPdx, dPdy, offset))
#define TEXTURE_PROJ_GRAD_OFFSET_FROM_ACES_CG(name, coord, dPdx, dPdy, offset) ACES_CG_TO_WORKING_SPACE(TEXTURE_PROJ_GRAD_OFFSET(name, coord, dPdx, dPdy, offset))

// TEXTURE_PROJ_LOD
#define TEXTURE_PROJ_LOD_FROM_LINEAR(name, coord, lod) LINEAR_TO_WORKING_SPACE(TEXTURE_PROJ_LOD(name, coord, lod))
#define TEXTURE_PROJ_LOD_FROM_SRGB(name, coord, lod) SRGB_TO_WORKING_SPACE(TEXTURE_PROJ_LOD(name, coord, lod))
#define TEXTURE_PROJ_LOD_FROM_DISPLAY_P3(name, coord, lod) DISPLAY_P3_TO_WORKING_SPACE(TEXTURE_PROJ_LOD(name, coord, lod))
#define TEXTURE_PROJ_LOD_FROM_DCI_P3(name, coord, lod) DCI_P3_TO_WORKING_SPACE(TEXTURE_PROJ_LOD(name, coord, lod))
#define TEXTURE_PROJ_LOD_FROM_REC2020(name, coord, lod) REC2020_TO_WORKING_SPACE(TEXTURE_PROJ_LOD(name, coord, lod))
#define TEXTURE_PROJ_LOD_FROM_ACES_CG(name, coord, lod) ACES_CG_TO_WORKING_SPACE(TEXTURE_PROJ_LOD(name, coord, lod))

// TEXTURE_PROJ_LOD_OFFSET
#define TEXTURE_PROJ_LOD_OFFSET_FROM_LINEAR(name, coord, lod, offset) LINEAR_TO_WORKING_SPACE(TEXTURE_PROJ_LOD_OFFSET(name, coord, lod, offset))
#define TEXTURE_PROJ_LOD_OFFSET_FROM_SRGB(name, coord, lod, offset) SRGB_TO_WORKING_SPACE(TEXTURE_PROJ_LOD_OFFSET(name, coord, lod, offset))
#define TEXTURE_PROJ_LOD_OFFSET_FROM_DISPLAY_P3(name, coord, lod, offset) DISPLAY_P3_TO_WORKING_SPACE(TEXTURE_PROJ_LOD_OFFSET(name, coord, lod, offset))
#define TEXTURE_PROJ_LOD_OFFSET_FROM_DCI_P3(name, coord, lod, offset) DCI_P3_TO_WORKING_SPACE(TEXTURE_PROJ_LOD_OFFSET(name, coord, lod, offset))
#define TEXTURE_PROJ_LOD_OFFSET_FROM_REC2020(name, coord, lod, offset) REC2020_TO_WORKING_SPACE(TEXTURE_PROJ_LOD_OFFSET(name, coord, lod, offset))
#define TEXTURE_PROJ_LOD_OFFSET_FROM_ACES_CG(name, coord, lod, offset) ACES_CG_TO_WORKING_SPACE(TEXTURE_PROJ_LOD_OFFSET(name, coord, lod, offset))

// TEXTURE_PROJ_OFFSET
#define TEXTURE_PROJ_OFFSET_FROM_LINEAR(name, coord, offset) LINEAR_TO_WORKING_SPACE(TEXTURE_PROJ_OFFSET(name, coord, offset))
#define TEXTURE_PROJ_OFFSET_FROM_SRGB(name, coord, offset) SRGB_TO_WORKING_SPACE(TEXTURE_PROJ_OFFSET(name, coord, offset))
#define TEXTURE_PROJ_OFFSET_FROM_DISPLAY_P3(name, coord, offset) DISPLAY_P3_TO_WORKING_SPACE(TEXTURE_PROJ_OFFSET(name, coord, offset))
#define TEXTURE_PROJ_OFFSET_FROM_DCI_P3(name, coord, offset) DCI_P3_TO_WORKING_SPACE(TEXTURE_PROJ_OFFSET(name, coord, offset))
#define TEXTURE_PROJ_OFFSET_FROM_REC2020(name, coord, offset) REC2020_TO_WORKING_SPACE(TEXTURE_PROJ_OFFSET(name, coord, offset))
#define TEXTURE_PROJ_OFFSET_FROM_ACES_CG(name, coord, offset) ACES_CG_TO_WORKING_SPACE(TEXTURE_PROJ_OFFSET(name, coord, offset))

#endif