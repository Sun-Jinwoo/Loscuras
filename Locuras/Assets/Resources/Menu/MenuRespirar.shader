// Deforma la ilustración del menú solo en las zonas de la máscara, para que parezca que respira.
// Máscara (RGBA): R = dragón (ondula), G = gólem (respira), B = frascos (flotan), A = personaje (respira).
Shader "Cronos/MenuRespirar"
{
    Properties
    {
        [PerRendererData] _MainTex ("Textura", 2D) = "white" {}
        _MaskTex ("Máscara RGBA", 2D) = "black" {}
        _Color ("Tinte", Color) = (1,1,1,1)
        _T ("Tiempo (lo pone el script)", Float) = 0
        _Amp ("Intensidad", Float) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };

            sampler2D _MainTex;
            sampler2D _MaskTex;
            fixed4 _Color;
            float _T;
            float _Amp;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 p = float2(i.uv.x, 1.0 - i.uv.y);          // coordenadas con Y hacia abajo
                float4 m = tex2D(_MaskTex, i.uv);
                float w = 6.2831853 * _T / 4.0;                   // un ciclo cada 4 s
                float2 d = float2(0.0, 0.0);

                // dragón: ondulación que crece hacia la cabeza
                float s = saturate((0.61 - p.y) / 0.46);
                d.x += m.r * (0.0065 * sin(w + p.y * 8.5) * s + 0.003 * sin(w * 0.5)) * _Amp;
                d.y += m.r * 0.0065 * sin(w + 0.8) * _Amp;

                // gólem: respira (escala desde su base)
                d += m.g * (p - float2(0.298, 0.80)) * (0.025 * sin(w + 1.2) * _Amp);

                // frascos: flotan
                d.y += m.b * 0.009 * sin(w * 1.5) * _Amp;
                d.x += m.b * 0.0022 * sin(w * 1.5 + 1.0) * _Amp;

                // personaje: respiración suave del torso
                d += m.a * (p - float2(0.50, 0.68)) * (0.006 * sin(w + 0.4) * _Amp);

                float2 q = p - d;
                float2 uv = float2(q.x, 1.0 - q.y);
                return tex2D(_MainTex, uv) * i.color;
            }
            ENDCG
        }
    }
}
