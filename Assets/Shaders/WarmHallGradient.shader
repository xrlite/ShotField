Shader "ShotField/WarmHallGradient"
{
    Properties
    {
        _TopColor("Top Color", Color) = (0.055, 0.047, 0.039, 1)
        _HorizonColor("Horizon Color", Color) = (0.145, 0.125, 0.102, 1)
        _BottomColor("Bottom Color", Color) = (0.035, 0.030, 0.026, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Pass
        {
            Cull Off
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _TopColor;
                half4 _HorizonColor;
                half4 _BottomColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionOS = input.positionOS.xyz;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float h = saturate(normalize(input.positionOS).y * 0.5 + 0.5);
                half3 lower = lerp(_BottomColor.rgb, _HorizonColor.rgb, smoothstep(0.02, 0.42, h));
                half3 color = lerp(lower, _TopColor.rgb, smoothstep(0.42, 1.0, h));
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
