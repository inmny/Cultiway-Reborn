Shader "Cultiway/WeaponThrust"
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
                float signedAcross = saturate(input.uv.y) * 2.0 - 1.0;
                float across = abs(signedAcross);

                float edgeSoftness = lerp(0.12, 0.46, _LayerMode);
                float sideMask = 1.0 - smoothstep(1.0 - edgeSoftness, 1.0, across);
                float rootMask = smoothstep(0.0, lerp(0.07, 0.14, _LayerMode), along);
                float tipMask = 1.0 - smoothstep(0.985, 1.0, along);

                const float twoPi = 6.28318530718;
                float spiralPhase = (along * 3.25 - _Time.y * _FlowSpeed) * twoPi;
                float spiralCenter = sin(spiralPhase) * 0.58;
                float ridgeWidth = lerp(0.11, 0.28, _LayerMode);
                float ridgeA = 1.0 - smoothstep(ridgeWidth, ridgeWidth * 2.15,
                                               abs(signedAcross - spiralCenter));
                float ridgeB = 1.0 - smoothstep(ridgeWidth, ridgeWidth * 2.15,
                                               abs(signedAcross + spiralCenter));
                float facing = cos(spiralPhase) * 0.5 + 0.5;
                ridgeA *= 0.42 + facing * 0.58;
                ridgeB *= 1.0 - facing * 0.58;
                float spiralRidges = saturate(ridgeA + ridgeB);

                float collar = pow(saturate(1.0 - abs(sin(spiralPhase * 0.5))), 5.0);
                float centerLine = pow(saturate(1.0 - across), lerp(1.3, 2.7, _LayerMode));
                float bodyFill = lerp(0.5, 0.2, _LayerMode) + centerLine * lerp(0.22, 0.16, _LayerMode);
                float headDistance = (along - 0.925) * 15.0;
                float headLight = exp2(-headDistance * headDistance);

                float detail = bodyFill + spiralRidges * lerp(0.42, 0.24, _LayerMode) +
                               collar * lerp(0.12, 0.07, _LayerMode);
                float alpha = input.color.a * _Opacity * sideMask * rootMask * tipMask * detail;
                float rim = smoothstep(0.58, 0.82, across) * (1.0 - smoothstep(0.86, 1.0, across));
                float highlight = spiralRidges * lerp(0.56, 0.24, _LayerMode) +
                                  rim * lerp(0.22, 0.08, _LayerMode) +
                                  headLight * lerp(0.58, 0.26, _LayerMode);
                fixed3 rgb = lerp(input.color.rgb, fixed3(1.0, 1.0, 1.0), saturate(highlight));
                return fixed4(rgb, alpha);
            }
            ENDCG
        }
    }

    Fallback Off
}
