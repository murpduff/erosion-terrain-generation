Shader "Custom/ErosionMountains" {
    Properties {
        _BaseAmplitude("Base Amplitude", Float) = 1.0
        _BaseFrequency("Base Frequency", Float) = 1.0
        _Octaves("Octaves", Float) = 1
        _ErosionLayers("Erosion Layers", Float) = 1
        _ErosionFrequency("Erosion Frequency", Float) = 1
        _ErosionMagnitude("Erosion Magnitude", Float) = 1
    }

    SubShader {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Pass {
            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : INTERNAL_NORMAL;
            };
            
            struct VertInfo {
                float3 pos;
                float2 gradient;
            };

            CBUFFER_START(UnityPerMaterial)
                float _BaseAmplitude;
                float _BaseFrequency;
                int _Octaves;

                int _ErosionLayers;
                float _ErosionFrequency;
                float _ErosionMagnitude;
            CBUFFER_END

            #include "Erosion.hlsl"

            Varyings vert(Attributes IN) {
                float3 pos = IN.positionOS.xyz;
                Varyings OUT = GetVertexInfo(pos.xz);

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target {
                float3 normal = IN.normalWS.xyz;
                half4 color = half4(1,1,1,1) * GetLighting(normal, GetMainLight().direction);
                return color;
            }
            ENDHLSL
        }
    }
}
