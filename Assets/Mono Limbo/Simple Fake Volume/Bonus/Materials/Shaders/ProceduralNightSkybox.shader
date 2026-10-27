Shader "URP/ProceduralNightSkybox"
{
    Properties
    {
        [Header(Sky Gradients)]
        _ZenithColor ("Zenith Color", Color) = (0.01, 0.02, 0.06, 1)
        _HorizonColor ("Horizon Color", Color) = (0.05, 0.15, 0.30, 1)
        _GroundColor ("Ground Color", Color) = (0.02, 0.02, 0.02, 1)
        _SkyFalloff ("Sky Falloff", Range(0.1, 5.0)) = 1.5

        [Header(Procedural Stars)]
        [HDR] _StarColor ("Star Color", Color) = (1.0, 1.0, 1.0, 1.0)
        _StarDensity ("Star Density", Range(10.0, 500.0)) = 200.0
        _StarThreshold ("Star Threshold", Range(0.9, 1.0)) = 0.99
        _TwinkleSpeed ("Twinkle Speed", Range(0.0, 10.0)) = 2.0
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Background" 
            "Queue" = "Background" 
            "PreviewType" = "Skybox" 
            "RenderPipeline" = "UniversalPipeline" // <--- This tag clears the package warning
        }

        Pass
        {
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            // Include Unity 6 URP standard library
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 viewDir    : TEXCOORD0;
            };

            // Declare properties in CBUFFER for SRP Batcher compatibility
            CBUFFER_START(UnityPerMaterial)
                half4 _ZenithColor;
                half4 _HorizonColor;
                half4 _GroundColor;
                float _SkyFalloff;

                half4 _StarColor;
                float _StarDensity;
                float _StarThreshold;
                float _TwinkleSpeed;
            CBUFFER_END

            // Standard high-frequency hash for stable pseudo-randomness
            float Hash3D(float3 p)
            {
                return frac(sin(dot(p, float3(12.9898, 78.233, 45.164))) * 43758.5453);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                // Standard skybox projection
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                // The object space vertex position acts as our view direction
                output.viewDir = input.positionOS.xyz;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Normalize to map coordinates onto a perfect sphere
                float3 dir = normalize(input.viewDir);
                float y = dir.y;

                // 1. Sky & Ground Gradient
                half3 skyColor = lerp(_HorizonColor.rgb, _ZenithColor.rgb, saturate(y * _SkyFalloff));
                half3 groundColor = _GroundColor.rgb;

                // Smoothly blend the horizon line to avoid sharp, ugly cuts
                half3 finalColor = lerp(groundColor, skyColor, smoothstep(-0.05, 0.05, y));

                // 2. Procedural Star Field (Calculated only above the horizon)
                if (y > 0.0)
                {
                    // Map a 3D grid across the sky sphere based on density
                    float3 p = dir * _StarDensity;
                    float3 cellID = floor(p);
                    float3 cellLocal = frac(p);

                    // Generate a random seed for the current grid cell
                    float rand = Hash3D(cellID);

                    // If the cell's random value exceeds the threshold, draw a star
                    if (rand > _StarThreshold)
                    {
                        // Calculate distance from the center of the cell
                        float dist = length(cellLocal - float3(0.5, 0.5, 0.5));

                        // Create a soft circular shape for the star
                        float starMask = smoothstep(0.4, 0.05, dist);

                        // Procedural twinkling offset by the cell's random value
                        float twinkle = sin(_Time.y * _TwinkleSpeed + rand * 100.0) * 0.5 + 0.5;

                        // Fade stars out gently as they approach the horizon
                        float horizonFade = smoothstep(0.0, 0.2, y);

                        finalColor += _StarColor.rgb * starMask * twinkle * horizonFade;
                    }
                }

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }
}