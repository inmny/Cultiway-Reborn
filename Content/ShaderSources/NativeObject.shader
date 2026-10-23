Shader "Cultiway/NativeObject"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        [HideInInspector] _NativeStamped ("Baked Original Shader", Float) = 0
        _Color ("Tint", Color) = (1,1,1,1)
        [MaterialToggle] PixelSnap ("Pixel snap", Float) = 0
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Cull Off
        CGINCLUDE
        #include "UnitySprites.cginc"
        float _CultiwayNativeDepthScale, _CultiwayNativeDepthBias, _NativeStamped;
        struct NativeVarying
        {
            float4 vertex:SV_POSITION;
            fixed4 color:COLOR;
            float2 uv:TEXCOORD0;
            float depth:TEXCOORD1;
            UNITY_VERTEX_OUTPUT_STEREO
        };
        NativeVarying nativeSprite(appdata_t v)
        {
            v2f original = SpriteVert(v);
            NativeVarying o;
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            o.vertex = original.vertex; o.color = original.color; o.uv = original.texcoord;
            if (_NativeStamped > .5) { o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.texcoord; o.color=1; }
            float4 local = _NativeStamped > .5 ? v.vertex : UnityFlipSprite(v.vertex.xyz,_Flip);
            float4 world = mul(unity_ObjectToWorld,local);
            float ground = unity_ObjectToWorld._m13 - unity_ObjectToWorld._m23;
            if (_NativeStamped > .5) { ground=v.color.r; o.color=1; }
            float rawDepth = 2*ground-world.y;
            world.z = rawDepth*_CultiwayNativeDepthScale+_CultiwayNativeDepthBias;
            float4 projected = mul(UNITY_MATRIX_VP,world);
            o.vertex.z = projected.z;
            // 深度与共同映射一致，不沿用原版精灵自身的屏幕高度裁剪。
            float clipDepth = projected.z/projected.w;
            o.depth = UNITY_NEAR_CLIP_VALUE < 0 ? clipDepth*.5+.5 : clipDepth;
            return o;
        }
        struct NativeOutput { fixed4 color:SV_Target; float depth:SV_Depth; };
        NativeOutput sampleNative(NativeVarying i)
        {
            NativeOutput o; o.color = SampleSpriteTexture(i.uv)*i.color;
            clip(o.color.a-.001);
            if (_NativeStamped < .5) o.color.rgb *= o.color.a;
            o.depth = saturate(i.depth);
            return o;
        }
        NativeOutput spriteDepth(NativeVarying i) { NativeOutput o=sampleNative(i); clip(o.color.a-.998); return o; }
        NativeOutput spriteOpaque(NativeVarying i) { return spriteDepth(i); }
        NativeOutput spritePartial(NativeVarying i) { NativeOutput o=sampleNative(i); clip(.998-o.color.a); return o; }
        ENDCG
        Pass
        {
            Name "Depth"
            ZWrite On
            ColorMask 0
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex nativeSprite
            #pragma fragment spriteDepth
            #pragma multi_compile _ PIXELSNAP_ON
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            ENDCG
        }
        Pass
        {
            Name "OpaqueColor"
            ZWrite Off
            Blend One OneMinusSrcAlpha
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex nativeSprite
            #pragma fragment spriteOpaque
            #pragma multi_compile _ PIXELSNAP_ON
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            ENDCG
        }
        Pass
        {
            Name "PartialColor"
            ZWrite Off
            Blend One OneMinusSrcAlpha
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex nativeSprite
            #pragma fragment spritePartial
            #pragma multi_compile _ PIXELSNAP_ON
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            ENDCG
        }
    }
    Fallback Off
}
