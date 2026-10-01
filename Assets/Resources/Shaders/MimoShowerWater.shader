Shader "MimoGarden/ShowerWater"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+30" }
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
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; float2 style:TEXCOORD1; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float2 style:TEXCOORD1; };
            Varyings Vert(Attributes i) { Varyings o; o.positionCS=TransformObjectToHClip(i.positionOS.xyz); o.uv=i.uv; o.style=i.style; return o; }
            half4 Frag(Varyings i):SV_Target
            {
                float alpha, shine;
                if(i.style.x > .5)
                {
                    float r2=dot(i.uv,i.uv); clip(1-r2);
                    float z=sqrt(saturate(1-r2));
                    shine=exp(-dot(i.uv-float2(-.3,.35),i.uv-float2(-.3,.35))*32);
                    alpha=(.18+pow(1-z,2)*.45+shine*.58)*smoothstep(1,.85,sqrt(r2));
                }
                else
                {
                    float edge=pow(saturate(1-abs(i.uv.x)),.5);
                    shine=pow(saturate(1-abs(i.uv.x+.30)*4),5)*(.6+.4*sin(i.uv.y*14-_Time.y*22+i.style.y*12));
                    alpha=edge*(.15+shine*.42);
                }
                return half4(lerp(half3(.70,.89,.97),half3(1,1,1),saturate(.24+shine)),alpha);
            }
            ENDHLSL
        }
    }
}
