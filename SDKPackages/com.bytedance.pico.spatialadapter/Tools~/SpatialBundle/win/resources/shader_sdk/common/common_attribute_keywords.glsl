#ifndef _ENGINE_PIPELINE_LAYOUT_ATTRIBUTE_KEYWORDS_GLSL
#define _ENGINE_PIPELINE_LAYOUT_ATTRIBUTE_KEYWORDS_GLSL

#define kDSetSharedSampler          0
#define kDSetUniformBuffer          1
#define kDSetCombinedImageSampler   2
#define kDSetSampledImage           3
#define kDSetStorageBuffer          4
#define kDSetStorageImage           5
#define kDSetInputAttachment        6

#define kVertex     1
#define kFragment   2
#define kCompute    4

#define kDomainSurface        1 //!< shaders applied to renderables
#define kDomainPostProcess    2 //!< shaders applied to rendered buffers
#define kDomainCompute        4 //!< compute shader
#define kDomainSpatial        8 //!< custom shaders

#define kWhite      0
#define kBlack      1
#define kGray       2
#define kRed        3
#define kGreen      4
#define kBlue       5
#define kYellow     6
#define kBump       7
#define kStorage    8

#define kSrgb       0
#define kP3         1
#define kRec2020    3
#define kAcescg     4
#define kLinear     6

#endif