Shader "ShotField/SpatialGlass"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (0.96, 0.93, 0.86, 0.68)
        _CornerRadius ("Corner Radius (px)", Range(0, 128)) = 32
        _BorderWidth ("Border Width (px)", Range(0, 8)) = 1
        _BorderColor ("Border Color", Color) = (1, 0.98, 0.92, 0.55)
        _HighlightColor ("Top Highlight", Color) = (1, 0.99, 0.96, 0.65)
        _HighlightHeight ("Highlight Height (px)", Range(0, 16)) = 2
        _InnerGlow ("Inner Glow", Range(0, 1)) = 0.35
        _RectSize ("Rect Size (px)", Vector) = (560, 220, 0, 0)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Cull Off
            ZWrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float4 _BaseColor;
            float _CornerRadius;
            float _BorderWidth;
            float4 _BorderColor;
            float4 _HighlightColor;
            float _HighlightHeight;
            float _InnerGlow;
            float4 _RectSize;

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float RoundedBoxSDF(float2 p, float2 halfSize, float radius)
            {
                float2 q = abs(p) - halfSize + radius;
                return min(max(q.x, q.y), 0.0) + length(max(q, 0.0)) - radius;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 size = max(_RectSize.xy, float2(1.0, 1.0));
                float2 pixel = (i.uv - 0.5) * size;
                float radius = clamp(_CornerRadius, 0.0, min(size.x, size.y) * 0.5);
                float d = RoundedBoxSDF(pixel, size * 0.5, radius);
                float aa = max(fwidth(d) * 1.25, 0.75);
                float mask = 1.0 - smoothstep(-aa, aa, d);

                float borderDistance = abs(d + _BorderWidth * 0.5);
                float border = 1.0 - smoothstep(_BorderWidth * 0.5 - aa,
                    _BorderWidth * 0.5 + aa, borderDistance);
                border *= mask;

                float topDistance = size.y * 0.5 - pixel.y;
                float topHighlight = (1.0 - smoothstep(0.0, max(_HighlightHeight, 0.1), topDistance))
                    * smoothstep(0.0, 1.5, topDistance);
                topHighlight *= mask;

                float innerEdge = 1.0 - smoothstep(0.0, 2.0, abs(d + 1.5));
                innerEdge *= mask * _InnerGlow;

                float3 color = _BaseColor.rgb;
                float alpha = _BaseColor.a;
                color = lerp(color, _BorderColor.rgb, border * _BorderColor.a);
                alpha = max(alpha, border * _BorderColor.a);
                color = lerp(color, _HighlightColor.rgb, topHighlight * _HighlightColor.a);
                alpha = max(alpha, topHighlight * _HighlightColor.a);
                color += _BorderColor.rgb * innerEdge;
                alpha = max(alpha, innerEdge * 0.35);
                return fixed4(color, alpha * mask);
            }
            ENDHLSL
        }
    }

    Fallback "UI/Default"
}
