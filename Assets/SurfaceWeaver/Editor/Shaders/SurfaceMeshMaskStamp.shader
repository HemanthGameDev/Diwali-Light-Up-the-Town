Shader "Hidden/SurfaceWeaver/MeshMaskStamp"
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
            Name "MeshProjectedMaskStamp"
            Blend One Zero
            ColorMask RGBA

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_BrushTex);
            SAMPLER(sampler_BrushTex);

            // Declare shader uniforms before any function that references them.
            // HLSL does not guarantee C#-style forward visibility for globals.
            float4x4 _TargetLocalToWorld;
            float4x4 _TargetWorldNormalMatrix;
            float3 _BrushCenterWorld;
            float3 _BrushNormalWorld;
            float _NormalCutoff;
            float _ProjectionDepth;
            float _FlipRasterY;
            float _RadiusWorld;
            float _Opacity;
            float _Hardness;
            float _ChannelIndex;
            float _TargetValue;
            float _UseBrushAlpha;
            float _BrushRotation;
            float _SmoothMode;
            float4 _MainTex_TexelSize;

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv0 : TEXCOORD0;
                float3 localPosition : TEXCOORD1;
                float3 localSurfaceNormal : TEXCOORD2;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 localPosition : TEXCOORD1;
                float3 localSurfaceNormal : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                // The temporary mesh is expressed in UV0 space. It is rendered directly
                // into the mask target, so no camera or object projection is involved.
                // The UV raster mesh POSITION is already in canonical mesh UV0 space.
                // Direct rendering to a RenderTexture may require flipping clip-space Y
                // on top-origin graphics APIs so UV0=(0,0) lands in the texture row that
                // the target material samples for UV0=(0,0).
                // IMPORTANT: the fragment must continue sampling the source mask with the
                // ORIGINAL logical mesh UV, not the flipped raster coordinate. Flipping
                // both values mixes opposite rows of the ping-pong mask and causes the
                // saved/previewed mask to appear mirrored or duplicated.
                float2 rasterPosition = input.positionOS.xy;
                if (_FlipRasterY > 0.5)
                    rasterPosition.y = 1.0 - rasterPosition.y;
                output.positionCS = float4(rasterPosition * 2.0 - 1.0, 0.0, 1.0);
                output.uv = input.uv0;
                output.localPosition = input.localPosition;
                output.localSurfaceNormal = input.localSurfaceNormal;
                return output;
            }

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

                float3 worldPosition = mul(_TargetLocalToWorld, float4(input.localPosition, 1.0)).xyz;
                float3 worldDelta = worldPosition - _BrushCenterWorld;
                float radius = max(_RadiusWorld, 0.0001);

                // Always restrict a stamp to faces that point sufficiently toward the
                // picked surface. Without this gate a large brush can paint the far side
                // of a cylinder and its perpendicular cap faces.
                float3 worldNormal = normalize(mul((float3x3)_TargetWorldNormalMatrix, input.localSurfaceNormal));
                float3 brushNormal = normalize(_BrushNormalWorld);
                float facing = dot(worldNormal, brushNormal);
                if (facing < _NormalCutoff)
                {
                    clip(-1.0);
                    return source;
                }

                // Use a true 3D spherical reach test. A planar disc alone accepts distant
                // points on the opposite side of thin/round meshes when the brush radius
                // is larger than the object's thickness.
                float distance01 = length(worldDelta) / radius;
                if (distance01 > 1.0)
                {
                    clip(-1.0);
                    return source;
                }

                // The depth slab additionally rejects separate, nearly parallel surfaces
                // along the brush normal. It is intentionally independent of the sphere.
                float normalDistance = dot(worldDelta, brushNormal);
                if (abs(normalDistance) > max(_ProjectionDepth, 0.001))
                {
                    clip(-1.0);
                    return source;
                }

                float3 planarDelta = worldDelta - brushNormal * normalDistance;

                // Build ONE tangent frame from the picked brush normal, not from each
                // triangle's interpolated normal. This keeps custom brush orientation stable
                // across adjacent faces and UV seam duplicates during a stamp.
                float3 tangentU = mul((float3x3)_TargetLocalToWorld, float3(1.0, 0.0, 0.0));
                tangentU -= brushNormal * dot(tangentU, brushNormal);
                float tangentULength = length(tangentU);
                if (tangentULength < 0.00001)
                {
                    tangentU = mul((float3x3)_TargetLocalToWorld, float3(0.0, 0.0, 1.0));
                    tangentU -= brushNormal * dot(tangentU, brushNormal);
                    tangentULength = length(tangentU);
                }
                if (tangentULength < 0.00001)
                {
                    float3 fallbackAxis = abs(brushNormal.y) < 0.9 ? float3(0.0, 1.0, 0.0) : float3(1.0, 0.0, 0.0);
                    tangentU = cross(fallbackAxis, brushNormal);
                    tangentULength = length(tangentU);
                }
                if (tangentULength < 0.00001)
                {
                    clip(-1.0);
                    return source;
                }
                tangentU /= tangentULength;
                float3 tangentV = normalize(cross(brushNormal, tangentU));

                float radialFalloff;
                if (_Hardness >= 0.999)
                {
                    radialFalloff = 1.0;
                }
                else
                {
                    radialFalloff = 1.0 - smoothstep(saturate(_Hardness), 1.0, distance01);
                }

                float brushAlpha = 1.0;
                if (_UseBrushAlpha > 0.5)
                {
                    float2 brushCentered = float2(
                        dot(planarDelta, tangentU),
                        dot(planarDelta, tangentV)) / (2.0 * radius);
                    float sineValue;
                    float cosineValue;
                    sincos(_BrushRotation, sineValue, cosineValue);
                    float2 rotated = float2(
                        cosineValue * brushCentered.x - sineValue * brushCentered.y,
                        sineValue * brushCentered.x + cosineValue * brushCentered.y);
                    float2 brushUv = rotated + 0.5;
                    if (brushUv.x < 0.0 || brushUv.x > 1.0 ||
                        brushUv.y < 0.0 || brushUv.y > 1.0)
                    {
                        clip(-1.0);
                        return source;
                    }
                    brushAlpha = SAMPLE_TEXTURE2D(_BrushTex, sampler_BrushTex, brushUv).r;
                }

                float amount = saturate(_Opacity * radialFalloff * brushAlpha);
                if (amount <= 0.000001)
                {
                    clip(-1.0);
                    return source;
                }

                float oldValue = GetChannel(source, channel);
                float targetValue = _TargetValue;

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

                return SetChannel(source, channel, lerp(oldValue, targetValue, amount));
            }
            ENDHLSL
        }
    }
    Fallback Off
}
