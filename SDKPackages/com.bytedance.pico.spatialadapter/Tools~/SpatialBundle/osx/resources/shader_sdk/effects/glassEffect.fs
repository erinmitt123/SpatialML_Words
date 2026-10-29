#ifndef _ENGINE_GLASS_EFFECT_FS_
#define _ENGINE_GLASS_EFFECT_FS_

// Gradient and Border
float calculateHighlightAngle(float2 viewDir, float highlightOffset)
{
    float len = length(viewDir.xy);

    if (len < 1e-7) return 135.0;

    viewDir =  viewDir / len;
    float newAngle = atan2(viewDir.y, viewDir.x);
    newAngle = degrees(newAngle);
    newAngle = mod(newAngle + 360.0, 360.0);
    return newAngle + highlightOffset;
}


float gaussian(float x, float sigma)
{
    return exp(-(x*x)/(2*sigma*sigma)) / (sqrt(2*PI)*sigma);
}

float2 angleToUV(float angle)
{
    float rad = angle * PI/180.0;
    float2 dir = float2(cos(rad), sin(rad));
    float tx = (dir.x > 0 ? 0.5 : -0.5) / dir.x;
    float ty = (dir.y > 0 ? 0.5 : -0.5) / dir.y;
    float t = min(abs(tx), abs(ty));
    return float2(0.5 + t * dir.x, 0.5 + t * dir.y);
}

float cross2D(float2 a, float2 b) {
    return a.x * b.y - a.y * b.x;
}

float getHighlightIntensity(float2 uv, float2 highlightPos, float2 highlightPosSymmetric, float intensity, float edgeWidth, float spread, float inwardOffset, float sdf)
{
    // 计算当前点到两个高光中心的距离
    float dist = distance(uv, highlightPos);
    float distSymmetric = distance(uv, highlightPosSymmetric);

    // 计算边缘遮罩（只计算一次）
    float edgeSmooth = 0.15;
    float outerEdgeMask = smoothstep(edgeWidth * (1 + edgeSmooth), edgeWidth * (1 - edgeSmooth), sdf);
    float innerEdgeMask = smoothstep(inwardOffset * (1 - edgeSmooth), inwardOffset * (1 + edgeSmooth), sdf);
    float edgeMask = outerEdgeMask * innerEdgeMask;

    // 计算两个高光的衰减
    float spreadDist = spread * 2;
    float highlightFalloff = 1.0 - saturate(dist / spreadDist);
    float highlightFalloffSymmetric = 1.0 - saturate(distSymmetric / spreadDist);

    // 计算两个高光强度
    float highlight = highlightFalloff * edgeMask * intensity;
    float highlightSymmetric = highlightFalloffSymmetric * edgeMask * intensity;

    // 合并高光贡献
    return 1.0 - (1.0 - highlight) * (1.0 - highlightSymmetric);
}

float getHighlightIntensity(float2 uv, float2 highlightPos, float intensity, float edgeWidth, float spread, float inwardOffset, float sdf)
{
    // 计算当前点到高光点的距离
    float dist = distance(uv, highlightPos);
    float edgeMask = step(sdf, edgeWidth) * step(inwardOffset, sdf);

    // 计算扩散效果
    float spreadDist = spread * 2;
    float highlight = 1 - saturate(dist / spreadDist);

    return highlight * edgeMask * intensity;
}

float SqrDistance(float2 a, float2 b)
{
    float2 diff = a - b;
    return dot(diff, diff);
}

// 计算点C到线段AB的最短距离的平方
float SqrDistancePointToLineSegment(float2 C, float2 A, float2 B, float2 sizeParams)
{
    // 计算向量
    float2 AB = B - A;
    float2 AC = C - A;

    // 计算AB长度的平方，防止除以零
    const float EPSILON = 0.0000001;
    float lenSqrAB = max(dot(AB, AB), EPSILON);

    // 计算投影比例t
    float t = clamp(dot(AC, AB) / lenSqrAB, 0.0, 1.0);

    // 计算C到投影点的向量并应用sizeParams
    float2 CP = (C - A - t * AB) * sizeParams.xy;

    // 返回距离平方
    return dot(CP, CP);
}

