
void mx_geometrymodifier(vec3 modelPositionOffset, vec4 color, vec3 normal,vec3 bitangent,  vec2 uv0,vec2 uv1,vec4 uv2,vec4 uv3,vec4 uv4,vec4 uv5,vec4 uv6,vec4 uv7,inout MaterialVertexInputs material)
{
    #ifdef HAS_MODIFIER_POSITION
        if (SPATIAL_PIPELINE) {
            material.worldPosition.xyz += mulMat3x3Float3(inverse(getWorldToRootMatrix()), modelPositionOffset);
            }
        else {
            material.worldPosition.xyz += modelPositionOffset;
            }
    #endif
    #ifdef HAS_ATTRIBUTE_NORMAL
    /// RCP 传入的normal和tangent都是模型空间，需要转换到世界空间
        #ifdef HAS_MODIFIER_NORMAL
            material.worldNormal = transformObjectToWorldNormal(normalize(normal));
        #endif
        #ifdef HAS_MODIFIER_BITANGENT
            vec3 objectBitangent;
            if (SPATIAL_PIPELINE) {
                objectBitangent = mulMat3x3Float3(inverse(getWorldToRootMatrix()), normalize(bitangent));
            }else{
                objectBitangent = normalize(bitangent);
            }
            vec3 objectNormal = transformObjectToWorldNormal(material.worldNormal);
            vec3 objectTangent = normalize(cross(objectBitangent,objectNormal));
            vertex_worldTangent.xyz = mulMat4x4Float3(getWorldFromModelMatrix(), objectTangent).xyz;// * sign(vertex_worldTangent.w));
            vertex_worldTangent.xyz *= sign(vertex_worldTangent.w);
        #endif
    #endif
    #if defined(HAS_ATTRIBUTE_COLOR) && defined(HAS_MODIFIER_COLOR)
        material.color_0 = color;
    #endif
    #if defined(HAS_ATTRIBUTE_UV0) && defined(HAS_MODIFIER_UV0)
        material.uv0 = uv0;
    #endif
    #if defined(HAS_ATTRIBUTE_UV1) && defined(HAS_MODIFIER_UV1)
        material.uv1 = uv1;
    #endif
    #if defined(HAS_ATTRIBUTE_UV2) && defined(HAS_MODIFIER_UV2)
        material.texcoord_2 = uv2;
    #endif
    #if defined(HAS_ATTRIBUTE_UV3) && defined(HAS_MODIFIER_UV3)
        material.texcoord_3 = uv3;
    #endif
    #if defined(HAS_ATTRIBUTE_UV4) && defined(HAS_MODIFIER_UV4)
        material.texcoord_4 = uv4;
    #endif
    #if defined(HAS_ATTRIBUTE_UV5) && defined(HAS_MODIFIER_UV5)
        material.texcoord_5 = uv5;
    #endif
    #if defined(HAS_ATTRIBUTE_UV6) && defined(HAS_MODIFIER_UV6)
        material.texcoord_6 = uv6;
    #endif
    #if defined(HAS_ATTRIBUTE_UV7) && defined(HAS_MODIFIER_UV7)
        material.texcoord_7 = uv7;
    #endif

}
