Shader "MimoGarden/SoapBubbles"
{
    Properties { _SoapMask ("Soap coverage", 2D) = "black" {} }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+20" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_SoapMask); SAMPLER(sampler_SoapMask);
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 corner:TEXCOORD0; float2 skin:TEXCOORD1; float2 sizeSeed:TEXCOORD2; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; half2 response:TEXCOORD1; };
            Varyings Vert(Attributes i)
            {
                Varyings o;
                float3 world = TransformObjectToWorld(i.positionOS.xyz);
                float3 normal = TransformObjectToWorldNormal(i.normalOS);
                float coverage = SAMPLE_TEXTURE2D_LOD(_SoapMask, sampler_SoapMask, i.skin, 0).r;
                float front = smoothstep(-.15, .25, dot(normal, normalize(GetCameraPositionWS() - world)));
                float size = i.sizeSeed.x * smoothstep(.02, .65, coverage) * front;
                float scale = length(TransformObjectToWorldDir(float3(1,0,0), false));
                float3 view = TransformWorldToView(world);
                view.xy += i.corner * size * scale;
                o.positionCS = mul(UNITY_MATRIX_P, float4(view, 1));
                o.uv = i.corner; o.response = half2(coverage * front, i.sizeSeed.y);
                return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                float r2 = dot(i.uv, i.uv); clip(1 - r2); clip(i.response.x - .015);
                float z = sqrt(saturate(1 - r2));
                float rim = pow(1 - z, 2.2);
                float ring = exp(-pow((sqrt(r2) - .88) * 17, 2));
                float phase = (z * 5 + i.uv.y * 1.4 + i.response.y * 4) * 6.283;
                half3 rainbow = .61 + .34 * cos(phase + half3(0,2.094,4.188));
                half3 color = lerp(half3(.78,.92,.97), rainbow, saturate(rim * .85 + ring * .55));
                float highlight = exp(-dot(i.uv - float2(-.32,.43), i.uv - float2(-.32,.43)) * 64);
                float crescent = exp(-pow((length(i.uv - float2(.08,-.07)) - .75) * 29, 2)) * smoothstep(-.15,.5,i.uv.y);
                color = lerp(color, 1, saturate(highlight * .95 + crescent * .75));
                float alpha = (.13 + rim * .49 + ring * .17 + highlight * .5 + crescent * .4) * smoothstep(1,.92,sqrt(r2));
                return half4(color, saturate(alpha) * smoothstep(.02,.25,i.response.x));
            }
            ENDHLSL
        }
    }
}
