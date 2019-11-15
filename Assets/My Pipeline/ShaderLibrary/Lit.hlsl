#ifndef MYRP_LIT_INCLUDE
#define MYRP_LIT_INCLUDE

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
CBUFFER_START(UnityPerFrame)   //逐帧
   float4x4 unity_MatrixVP;
CBUFFER_END

CBUFFER_START(UnityPerDraw)  
  float4x4 unity_ObjectToWorld;
  float4 unity_LightIndicesOffsetAndCount;
  float4 unity_4LightIndices0, unity_4LightIndices1;
CBUFFER_END

//最大可见灯光数量 宏定义
#define MAX_VISIBLE_LIGHTS 16 

CBUFFER_START(_LightBuffer)
  float4 _VisibleLightColors[MAX_VISIBLE_LIGHTS];  //灯光颜色
  float4 _VisibleLightDirectionsOrPositions[MAX_VISIBLE_LIGHTS];  //灯光方向
  float4 _VisibleLightAttenuations[MAX_VISIBLE_LIGHTS];  //点灯光的距离衰减
  float4 _VisibleLightSpotDirections[MAX_VISIBLE_LIGHTS];
CBUFFER_END

#define UNITY_MATRIX_M unity_ObjectToWorld

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"

UNITY_INSTANCING_BUFFER_START(PerInstance)   //对于Instance 属性颜色不一样
   UNITY_DEFINE_INSTANCED_PROP(float4,_Color)
UNITY_INSTANCING_BUFFER_END(PerInstance)

struct VertexInput
{
   float4 pos:POSITION;
   float3 normal:NORMAL;
   UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct VertexOutput
{
   float4 clipPos:SV_POSITION;
   float3 normal:TEXCOORD0;
   float3 worldPos:TEXCOORD1;
   float3 vertexLighting:TEXCOORD2;
   UNITY_VERTEX_INPUT_INSTANCE_ID
};

float3 DiffuseLight(int index,float3 normal,float3 worldPos)
{
   float3 lightColor=_VisibleLightColors[index].rgb;
   float4 lightPositionOrDirection=_VisibleLightDirectionsOrPositions[index];
   float4 lightAttenuation=_VisibleLightAttenuations[index];
   float3 spotDirection=_VisibleLightSpotDirections[index].xyz;

   float3 lightVector=lightPositionOrDirection.xyz-worldPos*lightPositionOrDirection.w;
   float3 lightDirection=normalize(lightVector);
   float diffuse=saturate(dot(normal,lightDirection));
   //计算灯光范围衰减,在灯光外是暗的照不亮的
   float rangeFade=dot(lightVector,lightVector)*lightAttenuation.x;
   rangeFade=saturate(1-rangeFade*rangeFade);
   rangeFade*=rangeFade;
   
   float spotFade=dot(spotDirection,lightDirection);
   spotFade=saturate(spotFade*lightAttenuation.z+lightAttenuation.w);
   spotFade*=spotFade;

   float distanceSqr = max(dot(lightVector, lightVector), 0.00001);

   diffuse*=spotFade*rangeFade/distanceSqr;
   return diffuse*lightColor;
}

VertexOutput LitPassVertex(VertexInput input)
{
   VertexOutput output;
   float4 worldPos=mul(UNITY_MATRIX_M,float4(input.pos.xyz,1.0));
   output.clipPos=mul(unity_MatrixVP,worldPos);
   output.normal=mul((float3x3)UNITY_MATRIX_M,input.normal);
   output.worldPos=worldPos.xyz;

   return output;
}

float4 LitPassFragment(VertexOutput input) : SV_TARGET
{
   UNITY_SETUP_INSTANCE_ID(input);
   input.normal=normalize(input.normal);
   float3 albedo=UNITY_ACCESS_INSTANCED_PROP(PreInstance,_Color).rgb;
   
   float3 diffuseLight=0;
   for(int i=0;i<min(unity_LightIndicesOffsetAndCount.y,4);i++)
   {
      int lightIndex=unity_4LightIndices0[i];
      diffuseLight+=DiffuseLight(lightIndex,input.normal,input.worldPos);
   }
    for(int i=4;i<min(unity_LightIndicesOffsetAndCount.y,8);i++)
   {
      int lightIndex=unity_4LightIndices1[i-4];
      diffuseLight+=DiffuseLight(lightIndex,input.normal,input.worldPos);
   }
   //float3 diffuseLight=saturate(dot(input.normal,float3(0,1,0)));

   float3 color=diffuseLight*albedo;
   return float4(color,1);
}

#endif //MYRP_LIT_INCLUDE