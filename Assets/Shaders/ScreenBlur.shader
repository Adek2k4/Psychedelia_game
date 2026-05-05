Shader "Hidden/Psychedelia/ScreenBlur"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Name "ScreenBlur"
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize;
            float _PsychedeliaBlurStrength;

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 SampleBlur(float2 uv, float2 texel, float strength)
            {
                float2 offset = texel * strength;
                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv) * 0.2h;
                color += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(offset.x, 0)) * 0.1h;
                color += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(-offset.x, 0)) * 0.1h;
                color += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(0, offset.y)) * 0.1h;
                color += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(0, -offset.y)) * 0.1h;
                color += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + offset) * 0.1h;
                color += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv - offset) * 0.1h;
                color += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(offset.x, -offset.y)) * 0.1h;
                color += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(-offset.x, offset.y)) * 0.1h;
                return color;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float strength = max(_PsychedeliaBlurStrength, 0.0);
                if (strength <= 0.001)
                {
                    return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                }

                return SampleBlur(input.uv, _MainTex_TexelSize.xy, strength);
            }
            ENDHLSL
        }
    }
}
