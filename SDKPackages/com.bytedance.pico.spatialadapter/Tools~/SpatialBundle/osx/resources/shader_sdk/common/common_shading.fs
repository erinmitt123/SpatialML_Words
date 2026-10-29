#ifndef _ENGINE_COMMON_SHADING_FS
#define _ENGINE_COMMON_SHADING_FS

// These variables should be in a struct but some GPU drivers ignore the
// precision qualifier on individual struct members
highp mat3  shading_tangentToWorld;   // TBN matrix
highp vec3  shading_position;         // position of the fragment in world space
      half3  shading_view;             // normalized vector from the fragment to the eye
      half3  shading_normal;           // normalized transformed normal, in world space
      half3  shading_geometricNormal;  // normalized geometric normal, in world space
      half3  shading_reflected;        // reflection of view about normal
      half   shading_NoV;              // dot(normal, view), always strictly >= MIN_N_DOT_V
      half3  shading_bentNormal;       // normalized transformed normal, in world space
      half3  shading_clearCoatNormal;  // normalized clear coat layer normal, in world space

highp vec2 shading_normalizedViewportCoord;

highp vec2 shading_normalizedViewportCoordVirtual;

//unpack normal by usd scale/bias
vec3 normalRGBScale(vec4 packedNormal, float scale)
{
    vec3 normal = packedNormal.rgb;
    normal.xy *= scale;
    return normal;
}

vec3 unpackNormalRGB(vec4 packedNormal)
{
    return packedNormal.rgb * 2.0 - 1.0;
}

vec3 unpackNormalRGBScale(vec4 packedNormal, float scale)
{
    vec3 normal;
    normal.xyz = packedNormal.rgb * 2.0 - 1.0;
    normal.xy *= scale;
    return normal;
}

// Unpack normal as DXT5nm (1, y, 0, x)
vec3 unpackNormalAGScale(vec4 packedNormal, float scale)
{
    vec3 normal;
    normal.xy = packedNormal.ag * 2.0 - 1.0;
    normal.z = max(1.0e-16, sqrt(1.0 - saturate(dot(normal.xy, normal.xy))));
    normal.xy *= scale;
    return normal;
}

// Unpack normal as BC5 (x, y, 0, 1)
vec3 unpackNormalRGScale(vec4 packedNormal, float scale)
{
    vec3 normal;
    normal.xy = packedNormal.rg * 2.0 - 1.0;
    normal.z = max(1.0e-16, sqrt(1.0 - saturate(dot(normal.xy, normal.xy))));
    normal.xy *= scale;
    return normal;
}

half computeOutputAlpha(half a) {
      if (BLEND_MODE_OPAQUE) {
            return 1.0;
      }

      if (BLEND_MODE_MASKED) {
            // If we reach this point in the code, we already know that the fragment is not discarded due
            // to the threshold factor. Therefore we can just output 1.0, which prevents a "punch through"
            // effect from occuring. We do this only for TRANSLUCENT views in order to prevent breakage
            // of ALPHA_TO_COVERAGE.
            return (frameUniforms.needsAlphaChannel == 1.0) ? 1.0 : a;
      }

      return a;
}

///alpha end

#endif
