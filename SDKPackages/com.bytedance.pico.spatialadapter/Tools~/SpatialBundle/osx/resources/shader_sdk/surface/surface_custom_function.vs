#ifndef _ENGINE_CUSTOM_FUNCTION_VS
#define _ENGINE_CUSTOM_FUNCTION_VS

// 该文件用于在vertex各个阶段插入自定义功能

// 该结构用于在各个阶段之间传输数据
struct CustomData {
    float padding;
}customData;

// 位于vertex中materialVertex()用户定制材质函数之前
void beforeMaterialVertex(inout MaterialVertexInputs material) {

}

// 位于vertex中materialVertex()用户定制材质函数之后
void afterMaterialVertex(inout MaterialVertexInputs material) {

}

#endif
