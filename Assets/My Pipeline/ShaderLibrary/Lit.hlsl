#ifndef MYRP_LIT_INCLUDE
#define MYRP_LIT_INCLUDE

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Shadow/ShadowSamplingTent.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/ImageBasedLighting.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/EntityLighting.hlsl"
#include "Lighting.hlsl"

CBUFFER_START(UnityPerCamera)
  float3 _WorldSpaceCameraPos;
CBUFFER_END

CBUFFER_START(UnityPerFrame)   //逐帧
float4x4 unity_MatrixVP;
float4 _DitherTexture_ST;
CBUFFER_END

CBUFFER_START(UnityPerDraw)  
float4x4 unity_ObjectToWorld,unity_WorldToObject;
float4 unity_LODFade;
float4 unity_LightIndicesOffsetAndCount;
float4 unity_4LightIndices0, unity_4LightIndices1;
//shadow Probe
float4 unity_ProbesOcclusion;
float4 unity_SpecCube0_BoxMin,unity_SpecCube0_BoxMax;
float4 unity_SpecCube0_ProbePosition,unity_SpecCube0_HDR;
//第二个光照探针
float4 unity_SpecCube1_BoxMin,unity_SpecCube1_BoxMax;
float4 unity_SpecCube1_ProbePosition,unity_SpecCube1_HDR;
//lightmap
float4 unity_LightmapST,unity_DynamicLightmapST;
//光照探针
float4 unity_SHAr,unity_SHAg,unity_SHAb;
float4 unity_SHBr,unity_SHBg,unity_SHBb;
float4 unity_SHC;
CBUFFER_END

//最大可见灯光数量 宏定义
#define MAX_VISIBLE_LIGHTS 16 

CBUFFER_START(_LightBuffer)
float4 _VisibleLightColors[MAX_VISIBLE_LIGHTS]; //灯光颜色
float4 _VisibleLightDirectionsOrPositions[MAX_VISIBLE_LIGHTS]; //灯光方向
float4 _VisibleLightAttenuations[MAX_VISIBLE_LIGHTS]; //点灯光的距离衰减
float4 _VisibleLightSpotDirections[MAX_VISIBLE_LIGHTS]; //聚光灯方向
float4 _VisibleLightOcclusionMasks[MAX_VISIBLE_LIGHTS]; //ShadowMask 方向
CBUFFER_END

CBUFFER_START(_ShadowBuffer)
  //float4x4 _WorldToShadowMatrix;
  //float _ShadowStrength;
  float4x4 _WorldToShadowMatrices[MAX_VISIBLE_LIGHTS];
  float4x4 _WorldToShadowCascadeMatrices[5];
  float4 _CascadeCullingSpheres[4];
  float4 _ShadowData[MAX_VISIBLE_LIGHTS];
  float4 _ShadowMapSize;
  float4 _CascadedShadowMapSize;
  float4 _GlobalShadowData;
  float _CascadedShadowStrength;
  float4 _SubtractiveShadowColor;
CBUFFER_END

CBUFFER_START(UnityPerMaterial)
  float4 _MainTex_ST;
  float _Cutoff;
  //float _Smoothness;
CBUFFER_END

CBUFFER_START(UnityProbeVolume)
  float4 unity_ProbeVolumeParams;
  float4x4 unity_ProbeVolumeWorldToObject;
  float3 unity_ProbeVolumeSizeInv;
  float3 unity_ProbeVolumeMin;
CBUFFER_END

TEXTURE2D_SHADOW(_ShadowMap);
SAMPLER_CMP(sampler_ShadowMap);

TEXTURE2D_SHADOW(_CascadedShadowMap);
SAMPLER_CMP(sampler_CascadedShadowMap);
//Alpha
TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);
//LightMap
TEXTURE2D(unity_Lightmap);
SAMPLER(samplerunity_Lightmap);
//dynamic LightMap
TEXTURE2D(unity_DynamicLightmap);
SAMPLER(samplerunity_DynamicLightmap);
//TEXTURECUBE(unity_SpecCube0);
//TEXTURECUBE(unity_SpecCube1);
//SAMPLER(samplerunity_SpecCube0);
TEXTURE3D_FLOAT(unity_ProbeVolumeSH);
SAMPLER(samplerunity_ProbeVolumeSH);

