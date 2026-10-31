Shader "Hidden/SurfaceWeaver/MaskStamp"
{
    Properties
    {
        _MainTex ("Source Mask", 2D) = "black" {}
        _BrushTex ("Brush Alpha", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            Name "MaskStamp"

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_BrushTex);
            SAMPLER(sampler_BrushTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 texcoord : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.texcoord;
                return output;
            }

            float4 _UvCenter;
            float4 _WorldPerUvU;
            float4 _WorldPerUvV;
            float _RadiusWorld;
            float _Opacity;
            float _Hardness;
            float _ChannelIndex;
            float _TargetValue;
            float _UseBrushAlpha;
            float _BrushRotation;
            float _FillMode;
            float _SmoothMode;
            float4 _MainTex_TexelSize;

            float GetChannel(float4 value, int channel)
            {
                if (channel == 0) return value.r;
                if (channel == 1) return value.g;
                if (channel == 2) return value.b;
                return value.a;
            }

            float4 SetChannel(float4 value, int channel, float channelValue)
            {
                if (channel == 0) value.r = channelValue;
                else if (channel == 1) value.g = channelValue;
                else if (channel == 2) value.b = channelValue;
                else value.a = channelValue;
                return value;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float4 source = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                int channel = (int)round(_ChannelIndex);

                if (_FillMode > 0.5)
                    return SetChannel(source, channel, _TargetValue);

                float2 uvDelta = input.uv - _UvCenter.xy;
                float3 worldDelta =
                    _WorldPerUvU.xyz * uvDelta.x +
                    _WorldPerUvV.xyz * uvDelta.y;

                float radius = max(_RadiusWorld, 0.0001);
                float distance01 = length(worldDelta) / radius;
                if (distance01 > 1.0)
                    return source;

                float radialFalloff;
                if (_Hardness >= 0.999)
                {
                    radialFalloff = 1.0;
                }
                else
                {
                    radialFalloff = 1.0 - smoothstep(
                        saturate(_Hardness),
                        1.0,
                        distance01);
                }

                float brushAlpha = 1.0;
                if (_UseBrushAlpha > 0.5)
                {
                    float3 tangentU = normalize(_WorldPerUvU.xyz);
                    float3 tangentV = _WorldPerUvV.xyz -
                        tangentU * dot(_WorldPerUvV.xyz, tangentU);

                    float tangentLength = length(tangentV);
                    if (tangentLength < 0.00001)
                        return source;

                    tangentV /= tangentLength;

                    float2 brushCentered = float2(
                        dot(worldDelta, tangentU),
                        dot(worldDelta, tangentV)) / (2.0 * radius);

                    float sineValue;
                    float cosineValue;
                    sincos(_BrushRotation, sineValue, cosineValue);

                    float2 rotated = float2(
                        cosineValue * brushCentered.x - sineValue * brushCentered.y,
                        sineValue * brushCentered.x + cosineValue * brushCentered.y);

                    float2 brushUv = rotated + 0.5;
                    if (brushUv.x < 0.0 || brushUv.x > 1.0 ||
                        brushUv.y < 0.0 || brushUv.y > 1.0)
                        return source;

                    brushAlpha = SAMPLE_TEXTURE2D(_BrushTex, sampler_BrushTex, brushUv).r;
                }

                float amount = saturate(_Opacity * radialFalloff * brushAlpha);
                float oldValue = GetChannel(source, channel);
                float targetValue = _TargetValue;

                // Smooth only the selected mask channel. A compact 3x3 Gaussian-like
                // neighborhood keeps the operation local and preserves the other RGBA channels.
                if (_SmoothMode > 0.5)
                {
                    float2 texel = _MainTex_TexelSize.xy;
                    float sum = oldValue * 4.0;
                    sum += GetChannel(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2(texel.x, 0.0)), channel) * 2.0;
                    sum += GetChannel(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv - float2(texel.x, 0.0)), channel) * 2.0;
                    sum += GetChannel(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2(0.0, texel.y)), channel) * 2.0;
                    sum += GetChannel(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv - float2(0.0, texel.y)), channel) * 2.0;
                    sum += GetChannel(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + texel), channel);
                    sum += GetChannel(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv - texel), channel);
                    sum += GetChannel(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2(texel.x, -texel.y)), channel);
                    sum += GetChannel(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2(-texel.x, texel.y)), channel);
                    targetValue = sum / 16.0;
                }

                float newValue = lerp(oldValue, targetValue, amount);
                return SetChannel(source, channel, newValue);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
