
void mx_environment_radiance(float3 baseColor, float roughness, float specular, float metallic, float3 normal, out float3 diffuseRadiance, out float3 specularRadiance)
{
    getEnvironmentRadiance(baseColor,roughness, specular, metallic, normal, diffuseRadiance, specularRadiance);
}
