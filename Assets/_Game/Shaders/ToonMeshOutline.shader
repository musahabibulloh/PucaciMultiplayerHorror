Shader "Pucaci/ToonMeshOutline"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (0.05, 0.05, 0.05, 1.0)
        _OutlineWidth ("Outline Width (Pixels)", Range(0.5, 4.0)) = 1.2
        _ZOffset ("Depth Offset", Range(0.0, 5.0)) = 0.5
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry+5"
        }

        LOD 100
        Cull Front
        ZWrite On
        ZTest LEqual

        Pass
        {
            Name "ToonMeshOutlinePass"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float _OutlineWidth;
                float _ZOffset;
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

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);

                float4 posCS = TransformWorldToHClip(vertexInput.positionWS);
                float3 normalCS = TransformWorldToHClipDir(normalInput.normalWS);

                // Normalisasi arah tepi di layar 2D
                float len = length(normalCS.xy);
                float2 dir = len > 0.0001 ? (normalCS.xy / len) : float2(0, 0);

                // Koreksi rasio aspek layar
                float2 aspect = float2(_ScreenParams.y / _ScreenParams.x, 1.0);

                // Skala ketebalan konstan di layar (crisp pixel outline, tidak terlalu tebal)
                float2 offset = dir * aspect * (_OutlineWidth * 0.0012) * posCS.w;
                posCS.xy += offset;

                #if UNITY_REVERSED_Z
                    posCS.z += _ZOffset * 0.0001 * posCS.w;
                #else
                    posCS.z -= _ZOffset * 0.0001 * posCS.w;
                #endif

                output.positionCS = posCS;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return _OutlineColor;
            }
            ENDHLSL
        }
    }
    FallBack Off
}
