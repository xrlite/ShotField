Shader "ShotField/WarmHallBeam"
{
    Properties
    {
        _BeamColor("Beam Color", Color) = (1, 0.78, 0.46, 0.04)
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" }
        Pass
        {
            Cull Off
            ZWrite Off
            Blend One One

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BeamColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionOS : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionOS = input.positionOS.xyz;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 viewDir = normalize(GetCameraPositionWS() - input.positionWS);
                float facing = pow(saturate(abs(dot(viewDir, normalize(input.normalWS)))), 1.4);
                float vertical = smoothstep(-1.0, -0.55, input.positionOS.y) *
                                 (1.0 - smoothstep(0.72, 1.0, input.positionOS.y));
                float alpha = _BeamColor.a * facing * vertical;
                return half4(_BeamColor.rgb * alpha, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
