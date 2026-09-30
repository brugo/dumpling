Shader "MimoGarden/FinishSparkles"
{
    Properties
    {
        [MainColor] _BaseColor ("Gel tint", Color) = (1,1,1,1)
        _Billboard ("Soft outer aura", Float) = 0
        _Intensity ("Sparkle brightness", Range(0,2)) = 1
        _FinishEnvironment ("Studio reflection", Cube) = "" {}
        _EnvironmentStrength ("Reflection strength", Float) = 1
        _SkinMask ("Washable skin coverage", 2D) = "white" {}
        _SoapMask ("Soap coverage", 2D) = "black" {}
        _GlitterVisibility ("Visible under clean gel", Range(0,1)) = 1
        [HideInInspector] _BodyCenter ("Gel center", Vector) = (0,1.2,0,0)
        [HideInInspector] _BodyExtents ("Gel dimensions", Vector) = (1.4,1.2,1.2,0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+10" }
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
            TEXTURECUBE(_FinishEnvironment); SAMPLER(sampler_FinishEnvironment);
            TEXTURE2D(_SkinMask); SAMPLER(sampler_SkinMask);
            TEXTURE2D(_SoapMask); SAMPLER(sampler_SoapMask);
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float4 _BodyCenter, _BodyExtents;
                half _Billboard, _Intensity, _EnvironmentStrength, _GlitterVisibility;
            CBUFFER_END
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float4 centerRadius : TEXCOORD1;
                float2 skinUV : TEXCOORD2;
                half4 color : COLOR;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                half4 response : TEXCOORD1;
                float2 skinUV : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 right = normalize(TransformViewToWorldDir(float3(1,0,0)));
                float3 up = normalize(TransformViewToWorldDir(float3(0,1,0)));
                float sx = length(TransformObjectToWorldDir(float3(1,0,0), false));
                float sy = length(TransformObjectToWorldDir(float3(0,1,0), false));
                output.uv = input.uv;
                output.skinUV = input.skinUV;
                if (_Billboard > .5h)
                {
                    float3 center = TransformObjectToWorld(float3(0,0,0));
                    float3 forward = SafeNormalize(_WorldSpaceCameraPos - center);
                    positionWS = center + right * input.positionOS.x * sx + up * input.positionOS.y * sy - forward * .4;
                    output.positionCS = TransformWorldToHClip(positionWS);
                    return output;
                }

                float3 centerWS = TransformObjectToWorld(input.centerRadius.xyz);
                half3 view = SafeNormalize(GetWorldSpaceViewDir(centerWS));
                half3 normal = normalize(TransformObjectToWorldNormal(input.normalOS));
                normal *= dot(normal, view) < 0 ? -1.0h : 1.0h;
                half3 reflected = reflect(-view, normal);
                half3 environment = SAMPLE_TEXTURECUBE_LOD(_FinishEnvironment, sampler_FinishEnvironment, reflected, .4).rgb * _EnvironmentStrength;
                half3 lightDir = SafeNormalize(_MainLightPosition.xyz + float3(0,.0001,0));
                half specular = pow(saturate(dot(normal, SafeNormalize(lightDir + view))), 105.0h);
                half reflectedLight = dot(environment, half3(.2126h,.7152h,.0722h));
                half reflection = saturate(reflectedLight * .78h + specular * 1.2h);
                // A foil reflects the full angular size of a softbox. Restricting
                // flashes to its tiny HDR peak loses all sparkle at phone scale.
                half flash = max(smoothstep(.52h, 1.62h, reflectedLight), specular);

                // Beer-Lambert approximation: far-side foil is muted by its path
                // through the gel. It never becomes an opaque dark particle.
                float3 extents = max(_BodyExtents.xyz, float3(.05,.05,.05));
                float3 p = (input.centerRadius.xyz - _BodyCenter.xyz) / extents;
                float3 ray = normalize(TransformWorldToObjectDir(view, false)) / extents;
                float b = dot(p, ray), a = max(dot(ray, ray), .0001);
                float distanceInGel = max(0, (-b + sqrt(max(0, b*b - a * (dot(p,p) - 1)))) / a);
                half transmission = exp2(-distanceInGel * .62);
                half grazing = pow(1.0h - saturate(dot(normal, view)), 4.0h);
                half3 foil = lerp(input.color.rgb, half3(1,1,1), grazing * .55h);
                // A very small pearlescent shift on selected cool/silver flakes.
                half3 interference = .92h + .08h * cos(6.283185h * (dot(normal, view) * 1.3h + half3(0,.333,.667)));
                foil *= lerp(half3(1,1,1), interference, .38h);
                half3 metallic = foil * (half3(.63h,.67h,.74h) + environment * 1.15h + specular * 3.0h);
                metallic = lerp(metallic * _BaseColor.rgb, metallic, transmission * .75h);
                output.color = half4(metallic * _Intensity, input.color.a);
                output.response = half4(reflection, flash, transmission, step(.0001, input.centerRadius.w));
                if (input.centerRadius.w > .0001)
                    positionWS = centerWS + (right * input.uv.x * sx + up * input.uv.y * sy) * input.centerRadius.w * lerp(.65h, 1.0h, flash);
                output.positionCS = TransformWorldToHClip(positionWS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                if (_Billboard > .5h)
                {
                    float2 radial = (input.uv - .5) * 2.0;
                    half glow = saturate(1.0h - dot(radial, radial));
                    return half4(_BaseColor.rgb * _Intensity, glow * glow * .55h * _BaseColor.a);
                }
                half clean = SAMPLE_TEXTURE2D(_SkinMask, sampler_SkinMask, input.skinUV).a;
                half soap = SAMPLE_TEXTURE2D(_SoapMask, sampler_SoapMask, input.skinUV).r;
                half visible = clean * clean * (1.0h - soap) * _GlitterVisibility * input.response.z * input.color.a;
                if (input.response.w > .5h)
                {
                    float2 p = abs(input.uv);
                    // A lens diffraction glint, revealed by reflected light. The
                    // long thin arms have soft tips and cannot look like confetti.
                    float2 aa = max(fwidth(input.uv), float2(.016,.016));
                    half horizontal = (1.0 - smoothstep(.024, .024 + aa.y * .7, p.y)) * pow(saturate(1.0 - p.x), 1.5);
                    half vertical = (1.0 - smoothstep(.024, .024 + aa.x * .7, p.x)) * pow(saturate(1.0 - p.y), 1.5);
                    half core = exp2(-dot(p,p) * 86.0);
                    half alpha = saturate(core + (horizontal + vertical) * .90h) * input.response.y * visible;
                    half3 glintColor = lerp(half3(2.3h,2.1h,1.65h), input.color.rgb, .16h);
                    return half4(glintColor * _Intensity, saturate(alpha * 1.30h));
                }
                half alpha = (.055h + input.response.x * .93h) * visible;
                return half4(input.color.rgb, alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