TEXTURE2D(unity_ShadowMask);
SAMPLER(samplerunity_ShadowMask);

TEXTURE2D(_DitherTexture);
SAMPLER(sampler_DitherTexture);

#define UNITY_MATRIX_M unity_ObjectToWorld
#define UNITY_MATRIX_I_M unity_WorldToObject

#if !defined(LIGHTMAP_ON)
   #if defined(_SHADOWMASK) ||defined(_DISTANCE_SHADOWMASK) || \
       defined(_SUBTRACTIVE_LIGHTING)
      #defined SHADOWS_SHADOWMASK
   #endif
#endif

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"

UNITY_INSTANCING_BUFFER_START(PerInstance)   //对于Instance 属性颜色不一样
   UNITY_DEFINE_INSTANCED_PROP(float4,_Color)
   UNITY_DEFINE_INSTANCED_PROP(float,_Metallic)
   UNITY_DEFINE_INSTANCED_PROP(float,_Smoothness)
   UNITY_DEFINE_INSTANCED_PROP(float4,_EmissionColor)
UNITY_INSTANCING_BUFFER_END(PerInstance)

struct VertexInput
{
    float4 pos : POSITION;
    float3 normal : NORMAL;
    float2 uv:TEXCOORD0;
    float2 lightmapUV:TEXCOORD1;
    float2 dynamicLightmapUV:TEXCOORD2;
   UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct VertexOutput
{
    float4 clipPos : SV_POSITION;
    float3 normal : TEXCOORD0;
    float3 worldPos : TEXCOORD1;
    float3 vertexLighting : TEXCOORD2;
    float2 uv:TEXCOORD3;
    #if defined(LIGHTMAP_ON)
      float2 lightmapUV:TEXCOORD4;
    #endif
    #if defined(DYNAMICLIGHTMAP_ON) 
      float2 dynamicLightmapUV:TEXCOORD5;
    #endif
   UNITY_VERTEX_INPUT_INSTANCE_ID
};

float3 SampleLightProbes(LitSurface s)
{
  if(unity_ProbeVolumeParams.x)
  {
 float4 coefficients[7];
   coefficients[0] = unity_SHAr;
	coefficients[1] = unity_SHAg;
	coefficients[2] = unity_SHAb;
	coefficients[3] = unity_SHBr;
	coefficients[4] = unity_SHBg;
	coefficients[5] = unity_SHBb;
	coefficients[6] = unity_SHC;
   return max(0.0,SampleSH9(coefficients,s.normal));
  }else{
      return SampleProbeVolumeSH4(TEXTURE3D_PARAM(unity_ProbeVolumeSH,samplerunity_ProbeVolumeSH),
      s.position,s.normal,unity_ProbeVolumeWorldToObject,unity_ProbeVolumeParams.y,unity_ProbeVolumeParams.z,unity_ProbeVolumeMin,unity_ProbeVolumeSizeInv);
  }
}

float3 SampleLightmap(float2 uv)
{
   return SampleSingleLightmap(TEXTURE2D_PARAM(unity_Lightmap,samplerunity_Lightmap),uv,float4(1,1,0,0),
   #if defined(UNITY_LIGHTMAP_FULL_HDR)
      false,
   #else
      true,
   #endif
   float4(LIGHTMAP_HDR_MULTIPLIER,LIGHTMAP_HDR_EXPONENT,0.0,0.0)
   );
}

float3 SampleDynamicLightmap(float2 uv)
{
   return SampleSingleLightmap(TEXTURE2D_PARAM(unity_DynamicLightmap,samplerunity_DynamicLightmap),uv,float4(1,1,0,0),
      false,
   float4(LIGHTMAP_HDR_MULTIPLIER,LIGHTMAP_HDR_EXPONENT,0.0,0.0)
   );
}

float3 GlobalIllumination(VertexOutput input,LitSurface surface)
{
   #if defined(LIGHTMAP_ON)
      float3 gi= SampleLightmap(input.lightmapUV);
      #if defined(_SUBTRACTIVE_LIGHTING)
          gi=SubtractiveLighting(surface,gi);
      #endif
      #if defined(DYNAMICLIGHTMAP_ON)
         gi+=SampleDynamicLightmap(input.dynamicLightmapUV);
      #endif
      return gi;
   #elif defined(DYNAMICLIGHTMAP_ON)
		  return SampleDynamicLightmap(input.dynamicLightmapUV);
   #else
      return SampleLightProbes(surface);
   #endif
   //return 0;
}



float4 BakeShadows(VertexOutput input,LitSurface surface)
{
    #if defined(LIGHTMAP_ON)
       #if defined(_SHADOWMASK) || defined(_DISTANCE_SHADOWMASK)|| \
		       defined(_SUBTRACTIVE_LIGHTING)
          return SAMPLE_TEXTURE2D(unity_ShadowMask,samplerunity_ShadowMask,input.lightmapUV);
       #endif
    #elif defined(_SHADOWMASK) || defined(_DISTANCE_SHADOWMASK)
       if(unity_ProbeVolumeParams.x)
       {
         return SampleProbeOcclusion(TEXTURE3D_PARAM(unity_ProbeVolumeSH,sampleunity_ProbeVolumeSH),surface.position,
                  unity_ProbeVolumeWorldToObject,unity_ProbeVolumeParams.y,unity_ProbeVolumeParams.z,unity_ProbeVolumeMin,unity_ProbeVolumeSizeInv);
       }
       return unity_ProbesOcclusion;
    #endif
    return 1.0;
}

float3 BoxProjection(float3 direction,float3 position,float4 cubemapPosition,float4 boxMin,float4 boxMax)
{
    UNITY_BRANCH
    if(cubemapPosition.w>0)
    {
       float3 factors=((direction>0?boxMax.xyz:boxMin.xyz)-position)/direction;
       float scalar=min(min(factors.x,factors.y),factors.z);
       direction=direction*scalar+(position-cubemapPosition.xyz);
    }
    return direction;
}
//
// float DistanceToCameraSqr(float3 worldPos)
// {
//   float3 cameraToFragment=worldPos-_WorldSpaceCameraPos;
//   return dot(cameraToFragment,cameraToFragment);
// }

//Blend factor
float RealtimeToBakedShadowsInterpolator(float3 worldPos)
{
   float d=distance(worldPos,_WorldSpaceCameraPos);
   return saturate(d*_GlobalShadowData.y+_GlobalShadowData.z);
}

float MixRealtimeAndBakedShadowAttenuation(float realtime,float4 bakedShadows,int lightIndex,float3 worldPos,bool isMainLight=false)
{
   float t=RealtimeToBakedShadowsInterpolator(worldPos);
   float fadedRealtime=saturate(realtime+t);
   float4 occlusionMask=_VisibleLightOcclusionMasks[lightIndex];
   float baked=dot(bakedShadows,occlusionMask);
   bool hasBakedShadows=occlusionMask.x>=0.0;
   #if defined(_SHADOWMASK)
      if(hasBakedShadows)
      {
         return min(fadedRealtime,baked);
      }
   #elif defined(_DISTANCE_SHADOWMASK)
       if(hasBakedShadows)
       {
           bool bakedOnly=_VisibleLightSpotDirections[lightIndex].w>0.0;
           if(!isMainLight&&bakedOnly)
           {
             return baked;
           }
           return lerp(realtime,baked,t);
       }
    #elif defined(_SUBTRACTIVE_LIGHTING)
      #if !defined(LIGHTMAP_ON)
         if(isMainLight)
         {
           return min(fadedRealtime,bakedShadows.x);
         }
      #endif
      #if !defined(_CASCADED_SHADOWS_HARD) && !defined(_CASCADED_SHADOWS_SOFT)
         if(lightIndex==0)
         {
             return bakedShadows.x;
         }
      #endif
   #endif
   return fadedRealtime;
}

bool SkipRealtimeShadows(float3 worldPos)
{
  return RealtimeToBakedShadowsInterpolator(worldPos)>=1.0;
}

float InsideCascadeCullingSphere(int index,float3 worldPos)
{
   float4 s=_CascadeCullingSpheres[index];
   return dot(worldPos-s.xyz,worldPos-s.xyz)<s.w;
}

float HardShadowAttenuation(float4 shadowPos,bool cascade=false)
{
	if (cascade) {
		return SAMPLE_TEXTURE2D_SHADOW(_CascadedShadowMap, sampler_CascadedShadowMap, shadowPos.xyz);
	}
	else {
		return SAMPLE_TEXTURE2D_SHADOW(_ShadowMap, sampler_ShadowMap, shadowPos.xyz);
	}	
}

float SoftShadowAttenuation(float4 shadowPos,bool cascade=false)
{
      real tentWeights[9];
	  real2 tentUVs[9];
	  float4 size = cascade ? _CascadedShadowMapSize : _ShadowMapSize;
	  SampleShadow_ComputeSamples_Tent_5x5(size,shadowPos.xy,tentWeights,tentUVs);
	  float  attenuation=0;
	  for(int i=0;i<9;i++)
	 {
	   //attenuation+=tentWeights[i]*SAMPLE_TEXTURE2D_SHADOW(_ShadowMap,sampler_ShadowMap,float3(tentUVs[i].xy,shadowPos.z));
		  attenuation += tentWeights[i] * HardShadowAttenuation(float4(tentUVs[i].xy,shadowPos.z,0), cascade);
	 }
     return attenuation;
}

float CascadedShadowAttenuation(float3 worldPos,bool applyStrength=true)
{
   #if !defined(_RECEIVE_SHADOWS) 
       return 1.0;
   #elif !defined(_CASCADED_SHADOWS_HARD) && !defined(_CASCADED_SHADOWS_SOFT) 
    	return 1.0;
  #endif
  if(SkipRealtimeShadows(worldPos))
  {
     return 1.0;
  }
  float4 cascadeFlags=float4(InsideCascadeCullingSphere(0,worldPos),
                            InsideCascadeCullingSphere(1,worldPos),
                            InsideCascadeCullingSphere(2,worldPos),
                            InsideCascadeCullingSphere(3,worldPos));
   cascadeFlags.yzw=saturate(cascadeFlags.yzw-cascadeFlags.xyz);
		float cascadedIndex = 4-dot(cascadeFlags,float4(4,3,2,1));
	float4 shadowPos = mul(_WorldToShadowCascadeMatrices[cascadedIndex],float4(worldPos,1.0));
  float attenuation;
  #if defined(_CASCADED_SHADOWS_HARD)
    attenuation=HardShadowAttenuation(shadowPos,true);
  #else
     attenuation=SoftShadowAttenuation(shadowPos,true);
  #endif
  if(applyStrength)
  {
     return lerp(1,attenuation,_CascadedShadowStrength);
  }
  else
  {
     return attenuation;
  }
}

float3 MainLight(LitSurface s,float shadowAttenuation)
{
   //float shadowAttenuation=CascadedShadowAttenuation(s.position);
   float3 lightColor=_VisibleLightColors[0].rgb;
   float3 lightDirection=_VisibleLightDirectionsOrPositions[0].xyz;
   //float diffuse=saturate(dot(normal,lightDirection));
   float3 color=LightSurface(s,lightDirection);
   color*=shadowAttenuation;
   return color*lightColor;
}

float ShadowAttenuation(int index,float3 worldPos)
{
    #if !defined(_RECEIVE_SHADOWS)
        return 1.0;
    #elif !defined(_SHADOWS_HARD) && !defined(_SHADOWS_SOFT)
       return 1.0;
    #endif
    if(_ShadowData[index].x<=0 ||SkipRealtimeShadows(worldPos)){
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

float3 SampleEnvironment(LitSurface s)
{
   float3 reflectVector=reflect(-s.viewDir,s.normal);
   float mip=PerceptualRoughnessToMipmapLevel(s.perceptualRoughness);

   float3 uvw=BoxProjection(reflectVector,s.position,unity_SpecCube0_ProbePosition,unity_SpecCube0_BoxMin,unity_SpecCube0_BoxMax);
   float4 sample=SAMPLE_TEXTURECUBE_LOD(unity_SpecCube0,samplerunity_SpecCube0,uvw,mip);
   float3 color=DecodeHDREnvironment(sample,unity_SpecCube0_HDR);
   //两个反射探针进行混合
   float blend=unity_SpecCube0_BoxMin.w;
   if(blend<0.99999)
   {
      uvw=BoxProjection(reflectVector,s.position,unity_SpecCube1_ProbePosition,unity_SpecCube1_BoxMin,unity_SpecCube1_BoxMax);
      sample=SAMPLE_TEXTURECUBE_LOD(unity_SpecCube1,samplerunity_SpecCube0,uvw,mip);
      color=lerp(DecodeHDREnvironment(sample,unity_SpecCube1_HDR),color,blend);
   }
   return color;
}

float3 GenericLight
    (
    int index, LitSurface s, float shadowAttenuation)
{
    float3 lightColor = _VisibleLightColors[index].rgb;
    float4 lightPositionOrDirection = _VisibleLightDirectionsOrPositions[index];
    float4 lightAttenuation = _VisibleLightAttenuations[index];
    float3 spotDirection = _VisibleLightSpotDirections[index].xyz;

    float3 lightVector = lightPositionOrDirection.xyz - s.position * lightPositionOrDirection.w;
    float3 lightDirection = normalize(lightVector);
    //float diffuse = saturate(dot(normal, lightDirection));
    float3 color=LightSurface(s,lightDirection);
   //计算灯光范围衰减,在灯光外是暗的照不亮的
    float rangeFade = dot(lightVector, lightVector) * lightAttenuation.x;
    rangeFade = saturate(1 - rangeFade * rangeFade);
    rangeFade *= rangeFade;
   
    float spotFade = dot(spotDirection, lightDirection);
    spotFade = saturate(spotFade * lightAttenuation.z + lightAttenuation.w);
    spotFade *= spotFade;

    float distanceSqr = max(dot(lightVector, lightVector), 0.00001);

    color *= shadowAttenuation *
    spotFade * rangeFade / distanceSqr;
    return color * lightColor;
}

float3 SubtractiveLighting(LitSurface s,float3 bakedLighting)
{
   float3 lightColor=_VisibleLightColors[0].rgb;
   float3 lightDirection=_VisibleLightDirectionsOrPositions[0].xyz;
   float diffuse=lightColor*saturate(dot(lightDirection,s.normal));
   float shadowAttenuation=saturate(CascadedShadowAttenuation(s.position,false)+RealtimeToBakedShadowsInterpolator(s.position));
   float3 shadowedLightingGuess=diffuse*(1.0-shadowAttenuation);
   float3 subtractedLighting=bakedLighting-shadowedLightingGuess;
   subtractedLighting=max(subtractedLighting,_SubtractiveShadowColor);
   subtractedLighting=lerp(bakedLighting,subtractedLighting,_CascadedShadowStrength);
   return min(bakedLighting,subtractedLighting);
}

void LODCrossFadeClip(float4 clipPos)
{
  float2 ditherUV=TRANSFORM_TEX(clipPos.xy,_DitherTexture);
   float lodClipBias=SAMPLE_TEXTURE2D(_DitherTexture,sampler_DitherTexture,ditherUV).a;
   if(unity_LODFade.x<0.5)
   {
       lodClipBias=1.0-lodClipBias;
   }
   clip(unity_LODFade.x-lodClipBias);
}

VertexOutput LitPassVertex
    (VertexInput input)
{
    VertexOutput output;
    float4 worldPos = mul(UNITY_MATRIX_M, float4(input.pos.xyz, 1.0));
    output.clipPos = mul(unity_MatrixVP, worldPos);
    #if defined(UNITY_ASSUME_UNIFORM_SCALING)  //缩放产生不一样效果是跟法线有问题 转换到模型空间
       output.normal = mul((float3x3) UNITY_MATRIX_M, input.normal);
    #else
       output.normal=normalize(mul(input.normal,(float3x3)UNITY_MATRIX_I_M));
    #endif
    output.worldPos = worldPos.xyz;
    output.uv=TRANSFORM_TEX(input.uv,_MainTex);
    #if defined(LIGHTMAP_ON)
      output.lightmapUV=input.lightmapUV*unity_LightmapST.xy+unity_LightmapST.zw;
    #endif
   //顶点光输出先设置为0  顶点光
    LitSurface surface=GetLitSurfaceVertex(output.normal,output.worldPos);
    output.vertexLighting = 0;
    for (int i = 4; i < min(unity_LightIndicesOffsetAndCount.y, 8); i++)
    {
        int lightIndex = unity_4LightIndices1[i - 4];
        output.vertexLighting += GenericLight
        (lightIndex, surface, 1);
    }
    #if defined(DYNAMICLIGHTMAP_ON)
       output.dynamicLightmapUV=input.dynamicLightmapUV*unity_DynamicLightmapST.xy+unity_DynamicLightmapST.zw;
    #endif
    return output;
}

float4 LitPassFragment
    (VertexOutput input,FRONT_FACE_TYPE isFrontFace:FRONT_FACE_SEMANTIC) :
    SV_TARGET
{
    UNITY_SETUP_INSTANCE_ID(input);
    #if defined(LOD_FADE_CROSSFADE)
       LODCrossFadeClip(input.clipPos);
    #endif
    input.normal = normalize(input.normal);
    input.normal=IS_FRONT_VFACE(isFrontFace,input.normal,-input.normal);
    //float3 albedo = UNITY_ACCESS_INSTANCED_PROP(PreInstance, _Color).rgb;
    float4 albedoAlpha=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,input.uv);
    albedoAlpha*=UNITY_ACCESS_INSTANCED_PROP(PreInstance,_Color);
    
    #if defined(_CLIPPING_ON)
      clip(albedoAlpha.a-_Cutoff);
    #endif
    //视野方向
    float3 viewDir=normalize(_WorldSpaceCameraPos-input.worldPos.xyz);
    //得到表面结构
    LitSurface surface=GetLitSurface(input.normal,input.worldPos,viewDir,albedoAlpha.rgb,
    UNITY_ACCESS_INSTANCED_PROP(PerInstance,_Metallic),
    UNITY_ACCESS_INSTANCED_PROP(PerInstance,_Smoothness));
    #if defined(_PREMULTIPLY_ALPHA)
       PremultiplyAlpha(surface,albedoAlpha.a);
    #endif
    //float3 diffuseLight = input.vertexLighting;
    //Bake Shadow
    float4 bakedShadows=BakeShadows(input,surface);
    float3 color=input.vertexLighting*surface.diffuse;
    #if defined(_CASCADED_SHADOWS_HARD) || defined(_CASCADED_SHADOWS_SOFT)
       #if !(defined(LIGHTMAP_ON)&&defined(_SUBTRACTIVE_LIGHTING))
       float shadowAttenuation=MixRealtimeAndBakedShadowAttenuation(CascadedShadowAttenuation(surface.position),bakedShadows,0,surface.position,true);
       color+=MainLight(surface,shadowAttenuation);
       #endif
    #endif
    for (int i = 0; i < min(unity_LightIndicesOffsetAndCount.y, 4); i++)
    {
        int lightIndex = unity_4LightIndices0[i];
        //float shadowAttenuation = ShadowAttenuation(lightIndex,input.worldPos);
        float shadowAttenuation=MixRealtimeAndBakedShadowAttenuation(ShadowAttenuation(lightIndex,surface.position),bakedShadows,lightIndex,surface.position);
        color += GenericLight(lightIndex, surface, shadowAttenuation);
    }
   
   //float3 diffuseLight=saturate(dot(input.normal,float3(0,1,0)));

    //float3 color = diffuseLight * albedoAlpha.rgb;
    color+=ReflectEnvironment(surface,SampleEnvironment(surface));
    //直接光+全局光照 Lightmap
    color+=GlobalIllumination(input,surface)*surface.diffuse;
    //直接自发光
    color+=UNITY_ACCESS_INSTANCED_PROP(PerInstance,_EmissionColor).rgb;
    return float4(color, albedoAlpha.a);
}

#endif //MYRP_LIT_INCLUDE