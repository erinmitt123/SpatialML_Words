#ifndef _ENGINE_COMMON_MATH_GLSL
#define _ENGINE_COMMON_MATH_GLSL

//------------------------------------------------------------------------------
// Common math
//------------------------------------------------------------------------------

/** @public-api */
#define PI                 3.14159265359
/** @public-api */
#define HALF_PI            1.570796327
/** @public-api */
#define RAD_TO_DEG         57.2957795131
#define DEG_TO_RAD         0.0174532925199

#define MEDIUMP_FLT_EPS      4.8828125e-4    // 2^-11, machine epsilon: 1 + EPS = 1 (half of the ULP for 1.0f)
#define MEDIUMP_FLT_MAX      65504.0
#define MEDIUMP_FLT_MIN      0.00006103515625
#define MEDIUMP_FLT_MIN_SQRT 0.0078125  // 2^-7 == sqrt(HALF_MIN), useful for ensuring HALF_MIN after x^2
#define FLT_MIN         1.175494351e-38 // Minimum floating-point number
#define FLT_MAX         3.402823466e+38 // Maximum floating-point number

#ifdef TARGET_MOBILE
#define FLT_EPS            MEDIUMP_FLT_EPS
#define FLT_MIN_SQRT       MEDIUMP_FLT_MIN_SQRT
#define saturateMediump(x) min(x, MEDIUMP_FLT_MAX)
#else
#define FLT_EPS            1e-5
#define FLT_MIN_SQRT       1e-10
#define saturateMediump(x) x
#endif

#define saturate(x)        clamp(x, 0.0, 1.0)
#define atan2(x, y)        atan(y, x)

//------------------------------------------------------------------------------
// Scalar operations
//------------------------------------------------------------------------------

/**
 * Computes x^5 using only multiply operations.
 *
 * @public-api
 */
half pow5(half x) {
    half x2 = x * x;
    return x2 * x2 * x;
}

/**
 * Computes x^2 as a single multiplication.
 *
 * @public-api
 */
half sq(half x) {
    return x * x;
}

//------------------------------------------------------------------------------
// Vector operations
//------------------------------------------------------------------------------

/**
 * Returns the maximum component of the specified vector.
 *
 * @public-api
 */
half max3(const half3 v) {
    return max(v.x, max(v.y, v.z));
}

half vmax(const half2 v) {
    return max(v.x, v.y);
}

half vmax(const half3 v) {
    return max(v.x, max(v.y, v.z));
}

half vmax(const half4 v) {
    return max(max(v.x, v.y), max(v.y, v.z));
}

/**
 * Returns the minimum component of the specified vector.
 *
 * @public-api
 */
half min3(const half3 v) {
    return min(v.x, min(v.y, v.z));
}

half vmin(const half2 v) {
    return min(v.x, v.y);
}

half vmin(const half3 v) {
    return min(v.x, min(v.y, v.z));
}

half vmin(const half4 v) {
    return min(min(v.x, v.y), min(v.y, v.z));
}

//------------------------------------------------------------------------------
// Trigonometry
//------------------------------------------------------------------------------

/**
 * Approximates acos(x) with a max absolute error of 9.0x10^-3.
 * Valid in the range -1..1.
 */
half acosFast(half x) {
    // Lagarde 2014, "Inverse trigonometric functions GPU optimization for AMD GCN architecture"
    // This is the approximation of degree 1, with a max absolute error of 9.0x10^-3
    half y = abs(x);
    half p = -0.1565827 * y + 1.570796;
    p *= sqrt(1.0 - y);
    return x >= 0.0 ? p : PI - p;
}

/**
 * Approximates acos(x) with a max absolute error of 9.0x10^-3.
 * Valid only in the range 0..1.
 */
half acosFastPositive(half x) {
    half p = -0.1565827 * x + 1.570796;
    return p * sqrt(1.0 - x);
}

half asinFast(half x) {
    // Approximation of the arcsin function
    return x * (1.5707288 + x * (-0.2121144 + x * (0.0742610 - x * 0.0187293)));
}

//------------------------------------------------------------------------------
// Matrix and quaternion operations
//------------------------------------------------------------------------------

/**
 * Multiplies the specified 3-component vector by the 4x4 matrix (m * v) in
 * high precision.
 *
 * @public-api
 */
highp vec4 mulMat4x4Float3(const highp mat4 m, const highp vec3 v) {
    return v.x * m[0] + (v.y * m[1] + (v.z * m[2] + m[3]));
}

/**
 * Multiplies the specified 3-component vector by the 3x3 matrix (m * v) in
 * high precision.
 *
 * @public-api
 */
highp vec3 mulMat3x3Float3(const highp mat4 m, const highp vec3 v) {
    return v.x * m[0].xyz + (v.y * m[1].xyz + (v.z * m[2].xyz));
}

/**
 * Extracts the normal vector of the tangent frame encoded in the specified quaternion.
 */
void toTangentFrame(const highp vec4 q, out highp vec3 n) {
    n = vec3( 0.0,  0.0,  1.0) +
        vec3( 2.0, -2.0, -2.0) * q.x * q.zwx +
        vec3( 2.0,  2.0, -2.0) * q.y * q.wzy;
}

/**
 * Extracts the normal and tangent vectors of the tangent frame encoded in the
 * specified quaternion.
 */
void toTangentFrame(const highp vec4 q, out highp vec3 n, out highp vec3 t) {
    toTangentFrame(q, n);
    t = vec3( 1.0,  0.0,  0.0) +
        vec3(-2.0,  2.0, -2.0) * q.y * q.yxw +
        vec3(-2.0,  2.0,  2.0) * q.z * q.zwx;
}

highp mat3 cofactor(const highp mat3 m) {
    highp float a = m[0][0];
    highp float b = m[1][0];
    highp float c = m[2][0];
    highp float d = m[0][1];
    highp float e = m[1][1];
    highp float f = m[2][1];
    highp float g = m[0][2];
    highp float h = m[1][2];
    highp float i = m[2][2];

    highp mat3 cof;
    cof[0][0] = e * i - f * h;
    cof[0][1] = c * h - b * i;
    cof[0][2] = b * f - c * e;
    cof[1][0] = f * g - d * i;
    cof[1][1] = a * i - c * g;
    cof[1][2] = c * d - a * f;
    cof[2][0] = d * h - e * g;
    cof[2][1] = b * g - a * h;
    cof[2][2] = a * e - b * d;
    return cof;
}

//------------------------------------------------------------------------------
// Random
//------------------------------------------------------------------------------

/*
 * Random number between 0 and 1, using interleaved gradient noise.
 * w must not be normalized (e.g. window coordinates)
 */
half interleavedGradientNoise(highp vec2 w) {
    const half3 m = vec3(0.06711056, 0.00583715, 52.9829189);
    return fract(m.z * fract(dot(w, m.xy)));
}

//lerp(1, b, t) =  1 + (b - 1) * t
half lerpWhiteTo(half b, half t)
{
    half oneMinusT = 1.0 - t;
    return oneMinusT + b * t;
}

//------------------------------------------------------------------------------
// Transform UV
//------------------------------------------------------------------------------

vec2 transformUv(vec2 uv, vec2 scale, vec2 offset, float rSin, float rCos) {
    mat3 M_Final = mat3(
        scale.x * rCos,      scale.y * rSin,       0.0,
       -scale.x * rSin,      scale.y * rCos,       0.0,
        scale.x * 0.5 + offset.x, scale.y * 0.5 + offset.y, 1.0
    );

    vec2 uv_final = (M_Final  * vec3(uv - vec2(0.5), 1.0)).xy;
    return uv_final;
}

#endif
