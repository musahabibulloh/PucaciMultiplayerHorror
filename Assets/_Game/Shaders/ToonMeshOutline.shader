Shader "Pucaci/ToonMeshOutline"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (0.0, 0.0, 0.0, 1.0)
        _OutlineWidth ("Outline Width", Range(0.5, 15.0)) = 3.5
        _ZOffset ("Z Offset", Range(0.0, 0.01)) = 0.0002
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "IgnoreProjector" = "True"
            "UniversalMaterialType" = "Unlit"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry+10"
        }
        LOD 100

        Cull Front
        ZWrite On
        ZTest LEqual
        Offset 1, 1

        Pass
        {
            Name "Outline"

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
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
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);

                output.positionCS = vertexInput.positionCS;

                // Screen-space extrusion along world normal projected to clip space
                float3 normalCS = TransformWorldToHClipDir(normalInput.normalWS);
                float2 screenNormal = normalCS.xy;
                float len = length(screenNormal);
                if (len > 0.0001)
                {
                    screenNormal /= len;

                    // Aspect ratio compensation so thickness is uniform horizontally and vertically
                    float aspect = (_ScreenParams.y > 1.0) ? (_ScreenParams.x / _ScreenParams.y) : 1.7778;
                    screenNormal.x /= aspect;
                    screenNormal = normalize(screenNormal);
                    screenNormal.x /= aspect;

                    // Screen outline thickness: ~2.5 pixels for clean comic cartoon silhouette (Image 2)
                    float outlineAmount = _OutlineWidth * 0.0011f;
                    output.positionCS.xy += screenNormal * outlineAmount * output.positionCS.w;
                }

                // Push depth slightly backward so front-faces always cleanly occlude back-faces
                #if UNITY_REVERSED_Z
                    output.positionCS.z -= _ZOffset * output.positionCS.w;
                #else
                    output.positionCS.z += _ZOffset * output.positionCS.w;
                #endif

                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return _OutlineColor;
            }
            ENDHLSL
        }
    }
}
