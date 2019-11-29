Shader "My Pipeline/Lit"
{
    Properties{
        _Color ("Color",Color) = (1,1,1,1)
    }

    SubShader
    {
        Pass{
            HLSLPROGRAM   //HLSL程序
            
            #pragma target 3.5
            //GPU instanceing
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling
            
            #pragma multi_compile _ _SHADOWS_HARD
            #pragma multi_compile _ _SHADOWS_SOFT

            #pragma vertex LitPassVertex
            #pragma fragment LitPassFragment

            #include "../ShaderLibrary/Lit.hlsl"
            ENDHLSL
        }

		 Pass{
		    Tags{ "LightMode"="ShadowCaster"	}
            HLSLPROGRAM   //HLSL程序
            
            #pragma target 3.5
            //GPU instanceing
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling

			// #pragma multi_compile _ _SHADOWS_HARD
            // #pragma multi_compile _ _SHADOWS_SOFT

            #pragma vertex ShadowCasterPassVertex
            #pragma fragment ShadowCasterPassFragment

            #include "../ShaderLibrary/ShadowCaster.hlsl"
            ENDHLSL
        }
    }
}
