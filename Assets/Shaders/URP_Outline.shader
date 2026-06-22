Shader "Custom/URP_Outline"
{
    Properties
    {
        [HDR] _OutlineColor ("Outline Color", Color) = (0.0, 2.0, 2.5, 1.0)
        _OutlineThickness ("Outline Thickness", Range(0.0, 0.1)) = 0.015
        _DissolveAmount ("Dissolve Amount", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline" "Queue"="Geometry+1" }
        LOD 100

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            
            Cull Front
            ZWrite On
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS  : SV_POSITION;
                float3 positionOS   : TEXCOORD0;
                float3 positionWS   : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float _OutlineThickness;
                float _DissolveAmount;
            CBUFFER_END
            
            float hash(float3 p) {
                p = frac(p * 0.3183099 + .1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                
                // Inflate mesh along normal
                float3 positionOS = input.positionOS.xyz + input.normalOS * _OutlineThickness;
                output.positionHCS = TransformObjectToHClip(positionOS);
                output.positionOS = input.positionOS.xyz;
                output.positionWS = TransformObjectToWorld(positionOS);
                
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Sweeping Line Dissolve matching the main shader
                float localSweep = (input.positionOS.x + input.positionOS.y + input.positionOS.z) * 0.1 + 0.5;
                float posNoise = hash(floor(input.positionWS * 15.0));
                
                // For outline, we don't have texture color, so we use a flat baseline for edgeNoise
                float edgeNoise = 0.15 + (posNoise * 0.3);
                
                float clipVal = localSweep + edgeNoise - (_DissolveAmount * 2.0);
                clip(clipVal);
                
                return _OutlineColor;
            }
            ENDHLSL
        }
    }
}
