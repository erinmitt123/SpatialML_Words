
void mx_texturesize(sampler2D tex_sampler, out float width, out float height, out float texelWidth, out float texelHeight)
{
    width = textureSize(tex_sampler, 0).x;
    height = textureSize(tex_sampler, 0).y;
    if (width < 1)
    {
        width = 1.0;
    }
    if (height < 1)
    {
        height = 1.0;
    }
    texelWidth = 1.0/width;
    texelHeight = 1.0/height;
}
