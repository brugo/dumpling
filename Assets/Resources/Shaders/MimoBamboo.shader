Shader "MimoGarden/NaturalBamboo"
{
    Properties
    {
        [MainColor] _BaseColor ("Bamboo tint", Color) = (.8,.65,.4,1)
        _Smoothness ("Silky grain", Range(0,1)) = .28
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Smoothness;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionOS : TEXCOORD0; float3 positionWS : TEXCOORD1; half3 normalWS : TEXCOORD2; };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionOS = v.positionOS.xyz;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                float3 p = i.positionOS;
                half3 n = normalize(i.normalWS);
                // Continuous fine fibres on the curved wall; cross grain on the woven cover.
                float across = abs(n.y) > .65 ? p.z + p.x * .13 : atan2(p.z, p.x) * 1.2;
                float along = abs(n.y) > .65 ? p.x : p.y;
                float fibre = across * 190.0 + sin(along * 8.0 + across * 9.0) * 1.4;
                float visible = 1.0 - smoothstep(.7, 2.5, fwidth(fibre));
                half fine = sin(fibre) * .045 * visible;
                half ribbon = sin(across * 29.0 + sin(along * 2.0)) * .035;
                half pores = pow(saturate(sin(across * 67.0 + along * 1.5)), 14.0) * .045 * visible;
                half3 base = _BaseColor.rgb * (1.0 + fine + ribbon - pores);
                Light key = GetMainLight();
                half wrap = saturate((dot(n, key.direction) + .25) / 1.25);
                half3 view = SafeNormalize(GetWorldSpaceViewDir(i.positionWS));
                half highlight = pow(saturate(dot(n, SafeNormalize(view + key.direction))), 32.0) * _Smoothness * .22;
                half3 lit = base * (max(SampleSH(n), half3(.18,.18,.18)) * .8 + key.color * (.25 + wrap * .65));
                return half4(lit + highlight * key.color, 1);
            }
            ENDHLSL
        }
    }
}
