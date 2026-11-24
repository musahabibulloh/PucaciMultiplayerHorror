Shader "Pucaci/ToonMeshOutline"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (0.0, 0.0, 0.0, 1.0)
        _OutlineWidth ("Outline Width (Fortnite Style)", Range(1.0, 25.0)) = 6.0
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
        Offset -0.5, -0.5

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float _OutlineWidth;
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

                float3 posVS = TransformWorldToView(posInputs.positionWS);
                float3 normVS = TransformWorldToViewNormal(normInputs.normalWS, true);

                float2 norm2D = normVS.xy;
                float len = length(norm2D);
                norm2D = (len > 0.001) ? (norm2D / len) : float2(0.0, 1.0);

                float dist = max(0.1, abs(posVS.z));
                float outlineScale = _OutlineWidth * 0.0035f * clamp(sqrt(dist), 1.0, 6.0);

                posVS.xy += norm2D * outlineScale;
                posVS.z += (posVS.z < 0.0) ? 0.015 : -0.015;

                output.positionCS = TransformWViewToHClip(posVS);
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
