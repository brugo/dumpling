Shader "MimoGarden/WovenBamboo"
{
    Properties { [MainColor] _BaseColor ("Natural split bamboo", Color) = (.85,.74,.58,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
            CBUFFER_END
            struct A { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct V { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; half3 normalWS:TEXCOORD1; float2 uv:TEXCOORD2; half4 color:COLOR; };
            V Vert(A i)
            {
                V o; o.positionWS = TransformObjectToWorld(i.positionOS.xyz); o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(i.normalOS); o.uv = i.uv; o.color = i.color; return o;
            }
            half4 Frag(V i):SV_Target
            {
                half3 n = normalize(i.normalWS);
                float fibre = i.uv.y * 46.0 + sin(i.uv.x * 9.0 + i.uv.y * 4.0) * .9;
                half visibility = 1.0 - smoothstep(.8, 2.4, fwidth(fibre));
                half grain = sin(fibre) * .035 + sin(fibre * 2.83 + i.uv.x * 3) * .017;
                half streak = sin(i.uv.y * 13.0 + sin(i.uv.x * 2.3)) * .025;
                half3 bamboo = _BaseColor.rgb * i.color.rgb * (1 + grain * visibility + streak);
                Light light = GetMainLight();
                half nl = saturate(dot(n, light.direction));
                half3 view = SafeNormalize(GetWorldSpaceViewDir(i.positionWS));
                half sheen = pow(saturate(dot(n, SafeNormalize(view + light.direction))), 42) * .10;
                half3 lit = bamboo * (max(SampleSH(n), half3(.15,.15,.15)) * .66 + light.color * (.16 + .78 * nl));
                return half4(lit + sheen * light.color, 1);
            }
            ENDHLSL
        }
    }
}
