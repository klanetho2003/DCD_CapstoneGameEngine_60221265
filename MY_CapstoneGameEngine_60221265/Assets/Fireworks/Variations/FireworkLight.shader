Shader "Capstone/Fireworks/Pattern Light"
{
    Properties
    {
        [MainTexture] _MainTex ("Particle mask", 2D) = "white" {}
        [KeywordEnum(Rim, Prism, Flow, Glitter)] _Effect ("Light effect", Float) = 0
        _Accent ("Accent", Color) = (1, 0.8, 0.6, 1)
        _Gain ("Brightness", Range(0, 4)) = 1.5
        _Rate ("Animation speed", Range(0, 6)) = 1.5
        [Toggle] _Trail ("Trail UVs", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha One
            Cull Off
            ZWrite Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_local _EFFECT_RIM _EFFECT_PRISM _EFFECT_FLOW _EFFECT_GLITTER
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _Accent;
                float _Gain, _Rate, _Trail, _Effect;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                float3 world : TEXCOORD1;
            };
            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.world = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.world);
                o.color = input.color;
                o.uv = input.uv;
                return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                float2 p = i.uv * 2.0 - 1.0;
                half mask = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).a;
                half3 light = i.color.rgb;
                float clock = _Time.y * _Rate;
                float gain = _Gain;
                #if defined(_EFFECT_RIM)
                    float distanceFromCore = lerp(length(p), abs(p.y), _Trail);
                    float rim = 1.0 - smoothstep(.07, .24, abs(distanceFromCore - .48));
                    light = lerp(light, _Accent.rgb, .62 * rim);
                    gain *= .83 + .65 * rim;
                #elif defined(_EFFECT_PRISM)
                    float band = .5 + .5 * sin(dot(i.world.xy, float2(2.8, 2.1)) + 3.0*p.y + clock);
                    light = lerp(light, _Accent.rgb, .49 * band);
                    gain *= .86 + .34 * band;
                #elif defined(_EFFECT_FLOW)
                    float flow = .5 + .5 * cos(6.28318 * (i.uv.x * 2.4 - clock) + i.world.y * .8);
                    light = lerp(light, _Accent.rgb, .29 * flow);
                    gain *= lerp(1.0, .46 + .89 * flow, _Trail);
                #elif defined(_EFFECT_GLITTER)
                    float phase = dot(i.world, float3(8.7, 13.1, 4.7)) + clock * 3.5;
                    float glint = pow(.5 + .5 * sin(phase), 4.0);
                    light = lerp(light, _Accent.rgb, .66 * glint);
                    gain *= .45 + 1.15 * glint;
                #endif
                return half4(light * gain, mask * i.color.a);
            }
            ENDHLSL
        }
    }
}
