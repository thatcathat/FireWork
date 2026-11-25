// 밤하늘 스카이박스: 위아래 그라디언트, 지평선의 도시 불빛, 깜박이는 별.
Shader "Fireworks/NightSky"
{
    Properties
    {
        _Top ("Zenith", Color) = (0.002, 0.004, 0.016, 1)
        _Horizon ("Horizon", Color) = (0.016, 0.026, 0.06, 1)
        _Glow ("Horizon Glow", Color) = (0.09, 0.055, 0.04, 1)
        _StarBrightness ("Star Brightness", Float) = 1.4
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Top;
                float4 _Horizon;
                float4 _Glow;
                float _StarBrightness;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; };

            Varyings vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.dir = input.positionOS.xyz;
                return o;
            }

            float hash13(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.zyx + 31.32);
                return frac((p.x + p.y) * p.z);
            }

            float3 hash33(float3 p)
            {
                p = frac(p * float3(0.1031, 0.1030, 0.0973));
                p += dot(p, p.yxz + 33.33);
                return frac((p.xxy + p.yxx) * p.zyx);
            }

            float starLayer(float3 d, float scale, float density, float radius)
            {
                float3 p = d * scale;
                float3 cell = floor(p);
                float h = hash13(cell);
                if (h > density) return 0;
                float3 center = 0.5 + (hash33(cell) - 0.5) * 0.6;
                float b = smoothstep(radius, 0.0, length(p - cell - center));
                float twinkle = 0.7 + 0.3 * sin(_Time.y * (1.0 + h * 60.0) + h * 400.0);
                return b * twinkle * (0.35 + 0.65 * frac(h * 91.7));
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float up = saturate(d.y);
                float3 col = lerp(_Horizon.rgb, _Top.rgb, pow(up, 0.45));
                col += _Glow.rgb * pow(1.0 - saturate(abs(d.y)), 10.0);
                float s = starLayer(d, 170.0, 0.10, 0.16) + starLayer(d, 70.0, 0.035, 0.11) * 1.8;
                col += s * _StarBrightness * smoothstep(0.02, 0.25, d.y);
                if (d.y < 0) col *= 0.5;
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
