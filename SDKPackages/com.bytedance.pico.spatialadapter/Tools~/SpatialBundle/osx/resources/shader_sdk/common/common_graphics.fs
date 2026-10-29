#ifndef _ENGINE_COMMON_GRAPHICS_FS
#define _ENGINE_COMMON_GRAPHICS_FS

//------------------------------------------------------------------------------
// Common color operations
//------------------------------------------------------------------------------

/**
 * Computes the luminance of the specified linear RGB color using the
 * luminance coefficients from Rec. 709.
 *
 * @public-api
 */
half luminance(const half3 linear) {
    return dot(linear, vec3(0.2126, 0.7152, 0.0722));
}

/**
 * Computes the pre-exposed intensity using the specified intensity and exposure.
 * This function exists to force highp precision on the two parameters
 */
float computePreExposedIntensity(const highp float intensity, const highp float exposure) {
    return intensity * exposure;
}

void unpremultiply(inout half4 color) {
    color.rgb /= max(color.a, FLT_EPS);
}

/**
 * Applies a full range YCbCr to sRGB conversion and returns an RGB color.
 *
 * @public-api
 */
half3 ycbcrToRgb(half luminance, half2 cbcr) {
    // Taken from https://developer.apple.com/documentation/arkit/arframe/2867984-capturedimage
    const mat4 ycbcrToRgbTransform = mat4(
         1.0000,  1.0000,  1.0000,  0.0000,
         0.0000, -0.3441,  1.7720,  0.0000,
         1.4020, -0.7141,  0.0000,  0.0000,
        -0.7010,  0.5291, -0.8860,  1.0000
    );
    return (ycbcrToRgbTransform * vec4(luminance, cbcr, 1.0)).rgb;
}

//------------------------------------------------------------------------------
// Tone mapping operations
//------------------------------------------------------------------------------

/*
 * The input must be in the [0, 1] range.
 */
half3 Inverse_Tonemap_Filmic(const half3 x) {
    return (0.03 - 0.59 * x - sqrt(0.0009 + 1.3702 * x - 1.0127 * x * x)) / (-5.02 + 4.86 * x);
}

/**
 * Applies the inverse of the tone mapping operator to the specified HDR or LDR
 * sRGB (non-linear) color and returns a linear sRGB color. The inverse tone mapping
 * operator may be an approximation of the real inverse operation.
 *
 * @public-api
 */
half3 inverseTonemapSRGB(half3 color) {
    // sRGB input
    color = clamp(color, 0.0, 1.0);
    return Inverse_Tonemap_Filmic(pow(color, vec3(2.2)));
}

/**
 * Applies the inverse of the tone mapping operator to the specified HDR or LDR
 * linear RGB color and returns a linear RGB color. The inverse tone mapping operator
 * may be an approximation of the real inverse operation.
 *
 * @public-api
 */
half3 inverseTonemap(half3 linear) {
    // Linear input
    return Inverse_Tonemap_Filmic(clamp(linear, 0.0, 1.0));
}

//------------------------------------------------------------------------------
// Common texture operations
//------------------------------------------------------------------------------

/**
 * Decodes the specified RGBM value to linear HDR RGB.
 */
half3 decodeRGBM(half4 c) {
    c.rgb *= (c.a * 16.0);
    return c.rgb * c.rgb;
}

//------------------------------------------------------------------------------
// Common screen-space operations
//------------------------------------------------------------------------------

// returns the frag coord in the GL convention with (0, 0) at the bottom-left
// resolution : width, height
highp vec2 getFragCoord(const highp vec2 resolution) {
#if defined(TARGET_METAL_ENVIRONMENT) || defined(TARGET_VULKAN_ENVIRONMENT)
    return vec2(gl_FragCoord.x, resolution.y - gl_FragCoord.y);
#else
    return gl_FragCoord.xy;
#endif
}

//------------------------------------------------------------------------------
// Common debug
//------------------------------------------------------------------------------

half3 heatmap(half v) {
    half3 r = v * 2.1 - vec3(1.8, 1.14, 0.3);
    return 1.0 - r * r;
}

//For Spatial Render
bool isInBox(mat4 inverseBoundaryTransformMatrix, half3 pixelWorldPosition){
    vec4 positionInVolumeSpace = inverseBoundaryTransformMatrix * vec4(pixelWorldPosition, 1.0);
    bool xInBox = abs(positionInVolumeSpace.x) <= 0.5;
    bool yInBox = abs(positionInVolumeSpace.y) <= 0.5;
    bool zInBox = abs(positionInVolumeSpace.z) <= 0.5;
    return xInBox && yInBox && zInBox;
}

