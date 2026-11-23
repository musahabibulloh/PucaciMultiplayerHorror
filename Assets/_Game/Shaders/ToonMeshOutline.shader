Shader "Pucaci/ToonMeshOutline"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (0.0, 0.0, 0.0, 1.0)
        _OutlineWidth ("Outline Width (Fortnite Style)", Range(1.0, 8.0)) = 3.5
        _ZOffset ("Depth Offset", Range(0.0, 5.0)) = 1.0
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry+10"
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

                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(input.normalOS);

                // View Space (Camera Space) Normal Extrusion
                // Standar emas industri cel-shading (Fortnite anime skins / Arc System Works / Genshin)
                float3 posVS = TransformWorldToView(posInputs.positionWS);
                float3 normVS = TransformWorldToViewDir(normInputs.normalWS);

                float dist = max(abs(posVS.z), 0.2f);
                float2 norm2D = normVS.xy;
                float len = length(norm2D);
                norm2D = (len > 0.0001f) ? (norm2D / len) : float2(0, 1);

                float screenH = (_ScreenParams.y > 10.0f) ? _ScreenParams.y : 1080.0f;
                float screenW = (_ScreenParams.x > 10.0f) ? _ScreenParams.x : 1920.0f;
                float2 aspect = float2(screenH / screenW, 1.0f);

                // Ketebalan tegas dalam satuan piksel layar (ala Fortnite / anime)
                float pixelOffset = (_OutlineWidth / screenH) * 2.0f;
                posVS.xy += norm2D * aspect * pixelOffset * dist;

                output.positionCS = TransformViewToHClip(posVS);

                #if UNITY_REVERSED_Z
                    output.positionCS.z -= _ZOffset * 0.0005f * output.positionCS.w;
                #else
                    output.positionCS.z += _ZOffset * 0.0005f * output.positionCS.w;
                #endif

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
