Shader "Unlit/PsychedelicKaleidoscope"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Channel0 ("Channel 0", 2D) = "white" {}
        _Channel1 ("Channel 1", 2D) = "white" {}
        _Channel2 ("Channel 2", 2D) = "white" {}
        _Channel3 ("Channel 3", 2D) = "white" {}
        _AnimationSpeed ("Animation Speed", Range(0.0, 5.0)) = 1.0
        _Scale ("Scale", Range(0.1, 10.0)) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float3 localPos : TEXCOORD1;
                float3 localNormal : NORMAL;
                UNITY_FOG_COORDS(2)
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            sampler2D _Channel0;
            sampler2D _Channel1;
            sampler2D _Channel2;
            sampler2D _Channel3;
            float4 _MainTex_ST;
            float _AnimationSpeed;
            float _Scale;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.localPos = v.vertex.xyz;
                o.localNormal = v.normal;
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            float2 kaleido(float2 uv)
            {
                float th = atan2(uv.y, uv.x);
                float r = pow(length(uv), .9);
                float f = 3.14159 / 3.5;

                th = abs(fmod(th + f/4.0, f) - f/2.0) / (1.0 + r);

                float2 result;
                sincos(th, result.y, result.x);
                return result * r * .1;
            }

            float2 transform(float2 at, float time)
            {
                float2 v;
                float th = .02 * time;
                float sinTh, cosTh;
                sincos(th, sinTh, cosTh);
                v.x = at.x * cosTh - at.y * sinTh - .2 * sinTh;
                v.y = at.x * sinTh + at.y * cosTh + .2 * cosTh;
                return v;
            }

            fixed4 scene(float2 at, float time)
            {
                float cycleDuration = 8.0; // Czas trwania jednego cyklu przejścia
                float numTextures = 4.0; // Liczba tekstur
                float totalTime = time / cycleDuration;

                float currentTextureIndex = floor(fmod(totalTime, numTextures));
                float nextTextureIndex = fmod(currentTextureIndex + 1.0, numTextures);
                float blendFactor = frac(totalTime);

                fixed4 color1, color2;

                // Pobieranie koloru z bieżącej tekstury
                if (currentTextureIndex < 1.0) color1 = tex2D(_Channel0, transform(at, time) * 5.0);
                else if (currentTextureIndex < 2.0) color1 = tex2D(_Channel1, transform(at, time) * 2.0);
                else if (currentTextureIndex < 3.0) color1 = tex2D(_Channel2, transform(at, time) * 3.0);
                else color1 = tex2D(_Channel3, transform(at, time) * 2.0);

                // Pobieranie koloru z następnej tekstury
                if (nextTextureIndex < 1.0) color2 = tex2D(_Channel0, transform(at, time) * 5.0);
                else if (nextTextureIndex < 2.0) color2 = tex2D(_Channel1, transform(at, time) * 2.0);
                else if (nextTextureIndex < 3.0) color2 = tex2D(_Channel2, transform(at, time) * 3.0);
                else color2 = tex2D(_Channel3, transform(at, time) * 2.0);

                // Mieszanie kolorów
                return lerp(color1, color2, blendFactor);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 absNormal = abs(i.localNormal);
                float2 uv = float2(0, 0);
                
                // Tworzymy mapowanie triplanar w przestrzeni lokalnej obiektu.
                // Wybieramy odpowiednie osie w zależności od tego, w którą stronę jest zwrócona ściana.
                if (absNormal.x > 0.5) {
                    uv = i.localPos.zy;
                } else if (absNormal.y > 0.5) {
                    uv = i.localPos.xz;
                } else {
                    uv = i.localPos.xy;
                }
                
                // --- EFEKT TRIPPY ---
                // Dzielimy przestrzeń (od -0.5 do 0.5) na 4 kwadraty i obracamy każdy o 180 stopni.
                // Środek danej ćwiartki to +/- 0.25. Obrót to: nowy_uv = 2 * srodek - stary_uv
                // Równanie upraszcza się do uv = sign(uv) * 0.5 - uv;
                // Dzięki temu środek kalejdoskopu zbiega się na rogach sześcianu!
                uv = sign(uv) * 0.5 - uv;
                // --------------------

                // Skalujemy nasze współrzędne
                uv *= _Scale;
                
                float time = _Time.y * _AnimationSpeed;

                fixed4 col = scene(kaleido(uv), time);
                
                // apply fog
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
}
