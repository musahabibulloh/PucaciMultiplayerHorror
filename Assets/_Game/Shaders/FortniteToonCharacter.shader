Shader "Pucaci/FortniteToonCharacter"
{
    Properties
    {
        _BaseMap ("Base Texture", 2D) = "white" {}
        _BaseColor ("Base Color Tint", Color) = (1, 1, 1, 1)
        _ShadowColor ("Shadow Tint (Cel-Shading)", Color) = (0.22, 0.20, 0.32, 1)
        _ShadowThreshold ("Shadow Threshold", Range(-1.0, 1.0)) = 0.05
        _ShadowSmoothness ("Shadow Edge Sharpness", Range(0.001, 0.3)) = 0.04
        _AmbientBrightness ("Comic Ambient Floor", Range(0.1, 1.0)) = 0.45
        _RimColor ("Comic Rim Light", Color) = (0.75, 0.85, 1.0, 1)
        _RimPower ("Rim Power", Range(0.5, 8.0)) = 3.5
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _ShadowColor;
                float4 _RimColor;
                float _ShadowThreshold;
                float _ShadowSmoothness;
                float _AmbientBrightness;
                float _RimPower;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs norm = GetVertexNormalInputs(input.normalOS);
                output.positionCS = pos.positionCS;
                output.positionWS = pos.positionWS;
                output.normalWS = norm.normalWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float4 baseTex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(GetCameraPositionWS() - input.positionWS);

                Light mainLight = GetMainLight();
                float NdotL = dot(normalWS, mainLight.direction);
                
                // Stepped 2-tone Cel Shading ramp (ala Fortnite / anime)
                float ramp = smoothstep(_ShadowThreshold - _ShadowSmoothness, _ShadowThreshold + _ShadowSmoothness, NdotL);

                // Warna cahaya: area terang vs bayangan kartun
                float3 litColor = lerp(_ShadowColor.rgb, mainLight.color, ramp);
                
                // Ambient comic floor agar di malam hari karakter tidak gelap gulita
                float3 ambient = _AmbientBrightness * float3(0.55, 0.55, 0.62);

                // Comic rim highlight (tepi kontur kartun)
                float NdotV = 1.0 - saturate(dot(normalWS, viewDirWS));
                float rim = pow(NdotV, _RimPower) * ramp;
                float3 rimColor = rim * _RimColor.rgb;

                float3 finalRgb = baseTex.rgb * (litColor + ambient) + rimColor;
                return half4(finalRgb, baseTex.a);
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
