Shader "Custom/URP_AncientTech"
{
    Properties
    {
        _BaseMap("Texture", 2D) = "white" {}
        _BaseColor("Stone Tint", Color) = (1, 1, 1, 1)
        [HDR] _EmissionColor("Rune Emission Color", Color) = (2.0, 1.0, 0.0, 1)
        [HDR] _EdgeColor("Edge Glow Color", Color) = (3.0, 1.5, 0.0, 1)
        _EdgeThickness("Edge Thickness (Pixels)", Range(0.0, 10.0)) = 3.0
        _DissolveAmount("Dissolve Amount", Range(0.0, 1.0)) = 0.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            
            Cull Back
            ZWrite On
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS  : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float3 positionWS   : TEXCOORD1;
                float3 normalWS     : NORMAL;
                float3 positionOS   : TEXCOORD2;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _EmissionColor;
                float4 _EdgeColor;
                float _EdgeThickness;
                float _DissolveAmount;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.positionOS = input.positionOS.xyz;
                return output;
            }

            // Simple 3D hash for noise
            float hash(float3 p) {
                p = frac(p * 0.3183099 + .1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);

                // -- BASE SHADER LOGIC --
                float2 distToEdgeUV = min(input.uv, 1.0 - input.uv);
                float2 pixelDist = distToEdgeUV / fwidth(input.uv);
                float minDist = min(pixelDist.x, pixelDist.y);
                float isEdge = 1.0 - smoothstep(_EdgeThickness - 0.5, _EdgeThickness + 0.5, minDist);
                
                Light mainLight = GetMainLight();
                half NdotL = saturate(dot(normalize(input.normalWS), mainLight.direction));
                half3 lighting = mainLight.color * NdotL + half3(0.3, 0.25, 0.2); 
                
                half3 stoneBase = texColor.rgb * _BaseColor.rgb * lighting;
                
                half runeMask = saturate(pow(texColor.r, 2.0)); 
                half3 runeGlow = runeMask * _EmissionColor.rgb;
                
                half3 finalColor = stoneBase + runeGlow;
                finalColor = lerp(finalColor, _EdgeColor.rgb, isEdge);

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }
}
