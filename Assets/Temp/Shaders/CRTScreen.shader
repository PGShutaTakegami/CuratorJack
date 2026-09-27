Shader "CJ/CRTScreen"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (0, 0, 0, 1)
        _SubMap("Sub Map", 2D) = "black" {}
        _Blend("Blend", Range(0, 1)) = 0

        [Header(Scanline)]
        _ScanlineCount("Scanline Count", Float) = 240
        _ScanlineIntensity("Scanline Intensity", Range(0, 1)) = 0.35

        [Header(Noise)]
        _NoiseIntensity("Noise Intensity", Range(0, 1)) = 0.12
        _ChromaticOffset("Chromatic Offset", Range(0, 0.02)) = 0.003
        _JitterIntensity("Jitter Intensity", Range(0, 0.02)) = 0.002

        [Header(Roll Bar)]
        _RollBarSpeed("Roll Bar Speed", Float) = 0.15
        _RollBarIntensity("Roll Bar Intensity", Range(0, 1)) = 0.12

        [Header(Flicker and Vignette)]
        _FlickerIntensity("Flicker Intensity", Range(0, 0.5)) = 0.04
        _VignetteIntensity("Vignette Intensity", Range(0, 1)) = 0.35

        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            float4 _SubMap_ST;
            half _Blend;
            float _ScanlineCount;
            half _ScanlineIntensity;
            half _NoiseIntensity;
            float _ChromaticOffset;
            float _JitterIntensity;
            float _RollBarSpeed;
            half _RollBarIntensity;
            half _FlickerIntensity;
            half _VignetteIntensity;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "CRTUnlit"
            Tags { "LightMode" = "UniversalForward" }

            Cull [_Cull]
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_SubMap);
            SAMPLER(sampler_SubMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half fogFactor : TEXCOORD1;
            };

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            // 画面 UV(0-1) で計算し、Sprite の切り出し範囲へ変換してからサンプルする。_Blend で _SubMap へクロスフェード
            half3 SampleScreen(float2 screenUV)
            {
                float2 clamped = saturate(screenUV);
                half3 baseColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, clamped * _BaseMap_ST.xy + _BaseMap_ST.zw).rgb;
                half3 subColor = SAMPLE_TEXTURE2D(_SubMap, sampler_SubMap, clamped * _SubMap_ST.xy + _SubMap_ST.zw).rgb;
                return lerp(baseColor, subColor, _Blend);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(output.positionHCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float time = _Time.y;
                float2 uv = input.uv;

                float scanRow = floor(uv.y * _ScanlineCount);
                uv.x += (Hash(float2(scanRow, floor(time * 30.0))) - 0.5) * _JitterIntensity;

                float2 chroma = float2(_ChromaticOffset, 0.0);
                half3 color;
                color.r = SampleScreen(uv + chroma).r;
                color.g = SampleScreen(uv).g;
                color.b = SampleScreen(uv - chroma).b;

                half scan = sin(uv.y * _ScanlineCount * TWO_PI) * 0.5 + 0.5;
                color *= lerp(1.0, scan, _ScanlineIntensity);

                half bar = saturate(1.0 - abs(frac(uv.y + time * _RollBarSpeed) - 0.5) * 8.0);
                color *= 1.0 + bar * _RollBarIntensity;

                half grain = Hash(input.uv * float2(640.0, 480.0) + frac(time) * 100.0) - 0.5;
                color += grain * _NoiseIntensity;

                color *= 1.0 - _FlickerIntensity * Hash(float2(floor(time * 24.0), 0.0));

                float2 fromCenter = input.uv - 0.5;
                color *= saturate(1.0 - dot(fromCenter, fromCenter) * _VignetteIntensity * 4.0);

                // 画像未割り当て時は _BaseColor が黒なのでノイズごと黒になる
                color = saturate(color) * _BaseColor.rgb;
                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            Cull [_Cull]
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            float4 DepthVert(float4 positionOS : POSITION) : SV_POSITION
            {
                return TransformObjectToHClip(positionOS.xyz);
            }

            half DepthFrag() : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
