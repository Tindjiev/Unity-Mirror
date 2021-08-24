Shader "Custom/SinEffectHorizontal"
{
    Properties
    {
        _MainTex ("Input Texture", 2D) = "white" {}
        _Frequency ("Frequency", Float) = 6.0
        _Phase ("Phase", Float) = 0.0
        _Amplitude ("Amplitude", Float) = 0.04
    }

    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            #define TAU 6.2831853071795864769

            struct appdata
            {
                float4 localPosition : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.position = UnityObjectToClipPos(v.localPosition);
                o.uv = v.uv;
                return o;
            }

            uniform sampler2D _MainTex;
            uniform float _Frequency, _Phase, _Amplitude;

            fixed4 frag(v2f i) : SV_Target
            {
                float AmpSmoothening = (cos(i.uv.y * TAU) + 1.0) * 0.5; // cos = (cos(x) + 1) / 2
                AmpSmoothening *= AmpSmoothening;
                AmpSmoothening *= AmpSmoothening;
                AmpSmoothening = 1.0 - AmpSmoothening * AmpSmoothening; // 1 - cos^8

                return tex2D(_MainTex, i.uv + float2(0.0, sin((i.uv.x - _Phase) * _Frequency * TAU) * _Amplitude * AmpSmoothening));
            }

            ENDCG
        }
    }
}
