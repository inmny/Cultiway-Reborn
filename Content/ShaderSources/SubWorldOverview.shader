Shader "Cultiway/SubWorldOverview"
{
    Properties
    {
        [PerRendererData] _MainTex ("Terrain Index Texture", 2D) = "white" {}
        [HideInInspector] _Color ("Tint", Color) = (1, 1, 1, 1)
        _PaletteTex ("Terrain Palette", 2D) = "white" {}
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
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

            sampler2D _MainTex;
            sampler2D _PaletteTex;
            fixed4 _Color;

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
                half encodedIndex = tex2D(_MainTex, input.uv).r;
                half paletteIndex = floor(encodedIndex * 255.0h + 0.5h);
                fixed4 terrainColor = tex2D(_PaletteTex, half2((paletteIndex + 0.5h) / 256.0h, 0.5h));
                return terrainColor * input.color;
            }
            ENDCG
        }
    }

    Fallback Off
}
