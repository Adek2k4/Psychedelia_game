Shader "Custom/KaleidoscopeTextureMorphNoWaves"
{
    Properties
    {
        [Header(Base Textures)]
        _MainTex ("Main Texture", 2D) = "white" {}
        _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Strength", Float) = 1.0

        [Header(Fractal Textures)]
        _Channel0 ("Channel 0", 2D) = "white" {}
        _Channel1 ("Channel 1", 2D) = "white" {}
        _Channel2 ("Channel 2", 2D) = "white" {}
        _Channel3 ("Channel 3", 2D) = "white" {}
        
        [Header(Settings)]
        _AnimationSpeed ("Animation Speed", Range(0.0, 5.0)) = 1.0
        _Scale ("Scale", Range(0.1, 10.0)) = 1.0
        _FractalNormalInfluence ("Fractal Normal Influence", Range(0.0, 5.0)) = 1.0
        _FractalOpacity ("Fractal Color Opacity", Range(0.0, 1.0)) = 0.5
        [Enum(Add,0,Multiply,1,Lerp,2)] _BlendMode ("Blend Mode", Float) = 2.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        LOD 100

        Pass
        {
            zWrite On
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct appdata
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float2 uv           : TEXCOORD0;
            };

            struct v2f
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float3 localPos     : TEXCOORD1;
                float3 localNormal  : NORMAL;
                float fogCoord       : TEXCOORD2;
            };

            sampler2D _MainTex;
            sampler2D _BumpMap;
            sampler2D _Channel0;
            sampler2D _Channel1;
            sampler2D _Channel2;
            sampler2D _Channel3;

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _BumpScale;
                float _AnimationSpeed;
                float _Scale;
                float _FractalNormalInfluence;
                float _FractalOpacity;
                float _BlendMode;
            CBUFFER_END

            v2f vert(appdata v)
            {
                v2f o;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = vertexInput.positionCS;
                o.uv = v.uv * _MainTex_ST.xy + _MainTex_ST.zw;
                o.localPos = v.positionOS.xyz;
                o.localNormal = v.normalOS;
                o.fogCoord = ComputeFogFactor(vertexInput.positionCS.z);
                return o;
            }

            float2 kaleido(float2 uv)
            {
                float th = atan2(uv.y, uv.x);
                float r = pow(length(uv), 0.9);
                float f = 3.14159 / 3.5;

                th = abs(fmod(th + f / 4.0, f) - f / 2.0) / (1.0 + r);

                float2 result;
                sincos(th, result.y, result.x);
                return result * r * 0.1;
            }

            float2 transformUv(float2 at, float time)
            {
                float2 v;
                float th = 0.02 * time;
                float sinTh, cosTh;
                sincos(th, sinTh, cosTh);
                v.x = at.x * cosTh - at.y * sinTh - 0.2 * sinTh;
                v.y = at.x * sinTh + at.y * cosTh + 0.2 * cosTh;
                return v;
            }

            half4 scene(float2 at, float time)
            {
                float cycleDuration = 8.0;
                float numTextures = 4.0;
                float totalTime = time / cycleDuration;

                float currentTextureIndex = floor(fmod(totalTime, numTextures));
                float nextTextureIndex = fmod(currentTextureIndex + 1.0, numTextures);
                float blendFactor = frac(totalTime);

                half4 c0 = tex2D(_Channel0, transformUv(at, time) * 5.0);
                half4 c1 = tex2D(_Channel1, transformUv(at, time) * 2.0);
                half4 c2 = tex2D(_Channel2, transformUv(at, time) * 3.0);
                half4 c3 = tex2D(_Channel3, transformUv(at, time) * 2.0);

                half4 color1 = c0;
                half4 color2 = c0;

                if (currentTextureIndex < 1.0) color1 = c0;
                else if (currentTextureIndex < 2.0) color1 = c1;
                else if (currentTextureIndex < 3.0) color1 = c2;
                else color1 = c3;

                if (nextTextureIndex < 1.0) color2 = c0;
                else if (nextTextureIndex < 2.0) color2 = c1;
                else if (nextTextureIndex < 3.0) color2 = c2;
                else color2 = c3;

                return lerp(color1, color2, blendFactor);
            }

            half4 frag(v2f i) : SV_Target
            {
                // Podstawowy kolor z MainTex
                half4 mainColor = tex2D(_MainTex, i.uv);

                // Pobranie i rozpakowanie Normal Map (w URP)
                half4 rawNormal = tex2D(_BumpMap, i.uv);
                half3 baseNormal = UnpackNormal(rawNormal); 
                baseNormal.xy *= _BumpScale;
                
                // Triplanar mapping w przestrzeni lokalnej obiektu
                float3 absNormal = abs(i.localNormal);
                float2 fractalUv = float2(0,0);
                
                if (absNormal.x > 0.5) fractalUv = i.localPos.zy;
                else if (absNormal.y > 0.5) fractalUv = i.localPos.xz;
                else fractalUv = i.localPos.xy;

                // Fraktalny trippy effect (obr�t w rogach)
                fractalUv = sign(fractalUv) * 0.5 - fractalUv;
                fractalUv *= _Scale;

                // Wp�yw bazowej normal mapy na koordynaty fraktala (efekt op�ywania)
                fractalUv += baseNormal.xy * 0.1;

                float time = _Time.y * _AnimationSpeed;
                half4 fracColor = scene(kaleido(fractalUv), time);

                half4 finalColor = mainColor;

                // Mieszanie koloru bazowego z wyliczonym fraktalem
                if (_BlendMode < 0.5) finalColor.rgb = mainColor.rgb + (fracColor.rgb * _FractalOpacity);
                else if (_BlendMode < 1.5) finalColor.rgb = mainColor.rgb * lerp(half3(1,1,1), fracColor.rgb, _FractalOpacity);
                else finalColor.rgb = lerp(mainColor.rgb, fracColor.rgb, _FractalOpacity);

                // Modulacja wypuk�o�ci (imitacja o�wietlenia bazuj�ca na fraktalu)
                float fracLuma = dot(fracColor.rgb, float3(0.299, 0.587, 0.114));
                float3 fractalNormalMod = float3(ddx(fracLuma), ddy(fracLuma), 0.1) * _FractalNormalInfluence;
                float3 finalNormal = normalize(baseNormal + fractalNormalMod);

                float lightingMod = saturate(dot(finalNormal, float3(0.5, 0.5, 1.0)));
                finalColor.rgb *= max(0.5, lightingMod);

                finalColor.rgb = MixFog(finalColor.rgb, i.fogCoord);
                return finalColor;
            }
            ENDHLSL
        }
    }
}
