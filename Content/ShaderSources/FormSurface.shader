Shader "Cultiway/FormSurface"
{
    Properties
    {
        _MainTex ("Surface Detail", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _Kind ("Surface Kind", Float) = 0
        _Roughness ("Roughness", Float) = 0.35
        _WorldTime ("World Time", Float) = 0
        _ZWrite ("Write Depth", Float) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Back
        ZWrite [_ZWrite]
        Blend One OneMinusSrcAlpha
        CGINCLUDE
        #include "UnityCG.cginc"
        sampler2D _MainTex;
        float4 _Color;
        float _Kind, _Roughness, _WorldTime;
        float4x4 _FP_VP;
        float4x4 _FormMatrix, _FormNormalMatrix;
        float4 _FormOrigin;
        float4 _FP_Eye, _FP_LightDir, _FP_LightCol, _FP_AmbCol;
        struct Input { float4 vertex:POSITION; float3 normal:NORMAL; float2 uv:TEXCOORD0; };
        struct Varying { float4 vertex:SV_POSITION; float3 world:TEXCOORD0; float3 normal:TEXCOORD1; float2 uv:TEXCOORD2; float3 eye:TEXCOORD3; float3 light:TEXCOORD4; };
        Varying baseVertex(Input v)
        {
            Varying o;
            o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
            o.normal = UnityObjectToWorldNormal(v.normal); o.uv = v.uv;
            o.vertex = 0; o.eye = 0; o.light = 0;
            return o;
        }
        Varying nativeVertex(Input v)
        {
            Varying o = baseVertex(v);
            o.world = mul(_FormMatrix, v.vertex).xyz;
            o.normal = normalize(mul((float3x3)_FormNormalMatrix,v.normal));
            float3 centre = mul(unity_ObjectToWorld, float4(0,0,0,1)).xyz;
            float3 projected = float3(o.world.x, o.world.y + o.world.z,
                (o.world.y - o.world.z) * .0001 - .5);
            o.vertex = mul(UNITY_MATRIX_VP, float4(projected,1));
            o.eye = normalize(float3(0,-.7,.7)); o.light = normalize(float3(-.45,-.3,.84));
            return o;
        }
        Varying worldfallVertex(Input v)
        {
            Varying o = baseVertex(v);
            o.vertex = mul(_FP_VP, float4(o.world,1));
            o.eye = _FP_Eye.xyz - o.world; o.light = _FP_LightDir.xyz;
            return o;
        }
        float4 shade(Varying i):SV_Target
        {
            float3 n = normalize(i.normal), eye = normalize(i.eye), light = normalize(i.light);
            float facing = saturate(dot(n,eye)), diffuse = saturate(dot(n,light));
            float rim = pow(1-facing,3);
            float2 uv = i.uv;
            if(_Kind > 2.5) uv += float2(.02,-.19) * _WorldTime;
            float detail = tex2D(_MainTex,uv*float2(8,3)).r;
            float3 base = _Color.rgb;
            float alpha = _Color.a;
            float specular = pow(saturate(dot(n,normalize(light+eye))),lerp(100,8,saturate(_Roughness)));
            if(_Kind < .5)
                base = base*(.34+diffuse*.55+detail*.16) + specular*.65 + float3(.16,.21,.24)*rim;
            else if(_Kind < 1.5)
            {
                float grain = .8 + .16*sin(uv.y*88 + sin(uv.x*12)*2.5);
                base *= grain * (.32+diffuse*.65) * (.75+detail*.35);
            }
            else if(_Kind < 2.5)
                base *= (.34+diffuse*.6) * (.65+detail*.5);
            else
            {
                float flow = sin(uv.x*36+sin(uv.y*19)*1.2)*.5+.5;
                base = base*(.55+diffuse*.4+detail*.12) + specular*.45 + float3(.08,.15,.18)*rim;
                base += flow*.025;
                alpha *= .72 + rim*.28;
            }
            return float4(base*alpha,alpha);
        }
        ENDCG
        Pass
        {
            Name "Native"
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex nativeVertex
            #pragma fragment shade
            ENDCG
        }
        Pass
        {
            Name "Worldfall"
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex worldfallVertex
            #pragma fragment shade
            ENDCG
        }
    }
    Fallback Off
}
