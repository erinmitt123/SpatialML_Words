vec3 rgbToSrgb(vec3 rgb) {
    return mix(12.92 * rgb, 1.055 * pow(rgb, vec3(1.0 / 2.4)) - 0.055, step(0.0031308, rgb));
}
vec3 srgbToRgb(vec3 srgb) {
    vec3 rgb = mix( srgb / 12.92, pow((srgb + 0.055) / 1.055, vec3(2.4)), step(0.04045, srgb) );
    return rgb;
}

vec3 blendHardLight(vec3 base, vec3 blend) {
    return mix((2.0*base*blend),(1.0-2.0*(1.0-base)*(1.0-blend)),step(0.5,blend));
}

vec3 blendHardLight(vec3 base, vec3 blend, float opacity) {
    base = rgbToSrgb(base);
    blend = rgbToSrgb(blend);
    vec3 srgbResult = blendHardLight(base, blend) * opacity + base * (1.0 - opacity);
    return srgbToRgb(srgbResult);
}

vec3 blendDirectAdd(vec3 base, vec3 blend, float opacity) {
    base = rgbToSrgb(base);
    blend = rgbToSrgb(blend);
    vec3 srgbResult = blend * opacity + base * (1.0 - opacity);
    return srgbToRgb(srgbResult);
}

half4 blendHoverEffectForInteractor(highp float radius,highp float innerOffset,vec3 hoverPosition,highp float distanceScale, half intensity, half4 baseColor,half4 hoverColor){
    highp float distance = distance(getUserWorldPosition(),hoverPosition) * distanceScale;
    distance = clamp(0.0,distance,radius);
    highp float highlight = (1.0 + 16.0 * innerOffset) * pow((distance - radius),4.0) / pow(radius,4.0);
    highlight = clamp(highlight,0.0,1.0);
    highlight = highlight * intensity * hoverColor.a;
    baseColor.rgb = blendHardLight(baseColor.rgb,hoverColor.rgb,highlight);
    return baseColor;
}

#define GlowEffect_MaxRadius 0.02
#define GlowEffect_MinRadius 0.01
#define GlowEffect_MinAlpha 0.0
#define GlowEffect_MaxAlpha 0.5
#define GlowEffect_SelectAlpha 1.0

half4 blendHoverEffectForInteractor1(highp float radius,highp float innerOffset,vec3 hoverPosition, vec3 hoverClosestPoint, bool SelectEnter, highp float distanceScale,half intensity,half4 baseColor,half4 hoverColor){
    highp float distanceL = clamp(0.0, distance(hoverClosestPoint, hoverPosition), radius);
    highp float ratio = clamp ((radius - distanceL) / radius , 0.0, 1.0);
    highp float hoverLightRadius = SelectEnter ? GlowEffect_MinRadius : (GlowEffect_MaxRadius - (GlowEffect_MaxRadius - GlowEffect_MinRadius) * ratio);
    //highp float highlight = (1.0 + 16.0 * innerOffset) * pow((distanceL - hoverLightRadius) / hoverLightRadius,4.0);
    highp float alpha = SelectEnter ? GlowEffect_SelectAlpha : (GlowEffect_MinAlpha + (GlowEffect_MaxAlpha - GlowEffect_MinAlpha) * ratio);
    highp float placeInCircle = clamp(1 - distance(getUserWorldPosition(), hoverClosestPoint) / hoverLightRadius, 0.0, 1.0);
    highp float highlight = alpha * intensity * placeInCircle * hoverColor.a;
    baseColor.rgb = blendDirectAdd(baseColor.rgb,hoverColor.rgb,highlight);
    return baseColor;
}

