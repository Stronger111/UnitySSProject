﻿Shader "Hidden/My Pipeline/PostEffectStack"
{
    SubShader
    {
        Cull Off
        ZTest Always
        ZWrite Off
        HLSLINCLUDE
        #include "../ShaderLibrary/PostEffectStack.hlsl"
        ENDHLSL

        Pass  //0 copy
        {
        //    Cull Off
        //    ZTest Always
        //    ZWrite Off

           HLSLPROGRAM
           #pragma target 3.5
           #pragma vertex DefaultPassVertex
           #pragma fragment CopyPassFragment
           ENDHLSL
        }

        Pass  //1 blur
        {
	        HLSLPROGRAM
			#pragma target 3.5
			#pragma vertex DefaultPassVertex
			#pragma fragment BlurPassFragment
			ENDHLSL
        }

        Pass { // 2 DepthStripes
			HLSLPROGRAM
			#pragma target 3.5
			#pragma vertex DefaultPassVertex
			#pragma fragment DepthStripesPassFragment
			ENDHLSL
		}

		Pass { // 3 ToneMapping
			HLSLPROGRAM
			#pragma target 3.5
			#pragma vertex DefaultPassVertex
			#pragma fragment ToneMappingPassFragment
			ENDHLSL
		}
    }
}
