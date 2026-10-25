// Lightweight stylised skybox for the environment system (mobile friendly: no textures, ~40 ALU).
// Three-colour gradient, sun/moon disc with glow, procedural twinkling stars and drifting clouds.
Shader "LastBrick/GradientSky"
{
    Properties
    {
        _TopColor ("Top Color", Color) = (0.25, 0.5, 0.95, 1)
        _HorizonColor ("Horizon Color", Color) = (0.8, 0.9, 1, 1)
        _BottomColor ("Bottom Color", Color) = (0.4, 0.45, 0.5, 1)
        _HorizonSharpness ("Horizon Sharpness", Range(0.5, 8)) = 2.5
        _HorizonOffset ("Horizon Offset", Range(-0.5, 0.5)) = 0
        [HDR] _SunColor ("Sun Color", Color) = (1, 0.9, 0.7, 1)
        _SunDirection ("Sun Direction", Vector) = (0, 0.3, 1, 0)
        _SunSize ("Sun Size", Range(0, 0.3)) = 0.05
        _SunGlow ("Sun Glow", Range(0, 4)) = 1
        _StarIntensity ("Star Intensity", Range(0, 3)) = 0
        _CloudColor ("Cloud Color", Color) = (1, 1, 1, 1)
        _CloudCoverage ("Cloud Coverage", Range(0, 1)) = 0.3
        _CloudSpeed ("Cloud Speed", Float) = 0.004
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _TopColor;
                half4 _HorizonColor;
                half4 _BottomColor;
                half _HorizonSharpness;
                half _HorizonOffset;
                half4 _SunColor;
                float4 _SunDirection;
                half _SunSize;
                half _SunGlow;
                half _StarIntensity;
                half4 _CloudColor;
                half _CloudCoverage;
                float _CloudSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 direction : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.direction = input.positionOS.xyz;
                return output;
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float Hash31(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i);
                float b = Hash21(i + float2(1, 0));
                float c = Hash21(i + float2(0, 1));
                float d = Hash21(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 dir = normalize(input.direction);
                float y = dir.y + _HorizonOffset;

                // Gradient
                half3 col;
                if (y >= 0)
                {
                    half t = 1.0 - pow(1.0 - saturate(y), _HorizonSharpness);
                    col = lerp(_HorizonColor.rgb, _TopColor.rgb, t);
                }
                else
                {
                    half t = 1.0 - pow(1.0 - saturate(-y), _HorizonSharpness * 2.0);
                    col = lerp(_HorizonColor.rgb, _BottomColor.rgb, t);
                }

                // Stars (fade near the horizon)
                if (_StarIntensity > 0.001)
                {
                    float3 grid = dir * 180.0;
                    float3 cell = floor(grid);
                    float h = Hash31(cell);
                    float3 local = frac(grid) - 0.5;
                    float star = step(0.9965, h) * saturate(1.0 - length(local) * 3.0);
                    float twinkle = 0.65 + 0.35 * sin(_Time.y * (2.0 + h * 4.0) + h * 100.0);
                    col += star * twinkle * _StarIntensity * saturate(y * 5.0);
                }

                // Clouds projected on a dome
                if (_CloudCoverage > 0.001 && y > 0)
                {
                    float2 uv = dir.xz / (dir.y + 0.2) * 1.6 + _Time.y * _CloudSpeed;
                    float n = ValueNoise(uv * 2.0) * 0.55 + ValueNoise(uv * 4.3) * 0.3 + ValueNoise(uv * 9.1) * 0.15;
                    float edge = 1.0 - _CloudCoverage;
                    float cloud = smoothstep(edge, edge + 0.25, n) * smoothstep(0.0, 0.25, y);
                    float3 sunDir = normalize(_SunDirection.xyz);
                    half sunLit = pow(saturate(dot(dir, sunDir)), 6.0);
                    half3 cloudCol = lerp(_CloudColor.rgb, _CloudColor.rgb + _SunColor.rgb * 0.35, sunLit);
                    col = lerp(col, cloudCol, cloud * _CloudColor.a);
                }

                // Sun / moon disc and glow
                float sd = dot(dir, normalize(_SunDirection.xyz));
                float sizeCos = 1.0 - _SunSize * _SunSize * 0.5;
                float disc = smoothstep(sizeCos - 0.0004, sizeCos + 0.0002, sd);
                float glow = pow(saturate(sd), 300.0) * 0.6 + pow(saturate(sd), 10.0) * 0.22;
                col += _SunColor.rgb * (disc * 2.0 + glow * _SunGlow);

                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
