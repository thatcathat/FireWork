// 수면: PlanarReflection이 그린 반사 텍스처(_FireworksReflectionTex)를 물결로 흔들어 보여 준다.
// 반사 카메라의 화면은 실제 반사상을 위아래로 뒤집은 것이라 v를 뒤집어 읽는다.
Shader "Fireworks/Water"
{
    Properties
    {
        _BaseColor ("Deep Color", Color) = (0.004, 0.008, 0.018, 1)
        _ReflectionStrength ("Reflection Strength", Float) = 0.85
        _Distortion ("Distortion", Float) = 0.018
        _Smear ("Vertical Smear", Float) = 0.03
        _WaveScale ("Wave Scale", Float) = 0.22
        _WaveSpeed ("Wave Speed", Float) = 0.7
        [Toggle] _FlipY ("Flip Reflection V", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_FireworksReflectionTex);
            SAMPLER(sampler_linear_clamp);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _ReflectionStrength;
                float _Distortion;
                float _Smear;
                float _WaveScale;
                float _WaveSpeed;
                float _FlipY;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float4 screenPos : TEXCOORD1; };

            Varyings vert(Attributes input)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.screenPos = ComputeScreenPos(o.positionCS);
                return o;
            }

            float2 waves(float2 p, float t)
            {
                float a = sin(p.x * 1.3 + t) + sin(p.y * 2.1 - t * 1.3) * 0.6 + sin((p.x + p.y) * 3.7 + t * 2.1) * 0.35 + sin((p.x - p.y * 1.7) * 7.3 - t * 2.9) * 0.2;
                float b = sin(p.y * 1.7 + t * 0.8) + sin(p.x * 2.9 + t * 1.6) * 0.6 + sin((p.x * 1.4 - p.y) * 4.3 - t * 1.9) * 0.35 + sin((p.x * 2.3 + p.y) * 8.1 + t * 3.3) * 0.2;
                return float2(a, b) / 2.15;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 uv = i.screenPos.xy / i.screenPos.w;
                float3 toCamera = _WorldSpaceCameraPos - i.positionWS;
                float dist = length(toCamera);
                float3 viewDir = toCamera / dist;

                float near = saturate(70.0 / dist);
                float2 w = waves(i.positionWS.xz * _WaveScale, _Time.y * _WaveSpeed);
                float2 offset = float2(w.x * _Distortion, w.y * _Distortion * 2.0) * lerp(0.35, 1.0, near);

                if (_FlipY > 0.5) uv.y = 1.0 - uv.y;
                uv += offset;

                float3 refl = 0;
                float total = 0;
                for (int k = -3; k <= 3; k++)
                {
                    float wgt = 1.0 - abs(k) / 4.0;
                    float2 tap = uv + float2(0, k * _Smear / 3.0 * (0.4 + 0.6 * near));
                    refl += SAMPLE_TEXTURE2D(_FireworksReflectionTex, sampler_linear_clamp, tap).rgb * wgt;
                    total += wgt;
                }
                refl /= total;

                float fresnel = lerp(0.45, 1.0, pow(1.0 - saturate(viewDir.y), 3.0));
                float3 col = _BaseColor.rgb + refl * _ReflectionStrength * fresnel;
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
