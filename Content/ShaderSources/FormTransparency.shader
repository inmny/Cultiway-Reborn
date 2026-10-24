Shader "Cultiway/FormTransparency"
{
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            Blend One OneMinusSrcAlpha
            CGPROGRAM
            #pragma target 3.5
            #pragma vertex vertex
            #pragma fragment resolve
            #include "UnityCG.cginc"
            sampler2D _CultiwayTransparentAccumulation, _CultiwayTransparentTransmission;
            struct Varying { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; };
            Varying vertex(uint id:SV_VertexID)
            {
                Varying o; o.uv=float2((id<<1)&2,id&2); o.vertex=float4(o.uv*2-1,0,1);
                return o;
            }
            float4 resolve(Varying i):SV_Target
            {
                float4 sum=tex2D(_CultiwayTransparentAccumulation,i.uv);
                float alpha=1-tex2D(_CultiwayTransparentTransmission,i.uv).r;
                return float4(sum.rgb/max(sum.a,.00001)*alpha,alpha);
            }
            ENDCG
        }
    }
    Fallback Off
}
