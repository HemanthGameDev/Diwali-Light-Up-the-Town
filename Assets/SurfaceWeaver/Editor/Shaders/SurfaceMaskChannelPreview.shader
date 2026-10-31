Shader "Hidden/SurfaceWeaver/MaskChannelPreview"
{
    Properties
    {
        _MainTex ("Packed Mask Page", 2D) = "black" {}
    }
    SubShader
    {
        Tags { "Queue" = "Overlay" }
        Cull Off
        ZWrite Off
        ZTest Always
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _ChannelIndex;

            struct Attributes
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float2 previewUv = input.uv;
                // RenderTexture UV orientation can differ from imported Texture2D assets.
                // Match Unity's documented convention instead of hard-coding a vertical flip.
                #if UNITY_UV_STARTS_AT_TOP
                if (_MainTex_TexelSize.y < 0.0)
                    previewUv.y = 1.0 - previewUv.y;
                #endif
                float4 packed = tex2D(_MainTex, previewUv);
                float value = packed.r;
                if (_ChannelIndex > 0.5 && _ChannelIndex < 1.5) value = packed.g;
                else if (_ChannelIndex > 1.5 && _ChannelIndex < 2.5) value = packed.b;
                else if (_ChannelIndex > 2.5) value = packed.a;
                return float4(value, value, value, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