float CalculateSdf(float4 sizeParams, float2 uvK, float2 G, sampler2D LTex, uint quadrant, out uint areaType) {
    // points
    // topEdge
    float2 topEdgeA = float2(G.x, 1.0);
    float2 topEdgeB = float2(1 - G.x, 1.0);
    // bottomEdge
    float2 bottomEdgeA = float2(G.x, 0.0);
    float2 bottomEdgeB = float2(1 - G.x, 0.0);
    // leftEdge
    float2 leftEdgeA = float2(0.0, G.y);
    float2 leftEdgeB = float2(0.0, 1.0 - G.y);
    // leftEdge
    float2 rightEdgeA = float2(1.0, G.y);
    float2 rightEdgeB = float2(1.0, 1.0 - G.y);

    float min_edge_dist = 10e8;
    float distToTop = SqrDistancePointToLineSegment(uvK, topEdgeA, topEdgeB, sizeParams.xy);
    float distToBottom = SqrDistancePointToLineSegment(uvK, bottomEdgeA, bottomEdgeB, sizeParams.xy);
    float distToLeft = SqrDistancePointToLineSegment(uvK, leftEdgeA, leftEdgeB, sizeParams.xy);
    float distToRight = SqrDistancePointToLineSegment(uvK, rightEdgeA, rightEdgeB, sizeParams.xy);
    if (distToTop < min_edge_dist)
    {
        min_edge_dist = distToTop;
        areaType = 1;
    }

    if (distToBottom < min_edge_dist)
    {
        min_edge_dist = distToBottom;
        areaType = 2;
    }

    if (distToLeft < min_edge_dist)
    {
        min_edge_dist = distToLeft;
        areaType = 3;
    }
    if (distToRight < min_edge_dist)
    {
        min_edge_dist = distToRight;
        areaType = 4;
    }

    float angleA = quadrant * 90.0;
    float angleB = angleA + 90.0;
    float2 offsets[8] = {
        float2(1.0, G.y), float2(G.x, 1.0),      // quadrant 0: fanA, fanB
        float2(G.x, 1.0), float2(0.0, G.y),      // quadrant 1
        float2(0.0, G.y), float2(G.x, 0.0),      // quadrant 2
        float2(G.x, 0.0), float2(1.0, G.y)       // quadrant 3
    };
    float2 fanA = offsets[quadrant * 2];
    float2 fanB = offsets[quadrant * 2 + 1];

    float distA = SqrDistance(uvK * sizeParams.xy, fanA * sizeParams.xy);
    float distB = SqrDistance(uvK * sizeParams.xy , fanB * sizeParams.xy);

    // 初始化最佳解
    float2 best_L = float2(0, 0);

    float min_circle_dist = 1e8;
    const int iterations = 10;  // 90°/128 = 0.703125

    float2 minSize = sizeParams.xy;
    float2 maxOffset = sizeParams.zw;

    for (int i = 0; i < iterations; i++) {

        // 2. 计算中间角度
        float angle = angleA + (angleB - angleA) * 0.5;

        // 3. 根据角度计算L点坐标（基于控制点G和纹理偏移）
        float thetaNorm = angle / 360.0; // 归一化到0-1
        float2 sampleUV = float2(thetaNorm, 0.5);
        float2 offset = textureLod(LTex, sampleUV, 0).xy * maxOffset / minSize;

        float2 L = G + offset.xy;

        float dist = SqrDistance(uvK * sizeParams.xy, L * sizeParams.xy);

        // 更新最佳解
        if (dist < min_circle_dist) {
            min_circle_dist = dist;
            best_L = L;
        }

        // 简单二分法调整角度范围
        if (distB < distA) {
            angleA = angle;
            distA = dist;
        } else {
            angleB = angle;
            distB = dist;
        }
    }

    areaType = min_edge_dist < min_circle_dist ? areaType : 6;

    return sqrt(min(min_circle_dist, min_edge_dist));
}

// 计算向量KB在平面A上的投影向量的函数
float3 projectVectorOntoPlane(float3 v, float3 planeNormal) {
    return v - dot(v, planeNormal) * planeNormal;
}

