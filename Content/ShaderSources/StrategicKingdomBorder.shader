Shader "Cultiway/StrategicKingdomBorder"
{
    Properties
    {
        _LineWidth ("Line Width", Float) = 0.12
        _Opacity ("Opacity", Range(0, 1)) = 0.9
        _PulseStrength ("Pulse Strength", Range(0, 1)) = 0.25
        _FlowSpeed ("Flow Speed", Float) = 1.0
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
                float2 lineData : TEXCOORD0;
                float2 miterExpand : TEXCOORD1;
                float2 highlight : TEXCOORD2;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 lineData : TEXCOORD0;
                half highlight : TEXCOORD1;
            };

            half _LineWidth;
            half _Opacity;
            half _PulseStrength;
            half _FlowSpeed;

            v2f vert(appdata v)
            {
                v2f o;
                v.vertex.xy += v.miterExpand * (v.lineData.y * _LineWidth * 0.5);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.lineData = v.lineData;
                o.highlight = v.highlight.x;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                half sideDistance = abs(i.lineData.y);
                half antialiasWidth = max(fwidth(sideDistance) * 1.25, 0.001);
                half edgeAlpha = 1.0 - smoothstep(
                    1.0 - antialiasWidth,
                    1.0,
                    sideDistance);

                half centerLine = 1.0 - smoothstep(
                    0.12 - antialiasWidth,
                    0.12 + antialiasWidth,
                    sideDistance);
                half3 color = lerp(i.color.rgb, i.color.rgb * 0.18, centerLine);

                half flowWave = sin(i.lineData.x * 0.65 - _Time.y * _FlowSpeed);
                half flow = smoothstep(0.72, 1.0, flowWave);
                half highlight = saturate(i.highlight) * _PulseStrength * flow;
                color = lerp(color, half3(1.0, 1.0, 1.0), highlight * 0.2);

                half alpha = i.color.a * _Opacity * edgeAlpha;
                return fixed4(saturate(color), saturate(alpha));
            }
            ENDCG
        }
    }
}
