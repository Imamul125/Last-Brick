Shader "Custom/URP_BrickDisolve"
{
    Properties
    {
        _BaseMap("Albedo Map", 2D) = "white" {}
        _BaseColor("Color", Color) = (1, 1, 1, 1)
        
        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.5
        
        [HDR] _EmissionColor("Emission Color", Color) = (0, 0, 0, 1)
        _EmissionMap("Emission Map", 2D) = "white" {}
        
        _DissolveAmount("Dissolve Amount", Range(0, 1)) = 0
        [HDR] _BurnColor("Burn Color", Color) = (2.0, 1.5, 0.0, 1.0)
    }
    
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "AlphaTest" }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            // URP Lighting & Shadow Keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            
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
                float3 positionWS   : TEXCOORD0;
                float3 normalWS     : NORMAL;
                float2 uv           : TEXCOORD1;
                float3 positionOS   : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _EmissionColor;
                float _Smoothness;
                float _DissolveAmount;
                float4 _BurnColor;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_EmissionMap);
            SAMPLER(sampler_EmissionMap);

            float hash(float3 p) {
                p = frac(p * 0.3183099 + .1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

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

            half4 frag(Varyings input) : SV_Target
            {
                // Sweeping Line Dissolve
                float localSweep = (input.positionOS.x + input.positionOS.y + input.positionOS.z) * 0.1 + 0.5;
                float posNoise = hash(floor(input.positionWS * 15.0));
                float edgeNoise = 0.15 + (posNoise * 0.3);
                
                float clipVal = localSweep + edgeNoise - (_DissolveAmount * 2.0);
                clip(clipVal); // Discards pixel if clipVal < 0

                // Sample Textures
                half4 albedoAlpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                half3 emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.uv).rgb * _EmissionColor.rgb;

                // Setup Surface Data for URP PBR Lighting
                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = albedoAlpha.rgb;
                surfaceData.metallic = 0.0; // The user didn't request metallic control, keeping it simple
                surfaceData.smoothness = _Smoothness;
                surfaceData.emission = emission;
                surfaceData.alpha = 1.0;

                // Setup Input Data for Shadows and Light Directions
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normalize(input.normalWS);
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                
                // Calculate main shadow coordinate
                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                    inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #else
                    inputData.shadowCoord = float4(0,0,0,0);
                #endif
                
                // Perform full URP PBR lighting calculation (shadows, directional lights, ambient)
                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                
                // Add magical glowing burn edge where the block is dissolving
                float burnEdge = 1.0 - smoothstep(0.0, 0.15, clipVal);
                color.rgb += burnEdge * _BurnColor.rgb * 3.0;

                return color;
            }
            ENDHLSL
        }

        // ShadowCaster pass ensures the shadow physically burns away matching the main mesh!
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ColorMask 0

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            // Core library
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            // Required for GetVertexPositionInputs
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS  : SV_POSITION;
                float3 positionWS   : TEXCOORD0;
                float3 positionOS   : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
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
                
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionHCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                
                output.positionOS = input.positionOS.xyz;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float localSweep = (input.positionOS.x + input.positionOS.y + input.positionOS.z) * 0.1 + 0.5;
                float posNoise = hash(floor(input.positionWS * 15.0));
                float edgeNoise = 0.15 + (posNoise * 0.3);
                
                float clipVal = localSweep + edgeNoise - (_DissolveAmount * 2.0);
                clip(clipVal);

                return 0;
            }
            ENDHLSL
        }
    }
}
