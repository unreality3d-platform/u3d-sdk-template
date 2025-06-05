Shader "Custom/TriplanarURP_WebGL_Optimized"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map (RGB)", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        
        _WorldTiling("World Space Tiling", Vector) = (1, 1, 1, 0)
        _WorldOffset("World Space Offset", Vector) = (0, 0, 0, 0)
        [Toggle(_USE_WORLD_SPACE)] _UseWorldSpace("Use World Space", Float) = 1
        _TriplanarBlendSharpness("Triplanar Blend Sharpness", Range(1, 20)) = 4

        [Toggle(_NORMALMAP)] _UseNormalMap("Use Normal Map", Float) = 0
        [Normal] _BumpMap("Normal Map", 2D) = "bump" {}
        _BumpScale("Normal Scale", Float) = 1.0
        
        [Toggle(_METALLICGLOSSMAP)] _UseMetallicMap("Use Metallic Map", Float) = 0
        _MetallicGlossMap("Metallic Map", 2D) = "white" {}
        _Metallic("Metallic", Range(0.0, 1.0)) = 0.0
        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.5
        
        [Toggle(_EMISSION)] _UseEmission("Use Emission", Float) = 0
        [HDR] _EmissionColor("Emission Color", Color) = (0, 0, 0, 1)
        _EmissionMap("Emission Map", 2D) = "white" {}
    }
    
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }
        
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _Metallic;
            half _Smoothness;
            half _BumpScale;
            half4 _EmissionColor;
            float4 _WorldTiling;
            float4 _WorldOffset;
            half _TriplanarBlendSharpness;
        CBUFFER_END

        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
        TEXTURE2D(_MetallicGlossMap); SAMPLER(sampler_MetallicGlossMap);
        TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);

        half3 CalculateTriplanarWeights(half3 worldNormal)
        {
            half3 blendWeights = pow(abs(worldNormal), _TriplanarBlendSharpness);
            half totalWeight = blendWeights.x + blendWeights.y + blendWeights.z;
            return blendWeights * rcp(totalWeight);
        }

        half4 SampleTriplanar(TEXTURE2D_PARAM(tex, samplerTex), float3 worldPos, half3 worldNormal, half3 tiling, half3 offset)
        {
            half3 blendWeights = CalculateTriplanarWeights(worldNormal);
            
            float2 uvX = worldPos.zy * tiling.x + offset.x;
            float2 uvY = worldPos.xz * tiling.y + offset.y;
            float2 uvZ = worldPos.xy * tiling.z + offset.z;
            
            half4 xTexture = SAMPLE_TEXTURE2D(tex, samplerTex, uvX);
            half4 yTexture = SAMPLE_TEXTURE2D(tex, samplerTex, uvY);
            half4 zTexture = SAMPLE_TEXTURE2D(tex, samplerTex, uvZ);
            
            return xTexture * blendWeights.x + yTexture * blendWeights.y + zTexture * blendWeights.z;
        }

        half3 SampleTriplanarNormal(TEXTURE2D_PARAM(tex, samplerTex), float3 worldPos, half3 worldNormal, half3 tiling, half3 offset)
        {
            #if defined(_NORMALMAP)
                half3 blendWeights = CalculateTriplanarWeights(worldNormal);
                
                float2 uvX = worldPos.zy * tiling.x + offset.x;
                float2 uvY = worldPos.xz * tiling.y + offset.y;
                float2 uvZ = worldPos.xy * tiling.z + offset.z;
                
                half3 xNormal = UnpackNormalScale(SAMPLE_TEXTURE2D(tex, samplerTex, uvX), _BumpScale);
                half3 yNormal = UnpackNormalScale(SAMPLE_TEXTURE2D(tex, samplerTex, uvY), _BumpScale);
                half3 zNormal = UnpackNormalScale(SAMPLE_TEXTURE2D(tex, samplerTex, uvZ), _BumpScale);
                
                half3 xNormalWS = half3(xNormal.z, xNormal.y, xNormal.x);
                half3 yNormalWS = half3(yNormal.x, yNormal.z, yNormal.y);
                half3 zNormalWS = zNormal;
                
                return normalize(xNormalWS * blendWeights.x + yNormalWS * blendWeights.y + zNormalWS * blendWeights.z);
            #else
                return worldNormal;
            #endif
        }
        ENDHLSL
        
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #pragma shader_feature_local _USE_WORLD_SPACE
            #pragma shader_feature_local_fragment _NORMALMAP
            #pragma shader_feature_local_fragment _METALLICGLOSSMAP
            #pragma shader_feature_local_fragment _EMISSION
            
            // WebGL-optimized multi_compile directives
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fog
            
            // Strip resource-intensive variants for WebGL
            #pragma skip_variants LIGHTMAP_ON DYNAMICLIGHTMAP_ON LIGHTMAP_SHADOW_MIXING SHADOWS_SHADOWMASK DIRLIGHTMAP_COMBINED
            #pragma skip_variants _ADDITIONAL_LIGHT_SHADOWS _SHADOWS_SOFT _SCREEN_SPACE_OCCLUSION _CLUSTERED_RENDERING
            #pragma skip_variants _REFLECTION_PROBE_BLENDING _REFLECTION_PROBE_BOX_PROJECTION _LIGHT_LAYERS
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                half3 normalOS : NORMAL;
                half4 tangentOS : TANGENT;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                #if defined(_NORMALMAP) && !defined(_USE_WORLD_SPACE)
                    half4 tangentWS : TEXCOORD3;
                #endif
                half3 viewDirWS : TEXCOORD4;
                half fogCoord : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };
            
            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                
                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = normalInput.normalWS;
                
                #if defined(_NORMALMAP) && !defined(_USE_WORLD_SPACE)
                    output.tangentWS = half4(normalInput.tangentWS, input.tangentOS.w);
                #endif
                
                output.viewDirWS = GetWorldSpaceViewDir(vertexInput.positionWS);
                output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
                output.fogCoord = ComputeFogFactor(output.positionCS.z);
                
                return output;
            }
            
            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                
                half4 albedoAlpha;
                half4 metallicGloss;
                half3 emission;
                half3 normalWS;
                
                #if defined(_USE_WORLD_SPACE)
                    half3 worldTiling = half3(_WorldTiling.xyz);
                    half3 worldOffset = half3(_WorldOffset.xyz);
                    
                    albedoAlpha = SampleTriplanar(_BaseMap, sampler_BaseMap, input.positionWS, input.normalWS, worldTiling, worldOffset) * _BaseColor;
                    
                    #if defined(_METALLICGLOSSMAP)
                        metallicGloss = SampleTriplanar(_MetallicGlossMap, sampler_MetallicGlossMap, input.positionWS, input.normalWS, worldTiling, worldOffset);
                    #else
                        metallicGloss = half4(_Metallic, half(0), half(0), _Smoothness);
                    #endif
                    
                    #if defined(_EMISSION)
                        emission = SampleTriplanar(_EmissionMap, sampler_EmissionMap, input.positionWS, input.normalWS, worldTiling, worldOffset).rgb * _EmissionColor.rgb;
                    #else
                        emission = _EmissionColor.rgb;
                    #endif
                    
                    normalWS = SampleTriplanarNormal(_BumpMap, sampler_BumpMap, input.positionWS, input.normalWS, worldTiling, worldOffset);
                #else
                    albedoAlpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                    
                    #if defined(_METALLICGLOSSMAP)
                        metallicGloss = SAMPLE_TEXTURE2D(_MetallicGlossMap, sampler_MetallicGlossMap, input.uv);
                    #else
                        metallicGloss = half4(_Metallic, half(0), half(0), _Smoothness);
                    #endif
                    
                    #if defined(_EMISSION)
                        emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.uv).rgb * _EmissionColor.rgb;
                    #else
                        emission = _EmissionColor.rgb;
                    #endif
                    
                    #if defined(_NORMALMAP)
                        half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv), _BumpScale);
                        half3 normalWS_base = normalize(input.normalWS);
                        half3 tangentWS = normalize(input.tangentWS.xyz);
                        half3 bitangentWS = normalize(cross(normalWS_base, tangentWS) * input.tangentWS.w);
                        normalWS = normalize(mul(normalTS, half3x3(tangentWS, bitangentWS, normalWS_base)));
                    #else
                        normalWS = normalize(input.normalWS);
                    #endif
                #endif
                
                half metallic = metallicGloss.r * _Metallic;
                half smoothness = metallicGloss.a * _Smoothness;
                
                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = albedoAlpha.rgb;
                surfaceData.metallic = metallic;
                surfaceData.specular = half3(0, 0, 0);
                surfaceData.smoothness = smoothness;
                surfaceData.normalTS = half3(0, 0, 1);
                surfaceData.emission = emission;
                surfaceData.occlusion = half(1);
                surfaceData.alpha = albedoAlpha.a;
                
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = normalize(input.viewDirWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.fogCoord = input.fogCoord;
                inputData.vertexLighting = half3(0, 0, 0);
                inputData.bakedGI = SampleSH(normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);
                
                return UniversalFragmentPBR(inputData, surfaceData);
            }
            ENDHLSL
        }
        
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            
            ZWrite On
            ZTest LEqual
            ColorMask 0
            
            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
            
            float3 _LightDirection;
            float3 _LightPosition;
            
            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                half3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };
            
            ShadowVaryings ShadowPassVertex(ShadowAttributes input)
            {
                ShadowVaryings output = (ShadowVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                half3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif

                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));

                #if UNITY_REVERSED_Z
                    output.positionCS.z = min(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    output.positionCS.z = max(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                return output;
            }
            
            half4 ShadowPassFragment(ShadowVaryings input) : SV_TARGET
            {
                return half4(0, 0, 0, 0);
            }
            ENDHLSL
        }
        
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            
            ZWrite On
            ColorMask 0
            
            HLSLPROGRAM
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            
            struct DepthOnlyAttributes
            {
                float4 position : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct DepthOnlyVaryings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };
            
            DepthOnlyVaryings DepthOnlyVertex(DepthOnlyAttributes input)
            {
                DepthOnlyVaryings output = (DepthOnlyVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.position.xyz);
                return output;
            }

            half4 DepthOnlyFragment(DepthOnlyVaryings input) : SV_TARGET
            {
                return half4(0, 0, 0, 0);
            }
            ENDHLSL
        }
    }
    
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}