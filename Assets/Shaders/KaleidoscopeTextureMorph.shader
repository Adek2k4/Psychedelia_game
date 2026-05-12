Shader "Custom/KaleidoscopeTextureMorph"
{
    Properties
    {
        // Base
        _MainTex ("Main Texture", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Strength", Float) = 1.0

        // Fractal Textures
        _Channel0 ("Channel 0", 2D) = "white" {}
        _Channel1 ("Channel 1", 2D) = "white" {}
        _Channel2 ("Channel 2", 2D) = "white" {}
        _Channel3 ("Channel 3", 2D) = "white" {}

        // Main Settings
        _AnimationSpeed ("Animation Speed", Range(0.0, 5.0)) = 1.0
        _Scale ("Scale", Range(0.1, 10.0)) = 1.0
        _FractalNormalInfluence ("Fractal Normal Influence", Range(0.0, 5.0)) = 0.0
        _FractalOpacity ("Fractal Color Opacity", Range(0.0, 1.0)) = 0.0
        [Enum(Add,0,Multiply,1,Lerp,2)] _BlendMode ("Blend Mode", Float) = 2.0

        // Waviness (texture / normal)
        [Toggle] _WaveTexEnable ("Waviness on Texture", Float) = 0.0
        [Toggle] _WaveNormEnable ("Waviness on Normal", Float) = 0.0
        _WaveStrength ("Waviness Strength", Range(0.0, 5.0)) = 0.0
        _WaveSpeed ("Waviness Speed", Range(0.0, 5.0)) = 1.0
        _WaveFreqMin ("Min Frequency", Range(1.0, 50.0)) = 5.0
        _WaveFreqMax ("Max Frequency", Range(1.0, 50.0)) = 15.0
        _WaveAmpMin ("Min Amplitude", Range(0.0, 0.2)) = 0.005
        _WaveAmpMax ("Max Amplitude", Range(0.0, 0.2)) = 0.03

        // Shadow waviness (independent)
        _ShadowWaveStrength ("Shadow Wave Strength", Range(0.0, 0.5)) = 0.05
        _ShadowWaveSpeed    ("Shadow Wave Speed",    Range(0.0, 5.0)) = 1.0
        _ShadowWaveFreqMin  ("Shadow Min Frequency", Range(0.1, 20.0)) = 1.0
        _ShadowWaveFreqMax  ("Shadow Max Frequency", Range(0.1, 20.0)) = 4.0
        _ShadowWaveAmpMin   ("Shadow Min Amplitude", Range(0.0, 0.5)) = 0.02
        _ShadowWaveAmpMax   ("Shadow Max Amplitude", Range(0.0, 0.5)) = 0.08
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        LOD 100

        // -------------------------------------------------------------
        // FORWARD PASS - URP lighting + our kaleidoscope
        // -------------------------------------------------------------
        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            // lighting / shadows keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

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
                float3 localNormal  : TEXCOORD2;
                float  fogCoord     : TEXCOORD3;
                float3 positionWS   : TEXCOORD4;
                float3 normalWS     : TEXCOORD5;
            };

            sampler2D _MainTex;
            sampler2D _BumpMap;
            sampler2D _Channel0;
            sampler2D _Channel1;
            sampler2D _Channel2;
            sampler2D _Channel3;

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _BaseColor;
                float  _BumpScale;
                float  _AnimationSpeed;
                float  _Scale;
                float  _FractalNormalInfluence;
                float  _FractalOpacity;
                float  _BlendMode;
                float  _WaveTexEnable;
                float  _WaveNormEnable;
                float  _WaveStrength;
                float  _WaveSpeed;
                float  _WaveFreqMin;
                float  _WaveFreqMax;
                float  _WaveAmpMin;
                float  _WaveAmpMax;
                float  _ShadowWaveStrength;
                float  _ShadowWaveSpeed;
                float  _ShadowWaveFreqMin;
                float  _ShadowWaveFreqMax;
                float  _ShadowWaveAmpMin;
                float  _ShadowWaveAmpMax;
            CBUFFER_END

            float2 GetWaveOffset(float2 uv, float time)
            {
                float t = time * _WaveSpeed;

                float freqX = lerp(_WaveFreqMin, _WaveFreqMax, sin(uv.y * 3.14 + t * 0.7) * 0.5 + 0.5);
                float freqY = lerp(_WaveFreqMin, _WaveFreqMax, cos(uv.x * 2.71 - t * 0.6) * 0.5 + 0.5);

                float ampX = lerp(_WaveAmpMin, _WaveAmpMax, cos(uv.y * 4.33 + t * 0.9) * 0.5 + 0.5);
                float ampY = lerp(_WaveAmpMin, _WaveAmpMax, sin(uv.x * 3.81 - t * 0.8) * 0.5 + 0.5);

                float offsetX = sin(uv.y * freqX + t) * ampX;
                float offsetY = cos(uv.x * freqY + t * 1.1) * ampY;

                return float2(offsetX, offsetY) * _WaveStrength;
            }

            v2f vert(appdata v)
            {
                v2f o;

                VertexPositionInputs vertexInput = GetVertexPositionInputs(v.positionOS.xyz);
                VertexNormalInputs   normalInput = GetVertexNormalInputs(v.normalOS);

                o.positionCS  = vertexInput.positionCS;
                o.uv          = v.uv * _MainTex_ST.xy + _MainTex_ST.zw;
                o.localPos    = v.positionOS.xyz;
                o.localNormal = v.normalOS;
                o.fogCoord    = ComputeFogFactor(vertexInput.positionCS.z);
                o.positionWS  = vertexInput.positionWS;
                o.normalWS    = normalInput.normalWS;
                return o;
            }

            float2 kaleido(float2 uv)
            {
                float th = atan2(uv.y, uv.x);
                float r  = pow(length(uv), 0.9);
                float f  = 3.14159 / 3.5;

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
                float numTextures   = 4.0;
                float totalTime     = time / cycleDuration;

                float currentTextureIndex = floor(fmod(totalTime, numTextures));
                float nextTextureIndex    = fmod(currentTextureIndex + 1.0, numTextures);
                float blendFactor         = frac(totalTime);

                half4 c0 = tex2D(_Channel0, transformUv(at, time) * 5.0);
                half4 c1 = tex2D(_Channel1, transformUv(at, time) * 2.0);
                half4 c2 = tex2D(_Channel2, transformUv(at, time) * 3.0);
                half4 c3 = tex2D(_Channel3, transformUv(at, time) * 2.0);

                half4 color1 = c0;
                half4 color2 = c0;

                if (currentTextureIndex < 1.0)      color1 = c0;
                else if (currentTextureIndex < 2.0) color1 = c1;
                else if (currentTextureIndex < 3.0) color1 = c2;
                else                                color1 = c3;

                if (nextTextureIndex < 1.0)         color2 = c0;
                else if (nextTextureIndex < 2.0)    color2 = c1;
                else if (nextTextureIndex < 3.0)    color2 = c2;
                else                                color2 = c3;

                return lerp(color1, color2, blendFactor);
            }

 half4 frag(v2f i) : SV_Target
{
    float2 waveOffset = GetWaveOffset(i.uv, _Time.y);
    float2 texUv  = i.uv + waveOffset * _WaveTexEnable;
    float2 normUv = i.uv + waveOffset * _WaveNormEnable;

    // 1) kolor bazowy
    half4 mainColor = tex2D(_MainTex, texUv) * _BaseColor;

    // 2) normal mapa (w "tangent space", ale użyjemy jej jako perturbacji)
    half4 rawNormal  = tex2D(_BumpMap, normUv);
    half3 bumpN      = UnpackNormal(rawNormal);     // ok. (0,0,1) gdy brak bumpa
    bumpN.xy        *= _BumpScale;

    float3 absNormal = abs(i.localNormal);
    float2 fractalUv = float2(0,0);

    if (absNormal.x > 0.5)      fractalUv = i.localPos.zy;
    else if (absNormal.y > 0.5) fractalUv = i.localPos.xz;
    else                        fractalUv = i.localPos.xy;

    fractalUv = sign(fractalUv) * 0.5 - fractalUv;
    fractalUv *= _Scale;
    // drobny wpływ bumpa na UV fraktala
    fractalUv += bumpN.xy * 0.1;

    float time = _Time.y * _AnimationSpeed;
    half4 fracColor = scene(kaleido(fractalUv), time);

    half4 finalColor = mainColor;

    // 3) mix koloru bazowego z fraktalem
    if (_BlendMode < 0.5)
        finalColor.rgb = mainColor.rgb + (fracColor.rgb * _FractalOpacity);
    else if (_BlendMode < 1.5)
        finalColor.rgb = mainColor.rgb * lerp(half3(1,1,1), fracColor.rgb, _FractalOpacity);
    else
        finalColor.rgb = lerp(mainColor.rgb, fracColor.rgb, _FractalOpacity);

    // 4) FAKTYCZNE "fale na normalach":
    //    bazujemy na normalWS, ale dorzucamy XY z bump mapy (falującej po normUv)
    float3 normalWS = normalize(i.normalWS);
    float3 bumpOffsetWS = float3(bumpN.x, bumpN.y, 0.0) * _FractalNormalInfluence;
    float3 finalNormalWS = normalize(normalWS + bumpOffsetWS);

    // 5) URP lighting + self-shadows
    float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
    Light mainLight = GetMainLight(shadowCoord);       // zawiera shadowAttenuation[web:38]

    float3 lightDir = mainLight.direction;
    float  NdotL    = saturate(dot(finalNormalWS, lightDir));

    float3 direct  = finalColor.rgb * mainLight.color * NdotL * mainLight.shadowAttenuation;
    float3 ambient = finalColor.rgb * 0.2;

    finalColor.rgb = direct + ambient;
    finalColor.rgb = MixFog(finalColor.rgb, i.fogCoord);
    return finalColor;
}
            ENDHLSL
        }

        // -------------------------------------------------------------
        // SHADOWCASTER - poziome falowanie z poprawnym biasem URP
        // -------------------------------------------------------------
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma target 2.0
            #pragma multi_compile_shadowcaster
            // na wzór URP: obsługa directional / punctual bias[web:130]
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct appdata_shadow
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct v2f_shadow
            {
                float4 positionCS : SV_POSITION;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _BaseColor;
                float  _BumpScale;
                float  _AnimationSpeed;
                float  _Scale;
                float  _FractalNormalInfluence;
                float  _FractalOpacity;
                float  _BlendMode;
                float  _WaveTexEnable;
                float  _WaveNormEnable;
                float  _WaveStrength;
                float  _WaveSpeed;
                float  _WaveFreqMin;
                float  _WaveFreqMax;
                float  _WaveAmpMin;
                float  _WaveAmpMax;
                float  _ShadowWaveStrength;
                float  _ShadowWaveSpeed;
                float  _ShadowWaveFreqMin;
                float  _ShadowWaveFreqMax;
                float  _ShadowWaveAmpMin;
                float  _ShadowWaveAmpMax;
            CBUFFER_END

            float ShadowWaveScalar(float2 posXZ, float time)
            {
                float t = time * _ShadowWaveSpeed;

                float2 p = posXZ * 0.2;
                float phase = p.x + p.y;

                float freq = lerp(_ShadowWaveFreqMin, _ShadowWaveFreqMax,
                                  sin(phase * 0.5 + t * 0.4) * 0.5 + 0.5);

                float baseWave = sin(phase * freq + t);

                float amp = lerp(_ShadowWaveAmpMin, _ShadowWaveAmpMax,
                                 cos(phase * 0.3 + t * 0.3) * 0.5 + 0.5);

                return baseWave * amp * _ShadowWaveStrength;
            }

            // Wersja GetShadowPositionHClip z naszym falowaniem przed ApplyShadowBias[web:128][web:130]
            float4 GetShadowPositionCustomHClip(appdata_shadow input)
            {
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS   = TransformObjectToWorldNormal(input.normalOS);

                // kierunek światła dla biasu
                float3 lightDirWS;
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    lightDirWS = normalize(_LightPosition - positionWS);
                #else
                    lightDirWS = _LightDirection;
                #endif

                // nasze falowanie w poziomie
                float3 upWS   = float3(0, 1, 0);
                float3 sideWS = cross(upWS, lightDirWS);
                if (all(abs(sideWS) < 1e-4))
                {
                    sideWS = float3(1, 0, 0);
                }
                sideWS = normalize(sideWS);

                float wave = ShadowWaveScalar(positionWS.xz, _Time.y);

                // opcjonalny fade przy ziemi (jeśli chcesz, możesz zostawić / skasować)
                // float fade = saturate((positionWS.y - 0.0) * 2.0);
                // wave *= fade;

                positionWS += sideWS * wave;

                float4 positionCS = TransformWorldToHClip(
                    ApplyShadowBias(positionWS, normalWS, lightDirWS)
                );
                positionCS = ApplyShadowClamping(positionCS);
                return positionCS;
            }

            v2f_shadow ShadowVert(appdata_shadow v)
            {
                v2f_shadow o;
                o.positionCS = GetShadowPositionCustomHClip(v);
                return o;
            }

            half4 ShadowFrag(v2f_shadow i) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}