float isInBoxGetDistance(in mat4 inverseBoundaryTransformMatrix,
                          in vec3 invScalesSq,
                          in uint blurredFacesMask,
                          in vec3 pixelWorldPosition) {
    vec4 positionInVolumeSpace = inverseBoundaryTransformMatrix * vec4(pixelWorldPosition, 1.0);
    vec3 pos = positionInVolumeSpace.xyz;
    vec3 d = abs(pos) - 0.5;
    vec3 localDist = max(d, vec3(0.0));

    if (localDist.x == 0.0 && localDist.y == 0.0 && localDist.z == 0.0) {
        return 0.0;
    }

    uvec3 faceMasks = uvec3(
        pos.x > 0.0 ? 8u : 4u,   // +X -> RIGHT (8) / -X -> LEFT (4)
        pos.y > 0.0 ? 1u : 2u,   // +Y -> UP (1) / -Y -> DOWN (2)
        pos.z > 0.0 ? 16u : 32u  // +Z -> FRONT (16) / -Z -> BACK (32)
    );

    bvec3 isOutside = greaterThan(localDist, vec3(0.0));
    bvec3 isFadeDisabled = equal(uvec3(blurredFacesMask) & faceMasks, uvec3(0u));
    localDist = mix(localDist, vec3(1.0e9), vec3(isOutside) * vec3(isFadeDisabled));

    vec3 worldDistSq = (localDist * localDist) / invScalesSq;
    return dot(worldDistSq, vec3(1.0));
}

//------------------------------------------------------------------------------
// Common packDepth
//------------------------------------------------------------------------------

half3 packDepth24(in half depth)
{
    half depthVal = depth * (256.0 * 256.0 * 256.0 - 1.0) / (256.0 * 256.0 * 256.0);
    half4 encode = fract(depthVal * vec4(1.0, 256.0, 256.0*256.0, 256.0 * 256.0 * 256.0));
    return encode.xyz - encode.yzw / 256.0 + 1.0 / 512.0;
}

half unpackDepth24(in half3 pack)
{
    half depth = dot(pack, 1.0 / vec3(1.0, 256.0, 256.0 * 256.0));
    return depth * (256.0 * 256.0 * 256.0) / (256.0 * 256.0 * 256.0 - 1.0);
}

//------------------------------------------------------------------------------
// Common blur
//------------------------------------------------------------------------------

half4 gaussian3x3(const highp sampler2D texture, const half2 uv, const half2 step)
{
    half4 res = vec4(0);

    half gaussianKernel[9];
    gaussianKernel[0] = 0.077847;
    gaussianKernel[1] = 0.123317;
    gaussianKernel[2] = 0.077847;

    gaussianKernel[3] = 0.123317;
    gaussianKernel[4] = 0.195346;
    gaussianKernel[5] = 0.123317;

    gaussianKernel[6] = 0.077847;
    gaussianKernel[7] = 0.123317;
    gaussianKernel[8] = 0.077847;

    for (int x = -1; x <= 1; ++x) {
        for (int y = -1; y <= 1; ++y) {
            half weight = gaussianKernel[x * 3 + y + 4];
            res += weight * textureLod(texture, uv + float2(x, y) * step, 0.0f);
        }
    }

    return res;
}

half4 gaussian5x5(const highp sampler2D texture, const half2 uv, const half2 step)
{
    half4 res = vec4(0.0);
    half gaussianKernel[25];
    gaussianKernel[0] = 0.002969;
    gaussianKernel[1] = 0.013306;
    gaussianKernel[2] = 0.021938;
    gaussianKernel[3] = 0.013306;
    gaussianKernel[4] = 0.002969;

    gaussianKernel[5] = 0.013306;
    gaussianKernel[6] = 0.059634;
    gaussianKernel[7] = 0.098320;
    gaussianKernel[8] = 0.059634;
    gaussianKernel[9] = 0.013306;

    gaussianKernel[10] = 0.021938;
    gaussianKernel[11] = 0.098320;
    gaussianKernel[12] = 0.162103;
    gaussianKernel[13] = 0.098320;
    gaussianKernel[14] = 0.021938;

    gaussianKernel[15] = 0.013306;
    gaussianKernel[16] = 0.059634;
    gaussianKernel[17] = 0.098320;
    gaussianKernel[18] = 0.059634;
    gaussianKernel[19] = 0.013306;

    gaussianKernel[20] = 0.002969;
    gaussianKernel[21] = 0.013306;
    gaussianKernel[22] = 0.021938;
    gaussianKernel[23] = 0.013306;
    gaussianKernel[24] = 0.002969;

    for (int x = -2; x <= 2; ++x) {
        for (int y = -2; y <= 2; ++y) {
            float weight = gaussianKernel[(x + 2) * 5 + (y + 2)];
            res += weight * textureLod(texture, uv + float2(x, y) * step, 0.0f);
        }
    }

    return res;
}

//------------------------------------------------------------------------------
// texture utilities
//------------------------------------------------------------------------------

float computeTextureLOD(vec2 uvdx, vec2 uvdy, float bias) {
    highp float d = max(dot(uvdx, uvdx), dot(uvdy, uvdy));
    return max(0.5 * log2(d) - bias, 0.0);
}

float computeTextureLOD(vec2 uvdx, vec2 uvdy, vec2 texSize, float bias) {
    vec2 ddx = uvdx * texSize;
    vec2 ddy = uvdy * texSize;
    return computeTextureLOD(ddx, ddy, bias);
}

#endif
