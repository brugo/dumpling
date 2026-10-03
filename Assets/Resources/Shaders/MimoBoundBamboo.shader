Shader "MimoGarden/BoundBamboo"
{
    Properties
    {
        [MainColor] _BaseColor ("Natural split bamboo", Color) = (.85,.74,.58,1)
        [MainTexture] _BaseMap ("Generated bamboo fibre", 2D) = "white" {}
        _GrainStrength ("Natural fibre variation", Range(0,1)) = 0
    }
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
                half _GrainStrength;
            CBUFFER_END
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
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
                // Mesh UVs follow each bent band, including rotated handles, without projection seams.
                float2 grainUV = i.uv;
                half3 fibre = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, grainUV).rgb;
                // Normalize the generated scan's measured linear mean to preserve the approved bamboo palette.
                half3 variation = lerp(half3(1,1,1), fibre / half3(.7227,.4852,.2425), _GrainStrength);
                half3 bamboo = _BaseColor.rgb * variation;
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
