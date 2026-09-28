Shader "CJ/CRTScreenUI"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
        _Color("Tint", Color) = (1, 1, 1, 1)

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

        [HideInInspector] _StencilComp("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "CRTScreenUI"

            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 2.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct Attributes
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float _ScanlineCount;
            half _ScanlineIntensity;
            half _NoiseIntensity;
            float _ChromaticOffset;
            float _JitterIntensity;
            float _RollBarSpeed;
            half _RollBarIntensity;
            half _FlickerIntensity;
            half _VignetteIntensity;

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            half4 SampleScreen(float2 uv)
            {
                return tex2D(_MainTex, saturate(uv)) + _TextureSampleAdd;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.worldPosition = input.vertex;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.texcoord = input.texcoord;
                output.color = input.color * _Color;
                return output;
            }

            fixed4 Frag(Varyings input) : SV_Target
            {
                float time = _Time.y;
                float2 uv = input.texcoord;

                float scanRow = floor(uv.y * _ScanlineCount);
                uv.x += (Hash(float2(scanRow, floor(time * 30.0))) - 0.5) * _JitterIntensity;

                float2 chroma = float2(_ChromaticOffset, 0.0);
                half4 center = SampleScreen(uv);
                half3 color;
                color.r = SampleScreen(uv + chroma).r;
                color.g = center.g;
                color.b = SampleScreen(uv - chroma).b;

                half scan = sin(uv.y * _ScanlineCount * UNITY_TWO_PI) * 0.5 + 0.5;
                color *= lerp(1.0, scan, _ScanlineIntensity);

                half bar = saturate(1.0 - abs(frac(uv.y + time * _RollBarSpeed) - 0.5) * 8.0);
                color *= 1.0 + bar * _RollBarIntensity;

                half grain = Hash(input.texcoord * float2(640.0, 480.0) + frac(time) * 100.0) - 0.5;
                color += grain * _NoiseIntensity;

                color *= 1.0 - _FlickerIntensity * Hash(float2(floor(time * 24.0), 0.0));

                float2 fromCenter = input.texcoord - 0.5;
                color *= saturate(1.0 - dot(fromCenter, fromCenter) * _VignetteIntensity * 4.0);

                half4 result = half4(saturate(color), center.a) * input.color;

                #ifdef UNITY_UI_CLIP_RECT
                result.a *= UnityGet2DClipping(input.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(result.a - 0.001);
                #endif

                return result;
            }
            ENDCG
        }
    }
}
