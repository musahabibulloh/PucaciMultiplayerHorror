Shader "Custom/CasperGhost"
{
    Properties
    {
        _BaseMap ("Texture", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)

        [Header(Ghost Transparency Settings)]
        [ColorUsage(true, true)]
        _GhostColor ("Ghost Tint & Transparency", Color) = (1.0, 1.0, 1.0, 0.28)
        [ColorUsage(true, true)]
        _RimColor ("Ghost Silhouette Rim Glow", Color) = (0.90, 0.95, 1.0, 0.8)
        _RimPower ("Rim Power", Range(0.5, 6.0)) = 2.0
        _RimIntensity ("Rim Intensity", Range(0.0, 5.0)) = 2.2
        _GhostBlend ("Ghost Blend (0=Solid, 1=Transparent)", Range(0.0, 1.0)) = 0.0

        [Header(Render State)]
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 5 // SrcAlpha
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 10 // OneMinusSrcAlpha
        [Enum(Off, 0, On, 1)] _ZWrite ("ZWrite", Float) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2 // Back
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "Queue" = "Transparent" 
            "RenderPipeline" = "UniversalPipeline" 
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float2 uv           : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 positionWS   : TEXCOORD1;
                float3 normalWS     : NORMAL;
                float2 uv           : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _GhostColor;
                float4 _RimColor;
                float _RimPower;
                float _RimIntensity;
                float _GhostBlend;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);

                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = normalInput.normalWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half4 baseTex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;

                // Hitung Fresnel / Rim Lighting untuk siluet kontur gaib tembus pandang
                half3 viewDir = normalize(GetCameraPositionWS() - input.positionWS);
                half3 normalWS = normalize(input.normalWS);
                half NdotV = saturate(dot(normalWS, viewDir));

                // Denyut halus aura gaib (subtle ghostly breathing pulse)
                float pulse = 0.94 + 0.06 * sin(_Time.y * 3.0);
                half rim = pow(1.0 - NdotV, _RimPower) * (_RimIntensity * pulse);

                // RGB: Tekstur asli karakter (baju putih, rambut hitam, wajah) tetap utuh
                // Ditambahkan pendaran lembut siluet putih keperakan di tepi tubuh agar lekuk bentuk karakternya tetap tegas terlihat
                half3 ghostRgb = (baseTex.rgb * _GhostColor.rgb) + (_RimColor.rgb * rim * 0.6);

                // Alpha:
                // Saat normal (blend == 0): alpha = 1.0 (padat, full bentuk)
                // Saat ghost (blend == 1): alpha = _GhostColor.a (tembus pandang di badan) + (rim * 0.45 di tepi/siluet)
                // Bentuk karakternya tetap tampak jelas tapi tembus pandang (bukan hilang total dan bukan biru padat)
                half ghostAlpha = saturate(_GhostColor.a + (rim * 0.45));

                // Transisi halus dari wujud asli padat ke wujud arwah tembus pandang
                half4 finalColor;
                finalColor.rgb = lerp(baseTex.rgb, ghostRgb, _GhostBlend);
                finalColor.a = lerp(1.0, ghostAlpha, _GhostBlend);

                return finalColor;
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
