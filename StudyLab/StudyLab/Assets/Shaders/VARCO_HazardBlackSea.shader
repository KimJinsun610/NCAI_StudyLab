Shader "VARCO/HazardBlackSea"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (0.01, 0.01, 0.02, 1)
        _RippleColor("Ripple Highlight Color", Color) = (0.12, 0.16, 0.24, 1)
        _RippleSpeed("Ripple Speed", Float) = 1.5
        _RippleScale("Ripple Scale", Float) = 4.0
        _FresnelColor("Fresnel Rim Color", Color) = (0.2, 0.28, 0.4, 1)
        _FresnelPower("Fresnel Power", Float) = 3.0
        _DisplaceAmount("Vertex Displace Amount", Float) = 0.03
        _DisplaceSpeed("Vertex Displace Speed", Float) = 2.0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

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
                float4 _RippleColor;
                float _RippleSpeed;
                float _RippleScale;
                float4 _FresnelColor;
                float _FresnelPower;
                float _DisplaceAmount;
                float _DisplaceSpeed;
            CBUFFER_END

            float Waves(float2 uv, float t)
            {
                float w1 = sin(uv.x * _RippleScale + t * _RippleSpeed);
                float w2 = sin(uv.y * _RippleScale * 1.3 - t * _RippleSpeed * 0.8);
                float w3 = sin((uv.x + uv.y) * _RippleScale * 0.7 + t * _RippleSpeed * 1.4);
                return (w1 + w2 + w3) / 3.0;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                float t = _TimeParameters.x * _DisplaceSpeed;
                float3 positionOS = IN.positionOS.xyz;
                // 큐브 형태는 유지하되(모서리만 살짝 출렁), 검은 바다처럼 표면이 숨쉬듯 움직이는 느낌만 더합니다.
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
                float wSharp = pow(saturate(w * 0.5 + 0.5), 3.0);

                float3 viewDir = normalize(IN.viewDirWS);
                float3 normalWS = normalize(IN.normalWS);
                float fresnel = pow(1.0 - saturate(dot(normalWS, viewDir)), _FresnelPower);

                half3 color = lerp(_BaseColor.rgb, _RippleColor.rgb, wSharp);
                color += _FresnelColor.rgb * fresnel;

                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
