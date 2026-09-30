Shader "MimoGarden/PastelFinish"
{
    Properties
    {
        [MainTexture] _BaseMap ("Washable skin", 2D) = "white" {}
        [MainColor] _BaseColor ("Pastel tint", Color) = (1,1,1,1)
        [HideInInspector] _Color ("Legacy tint", Color) = (1,1,1,1)
        _Opacity ("Body opacity", Range(0,1)) = 1
        _Smoothness ("Surface smoothness", Range(0,1)) = .2
        _SpecularStrength ("Surface shine", Range(0,1)) = .08
        _RimStrength ("Pearl rim", Range(0,2)) = .1
        _RimColor ("Pearl rim color", Color) = (1,1,1,1)
        _Iridescence ("Nacre interference", Range(0,1)) = 0
        _Grain ("Sugar crystal coverage", Range(0,1)) = 0
        _FinishEnvironment ("Studio reflection", Cube) = "" {}
        _EnvironmentStrength ("Reflection exposure", Range(0,3)) = 1
        _EnvironmentMipCount ("Reflection mip count", Float) = 7
        _Transmission ("Gel transmission", Range(0,1)) = 0
        _SceneRefraction ("Opaque scene texture available", Range(0,1)) = 0
        _RefractionStrength ("Gel refraction", Range(0,.08)) = .02
        _Thickness ("Optical thickness", Range(.1,4)) = 1.3
        _Subsurface ("Soft light scattering", Range(0,1)) = .12
        _CoatStrength ("Clear wet coating", Range(0,1)) = .1
        _MicroNormalStrength ("Surface micro relief", Range(0,1)) = .18
        _DetailScale ("Microstructure density", Float) = 72
        _FinishKind ("Finish family", Float) = 0
        _SoapMask ("Soap coverage in R", 2D) = "black" {}
        _SoapColor ("Foam color", Color) = (.98,.99,1,1)
        _SoapAmount ("Soap mask strength", Range(0,1)) = 0
        [HideInInspector] _SrcBlend ("Source blend", Float) = 1
        [HideInInspector] _DstBlend ("Destination blend", Float) = 0
        [HideInInspector] _ZWrite ("Depth write", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "Pastel surface"
            Tags { "LightMode"="UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_SoapMask); SAMPLER(sampler_SoapMask);
            TEXTURECUBE(_FinishEnvironment); SAMPLER(sampler_FinishEnvironment);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor, _Color, _RimColor, _SoapColor;
                half _Opacity, _Smoothness, _SpecularStrength, _RimStrength;
                half _Iridescence, _Grain, _SoapAmount, _FinishKind;
                half _EnvironmentStrength, _EnvironmentMipCount, _Transmission, _SceneRefraction;
                half _RefractionStrength, _Thickness, _Subsurface, _CoatStrength;
                half _MicroNormalStrength, _DetailScale;
                half _SrcBlend, _DstBlend, _ZWrite;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            float MimoHash(float2 cell)
            {
                float3 p = frac(float3(cell.xyx) * .1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            float2 MimoHash2(float2 p)
            {
                return float2(MimoHash(p), MimoHash(p + float2(71.7, 13.4)));
            }

            float MimoNoise(float2 p)
            {
                float2 cell = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(MimoHash(cell), MimoHash(cell + float2(1,0)), f.x),
                    lerp(MimoHash(cell + float2(0,1)), MimoHash(cell + 1.0), f.x), f.y);
            }

            // Derivative frame also supports imported meshes and procedural pieces with
            // no stored tangents. The denominator is clamped for degenerate UV triangles.
            void MimoFrame(float3 p, float2 uv, half3 n, out half3 tangent, out half3 bitangent)
            {
                float3 px = ddx(p), py = ddy(p);
                float2 tx = ddx(uv), ty = ddy(uv);
                float3 a = cross(py, n), b = cross(n, px);
                float3 t = a * tx.x + b * ty.x;
                float3 bt = a * tx.y + b * ty.y;
                float inverseLength = rsqrt(max(max(dot(t,t), dot(bt,bt)), 1e-12));
                tangent = t * inverseLength;
                bitangent = bt * inverseLength;
            }

            // Small individual hexagonal crystals sit on a continuous frosted surface.
            // Their reflective facets must not replace the entire diffuse surface normal:
            // doing that makes sugar look like a tiled mosaic or reptile scales.
            void MimoCrystal(float2 uv, out float2 slope, out half coverage, out half facet)
            {
                float2 p = uv * _DetailScale * float2(2.7,1), cell = floor(p), local = frac(p);
                float first = 8.0, second = 8.0;
                float2 chosen = 0, crystalDelta = 0;
                [unroll] for (int y = -1; y <= 1; y++)
                [unroll] for (int x = -1; x <= 1; x++)
                {
                    float2 offset = float2(x,y);
                    float2 seed = MimoHash2(cell + offset);
                    float2 delta = offset + .18 + seed * .64 - local;
                    float d = dot(delta, delta);
                    if (d < first) { second = first; first = d; chosen = seed; crystalDelta = delta; }
                    else second = min(second, d);
                }
                float angle = chosen.x * 6.283185;
                float2 rotated = float2(crystalDelta.x * cos(angle) - crystalDelta.y * sin(angle),
                    crystalDelta.x * sin(angle) + crystalDelta.y * cos(angle));
                float2 hex = abs(rotated);
                float edge = max(hex.x * .866025 + hex.y * .5, hex.y);
                float radius = .25 + chosen.y * .12;
                float aa = max(fwidth(edge) * .65, .025);
                coverage = (1.0h - smoothstep(radius - aa, radius + aa, edge)) * smoothstep(.1,.22,chosen.y);
                slope = (chosen - .5) * 1.9;
                facet = chosen.x;
            }

            // Five spatial samples approximate the loss of background detail through a
            // thick jelly. Offsets are in equal screen pixels on portrait and landscape.
            half3 MimoRefractedScene(float2 uv, half pathLength, float2 screenAspect)
            {
                float2 blur = screenAspect * (.0018 + pathLength * .0038);
                half3 color = SampleSceneColor(saturate(uv)) * .28h;
                color += SampleSceneColor(saturate(uv + blur * float2(.95,.65))) * .18h;
                color += SampleSceneColor(saturate(uv + blur * float2(-.95,-.65))) * .18h;
                color += SampleSceneColor(saturate(uv + blur * float2(.65,-.95))) * .18h;
                color += SampleSceneColor(saturate(uv + blur * float2(-.65,.95))) * .18h;
                return color;
            }

            half3 MimoReflectionRadiance(half3 radiance)
            {
                // HDR softboxes retain their shape instead of clipping a large white
                // hole into a pastel body when the mobile camera has no tonemapping.
                radiance *= _EnvironmentStrength;
                half shoulder = lerp(.60h, .24h, saturate(_Transmission * 1.4h));
                return radiance / (1.0h + radiance * shoulder);
            }

            // Isotropic GGX with a finite roughness floor: stable on GLES half precision,
            // with a narrow wet highlight and a broader, energy-limited material lobe.
            half MimoSpecular(half3 n, half3 v, half3 l, half roughness)
            {
                float3 h = SafeNormalize((float3)v + l);
                float nl = saturate(dot(n,l)), nv = max(.04, saturate(dot(n,v)));
                float nh = saturate(dot(n,h)), lh = saturate(dot(l,h));
                float r = max(.065, roughness), a = r * r, a2 = a * a;
                float d = nh * nh * (a2 - 1.0) + 1.0;
                float distribution = a2 / max(3.141593 * d * d, 1e-6);
                float k = (r + 1.0) * (r + 1.0) * .125;
                float visibility = rcp(max((nl * (1.0 - k) + k) * (nv * (1.0 - k) + k), .025));
                float fresnel = .04 + .96 * pow(1.0 - lh, 5.0);
                return min(5.0, distribution * visibility * fresnel * nl * .25);
            }

            half3 MimoLight(Light light, half3 baseColor, half3 n, half3 v,
                half roughness, half clean, half transmission)
            {
                half nl = dot(n, light.direction);
                half diffuse = saturate(nl) * .72h;
                half wrapped = saturate((nl + .5h) / 1.5h);
                half backScatter = pow(saturate(dot(v, -light.direction) * .5h + .5h), 3.0h);
                half scattering = _Subsurface * (wrapped * .35h + backScatter * (1.0h - saturate(nl)) * .3h);
                half specular = MimoSpecular(n, v, light.direction, roughness);
                half coat = MimoSpecular(n, v, light.direction, .11h);
                half3 response = baseColor * (diffuse + scattering) * (1.0h - transmission * .8h);
                response += (specular * (.5h + _SpecularStrength * 2.4h) + coat * _CoatStrength * .35h) * clean;
                return response * light.color * light.distanceAttenuation * light.shadowAttenuation;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 skin = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                // Alpha is CLEAN COVERAGE, not material opacity. Keep painted dirt opaque
                // and suppress every optical treatment wherever dirt is still present.
                half clean = saturate(skin.a), dirt = 1.0h - clean;
                half3 tint = max(skin.rgb * _BaseColor.rgb, half3(.015,.015,.015));
                half3 geometricNormal = normalize(input.normalWS);
                half3 view = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
                half3 tangent, bitangent;
                MimoFrame(input.positionWS, input.uv, geometricNormal, tangent, bitangent);
                float2 detailUV = input.uv * _DetailScale * float2(2.7,1);
                float detailFootprint = max(length(ddx(detailUV)), length(ddy(detailUV)));
                half detailVisibility = 1.0h - smoothstep(.6, 1.8, detailFootprint);
                half grainNoise = MimoNoise(detailUV);
                float2 microSlope = float2(MimoNoise(detailUV + float2(7.3,19.7)),
                    MimoNoise(detailUV + float2(29.1,3.8))) * 2.0 - 1.0;
                half crystalCoverage = 0, crystalFacet = .5;
                float2 facetSlope = 0;
                if (_Grain > .001h)
                {
                    MimoCrystal(input.uv, facetSlope, crystalCoverage, crystalFacet);
                }
                half3 normal = normalize(geometricNormal +
                    (tangent * microSlope.x + bitangent * microSlope.y) * _MicroNormalStrength *
                    lerp(1.0h,.30h,_Grain) * detailVisibility * clean);
                half nv = saturate(dot(normal, view));
                half rim = Pow4(1.0h - nv);
                half roughness = max(.07h, 1.0h - _Smoothness);
                roughness = saturate(roughness + (grainNoise - .5h) * .07h * detailVisibility + dirt * .5h);
                half transmission = _Transmission * clean;
                half soft = 1.0h - step(.5h, _FinishKind);
                half magic = step(4.5h, _FinishKind);

                // Mochi has minute porous relief and soft grazing sheen. Sugar has packed
                // translucent facets with lighter edges; neither is a layer of dark specks.
                half3 albedo = tint * (1.0h + (grainNoise - .5h) * .055h * soft * detailVisibility);
                albedo = lerp(albedo, lerp(albedo, half3(1, .985, .97),
                    .15h + crystalCoverage * .10h), _Grain * clean);
                half3 ambient = max(SampleSH(normal), half3(.12,.12,.12));
                half3 color = albedo * ambient * .72h * (1.0h - transmission * .7h);
                Light mainLight = GetMainLight();
                color += MimoLight(mainLight, albedo, normal, view, roughness, clean, transmission);
                #if defined(_ADDITIONAL_LIGHTS)
                    uint lightCount = GetAdditionalLightsCount();
                    LIGHT_LOOP_BEGIN(lightCount)
                        Light extraLight = GetAdditionalLight(lightIndex, input.positionWS);
                        color += MimoLight(extraLight, albedo, normal, view, roughness, clean, transmission);
                    LIGHT_LOOP_END
                #endif

                // Film interference is driven by optical viewing angle and subtle layer
                // thickness, so pearl rolls through cream, rose and blue as it turns.
                half layer = MimoNoise(input.uv * 4.0) * .14h;
                half3 film = .70h + .30h * cos(6.283185h *
                    ((1.0h - nv) * lerp(1.05h,1.7h,magic) + layer + half3(.02,.35,.68)));
                half3 reflectionDir = reflect(-view, normal);
                half mip = roughness * max(0.0h, _EnvironmentMipCount - 1.0h);
                half3 environment = MimoReflectionRadiance(SAMPLE_TEXTURECUBE_LOD(_FinishEnvironment, sampler_FinishEnvironment, reflectionDir, mip).rgb);
                half coatMip = lerp(2.1h + roughness * 2.0h, .50h, saturate(transmission * 1.4h));
                half3 coatEnvironment = MimoReflectionRadiance(SAMPLE_TEXTURECUBE_LOD(_FinishEnvironment, sampler_FinishEnvironment, reflectionDir, coatMip).rgb);
                half fresnel = .035h + .72h * pow(1.0h - nv, 5.0h);
                half3 filmTint = lerp(half3(1,1,1), film, _Iridescence * .75h);
                half reflectance = fresnel + _SpecularStrength * .075h + _CoatStrength * .075h;
                half3 reflection = environment * reflectance * filmTint;
                reflection += coatEnvironment * _CoatStrength * (.055h + rim * .20h);

                if (transmission > .001h)
                {
                    // Approximate the optical path through a rounded, solid gel. Beer-
                    // Lambert absorption colors the thick center while the silhouette is
                    // thinner, brighter and dominated by Fresnel reflections.
                    half pathLength = _Thickness * (.24h + .76h * sqrt(saturate(dot(geometricNormal, view))));
                    half3 absorption = -log(clamp(tint, .04h, .99h)) * 3.15h + .16h;
                    half3 transmittance = exp(-absorption * pathLength);
                    half3 behind = albedo * (.52h + ambient * .42h);
                    if (_SceneRefraction > .5h)
                    {
                        float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                        half3 viewNormal = mul((float3x3)UNITY_MATRIX_V, normal);
                        // Small screen-space bend has no render texture/depth dependency
                        // when opaque-copy availability is false on the active camera.
                        float2 screenAspect = float2(_ScaledScreenParams.y / _ScaledScreenParams.x,1);
                        float2 bend = viewNormal.xy * screenAspect * _RefractionStrength *
                            (.60h + pathLength * .40h) * (.40h + .60h * nv);
                        behind = MimoRefractedScene(screenUV - bend, pathLength, screenAspect);
                    }
                    half wrap = saturate(dot(geometricNormal, mainLight.direction) * .5h + .5h);
                    half extinction = 1.0h - dot(transmittance, half3(.3333,.3333,.3333));
                    half3 volume = behind * transmittance * .82h;
                    // Scatter carries the gel's hue. Using per-channel (1-T) here would
                    // incorrectly fill the absorbed green wavelengths back into pink gel.
                    volume += albedo * extinction * (.78h + .22h * wrap);
                    volume = lerp(volume, albedo * (.58h + .30h * wrap), .12h);
                    volume *= 1.0h - pow(1.0h - nv, 2.4h) * .18h;
                    volume += albedo * _Subsurface * pow(1.0h - nv, 2.0h) * .10h;
                    color = lerp(color, volume * (1.0h - fresnel * .6h), transmission);
                }
                color += reflection * clean;
                color += film * _Iridescence * (.038h + pow(1.0h - nv, 2.0h) * .11h) * clean;
                if (_Grain > .001h)
                {
                    half3 crystalNormal = normalize(geometricNormal +
                        (tangent * facetSlope.x + bitangent * facetSlope.y) * .72h);
                    half3 crystalReflection = MimoReflectionRadiance(SAMPLE_TEXTURECUBE_LOD(_FinishEnvironment,
                        sampler_FinishEnvironment, reflect(-view,crystalNormal), 1.1h).rgb);
                    half crystalFlash = MimoSpecular(crystalNormal, view, mainLight.direction, .16h);
                    half3 glint = max(crystalReflection - .22h, 0.0h) * .27h + crystalFlash * .15h;
                    color += glint * crystalCoverage * _Grain * clean * detailVisibility;
                }
                color += _RimColor.rgb * _RimStrength * (rim * .075h +
                    soft * pow(1.0h - nv, 3.0h) * .065h) * clean;
                color += film * magic * (.04h + pow(1.0h - nv,1.6h) * .22h) * clean;

                // Most of the transparency is already physically composed above, so gel
                // retains its colored body and eyes instead of becoming an alpha ghost.
                half alpha = saturate(_Opacity + (1.0h - _Opacity) * rim + dirt);
                if (_SoapAmount > .001h)
                {
                    half soap = saturate(SAMPLE_TEXTURE2D(_SoapMask, sampler_SoapMask, input.uv).r * _SoapAmount);
                    float2 bubble = frac(input.uv * float2(79,61)) - .5;
                    half pores = smoothstep(.035, .18, dot(bubble,bubble));
                    half foamWrap = saturate(dot(geometricNormal, mainLight.direction) * .5h + .5h);
                    half3 foam = _SoapColor.rgb * (.83h + .17h * foamWrap) - pores * .025h;
                    color = lerp(color, foam, soap);
                    alpha = lerp(alpha, 1.0h, soap);
                }
                half3 shoulder = max(color - .82h, 0.0h);
                color = min(color, .82h) + shoulder / (1.0h + shoulder * 5.5h);
                return half4(max(color, 0.0h), alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
