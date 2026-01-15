Shader "Custom/BoidsInstanced"
{
    Properties
    {
        [Header(Base Maps)]
        _BaseMap ("Albedo", 2D) = "white" {}
        _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Scale", Float) = 1.0
        [Toggle] _USE_ADDITIONAL_LIGHTS("Additional Lights", Float) = 0.0

        [Header(Albedo Variation)]
        _ColorA ("Color A", Color) = (1,1,1,1)
        _ColorB ("Color B", Color) = (0,0.5,1,1)
        
        [Header(Scale)]
        _MinScale ("Min Scale", Float) = 0.5
        _MaxScale ("Max Scale", Float) = 1.5

        [Header(Tail Animation)]
        _WaveSpeed ("Wave Speed", Float) = 5.0
        _WaveFrequency ("Wave Frequency", Float) = 2.0
        _WaveAmplitude ("Wave Amplitude", Float) = 0.2
        
        [Header(Emission)]
        [HDR] _EmissionColor ("Emission Color", Color) = (1, 0.5, 0, 1)
        _MinSpeed ("Min Speed (Emission)", Float) = 2.0
        _MaxSpeed ("Max Speed (Emission)", Float) = 10.0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 300

        // ---------------------------------------------------------
        // SHARED LOGIC
        // ---------------------------------------------------------
        HLSLINCLUDE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RealtimeLights.hlsl"
            
            struct Boid
            {
                float3 position;
                float3 direction;
            };

            StructuredBuffer<Boid> boidBuffer;
            StructuredBuffer<float3> velocityBuffer;

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _ColorA;
                float4 _ColorB;
                float _BumpScale;
                float _WaveSpeed;
                float _WaveFrequency;
                float _WaveAmplitude;
                float4 _EmissionColor;
                float _MinSpeed;
                float _MaxSpeed;
                float _MinScale;
                float _MaxScale;
            CBUFFER_END

            float rand(float co) { return frac(sin(co * 12.9898) * 43758.5453); }

            struct BoidWorldData
            {
                float3 positionWS;
                float3 normalWS;
                float3 tangentWS;
                float4 color;
                float3 emission;
            };

            BoidWorldData CalculateBoidData(uint instanceID, float3 positionOS, float3 normalOS, float3 tangentOS)
            {
                BoidWorldData output = (BoidWorldData)0;

                Boid data = boidBuffer[instanceID];
                float3 pos = data.position;
                float3 dir = normalize(data.direction);
                if (length(dir) < 0.001)
                    dir = float3(0, 0, 1);
                float3 up = float3(0, 1, 0);

                // Custom LookAt matrix
                float3 xaxis = normalize(cross(up, dir));
                float3 yaxis = normalize(cross(dir, xaxis));
                if (length(xaxis) < 0.001) { xaxis = float3(1, 0, 0); yaxis = float3(0, 0, 1); }

                // apply a random scale
                float scaleRandom = rand(instanceID + 73.5); 
                float currentScale = lerp(_MinScale, _MaxScale, scaleRandom);
                xaxis *= currentScale;
                yaxis *= currentScale;
                dir *= currentScale;

                float4x4 objectToWorld = float4x4(
                    xaxis.x, yaxis.x, dir.x, pos.x,
                    xaxis.y, yaxis.y, dir.y, pos.y,
                    xaxis.z, yaxis.z, dir.z, pos.z,
                    0, 0, 0, 1
                );

                // tail animation
                float randomOffset = rand(instanceID);
                float3 animatedPosOS = positionOS;
                float wave = sin((_Time.y + randomOffset) * _WaveSpeed + animatedPosOS.z * _WaveFrequency) * _WaveAmplitude;
                animatedPosOS.x += wave;

                output.positionWS = mul(objectToWorld, float4(animatedPosOS, 1.0)).xyz;
                output.normalWS = normalize(mul((float3x3)objectToWorld, normalOS));
                output.tangentWS = normalize(mul((float3x3)objectToWorld, tangentOS));

                // color
                float colorLerpT = rand(instanceID + 42.1);
                output.color = lerp(_ColorA, _ColorB, colorLerpT);

                float3 velocity = velocityBuffer[instanceID];
                float speed = length(velocity);
                float emissionStrength = smoothstep(_MinSpeed, _MaxSpeed, speed);
                output.emission = _EmissionColor.rgb * emissionStrength;

                return output;
            }
        ENDHLSL

        // ---------------------------------------------------------
        // Pass 1: Universal Forward
        // ---------------------------------------------------------
        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 5.0

            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ _ADDITIONAL_LIGHTS _ADDITIONAL_LIGHTS_VERTEX
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            
            #pragma multi_compile_instancing
            
            #pragma shader_feature _USE_ADDITIONAL_LIGHTS_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            #define UNITY_INDIRECT_DRAW_ARGS IndirectDrawIndexedArgs
            #include "UnityIndirect.cginc"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float4 tangentOS    : TANGENT;
                float2 uv           : TEXCOORD0;
                uint instanceID     : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 positionWS   : TEXCOORD0;
                float3 normalWS     : TEXCOORD1;
                float4 tangentWS    : TEXCOORD3;
                float2 uv           : TEXCOORD4;
                float4 perInstanceColor : TEXCOORD5;
                float3 emission     : TEXCOORD6;
            };

            TEXTURE2D(_BaseMap);    SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap);    SAMPLER(sampler_BumpMap);


            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                InitIndirectDrawArgs(0);
                
                BoidWorldData boidData = CalculateBoidData(input.instanceID, input.positionOS.xyz, input.normalOS, input.tangentOS.xyz);
                
                output.positionWS = boidData.positionWS;
                output.positionCS = TransformWorldToHClip(boidData.positionWS);
                output.normalWS = boidData.normalWS;
                output.perInstanceColor = boidData.color;
                output.tangentWS = float4(boidData.tangentWS, input.tangentOS.w); 
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.emission = boidData.emission;

                return output;
            }

            float3 ShadingFunction(float3 normalWS, Light light)
            {
                float NdotL = dot(normalWS, normalize(light.direction));
                NdotL = (NdotL + 1) * 0.5; // Half Lambert
                return saturate(NdotL) * light.color * light.shadowAttenuation;
            }

            half4 frag(Varyings input) : SV_Target0
            {
                float2 uv = input.uv;
                
                half4 albedoAlpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv) * input.perInstanceColor;
                half3 albedo = albedoAlpha.rgb;
                
                half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv), _BumpScale);
                float3 bitangent = cross(input.normalWS, input.tangentWS.xyz) * input.tangentWS.w;
                float3x3 TBN = float3x3(input.tangentWS.xyz, bitangent, input.normalWS);
                float3 normalWS = normalize(mul(normalTS, TBN));
                
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS.xy);

                half4 shadowMask = CalculateShadowMask(inputData);
                
                float3 lighting = 0;
                
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS), input.positionWS, shadowMask);
                mainLight.distanceAttenuation = 1;
                lighting += ShadingFunction(normalWS, mainLight);
                
