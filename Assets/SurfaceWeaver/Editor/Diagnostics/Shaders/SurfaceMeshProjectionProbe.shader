Shader "Hidden/SurfaceWeaver/MeshProjectionProbe"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off
        ZTest Always

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        float4x4 _TargetLocalToWorld;
        float4x4 _TargetWorldNormalMatrix;
        float3 _BrushCenterWorld;
        float3 _BrushNormalWorld;
        float _RadiusWorld;
        float _ProjectionDepth;
        float _NormalCutoff;
        float _FlipRasterY;

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
            float2 raster = input.positionOS.xy;
            if (_FlipRasterY > 0.5)
                raster.y = 1.0 - raster.y;
            output.positionCS = float4(raster * 2.0 - 1.0, 0.0, 1.0);
            output.uv = input.uv0;
            output.localPosition = input.localPosition;
            output.localSurfaceNormal = input.localSurfaceNormal;
            return output;
        }
        ENDHLSL

        // Pass 0: proves that the UV-space raster mesh reaches the target RenderTexture.
        Pass
        {
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment FragCoverage
            float4 FragCoverage(Varyings input) : SV_Target
            {
                return float4(1, 1, 1, 1);
            }
            ENDHLSL
        }

        // Pass 1: applies only the production world-space facing, radius and depth gates.
        Pass
        {
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment FragAccepted
            float4 FragAccepted(Varyings input) : SV_Target
            {
                float3 worldPosition = mul(_TargetLocalToWorld, float4(input.localPosition, 1.0)).xyz;
                float3 worldDelta = worldPosition - _BrushCenterWorld;
                float3 worldNormal = normalize(mul((float3x3)_TargetWorldNormalMatrix, input.localSurfaceNormal));
                float3 brushNormal = normalize(_BrushNormalWorld);

                if (dot(worldNormal, brushNormal) < _NormalCutoff)
                    discard;
                if (length(worldDelta) > max(_RadiusWorld, 0.0001))
                    discard;
                if (abs(dot(worldDelta, brushNormal)) > max(_ProjectionDepth, 0.001))
                    discard;

                return float4(1, 1, 1, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
