Shader "HemanthGameDev/SurfaceWeaver/URP Layered Surface"
{
    Properties
    {
        [NoScaleOffset] _BaseMap ("Base Albedo", 2D) = "white" {}
        _BaseTint ("Base Tint", Color) = (1,1,1,1)
        _BaseTiling ("Base Tiling", Vector) = (1,1,0,0)
        [Enum(Mesh UV0,0,World Planar XZ,1)] _MappingMode ("Texture Mapping", Float) = 0
        [NoScaleOffset] _MaskPage0 ("Packed RGBA Mask Page", 2D) = "black" {}
        _LayerCount ("Overlay Layer Count", Float) = 0

        [NoScaleOffset] _Layer0Map ("Layer 0 Albedo", 2D) = "white" {}
        _Layer0Tint ("Layer 0 Tint", Color) = (1,1,1,1)
        _Layer0Tiling ("Layer 0 Tiling", Vector) = (1,1,0,0)
        _Layer0Params ("Layer 0 Params (Opacity, Channel, Page, Enabled)", Vector) = (1,0,0,0)

        [NoScaleOffset] _Layer1Map ("Layer 1 Albedo", 2D) = "white" {}
        _Layer1Tint ("Layer 1 Tint", Color) = (1,1,1,1)
        _Layer1Tiling ("Layer 1 Tiling", Vector) = (1,1,0,0)
        _Layer1Params ("Layer 1 Params (Opacity, Channel, Page, Enabled)", Vector) = (1,1,0,0)

        [NoScaleOffset] _Layer2Map ("Layer 2 Albedo", 2D) = "white" {}
        _Layer2Tint ("Layer 2 Tint", Color) = (1,1,1,1)
        _Layer2Tiling ("Layer 2 Tiling", Vector) = (1,1,0,0)
        _Layer2Params ("Layer 2 Params (Opacity, Channel, Page, Enabled)", Vector) = (1,2,0,0)

        [NoScaleOffset] _Layer3Map ("Layer 3 Albedo", 2D) = "white" {}
        _Layer3Tint ("Layer 3 Tint", Color) = (1,1,1,1)
        _Layer3Tiling ("Layer 3 Tiling", Vector) = (1,1,0,0)
        _Layer3Params ("Layer 3 Params (Opacity, Channel, Page, Enabled)", Vector) = (1,3,0,0)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "UniversalForwardOnly"
            Tags { "LightMode" = "UniversalForwardOnly" }
            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_MaskPage0); SAMPLER(sampler_MaskPage0);
            TEXTURE2D(_Layer0Map); SAMPLER(sampler_Layer0Map);
            TEXTURE2D(_Layer1Map); SAMPLER(sampler_Layer1Map);
            TEXTURE2D(_Layer2Map); SAMPLER(sampler_Layer2Map);
            TEXTURE2D(_Layer3Map); SAMPLER(sampler_Layer3Map);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseTint;
                float4 _BaseTiling;
                float _MappingMode;
                float _LayerCount;
                float4 _Layer0Tint, _Layer0Tiling, _Layer0Params;
                float4 _Layer1Tint, _Layer1Tiling, _Layer1Params;
                float4 _Layer2Tint, _Layer2Tiling, _Layer2Params;
                float4 _Layer3Tint, _Layer3Tiling, _Layer3Params;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = NormalizeNormalPerVertex(normalInputs.normalWS);
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half ReadChannel(half4 maskValue, float channel)
            {
                if (channel < 0.5) return maskValue.r;
                if (channel < 1.5) return maskValue.g;
                if (channel < 2.5) return maskValue.b;
                return maskValue.a;
            }

            half3 ApplyLayer(half3 currentColor, half3 layerColor, half maskValue, float4 layerParams)
            {
                half weight = saturate(maskValue * layerParams.x) * saturate(layerParams.w);
                return lerp(currentColor, layerColor, weight);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 mappingCoords = (_MappingMode > 0.5) ? input.positionWS.xz : input.uv;
                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, mappingCoords * _BaseTiling.xy).rgb * _BaseTint.rgb;

                // One packed RGBA mask sample feeds all four overlay layers.
                half4 packedMasks = SAMPLE_TEXTURE2D(_MaskPage0, sampler_MaskPage0, input.uv);

                if (_LayerCount > 0.5 && _Layer0Params.w > 0.5)
                {
                    half mask = ReadChannel(packedMasks, _Layer0Params.y);
                    half3 layer = SAMPLE_TEXTURE2D(_Layer0Map, sampler_Layer0Map, mappingCoords * _Layer0Tiling.xy).rgb * _Layer0Tint.rgb;
                    albedo = ApplyLayer(albedo, layer, mask, _Layer0Params);
                }
                if (_LayerCount > 1.5 && _Layer1Params.w > 0.5)
                {
                    half mask = ReadChannel(packedMasks, _Layer1Params.y);
                    half3 layer = SAMPLE_TEXTURE2D(_Layer1Map, sampler_Layer1Map, mappingCoords * _Layer1Tiling.xy).rgb * _Layer1Tint.rgb;
                    albedo = ApplyLayer(albedo, layer, mask, _Layer1Params);
                }
                if (_LayerCount > 2.5 && _Layer2Params.w > 0.5)
                {
                    half mask = ReadChannel(packedMasks, _Layer2Params.y);
                    half3 layer = SAMPLE_TEXTURE2D(_Layer2Map, sampler_Layer2Map, mappingCoords * _Layer2Tiling.xy).rgb * _Layer2Tint.rgb;
                    albedo = ApplyLayer(albedo, layer, mask, _Layer2Params);
                }
                if (_LayerCount > 3.5 && _Layer3Params.w > 0.5)
                {
                    half mask = ReadChannel(packedMasks, _Layer3Params.y);
                    half3 layer = SAMPLE_TEXTURE2D(_Layer3Map, sampler_Layer3Map, mappingCoords * _Layer3Tiling.xy).rgb * _Layer3Tint.rgb;
                    albedo = ApplyLayer(albedo, layer, mask, _Layer3Params);
                }

                half3 normalWS = NormalizeNormalPerPixel(input.normalWS);
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half ndotl = saturate(dot(normalWS, mainLight.direction));
                half3 lighting = SampleSH(normalWS);
                lighting += mainLight.color * (ndotl * mainLight.distanceAttenuation * mainLight.shadowAttenuation);

                #if defined(_ADDITIONAL_LIGHTS)
                    uint additionalLightsCount = GetAdditionalLightsCount();
                    for (uint lightIndex = 0u; lightIndex < additionalLightsCount; ++lightIndex)
                    {
                        Light light = GetAdditionalLight(lightIndex, input.positionWS);
                        half additionalNdotL = saturate(dot(normalWS, light.direction));
                        lighting += light.color * (additionalNdotL * light.distanceAttenuation * light.shadowAttenuation);
                    }
                #endif

                half3 finalColor = albedo * lighting;
                finalColor = MixFog(finalColor, input.fogFactor);
                return half4(finalColor, 1.0);
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
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseTint;
                float4 _BaseTiling;
                float _MappingMode;
                float _LayerCount;
                float4 _Layer0Tint, _Layer0Tiling, _Layer0Params;
                float4 _Layer1Tint, _Layer1Tiling, _Layer1Params;
                float4 _Layer2Tint, _Layer2Tiling, _Layer2Params;
                float4 _Layer3Tint, _Layer3Tiling, _Layer3Params;
            CBUFFER_END

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            ShadowVaryings ShadowVert(ShadowAttributes input)
            {
                ShadowVaryings output = (ShadowVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif
                float4 positionCS = TransformWorldToHClip(
                    ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #endif
                output.positionCS = positionCS;
                return output;
            }

            half4 ShadowFrag(ShadowVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseTint;
                float4 _BaseTiling;
                float _MappingMode;
                float _LayerCount;
                float4 _Layer0Tint, _Layer0Tiling, _Layer0Params;
                float4 _Layer1Tint, _Layer1Tiling, _Layer1Params;
                float4 _Layer2Tint, _Layer2Tiling, _Layer2Params;
                float4 _Layer3Tint, _Layer3Tiling, _Layer3Params;
            CBUFFER_END

            struct DepthAttributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            DepthVaryings DepthVert(DepthAttributes input)
            {
                DepthVaryings output = (DepthVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 DepthFrag(DepthVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return 0;
            }
            ENDHLSL
        }
    }
    FallBack Off
}