float SimpleSunSpecular(float3 N, float3 V, float3 L, float roughness, float specColor)
{
    float3 H = normalize(V + L);
    float NoH = saturate(dot(N, H));
    float NoL = saturate(dot(N, L));

    // 简化的高光计算
    float specPower = (1.0 - roughness) * 128.0 + 8.0;
    float specularTerm = pow(NoH, specPower);

    // 模拟太阳光聚焦效果
    float sunFocus = pow(NoH, mix(1.0, 64.0, (1.0 - roughness) * 0.9));

    // 应用菲涅尔效果的简化版本
    float fresnelTerm = pow(1.0 - saturate(dot(H, V)), 5.0) * 0.8 + 0.2;

    return specColor * specularTerm * sunFocus * fresnelTerm * NoL;
}

float3 GetSpecularNormalFromSdf(float sdf, float maxSdf,  uint ereaType)
{
    int2 signed2[4] = {
        int2(0, 1), int2(0,-1),
        int2(-1, 0), int2(1, 0)
    };
    float t = saturate(saturate(sdf / maxSdf) + 0.0);
    float nx = 1.0f - t;
    float ny = nx;
    float nz = t;
    return normalize(float3(nx * signed2[ereaType-1].x, ny * signed2[ereaType-1].y, nz));
}

float3 RotateSpecularNormal(float3 normal, float2 rotationDirection)
{
    float nx = rotationDirection.x;
    float ny = rotationDirection.y;

    // 根据旋转矩阵的转置进行坐标转换
    return float3(
        -normal.x * ny + normal.y * nx,  // N_new_x
        normal.x * nx + normal.y * ny, // N_new_y
        normal.z                        // N_new_z保持不变
    );
}

vec3 blendAlpha(vec3 base, vec3 blend, float opacity) {
    return blend * opacity + base * (1.0 - opacity);
}

vec3 blendColorDodge(vec3 base, vec3 blend) {
    const float EPSILON = 0.00001;
    vec3 result = base / max(1.0 - blend, EPSILON);
    return clamp(result, 0.0, 1.0);
}

 vec3 blendColorDodge(vec3 base, vec3 blend, float opacity) {
 	return (blendColorDodge(base, blend) * opacity + base * (1.0 - opacity));
 }

// Triangle noise dither function.
float dither_TriangleNoise0(float temporalNoise) {
    vec3 uv_time = vec3(shading_normalizedViewportCoord.xy, temporalNoise);
    float noise = fract(sin(dot(uv_time, vec3(12.9898, 78.233, 45.678))) * 43758.5453);
    noise = clamp(noise * 2.0 - 1.0, -0.5, 0.5);
    return noise / 255.0;
}

void applyGlassEffect(inout vec4 albedo) {
    vec3 fragPosition = getWorldPosition();
    vec4 screenUV = frameUniforms.centerClipFromWorldMatrix * float4(fragPosition, 1.0);
    vec2 texUV = screenUV.xy / screenUV.w;
    texUV = texUV * 0.5 + vec2(0.5);
    texUV.y = 1.0 - texUV.y;
    vec3 groundGlassColor = texture(sampler2DArray(light_groundGlassMap, s_linear_clamp_sampler), vec3(texUV, object_uniforms.groundGlassIndex)).rgb;

    // groundGlassColor = rgbToSrgb(groundGlassColor);
    vec3 alphaBlendResult = blendAlpha(groundGlassColor,materialParams.glassEffectAlphaBlendColor.rgb,materialParams.glassEffectAlphaBlendColor.a);
    vec3 colorDodgeBlendResult = blendColorDodge(alphaBlendResult,materialParams.glassEffectColorDodgeColor.rgb,materialParams.glassEffectColorDodgeColor.a);
    albedo.rgb = albedo.rgb + colorDodgeBlendResult * (1.0 - albedo.a);
    albedo.rgb += dither_TriangleNoise0(frameUniforms.temporalNoise);
    albedo.a = 1.0;

    albedo = vec4(albedo.rgb * ((albedo.rgb * (albedo.rgb * 0.34159177 + 0.63539077) + 0.02607818)), albedo.a);
}

#endif