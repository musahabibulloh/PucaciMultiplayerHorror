Shader "Pucaci/CartoonOutline"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (0.0, 0.0, 0.0, 1.0)
        _OutlineThickness ("Outline Thickness", Range(0.5, 4.0)) = 1.25
        _DepthSensitivity ("Depth Sensitivity", Range(1.0, 50.0)) = 12.0
        _DepthThreshold ("Depth Threshold", Range(0.001, 0.5)) = 0.04
        _NormalSensitivity ("Normal Sensitivity", Range(0.1, 10.0)) = 1.8
        _NormalThreshold ("Normal Threshold", Range(0.01, 1.0)) = 0.25
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
            "RenderPipeline" = "UniversalPipeline" 
        }

        LOD 100
        ZWrite Off
        ZTest Always
        Cull Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "CartoonOutlinePass"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float _OutlineThickness;
                float _DepthSensitivity;
                float _DepthThreshold;
                float _NormalSensitivity;
                float _NormalThreshold;
            CBUFFER_END

            struct Attributes
            {
                uint vertexID : SV_VertexID;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
                output.uv = GetFullScreenTriangleTexCoord(input.vertexID);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.uv;
                float2 texelSize = 1.0 / _ScreenParams.xy;
                float halfW = _OutlineThickness * 0.5;

                float2 uv0 = uv + float2(-halfW, -halfW) * texelSize;
                float2 uv1 = uv + float2( halfW,  halfW) * texelSize;
                float2 uv2 = uv + float2(-halfW,  halfW) * texelSize;
                float2 uv3 = uv + float2( halfW, -halfW) * texelSize;

                // 1. Ambil Scene Depth
                float rawDepth = SampleSceneDepth(uv);
                float linearDepth = LinearEyeDepth(rawDepth, _ZBufferParams);

                // Abaikan jika langit (skybox / infinite depth)
                if (linearDepth > _ProjectionParams.z * 0.98)
                {
                    return half4(0, 0, 0, 0);
                }

                float d0 = LinearEyeDepth(SampleSceneDepth(uv0), _ZBufferParams);
                float d1 = LinearEyeDepth(SampleSceneDepth(uv1), _ZBufferParams);
                float d2 = LinearEyeDepth(SampleSceneDepth(uv2), _ZBufferParams);
                float d3 = LinearEyeDepth(SampleSceneDepth(uv3), _ZBufferParams);

                float avgDepth = max(0.2, (d0 + d1 + d2 + d3) * 0.25);
                float depthDiff1 = abs(d0 - d1) / avgDepth;
                float depthDiff2 = abs(d2 - d3) / avgDepth;
                float depthEdge = (depthDiff1 + depthDiff2) * _DepthSensitivity;
                depthEdge = smoothstep(_DepthThreshold, _DepthThreshold * 2.5, depthEdge);

                // 2. Ambil Scene Normals
                float3 n0 = SampleSceneNormals(uv0);
                float3 n1 = SampleSceneNormals(uv1);
                float3 n2 = SampleSceneNormals(uv2);
                float3 n3 = SampleSceneNormals(uv3);

                float normalDiff1 = 1.0 - saturate(dot(n0, n1));
                float normalDiff2 = 1.0 - saturate(dot(n2, n3));
                float normalEdge = (normalDiff1 + normalDiff2) * _NormalSensitivity;
                normalEdge = smoothstep(_NormalThreshold, _NormalThreshold * 2.0, normalEdge);

                // 3. Gabungkan deteksi tepi (Outline Mask)
                float edge = saturate(max(depthEdge, normalEdge));

                return half4(_OutlineColor.rgb, edge * _OutlineColor.a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
