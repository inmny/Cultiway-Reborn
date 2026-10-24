Shader "Cultiway/FormSurface"
{
    Properties
    {
        _MainTex ("Surface Detail", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _Dye ("Display Dye", Color) = (1,1,1,1)
        _Kind ("Surface Kind", Float) = 0
        _Roughness ("Roughness", Float) = 0.35
        _WorldTime ("World Time", Float) = 0
        _ZWrite ("Write Depth", Float) = 1
        [HideInInspector] _NativeDepthScale ("Native Depth Scale", Float) = 1
        [HideInInspector] _NativeDepthBias ("Native Depth Bias", Float) = 0
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
        float4 _Color, _Dye;
        float _Kind, _Roughness, _WorldTime;
        float4x4 _FP_VP;
        float4x4 _FormMatrix, _FormNormalMatrix;
        float _UseSkinning, _NativeDepthScale, _NativeDepthBias;
        float _TerrainEnabled;
        sampler2D _TerrainHeights;
        float4 _TerrainSize;
        float4 _TerrainAnchor;
        float4 _FormBuild;
        float4x4 _FormBones[32];
        float4 _FP_Eye, _FP_LightDir, _FP_LightCol, _FP_AmbCol, _FP_View, _FP_Cam, _FP_CamF;
        struct Input { float4 vertex:POSITION; float3 normal:NORMAL; float2 uv:TEXCOORD0; float4 controls:TEXCOORD1; float4 weights:TEXCOORD2; float2 surface:TEXCOORD3; float4 build:TEXCOORD4; };
        struct Varying { float4 vertex:SV_POSITION; float3 world:TEXCOORD0; float3 normal:TEXCOORD1; float2 uv:TEXCOORD2; float3 eye:TEXCOORD3; float3 light:TEXCOORD4; float2 surface:TEXCOORD5; float buildAt:TEXCOORD6; };
        Varying baseVertex(Input v)
        {
            Varying o;
            o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
            o.normal = UnityObjectToWorldNormal(v.normal); o.uv = v.uv;
            o.surface = v.surface; o.surface.x *= length(mul((float3x3)unity_ObjectToWorld,float3(1,0,0)));
            o.vertex = 0; o.eye = 0; o.light = 0;
            o.buildAt = v.build.w;
            return o;
        }
        Input posedVertex(Input v)
        {
            if (_FormBuild.z > .5)
            {
                float factor = saturate((_FormBuild.x-v.build.w)/_FormBuild.y);
                v.vertex.xyz = lerp(v.build.xyz,v.vertex.xyz,factor);
                v.surface.x *= factor;
            }
            if (_UseSkinning > .5)
            {
                float4 position = 0; float3 normal = 0;
                [unroll] for (int k = 0; k < 4; k++)
                {
                    float4x4 bone = _FormBones[(int)v.controls[k]];
                    position += mul(bone,v.vertex) * v.weights[k];
                    normal += mul((float3x3)bone,v.normal) * v.weights[k];
                }
                v.vertex = position; v.surface.x *= length(normal); v.normal = normalize(normal);
            }
            return v;
        }
        Varying nativeVertex(Input v)
        {
            v = posedVertex(v);
            Varying o = baseVertex(v);
            o.world = mul(_FormMatrix, v.vertex).xyz;
            o.normal = normalize(mul((float3x3)_FormNormalMatrix,v.normal));
            o.surface.x = v.surface.x * length(mul((float3x3)_FormMatrix,float3(1,0,0)));
            float elevation = o.world.z;
            if (_TerrainEnabled > .5)
            {
                float ground = _TerrainAnchor.y > .5 ? _TerrainAnchor.x :
                    any(o.world.xy < 0) || any(o.world.xy >= _TerrainSize.xy) ? _TerrainSize.z :
                    tex2Dlod(_TerrainHeights,float4(o.world.xy / _TerrainSize.xy,0,0)).r;
                elevation -= ground;
            }
            float3 projected = float3(o.world.x, o.world.y + elevation,
                (o.world.y - elevation) * _NativeDepthScale + _NativeDepthBias);
            o.vertex = mul(UNITY_MATRIX_VP, float4(projected,1));
            o.eye = normalize(float3(0,-.7,.7)); o.light = normalize(float3(-.45,-.3,.84));
            return o;
        }
        Varying worldfallVertex(Input v)
        {
            v = posedVertex(v);
            Varying o = baseVertex(v);
            o.vertex = mul(_FP_VP, float4(o.world,1));
            o.eye = _FP_Eye.xyz - o.world; o.light = _FP_LightDir.xyz;
            return o;
        }
        float waterNoise(float2 p)
        {
            float2 cell = floor(p), f = frac(p); f = f*f*(3-2*f);
            float4 q = float4(dot(cell,float2(127.1,311.7)),dot(cell+float2(1,0),float2(127.1,311.7)),
                dot(cell+float2(0,1),float2(127.1,311.7)),dot(cell+1,float2(127.1,311.7)));
            q = frac(sin(q)*43758.5453);
            return lerp(lerp(q.x,q.y,f.x),lerp(q.z,q.w,f.x),f.y);
        }
        float4 shadeSurface(Varying i, bool worldfall)
        {
            if (_FormBuild.z > .5) clip(_FormBuild.x-i.buildAt-.000001);
            float3 n = normalize(i.normal), eye = normalize(i.eye), light = normalize(i.light);
            float facing = saturate(dot(n,eye)), diffuse = saturate(dot(n,light));
            float rim = pow(1-facing,3);
            float2 uv = i.uv;
            float detail = tex2D(_MainTex,uv*float2(8,3)).r;
            float3 base = _Color.rgb * _Dye.rgb;
            float alpha = _Color.a;
            // 原版保持原有投影受光；三维按外部场景的实际环境色和主光色着色。
            float3 ambient = worldfall ? max(0,_FP_AmbCol.rgb)/.34 : 1;
            float3 direct = worldfall ? max(0,_FP_LightCol.rgb)/.55 : 1;
            float3 environmentLight = worldfall ? max(0,_FP_AmbCol.rgb)+max(0,_FP_LightCol.rgb)*.5 : 1;
            float specular = pow(saturate(dot(n,normalize(light+eye))),lerp(100,8,saturate(_Roughness)));
            if(_Kind < .5)
                base = base*((.34+detail*.16)*ambient+diffuse*.55*direct) + specular*.65*direct + float3(.16,.21,.24)*rim*environmentLight;
            else if(_Kind < 1.5)
            {
                float grain = .8 + .16*sin(uv.y*88 + sin(uv.x*12)*2.5);
                base *= grain * (.32*ambient+diffuse*.65*direct) * (.75+detail*.35);
            }
            else if(_Kind < 2.5)
                base *= (.34*ambient+diffuse*.6*direct) * (.65+detail*.5);
            else if(_Kind < 3.5)
            {
                // 使用随表面变形的坐标扰动反光，轮廓与真实作用形状保持原姿态。
                float2 flow = uv * float2(8,4) + float2(.025,-.075) * _WorldTime;
                // 环向坐标两端取相同值及导数，闭合表面不会因流动露出接缝。
                float seam = smoothstep(0,1,uv.x);
                float h = lerp(waterNoise(flow),waterNoise(flow-float2(8,0)),seam);
                h += .35*lerp(waterNoise(flow*1.73+float2(.31,.27)),waterNoise((flow-float2(8,0))*1.73+float2(.31,.27)),seam);
                float3 dx = ddx(i.world), dy = ddy(i.world);
                float3 across = cross(dy,n), along = cross(n,dx);
                float determinant = dot(dx,across);
                float3 slope = (ddx(h)*across+ddy(h)*along) / max(abs(determinant),.00001) * sign(determinant);
                n = normalize(n - clamp(slope,-.4,.4)*.035);
                facing = saturate(dot(n,eye)); diffuse = saturate(dot(n,light));
                float fresnel = .035 + .965*pow(1-facing,5);
                float3 reflected = reflect(-eye,n);
                float sky = smoothstep(-.2,.8,reflected.z);
                float3 environment = lerp(float3(.06,.24,.32),float3(.36,.65,.78),sky);
                float broad = pow(saturate(dot(n,normalize(light+eye))),22);
                float shine = pow(saturate(dot(n,normalize(light+eye))),120);
                // 斜看时光线穿过的材料更长，不能反而把表面当成更薄的水。
                float thickness = max(.025,i.surface.x)/max(.2,facing);
                float depth = 1-exp(-thickness*.7);
                float3 shallow = base*1.25 + float3(.025,.12,.12);
                float3 deep = base*float3(.25,.65,.8);
                base = lerp(shallow,deep,depth)*(.58*ambient+.38*diffuse*direct);
                base = lerp(base,environment*environmentLight,fresnel*.45) + (broad*.04 + shine*.25)*direct;
                alpha = saturate(alpha*(.28+.67*depth)+fresnel*.35);
                float foam = saturate(i.surface.y*(.45+.7*waterNoise(uv*float2(24,6))));
                base = lerp(base,float3(.72,.91,.92)*(.75*ambient+.2*diffuse*direct),foam);
                alpha = lerp(alpha,.94,foam);
            }
            else if(_Kind < 4.5)
            {
                // 霜面固定在材料坐标上；冻结后不再沿用液态流动，也不伪造损伤裂纹。
                float crystal = .65*waterNoise(uv*float2(48,24))+.35*waterNoise(uv*float2(13,7));
                float frost = smoothstep(.52,.82,crystal)*.5;
                float thickness = max(.025,i.surface.x)/max(.2,facing);
                float depth = 1-exp(-thickness*.7);
                float fresnel = .055+.945*pow(1-facing,5);
                float sky = smoothstep(-.2,.8,reflect(-eye,n).z);
                float3 environment = lerp(float3(.14,.28,.34),float3(.66,.82,.89),sky);
                float3 body = lerp(base*1.08,base*float3(.42,.72,.84),depth);
                body *= .68*ambient+diffuse*.3*direct;
                body = lerp(body,float3(.82,.92,.95)*(.7*ambient+.25*diffuse*direct),frost);
                base = lerp(body,environment*environmentLight,fresnel*.65)+specular*lerp(.45,.12,frost)*direct;
                alpha = saturate(alpha*(.32+.65*depth)+fresnel*.18);
                alpha = lerp(alpha,.84,frost);
            }
            else if(_Kind < 5.5)
                base *= .84*ambient+.16*diffuse*direct;
            else if(_Kind < 6.5)
            {
                float flow = waterNoise(uv*float2(8,4)+float2(.03,-.12)*_WorldTime);
                float crust = smoothstep(.3,.75,flow);
                base = lerp(base,base*float3(.38,.22,.14),crust*.72);
                base *= .85+.15*diffuse;
                base += float3(.45,.14,.02)*rim + specular*.24;
            }
            else
            {
                // 镜面按真实表面法向反射环境方向；没有读取场景颜色或假称物件倒影。
                float3 reflected = reflect(-eye,n);
                float sky = smoothstep(-.12,.65,reflected.z);
                float horizon = exp(-abs(reflected.z)*8);
                float3 environment = lerp(float3(.12,.14,.12),float3(.48,.67,.79),sky);
                environment += horizon*float3(.11,.12,.12);
                base = lerp(base*(.26*ambient+.22*diffuse*direct),environment*environmentLight,.82);
                base += specular*.85*direct;
            }
            return float4(base*alpha,alpha);
        }
        float4 shadeNative(Varying i):SV_Target { return shadeSurface(i,false); }
        float4 shadeWorldfall(Varying i):SV_Target { return shadeSurface(i,true); }
        // 场景颜色和资料必须同时写入，否则后处理仍会在实体上画出后方轮廓。
        struct SceneOutput { float4 color:SV_Target0; float4 data:SV_Target1; };
        SceneOutput worldfallSolid(Varying i)
        {
            SceneOutput o;
            o.color = shadeSurface(i,true);
            float3 relative = i.world - _FP_Eye.xyz;
            float ahead = dot(relative.xy,_FP_View.xy);
            float cameraDepth = dot(relative,_FP_CamF.xyz);
            float depth = _FP_Cam.z <= .5 ? ahead :
                (_FP_Cam.w <= .5 ? cameraDepth : max(ahead,cameraDepth*.05));
            // 普通实体类别为3；编号0代表无人物，不能借用真实人物的拾取编号。
            o.data = float4(depth,0,3,_Kind >= 5.5 && _Kind < 6.5 ? 1 : 0);
            return o;
        }
        ENDCG
        Pass
        {
            Name "Native"
            Tags { "LightMode"="Always" }
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex nativeVertex
            #pragma fragment shadeNative
            ENDCG
        }
        Pass
        {
            Name "Worldfall"
            Tags { "LightMode"="CultiwayWorldfall" }
            ZWrite Off
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex worldfallVertex
            #pragma fragment shadeWorldfall
            ENDCG
        }
        Pass
        {
            Name "WorldfallSolid"
            Tags { "LightMode"="CultiwayWorldfallSolid" }
            ZWrite On
            Blend Off
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex worldfallVertex
            #pragma fragment worldfallSolid
            ENDCG
        }
    }
    Fallback Off
}
