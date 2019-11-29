#ifndef MYRP_LIT_INCLUDE
#define MYRP_LIT_INCLUDE

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Shadow/ShadowSamplingTent.hlsl"

CBUFFER_START(UnityPerCamera)
  float3 _WorldSpaceCameraPos;
CBUFFER_END

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

float4 _VisibleLightColors[MAX_VISIBLE_LIGHTS]; //灯光颜色
float4 _VisibleLightDirectionsOrPositions[MAX_VISIBLE_LIGHTS]; //灯光方向
float4 _VisibleLightAttenuations[MAX_VISIBLE_LIGHTS]; //点灯光的距离衰减
float4 _VisibleLightSpotDirections[MAX_VISIBLE_LIGHTS];
CBUFFER_END

CBUFFER_START(_ShadowBuffer)
  //float4x4 _WorldToShadowMatrix;
  //float _ShadowStrength;
  float4x4 _WorldToShadowMatrices[MAX_VISIBLE_LIGHTS];
  float4 _ShadowData[MAX_VISIBLE_LIGHTS];
  float4 _ShadowMapSize;
  float4 _GlobalShadowData;
CBUFFER_END

TEXTURE2D_SHADOW(_ShadowMap);
SAMPLER_CMP(sampler_ShadowMap);

#define UNITY_MATRIX_M unity_ObjectToWorld

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"

UNITY_INSTANCING_BUFFER_START(PerInstance)   //对于Instance 属性颜色不一样
   UNITY_DEFINE_INSTANCED_PROP(float4,_Color)
UNITY_INSTANCING_BUFFER_END(PerInstance)

struct VertexInput
{
    float4 pos : POSITION;
    float3 normal : NORMAL;
   UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct VertexOutput
{
    float4 clipPos : SV_POSITION;
    float3 normal : TEXCOORD0;
    float3 worldPos : TEXCOORD1;
    float3 vertexLighting : TEXCOORD2;
   UNITY_VERTEX_INPUT_INSTANCE_ID
};

float DistanceToCameraSqr(float3 worldPos)
{
  float3 cameraToFragment=worldPos-_WorldSpaceCameraPos;
  return dot(cameraToFragment,cameraToFragment);
}

float HardShadowAttenuation(float4 shadowPos)
{
  return SAMPLE_TEXTURE2D_SHADOW(_ShadowMap, sampler_ShadowMap, shadowPos.xyz);
}

float SoftShadowAttenuation(float4 shadowPos)
{
      real tentWeights[9];
	  real2 tentUVs[9];
	  SampleShadow_ComputeSamples_Tent_5x5(_ShadowMapSize,shadowPos.xy,tentWeights,tentUVs);
	  float  attenuation=0;
	  for(int i=0;i<9;i++)
	 {
	   attenuation+=tentWeights[i]*SAMPLE_TEXTURE2D_SHADOW(_ShadowMap,sampler_ShadowMap,float3(tentUVs[i].xy,shadowPos.z));
	 }
     return attenuation;
}

float ShadowAttenuation(int index,float3 worldPos)
{
    #if !defined(_SHADOWS_HARD) && !defined(_SHADOWS_SOFT)
       return 1.0;
    #endif
    if(_ShadowData[index].x<=0 || DistanceToCameraSqr(worldPos)>_GlobalShadowData.y){
        return 1.0;
    }
    float4 shadowPos = mul(_WorldToShadowMatrices[index], float4(worldPos, 1.0));
    shadowPos.xyz /= shadowPos.w;
    shadowPos.xy=saturate(shadowPos.xy);
    shadowPos.xy=shadowPos.xy*_GlobalShadowData.x+_ShadowData[index].zw;
    float attenuation ;
    #if defined(_SHADOWS_HARD)
      #if defined(_SHADOWS_SOFT)
    if(_ShadowData[index].y==0){   //标识为硬阴影 1为软阴影
       attenuation =HardShadowAttenuation(shadowPos);
    }else{
	 attenuation=SoftShadowAttenuation(shadowPos);
    }
      #else
       attenuation =HardShadowAttenuation(shadowPos);
      #endif
    #else
       attenuation=SoftShadowAttenuation(shadowPos);
    #endif
    return lerp(1, attenuation, _ShadowData[index].x);  //存取灯光强度
}

float3 DiffuseLight
    (
    int index, float3 normal, float3 worldPos, float shadowAttenuation)
{
    float3 lightColor = _VisibleLightColors[index].rgb;
    float4 lightPositionOrDirection = _VisibleLightDirectionsOrPositions[index];
    float4 lightAttenuation = _VisibleLightAttenuations[index];
    float3 spotDirection = _VisibleLightSpotDirections[index].xyz;

    float3 lightVector = lightPositionOrDirection.xyz - worldPos * lightPositionOrDirection.w;
    float3 lightDirection = normalize(lightVector);
    float diffuse = saturate(dot(normal, lightDirection));
   //计算灯光范围衰减,在灯光外是暗的照不亮的
    float rangeFade = dot(lightVector, lightVector) * lightAttenuation.x;
    rangeFade = saturate(1 - rangeFade * rangeFade);
    rangeFade *= rangeFade;
   
    float spotFade = dot(spotDirection, lightDirection);
    spotFade = saturate(spotFade * lightAttenuation.z + lightAttenuation.w);
    spotFade *= spotFade;

    float distanceSqr = max(dot(lightVector, lightVector), 0.00001);

    diffuse *= shadowAttenuation *
    spotFade * rangeFade / distanceSqr;
    return diffuse * lightColor;
}

VertexOutput LitPassVertex
    (VertexInput input)
{
    VertexOutput output;
    float4 worldPos = mul(UNITY_MATRIX_M, float4(input.pos.xyz, 1.0));
    output.clipPos = mul(unity_MatrixVP, worldPos);
    output.normal = mul((float3x3) UNITY_MATRIX_M, input.normal);
    output.worldPos = worldPos.xyz;
   //顶点光输出先设置为0
    output.vertexLighting = 0;
    for (int i = 4; i < min(unity_LightIndicesOffsetAndCount.y, 8); i++)
    {
        int lightIndex = unity_4LightIndices1[i - 4];
        output.vertexLighting += DiffuseLight(lightIndex, output.normal, output.worldPos, 1);
    }
    return output;
}

float4 LitPassFragment
    (VertexOutput input) :
    SV_TARGET
{
    UNITY_SETUP_INSTANCE_ID(input);
    input.normal = normalize(input.normal);
    float3 albedo = UNITY_ACCESS_INSTANCED_PROP(PreInstance, _Color).rgb;
   
    float3 diffuseLight = input.vertexLighting;
    for (int i = 0; i < min(unity_LightIndicesOffsetAndCount.y, 4); i++)
    {
        int lightIndex = unity_4LightIndices0[i];
        float shadowAttenuation = ShadowAttenuation(lightIndex,input.worldPos);
        diffuseLight += DiffuseLight(lightIndex, input.normal, input.worldPos, shadowAttenuation);
    }
   
   //float3 diffuseLight=saturate(dot(input.normal,float3(0,1,0)));

    float3 color = diffuseLight * albedo;
    return float4(color, 1);
}

#endif //MYRP_LIT_INCLUDE