Shader "MimoGarden/SoftSteam"
{
    Properties { _BaseColor ("Steam", Color) = (1,.97,.9,.15) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
            CBUFFER_END
            struct A { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; };
            V Vert(A i) { V o; o.vertex = TransformObjectToHClip(i.vertex.xyz); o.uv = i.uv; return o; }
            half4 Frag(V i) : SV_Target
            {
                float2 p = i.uv * 2 - 1;
                half soft = saturate(1 - dot(p,p));
                return half4(_BaseColor.rgb, soft * soft * _BaseColor.a);
            }
            ENDHLSL
        }
    }
}
