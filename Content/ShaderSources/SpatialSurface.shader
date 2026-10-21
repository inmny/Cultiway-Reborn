Shader "Cultiway/SpatialSurface"
{
    Properties { _WorldTime ("World Time", Float) = 0 }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION; float4 surface:TEXCOORD0; float4 lighting:TEXCOORD1; fixed4 color:COLOR; };
            struct v2f { float4 vertex:SV_POSITION; float4 surface:TEXCOORD0; float4 lighting:TEXCOORD1; fixed4 color:COLOR; };
            float _WorldTime;

            float hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float noise(float2 p)
            {
                float2 i=floor(p), f=frac(p);f=f*f*(3.0-2.0*f);
                return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);
            }
            float billow(float2 p) { return noise(p)*.55+noise(p*2.07+11.3)*.3+noise(p*4.19-7.1)*.15; }

            v2f vert(appdata input)
            {
                v2f o;
                if(input.surface.w<1.5 && input.surface.w>.5)
                    input.vertex.xy+=float2(sin(_WorldTime*7.0+input.surface.z*11.0),cos(_WorldTime*5.0+input.surface.x*9.0))*input.lighting.z*.009*input.surface.z;
                o.vertex=UnityObjectToClipPos(input.vertex);o.surface=input.surface;o.lighting=input.lighting;o.color=input.color;
                return o;
            }

            fixed4 frag(v2f i):SV_Target
            {
                float3 p=i.surface.xyz;
                float kind=i.surface.w, facing=i.lighting.x, light=i.lighting.y;
                float t=_WorldTime;
                float n=billow(p.xy*4.0+p.z*2.7+float2(t*.11,-t*.17));
                float3 rgb=i.color.rgb;
                float alpha=i.color.a;
                if(kind<1.5)
                {
                    n=billow(float2(p.x*5.0+p.y*1.7,p.z*4.0-t*1.6));
                    float heat=saturate(n*1.35-p.z*.2);
                    float3 pigment=rgb/max(.08,max(rgb.r,max(rgb.g,rgb.b)));
                    rgb=lerp(pigment*.12,pigment,smoothstep(.18,.65,heat));
                    rgb=lerp(rgb,float3(1,.92,.48),smoothstep(.63,.91,heat)*(1.0-p.z*.6));
                    alpha*=2.8*smoothstep(.15,.48,n)*smoothstep(-.08,.35,facing);
                }
                else if(kind<2.5)
                {
                    float flow=billow(float2(p.x*6.0-t*.85,p.y*9.0+p.z*2.0));
                    float foam=smoothstep(.7,.91,flow)*smoothstep(.65,.95,p.z);
                    rgb=lerp(rgb*(.7+flow*.35),float3(.8,.94,.94),foam*.55);
                    rgb*=.7+light*.3;
                    rgb+=pow(light,18.0)*.1;
                    rgb+=float3(.025,.06,.065)*pow(1.0-abs(facing),3.0);
                    alpha*=.85+flow*.15;
                }
                else if(kind<3.5)
                {
                    float ridge=abs(noise(p.xy*13.0+p.z*8.0)-.5)*2.0;
                    float layer=.9+.1*sin((p.z+p.x*.19)*17.0+noise(p.xy*3.0)*2.0);
                    rgb=lerp(dot(rgb,float3(.333,.333,.333)).xxx,rgb,.48);
                    rgb*=lerp(.65,1.1,smoothstep(.01,.09,ridge))*layer*(.48+light*.52);
                    rgb*=.96+noise(p.xy*38.0+p.z*11.0)*.08;
                    alpha*=smoothstep(-.12,.1,facing);
                }
                else if(kind<4.5)
                {
                    float frost=smoothstep(.68,.85,noise(p.xy*32.0+p.z*13.0));
                    rgb=lerp(rgb*(.4+light*.6),float3(.84,.97,1),frost*.65+pow(1.0-abs(facing),4.0)*.28);
                    alpha*=.8*smoothstep(-.15,.2,facing);
                }
                else if(kind<5.5)
                {
                    float veins=pow(abs(sin(p.z*28.0+p.x*9.0)),16.0)*.18;
                    rgb*=.35+light*.65+n*.22;rgb+=veins*rgb;
                }
                else if(kind<6.5)
                {
                    rgb*=.35+n*.7;
                    alpha*=2.0*smoothstep(.18,.66,n)*smoothstep(0,.5,abs(facing));
                }
                else if(kind<7.5)
                {
                    rgb*=.4+n*.65;
                    rgb=lerp(rgb,min(float3(1,1,1),rgb*1.4+.1),smoothstep(.64,.86,n)*.4);
                    alpha*=2.1*(.55+n*.45)*smoothstep(0,.35,abs(facing));
                }
                else if(kind<8.5)
                {
                    float grain=noise(float2(p.x*28.0+p.z*2.0,p.z*2.0+p.y));
                    rgb*=lerp(.35,1.15,smoothstep(.15,.8,grain))*(.35+light*.65);
                    alpha*=smoothstep(-.12,.15,facing);
                }
                else
                {
                    float brush=noise(p.xy*float2(90,5)+p.z*3.0)*.12;
                    rgb*=.35+light*.75+brush;
                    rgb+=pow(saturate(light),14.0)*.45;
                    alpha*=smoothstep(-.12,.12,facing);
                }
                return fixed4(rgb,min(.93,alpha));
            }
            ENDCG
        }
    }
    Fallback Off
}
