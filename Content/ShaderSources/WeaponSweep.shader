Shader "Cultiway/WeaponSweep"
{
    Properties
    {
        [HideInInspector] _Color ("Tint", Color) = (1, 1, 1, 1)
        _Opacity ("Opacity", Range(0, 1)) = 1
        _LayerMode ("Layer Mode", Range(0, 1)) = 0
        _FlowSpeed ("Flow Speed", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            fixed4 _Color;
            float _Opacity;
            float _LayerMode;
            float _FlowSpeed;

            v2f vert(appdata input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                output.color = input.color * _Color;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float along = saturate(input.uv.x);
                float radial = saturate(input.uv.y);

                float innerSoftness = lerp(0.055, 0.18, _LayerMode);
                float outerSoftness = lerp(0.095, 0.24, _LayerMode);
                float radialMask = smoothstep(0.0, innerSoftness, radial) *
                                   (1.0 - smoothstep(1.0 - outerSoftness, 1.0, radial));

                float tailMask = pow(along, lerp(0.52, 0.78, _LayerMode));
                tailMask *= smoothstep(0.0, 0.075, along);
                float front = 1.0 - smoothstep(0.0, 0.16, 1.0 - along);

                const float twoPi = 6.28318530718;
                float phase = (along * 2.35 + radial * 0.32 - _Time.y * _FlowSpeed) * twoPi;
                float broadFlow = sin(phase) * 0.5 + 0.5;
                float filament = sin((along * 5.2 - radial * 1.15 - _Time.y * _FlowSpeed * 0.58) *
                                     twoPi) * 0.5 + 0.5;
                float detail = lerp(0.82 + broadFlow * 0.13 + filament * 0.05,
                                    0.5 + broadFlow * 0.2 + filament * 0.08,
                                    _LayerMode);

                float alpha = input.color.a * _Opacity * radialMask * tailMask * detail;
                float highlight = front * lerp(0.38, 0.18, _LayerMode) + filament * 0.07;
                fixed3 rgb = lerp(input.color.rgb, fixed3(1.0, 1.0, 1.0), saturate(highlight));
                return fixed4(rgb, alpha);
            }
            ENDCG
        }
    }

    Fallback Off
}
