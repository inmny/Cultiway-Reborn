Shader "Cultiway/StrategicKingdomFill"
{
    Properties
    {
        _Opacity ("Opacity", Range(0, 1)) = 0.28
        _PulseStrength ("Pulse Strength", Range(0, 1)) = 0.2
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            ZWrite Off
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 highlight : TEXCOORD1;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 worldPosition : TEXCOORD0;
                half highlight : TEXCOORD1;
            };

            half _Opacity;
            half _PulseStrength;

            v2f vert(appdata v)
            {
                v2f o;
                float4 worldPosition = mul(unity_ObjectToWorld, v.vertex);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.worldPosition = worldPosition.xy;
                o.highlight = v.highlight.x;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                half grain = sin(dot(i.worldPosition, float2(0.071, 0.113)))
                    * cos(dot(i.worldPosition, float2(-0.097, 0.053)));
                half pulse = (0.5 + 0.5 * sin(_Time.y * 2.0))
                    * saturate(i.highlight) * _PulseStrength;

                half3 color = i.color.rgb * (1.0 + grain * 0.018);
                color = lerp(color, half3(1.0, 1.0, 1.0), pulse * 0.08);
                half alpha = i.color.a * _Opacity * (1.0 + pulse * 0.15);
                return fixed4(saturate(color), saturate(alpha));
            }
            ENDCG
        }
    }
}