#if defined(_ADDITIONAL_LIGHTS) && defined(_USE_ADDITIONAL_LIGHTS_ON)
	            
	            #if USE_CLUSTER_LIGHT_LOOP
	            for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++) {
		            CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
		            Light light = GetAdditionalLight(lightIndex, input.positionWS, shadowMask);
		            {
		                lighting += ShadingFunction(normalWS, light);
		            }
	            }
	            #endif

                uint pixelLightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light additionalLight = GetAdditionalLight(lightIndex, input.positionWS, shadowMask);
                    lighting += ShadingFunction(normalWS, additionalLight);
                LIGHT_LOOP_END
#endif

                float3 finalLighting = albedo * lighting * (1 + input.emission);
                
                return half4(finalLighting, 1);
            }
            ENDHLSL
        }

        // ---------------------------------------------------------
        // Pass 2: Shadow Caster
        // ---------------------------------------------------------
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 5.0
            
            #pragma multi_compile_instancing
            #pragma multi_compile_shadowcaster 

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            
            #define UNITY_INDIRECT_DRAW_ARGS IndirectDrawIndexedArgs
            #include "UnityIndirect.cginc"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                uint instanceID     : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                InitIndirectDrawArgs(0);

                BoidWorldData boidData = CalculateBoidData(input.instanceID, input.positionOS.xyz, input.normalOS, float3(0,0,0));
                float3 lightDirection = _MainLightPosition.xyz;
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(boidData.positionWS, boidData.normalWS, lightDirection));

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return 0;
            }
            
            ENDHLSL
        }
    }
}