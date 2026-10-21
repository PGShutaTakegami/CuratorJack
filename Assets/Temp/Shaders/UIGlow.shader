// 角の丸いカードの外側だけを柔らかく光らせる UI シェーダー。
// Image の矩形はカードより _Spread だけ外に広げて置き、内側（カードの部分）は透明にする。色と透明度は Image の色で決める。
Shader "CJ/UIGlow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _CardSize ("Card Size", Vector) = (300, 400, 0, 0)
        _Radius ("Corner Radius", Float) = 18
        _Spread ("Glow Spread", Float) = 16

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
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
        Blend SrcAlpha One
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
            };

            fixed4 _Color;
            float4 _CardSize;
            float _Radius;
            float _Spread;
            float4 _ClipRect;

            v2f vert(appdata v)
            {
                v2f o;
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }

            // 角丸の矩形からの距離（内側は負）
            float roundedBoxDistance(float2 p, float2 halfSize, float radius)
            {
                float2 q = abs(p) - (halfSize - radius);
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - radius;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 size = _CardSize.xy + _Spread * 2.0;
                float2 p = (i.uv - 0.5) * size;
                float d = roundedBoxDistance(p, _CardSize.xy * 0.5, _Radius);

                float outside = smoothstep(-2.0, 0.0, d);
                float falloff = 1.0 - saturate(d / _Spread);
                fixed4 col = i.color;
                col.a *= outside * falloff * falloff;
                col.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                return col;
            }
            ENDCG
        }
    }
}
