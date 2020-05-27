Shader "My Pipeline/Unlit"
{
    Properties{
        _Color("Color",Color)=(1,1,1,1)
        _MainTex("Albedo & Alpha",2D)="white"{}
        _Cutoff("Alpha Cutoff",Range(0,1))=0.5
    }

    SubShader
    {
        Pass{
            HLSLPROGRAM   //HLSL程序
            
            #pragma target 3.5
            //GPU instanceing
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling

            #pragma vertex UnlitPassVertex
            #pragma fragment UnlitPassFragment

            #include "../ShaderLibrary/Unlit.hlsl"
            ENDHLSL
        }
    }
}
