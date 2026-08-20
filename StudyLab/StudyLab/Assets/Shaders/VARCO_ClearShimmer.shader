Shader "VARCO/ClearShimmer"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (1, 0.85, 0.3, 0.5)
        _ShimmerColor("Shimmer Highlight Color", Color) = (1, 0.98, 0.82, 0.85)
        _ShimmerSpeed("Shimmer Speed", Float) = 1.2
        _ShimmerScale("Shimmer Scale", Float) = 5.0
        _FresnelColor("Fresnel Rim Color", Color) = (1, 0.95, 0.6, 1)
        _FresnelPower("Fresnel Power", Float) = 2.5
        _DisplaceAmount("Vertex Displace Amount", Float) = 0.025
        _DisplaceSpeed("Vertex Displace Speed", Float) = 1.6
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float3 viewDirWS   : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _ShimmerColor;
                float _ShimmerSpeed;
                float _ShimmerScale;
                float4 _FresnelColor;
                float _FresnelPower;
                float _DisplaceAmount;
                float _DisplaceSpeed;
            CBUFFER_END

            float Waves(float2 uv, float t)
            {
                float w1 = sin(uv.x * _ShimmerScale + t * _ShimmerSpeed);
                float w2 = sin(uv.y * _ShimmerScale * 1.3 - t * _ShimmerSpeed * 0.8);
                float w3 = sin((uv.x + uv.y) * _ShimmerScale * 0.7 + t * _ShimmerSpeed * 1.4);
                return (w1 + w2 + w3) / 3.0;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                float t = _TimeParameters.x * _DisplaceSpeed;
                float3 positionOS = IN.positionOS.xyz;
                // 큐브 형태는 유지하되 표면이 빛나며 일렁이는 느낌을 주도록 정점을 살짝 흔듭니다.
                float wobble = Waves(positionOS.xz, t);
                positionOS += IN.normalOS * wobble * _DisplaceAmount;

                VertexPositionInputs vertexInput = GetVertexPositionInputs(positionOS);
                OUT.positionWS = vertexInput.positionWS;
                OUT.positionHCS = vertexInput.positionCS;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.viewDirWS = GetWorldSpaceViewDir(vertexInput.positionWS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float t = _TimeParameters.x;
                float2 waveUV = IN.positionWS.xz + IN.positionWS.y * 0.35;
                float w = Waves(waveUV, t);
                float wSharp = pow(saturate(w * 0.5 + 0.5), 2.0);

                float3 viewDir = normalize(IN.viewDirWS);
                float3 normalWS = normalize(IN.normalWS);
                float fresnel = pow(1.0 - saturate(dot(normalWS, viewDir)), _FresnelPower);

                half3 color = lerp(_BaseColor.rgb, _ShimmerColor.rgb, wSharp);
                color += _FresnelColor.rgb * fresnel;

                float alpha = lerp(_BaseColor.a, _ShimmerColor.a, wSharp);
                alpha = saturate(alpha + fresnel * 0.3);

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
