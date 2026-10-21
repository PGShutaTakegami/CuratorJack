Shader "CJ/Outline"
{
    Properties
    {
        [HDR] _OutlineColor("Outline Color", Color) = (1, 0.75, 0.3, 1)
        _OutlineWidth("Outline Width (World)", Range(0, 0.2)) = 0.03
        [Toggle] _Radial("Extrude From Center", Float) = 0
        _Center("Center (World)", Vector) = (0, 0, 0, 0)
        _StencilRef("Stencil Ref", Range(1, 255)) = 64
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry+10"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _OutlineColor;
            float _OutlineWidth;
            float _Radial;
            float4 _Center;
            float _StencilRef;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            UNITY_VERTEX_OUTPUT_STEREO
        };

        Varyings VertMask(Attributes input)
        {
            Varyings output;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
            return output;
        }

        Varyings VertOutline(Attributes input)
        {
            Varyings output;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

            // 太さをワールド単位で決め、拡大率の大きいメッシュでも同じ太さにする
            float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
            float3 normalWS = normalize(TransformObjectToWorldNormal(input.normalOS));
            // 角ばったメッシュは面ごとの法線で広げると縁が途切れるため、中心から外へ広げる
            float3 radialWS = SafeNormalize(positionWS - _Center.xyz);
            float3 direction = SafeNormalize(lerp(normalWS, radialWS, _Radial));
            positionWS += direction * _OutlineWidth;
            output.positionCS = TransformWorldToHClip(positionWS);
            return output;
        }

        half4 FragMask(Varyings input) : SV_Target
        {
            return 0;
        }

        half4 FragOutline(Varyings input) : SV_Target
        {
            return _OutlineColor;
        }
        ENDHLSL

        // URP は SRPDefaultUnlit → UniversalForward の順に描くため、先にモデルの形をステンシルへ記録する
        Pass
        {
            Name "OutlineMask"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Off
            ZWrite Off
            ColorMask 0
            Stencil
            {
                Ref [_StencilRef]
                Comp Always
                Pass Replace
            }

            HLSLPROGRAM
            #pragma vertex VertMask
            #pragma fragment FragMask
            ENDHLSL
        }

        // 膨らませた形のうち、モデルの外側（ステンシルが記録されていない所）だけを縁として描く
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
            ZWrite On
            Stencil
            {
                Ref [_StencilRef]
                Comp NotEqual
            }

            HLSLPROGRAM
            #pragma vertex VertOutline
            #pragma fragment FragOutline
            ENDHLSL
        }
    }
}
