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
        [HideInInspector] _FormContact ("Local Contact Region", Vector) = (0,0,0,0)
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
        #include "FormTransparency.cginc"
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
        float4 _FormBuild, _FormContact;
        float _FormClosedVolume, _FormHideBelowGround;
        float4x4 _FormBones[32];
        float4 _FP_Eye, _FP_LightDir, _FP_LightCol, _FP_AmbCol, _FP_View, _FP_Cam, _FP_CamF;
        #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
        StructuredBuffer<float4> _FormInstances, _FormPoses;
        uint _FormBatchStart;
        float4 formData(uint instance,uint row) { return _FormInstances[(instance+_FormBatchStart)*12+row]; }
        float4x4 formMatrix(uint instance,uint row)
        { return float4x4(formData(instance,row),formData(instance,row+1),formData(instance,row+2),formData(instance,row+3)); }
        #endif
        // 参数由本入口按显式矩阵行读取，不借用Unity的每实例矩阵常量数组。
        void setupForm() {}
        struct Input { float4 vertex:POSITION; float3 normal:NORMAL; float2 uv:TEXCOORD0; float4 controls:TEXCOORD1; float4 weights:TEXCOORD2; float2 surface:TEXCOORD3; float4 build:TEXCOORD4; float4 current:TEXCOORD5; UNITY_VERTEX_INPUT_INSTANCE_ID };
        struct Varying
        {
            float4 vertex:SV_POSITION; float3 world:TEXCOORD0; float3 normal:TEXCOORD1; float2 uv:TEXCOORD2;
            float3 eye:TEXCOORD3; float3 light:TEXCOORD4; float2 surface:TEXCOORD5; float buildAt:TEXCOORD6; float3 material:TEXCOORD7;
            float4 current:TEXCOORD9;
            #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
            nointerpolation uint instance:TEXCOORD8;
            #endif
        };
        struct FormState { float4x4 world,normal; float4 build,anchor; int pose; };
        FormState formState(bool native)
        {
            FormState state;
            #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
            uint instance=unity_InstanceID;
            state.world=formMatrix(instance,0); state.normal=formMatrix(instance,4);
            state.build=formData(instance,10); state.anchor=formData(instance,11); state.pose=(int)state.anchor.w;
            #else
            state.world=native?_FormMatrix:unity_ObjectToWorld; state.normal=native?_FormNormalMatrix:transpose(unity_WorldToObject);
            state.build=_FormBuild; state.anchor=_TerrainAnchor; state.pose=_UseSkinning>.5?0:-1;
            #endif
            return state;
        }
        float4x4 formBone(int pose,int bone)
        {
            #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
            uint row=pose+bone*4;
            return float4x4(_FormPoses[row],_FormPoses[row+1],_FormPoses[row+2],_FormPoses[row+3]);
            #else
            return _FormBones[bone];
            #endif
        }
        float4 formBuild(Varying i)
        {
            #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
            return formData(i.instance,10);
            #else
            return _FormBuild;
            #endif
        }
        float4 formTint(Varying i)
        {
            #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
            return formData(i.instance,8);
            #else
            return _Color;
            #endif
        }
        float4 formDye(Varying i)
        {
            #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
            return formData(i.instance,9);
            #else
            return _Dye;
            #endif
        }
        float formClosed(Varying i)
        {
            #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
            return fmod(formData(i.instance,11).z,2);
            #else
            return _FormClosedVolume;
            #endif
        }
        Varying baseVertex(Input v,FormState state)
        {
            Varying o;
            o.world = mul(state.world, v.vertex).xyz;
            float3 normal = mul((float3x3)state.normal,v.normal);
            o.normal = normalize(normal); o.uv = v.uv;
            o.surface = v.surface; o.surface.x /= max(.00001,length(normal));
            o.vertex = 0; o.eye = 0; o.light = 0;
            o.buildAt = v.build.w; o.current = v.current;
            o.material = v.vertex.xyz;
            #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
            o.instance=unity_InstanceID;
            #endif
            return o;
        }
        Input posedVertex(Input v,FormState state)
        {
            if (state.build.z > .5)
            {
                float factor = saturate((state.build.x-v.build.w)/state.build.y);
                v.vertex.xyz = lerp(v.build.xyz,v.vertex.xyz,factor);
                v.surface.x *= factor;
            }
            if (state.pose >= 0)
            {
                float4 position = 0; float3 normal = 0;
                [unroll] for (int k = 0; k < 4; k++)
                {
                    float4x4 bone = formBone(state.pose,(int)v.controls[k]);
                    position += mul(bone,v.vertex) * v.weights[k];
                    normal += mul((float3x3)bone,v.normal) * v.weights[k];
                }
                v.vertex = position; v.surface.x *= length(normal); v.normal = normalize(normal);
            }
            if (_Kind > 19.5 && _Kind < 20.5)
            {
                // 只收拢中上段外焰。基部、作用芯和共同着地仍读取原火身。
                float height = smoothstep(.23,.85,v.uv.y);
                float angle = v.uv.x*6.2831853;
                float roll = .5+.5*sin(angle*3-v.uv.y*8+_WorldTime*5.7);
                v.vertex.xy *= 1-.17*height*roll;
            }
            return v;
        }
        Varying nativeVertex(Input v)
        {
            UNITY_SETUP_INSTANCE_ID(v);
            FormState state=formState(true);
            float3 material = v.vertex.xyz;
            v = posedVertex(v,state);
            Varying o = baseVertex(v,state);
            o.material = material;
            if (_Kind > 14.5 && _Kind < 15.5) o.material.z = v.vertex.z;
            float elevation = o.world.z;
            if (_TerrainEnabled > .5)
            {
                float ground = state.anchor.y > .5 ? state.anchor.x :
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
            UNITY_SETUP_INSTANCE_ID(v);
            FormState state=formState(false);
            float3 material = v.vertex.xyz;
            v = posedVertex(v,state);
            Varying o = baseVertex(v,state);
            o.material = material;
            if (_Kind > 14.5 && _Kind < 15.5) o.material.z = v.vertex.z;
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
        // 一个像素容不下完整细纹时，让它趋近所在材料的平均色，避免远景网点和跳动。
        float resolvedDetail(float2 cellDx, float2 cellDy)
        {
            return 1-smoothstep(.35,1.25,max(length(cellDx),length(cellDy)));
        }
        float resolvedVolumeDetail(float3 p)
        {
            return 1-smoothstep(.35,1.25,max(length(ddx(p)),length(ddy(p))));
        }
        // 绑定空间连续取样；同一接缝两侧、平移、转向与骨架变形均保留原材料位置。
        float volumeNoise(float3 p)
        {
            float detail = resolvedVolumeDetail(p);
            float z=floor(p.z),blend=frac(p.z); blend=blend*blend*(3-2*blend);
            float noise = lerp(waterNoise(p.xy+z*float2(13.1,7.7)),waterNoise(p.xy+(z+1)*float2(13.1,7.7)),blend);
            return lerp(.5,noise,detail);
        }
        float circularWaterNoise(float2 flow, float period, float seam)
        {
            float detail = resolvedDetail(ddx(flow),ddy(flow));
            return lerp(.5,lerp(waterNoise(flow),waterNoise(flow-float2(period,0)),seam),detail);
        }
        // 强度由共同世界时钟发布；绘制只在实际表面附近改变反光，不生成独立图形。
        float formFeedback(Varying i)
        {
            float strength = formBuild(i).w;
            if (strength <= 0) return 0;
            float4 region;
            #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
            uint at = (uint)strength-1;
            region = _FormPoses[at]; strength = _FormPoses[at+1].x;
            #else
            region = _FormContact;
            #endif
            float distance = length(i.world-region.xyz)/max(region.w,.00001);
            return (1-smoothstep(.15,1,distance))*strength;
        }
        float4 shadeSurface(Varying i, bool worldfall)
        {
            float4 build=formBuild(i),tint=formTint(i),dye=formDye(i);
            if (build.z > .5) clip(build.x-i.buildAt-.000001);
            // 原版没有真实地形深度；显式地面模型按同一高度图隐藏尚在地下的表面。
            #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
            float4 anchor=formData(i.instance,11); float hideBelow=step(1.5,anchor.z);
            #else
            float4 anchor=_TerrainAnchor; float hideBelow=_FormHideBelowGround;
            #endif
            if (!worldfall && hideBelow>.5)
            {
                float ground=0;
                if (_TerrainEnabled>.5) ground=anchor.y>.5?anchor.x:
                    any(i.world.xy<0)||any(i.world.xy>=_TerrainSize.xy)?_TerrainSize.z:
                    tex2D(_TerrainHeights,i.world.xy/_TerrainSize.xy).r;
                clip(i.world.z-ground+.00001);
            }
            float3 n = normalize(i.normal), eye = normalize(i.eye), light = normalize(i.light);
            float facing = saturate(dot(n,eye)), diffuse = saturate(dot(n,light));
            float rim = pow(1-facing,3);
            float2 uv = i.uv;
            float detail = tex2D(_MainTex,uv*float2(8,3)).r;
            float3 base = tint.rgb * dye.rgb;
            float alpha = tint.a;
            // 原版保持原有投影受光；三维按外部场景的实际环境色和主光色着色。
            float3 ambient = worldfall ? max(0,_FP_AmbCol.rgb)/.34 : 1;
            float3 direct = worldfall ? max(0,_FP_LightCol.rgb)/.55 : 1;
            float3 environmentLight = worldfall ? max(0,_FP_AmbCol.rgb)+max(0,_FP_LightCol.rgb)*.5 : 1;
            float specular = pow(saturate(dot(n,normalize(light+eye))),lerp(100,8,saturate(_Roughness)));
            if(_Kind < .5)
                base = base*((.34+detail*.16)*ambient+diffuse*.55*direct) + specular*.65*direct + float3(.16,.21,.24)*rim*environmentLight;
            else if(_Kind < 1.5)
            {
                float3 q = i.material;
                float grain = volumeNoise(q*float3(5,9,15));
                float strata = volumeNoise(q*float3(1.4,1.4,5));
                base *= (.64+.22*grain+.13*strata) * (.32*ambient+diffuse*.65*direct);
            }
            else if(_Kind < 2.5)
                base *= (.34*ambient+diffuse*.6*direct) * (.65+detail*.5);
            else if(_Kind < 3.5 && formClosed(i) > .5)
            {
                // 封闭厚水体直接按曲面法向和真实截面受光，水纹只在材料内流动。
                // 贴地水层仍从自身输运资料读取干边和前沿泡沫。
                float3 q = i.material;
                float current = volumeNoise(q*float3(2.4,1.1,.8) + float3(.008,-.64,.02)*_WorldTime);
                float broad = volumeNoise(q*float3(.74,.55,1.8) - float3(.01,.21,0)*_WorldTime);
                float thickness = max(.025,i.surface.x)/max(.28,facing);
                float depth = 1-exp(-thickness*(.92+.23*broad));
                float3 shallow = base*1.18 + float3(.024,.10,.12);
                float3 deep = base*float3(.38,.73,.87);
                float3 body = lerp(shallow,deep,depth)*(.68*ambient+.32*diffuse*direct);
                float fresnel = .055+.945*pow(1-facing,4);
                float sky = smoothstep(-.2,.8,reflect(-eye,n).z);
                float3 reflection = lerp(float3(.09,.26,.34),float3(.54,.76,.83),sky);
                float stream = smoothstep(.38,.75,current);
                base = lerp(body,reflection*environmentLight,saturate(.56*fresnel+.10*stream));
                base += specular*(.18+.33*stream)*direct;
                float foam = saturate(i.surface.y*(.52+.60*current));
                base = lerp(base,float3(.75,.93,.95)*(.72*ambient+.23*diffuse*direct),foam);
                alpha = lerp(saturate(alpha*(.52+.44*depth)+fresnel*.18),.93,foam);
            }
            else if(_Kind < 3.5)
            {
                float h;
                float wet = 1;
                float front = 0;
                float movingLight = 0;
                if (i.current.z > 0)
                {
                    float speed = length(i.current.xy);
                    float2 direction = i.current.xy / max(.25,speed);
                    float2 p = i.material.xy * .85;
                    // 宽水纹随流移动；较细的波面只影响反光，不改变水层轮廓。
                    float2 drift = direction * min(speed,9)*.65;
                    float phase = frac(_WorldTime*1.3);
                    float blend = 1-abs(2*phase-1);
                    float2 a = p - drift*phase, b = p - drift*frac(phase+.5);
                    float broad = lerp(waterNoise(b),waterNoise(a),blend);
                    float foamCuts = lerp(waterNoise(b*2.7+1.3),waterNoise(a*2.7+1.3),blend);
                    float detail = resolvedDetail(ddx(p*2.7),ddy(p*2.7));
                    float pattern = lerp(.5,foamCuts,detail);
                    h = .74*broad + .35*pattern;
                    wet = smoothstep(i.current.z,8*i.current.z,i.surface.x);
                    movingLight = smoothstep(.28,.83,broad) * smoothstep(.25,3.5,speed) * wet;
                    front = i.current.w*smoothstep(.33,.68,pattern)*.86;
                }
                else
                {
                    // 原形体继续使用随表面变形的环向纹理，闭合接缝两端取相同值与导数。
                    float2 flow = uv * float2(8,4) + float2(.025,-.075) * _WorldTime;
                    float seam = smoothstep(0,1,uv.x);
                    h = circularWaterNoise(flow,8,seam);
                    h += .35*circularWaterNoise(flow*1.73+float2(.31,.27),8*1.73,seam);
                }
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
                float layers=volumeNoise(i.material*.8+float3(.015,-.04,.008)*_WorldTime);
                float depth = 1-exp(-thickness*(.63+.14*layers));
                float3 shallow = base*1.25 + float3(.025,.12,.12);
                float3 deep = base*float3(.25,.65,.8);
                base = lerp(shallow,deep,depth)*(.58*ambient+.38*diffuse*direct);
                base = lerp(base,environment*environmentLight,fresnel*.45) + (broad*.04 + shine*.25)*direct;
                base = lerp(base,environment*environmentLight,movingLight*.37);
                alpha = saturate(alpha*(.28+.67*depth)+fresnel*.35+movingLight*.18);
                // 泡沫随材料位置连起来，展开坐标翻边时不会换一块纹理。
                float foam = saturate(i.surface.y*(.45+.7*volumeNoise(i.material*float3(9,9,6))));
                foam = max(foam,front);
                base = lerp(base,float3(.72,.91,.92)*(.75*ambient+.2*diffuse*direct),foam);
                alpha = lerp(alpha,.94,foam)*wet;
            }
            else if(_Kind < 4.5)
            {
                // 霜白集中在实在的薄缘和泡沫；厚冰的雾芯和内层固定在材料坐标。
                // 损伤裂面由真实分件露出，冰材本身不随时间画出裂纹。
                float3 q = i.material;
                float detail = resolvedDetail(ddx(q*10),ddy(q*10));
                float grain = lerp(.5,volumeNoise(q*10),detail);
                float cloud = volumeNoise(q*1.9);
                float frost = saturate((.22 + .75*i.surface.y)*smoothstep(.29,.76,.5*cloud+.5*grain));
                float thickness = max(.025,i.surface.x)/max(.22,facing);
                float broad = volumeNoise(q*float3(1.7,2.1,3.2) + float3(.34,-.15,.28));
                float depth = 1-exp(-thickness*(.78 + .48*broad));
                float frostCore = depth*smoothstep(.46,.8,.6*broad+.4*cloud);
                float fresnel = .045+.955*pow(1-facing,5);
                float sky = smoothstep(-.2,.8,reflect(-eye,n).z);
                float3 environment = lerp(float3(.13,.28,.37),float3(.72,.88,.96),sky);
                float3 body = lerp(base*1.18,base*float3(.31,.66,.85),depth);
                body = lerp(body,float3(.63,.82,.89),frostCore*.42);
                body *= .70*ambient+diffuse*.3*direct;
                body = lerp(body,float3(.85,.94,.98)*(.72*ambient+.26*diffuse*direct),frost);
                base = lerp(body,environment*environmentLight,fresnel*.68)+specular*lerp(.50,.12,frost)*direct;
                alpha = saturate(alpha*(.28+.78*depth)+fresnel*.22);
                alpha = lerp(alpha,.88,frost);
            }
            else if(_Kind < 5.5)
                base *= .84*ambient+.16*diffuse*direct;
            else if(_Kind < 6.5)
            {
                float flow = volumeNoise(i.material*float3(4,4,3)+float3(.04,-.12,.03)*_WorldTime);
                float crust = smoothstep(.3,.75,flow);
                base = lerp(base,base*float3(.38,.22,.14),crust*.72);
                base *= .85+.15*diffuse;
                base += float3(.45,.14,.02)*rim + specular*.24;
            }
            else if(_Kind < 7.5)
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
            else if(_Kind < 8.5)
            {
                // 裂口内部保留深暗体量；流动附着在形体表面，不模拟尚未实现的空间扭曲。
                float folds = volumeNoise(i.material*2.2+float3(.018,-.025,.06)*_WorldTime);
                float deep = smoothstep(.25,.8,folds);
                base = base*(.24+.65*deep)+float3(.025,.008,.045)*rim;
            }
            else if(_Kind < 9.5)
            {
                // 厚裂唇的宽面受光与有限内辉，避免依赖单根发光轮廓线。
                float strata = volumeNoise(i.material*4+float3(0,0,-.1)*_WorldTime);
                base *= (.34+.3*strata)*ambient+.5*diffuse*direct;
                base += tint.rgb*(.10+.14*strata)+specular*.16*direct;
            }
            else if(_Kind < 10.5)
            {
                // 毒液保留黏稠宽面和深色积液，不沿用海水的蓝色反射与白沫。
                float flow=volumeNoise(i.material*2.4+float3(.025,-.04,.015)*_WorldTime);
                float thickness=max(.035,i.surface.x);
                float depth=1-exp(-thickness*4);
                base=lerp(base*1.35,base*.42,depth)*(.62*ambient+.33*diffuse*direct);
                base+=tint.rgb*(.06+.10*flow)+specular*.15*direct;
                alpha=saturate(alpha*(.55+.4*depth));
            }
            else if(_Kind < 11.5)
            {
                // 平行于地面的疏密层；宽面上能看出前涌、背后拉长和逐渐混入空气。
                float3 q = i.material;
                float density = volumeNoise(q * float3(2.5,1.25,4.2) + float3(.12,-.85,.16)*_WorldTime);
                float grain = volumeNoise(q * float3(4.8,2.4,7.0) - float3(.16,.47,.13)*_WorldTime);
                float folds = smoothstep(.18,.76,.72*density+.28*grain);
                base *= (.60+.56*folds)*max(float3(.68,.72,.61),.47*ambient+.50*direct);
                base += tint.rgb*(.06+.29*folds)*(.47+.53*diffuse);
                float chord = smoothstep(.01,.78,abs(dot(n,eye)));
                float film = i.surface.x > .01 ? i.surface.y : 1;
                alpha *= (.05+.95*chord)*(.53+.47*folds)*film;
            }
            else if (_Kind < 12.5)
            {
                // 不规则混沌体积的暗腔与宽层次；不是尚未实现的空间折射。
                float fold=volumeNoise(i.material*2.1+float3(.02,-.015,.04)*_WorldTime);
                float deep=volumeNoise(i.material*.8+float3(-.01,.025,.012)*_WorldTime);
                base*=.30+.75*fold;
                base+=tint.rgb*deep*.2;
                alpha*=pow(abs(dot(n,eye)),.45)*(.65+.35*fold);
            }
            else if (_Kind < 13.5)
            {
                // 风保留宽流面的体量和流向，明暗随材料坐标平移，不借用冰霜、海浪或烟雾。
                float band=.5+.5*sin(i.material.x*2.1+i.material.z*3.4-_WorldTime*5.8);
                float broad=smoothstep(.1,.9,band);
                base*=.50*ambient+(.22+.22*broad)*direct;
                base+=tint.rgb*pow(1-facing,2)*.18*environmentLight;
                alpha*=.68+.25*broad;
            }
            else if (_Kind < 14.5)
            {
                // 护盾通过宽面明暗和侧缘光泽呈现能量厚度，不借用水波或画满细线。
                float energy=.5+.5*sin(i.material.z*2.4+sin(i.material.x*1.7)*.6-_WorldTime*2.5);
                float edge=pow(1-facing,2);
                base*=.56*ambient+(.25+.15*energy)*direct;
                base+=tint.rgb*(edge*.26+energy*.10)*environmentLight;
                alpha*=.55+.30*edge+.15*energy;
            }
            else if (_Kind < 15.5)
            {
                // 自发光截面按视线穿过的厚度淡出，侧缘无实体柱的高光与硬线。
                float chord = pow(abs(dot(n, eye)), 1.35);
                float stream = volumeNoise(float3(i.material.x*13, i.material.y*13,
                    i.material.z*2.1-_WorldTime*4.8));
                base *= .95 + (.37-.18*_Roughness)*stream;
                alpha *= chord*(.8+.2*stream);
            }
            else if (_Kind < 16.5 || (_Kind > 20.5 && _Kind < 21.5))
            {
                // 岩层色随制作坐标固定；厚层缓慢变色，矿粒只在近处补出不均匀的粗面。
                float3 q = i.material;
                // 地脉资料以 Y 为高，其余岩层以 Z 为高。
                if (_Kind > 20.5) q = q.xzy;
                float warp = .25 * volumeNoise(q * float3(1.8, 3.0, .75));
                float layer = smoothstep(.12, .86, .5 + .5 * sin((q.z + warp) * 11.5 + q.x * .8));
                float grains = volumeNoise(q * float3(8, 18, 10));
                float3 mineral = lerp(float3(.66, .62, .57), float3(1.22, 1.18, 1.04), layer);
                base *= mineral * (.76 + .43 * grains) * (max(float3(.57, .57, .57), .63 * ambient) + diffuse * .46 * direct);
            }
            else if (_Kind < 17.5)
            {
                // 疏密沿实际卷面向上流动，端部降低透光量，风底保留较暗的作用轮廓。
                float3 q = i.material;
                float flow = volumeNoise(q * float3(1.3,1.3,1.8) - float3(.06,.03,1.7)*_WorldTime);
                float broad = smoothstep(.12,.84,flow);
                float chord = sqrt(abs(dot(n,eye)));
                base *= (.74 + .28*broad) * max(float3(.66,.66,.66),.47*ambient+.46*direct);
                base += tint.rgb * .16 * broad;
                alpha *= i.surface.y * (.54+.46*broad) * (.24+.76*chord);
            }
            else if (_Kind < 18.5)
            {
                // 宽的内折层在凹面向内移动；厚处暗透，转折的边缘才有窄光泽。
                float3 q = i.material;
                float radius = length(q.xy);
                float curl = q.z*1.2 + radius*2.5 + .42*sin(q.x*2.4-q.y*2.2);
                float folds = .5+.5*sin(curl+_WorldTime*3.9);
                float grain = volumeNoise(q*float3(2.5,2.5,3.4)+float3(.02,-.06,.10)*_WorldTime);
                float shoulder = smoothstep(.12,.72,q.z);
                float lightEdge = pow(1-facing,2)*(.28+.72*shoulder);
                base *= (.38+.35*grain+.47*folds+.34*shoulder)*max(float3(.62,.62,.62),.55*ambient+.35*direct);
                base += float3(.125,.10,.25)*lightEdge*(.70+.30*grain)*environmentLight;
                alpha *= (.49+.15*grain+.23*folds+.10*shoulder)*(1-.28*smoothstep(1.3,2.46,radius)*(1-shoulder));
            }
            else if (_Kind < 19.5)
            {
                // 边缘沿实际厚面翻起；中间地面和着火的人物保持可见。
                float3 q = i.material;
                float hot = volumeNoise(q * float3(4,4,2) + float3(.1,.07,1.9)*_WorldTime);
                float high = smoothstep(.04,.35,-q.z);
                base *= .66+.55*hot;
                base += float3(.34,.22,.04)* (.23+.77*hot) * (1-.65*high);
                alpha *= i.surface.y*(.54+.46*hot)*(.58+.42*abs(dot(n,eye)));
            }
            else if (_Kind < 20.5)
            {
                // 亮芯藏在有厚度的火身内；外焰随上行热流变化，薄处透出背景。
                float3 q = i.material;
                float bands = volumeNoise(q*float3(2.6,2.6,2.9)-float3(.08,.03,3.4)*_WorldTime);
                float folds = volumeNoise(q*float3(4.8,4.8,5.2)-float3(.04,.08,5.2)*_WorldTime);
                float height = saturate(uv.y);
                float chord = sqrt(abs(dot(n,eye)));
                float body = smoothstep(.035,.23,height)*(1-smoothstep(.63,1,height));
                float hearth = body*chord*smoothstep(.20,.88,.72*bands+.28*folds);
                float inner = smoothstep(.13,.65,hearth);
                float outer = .52+.44*bands+.18*folds;
                base = lerp(base*float3(.83,.34,.21)*outer,
                    (tint.rgb*float3(1.15,1.35,1.5)+float3(.21,.32,.13))*dye.rgb,inner);
                alpha *= (.40+.60*chord)*(.47+.42*body+.17*inner)*
                         (1-smoothstep(.71,1.02,height)*(.51-.28*bands));
            }
            else if (_Kind < 22.5)
            {
                // 煞风的厚薄跟着剪切面走；暗面有疏密，折面受光，不描亮边。
                float fold = volumeNoise(i.material * float3(2.7,2.7,5.9) - float3(.16,-.09,1.6)*_WorldTime);
                float chord = sqrt(abs(dot(n,eye)));
                float density = smoothstep(.10,.84,fold);
                base *= (.54+.62*density)*max(float3(.78,.76,.82),.49*ambient+.54*direct);
                base += tint.rgb*(.14+.29*diffuse)*density;
                alpha *= i.surface.y*(.48+.52*density)*(.29+.71*chord);
            }
            else if (_Kind > 26.5)
            {
                // 对端实际地块的颜色和立体高差。退进门框后的石壁保留真实受光。
                base = i.current.rgb * i.current.a * (.66 + .34 * diffuse);
            }
            else if (_Kind > 25.5)
            {
                // 暖质有宽的流层、受光面和深色薄边；流动藏在材质内，不额外画线。
                float3 q = i.material;
                float inner = volumeNoise(q * float3(2.1,2.1,2.9) - float3(.09,.04,2.7) * _WorldTime);
                float cells = volumeNoise(q * float3(7.4,7.4,5.5) + float3(-.11,.12,-1.4) * _WorldTime);
                float molten = smoothstep(.29,.76,.70*inner+.30*cells);
                float face = sqrt(abs(dot(n,eye)));
                float hot = molten*face*(.64+.36*diffuse);
                base *= (.34+.72*molten) * max(float3(.78,.72,.64),.56*ambient+.58*direct);
                base += (tint.rgb*.50+float3(.48,.33,.13))*hot + (.23+.23*molten)*specular*direct;
                alpha *= (.47+.53*face)*(.69+.31*molten);
            }
            else if (_Kind > 24.5)
            {
                // 薄阴质压在地面，厚肩分出内外两层；散开的低边不用硬轮廓描圆。
                float3 q = i.material;
                float r = length(q.xy);
                float warped = r*3.4 + .68*sin(q.x*2.1+q.y*1.7) + .7*q.z;
                float flow = .5+.5*sin(warped+_WorldTime*4.8);
                float grain = volumeNoise(q*float3(4.4,4.4,4.1)+float3(.10,-.07,.01)*_WorldTime);
                float thick = smoothstep(.11,.66,q.z);
                float fringe = 1-smoothstep(1.84,2.62,r+(grain-.5)*.21);
                float fold = smoothstep(.22,.88,flow)*(.36+.64*grain);
                base = base*(.28+.74*thick+.59*fold)*(.64*ambient+.34*direct);
                base += float3(.18,.13,.27)*thick*(.27+.73*fold)*(.44+.56*diffuse)*environmentLight;
                alpha *= (.40+.34*fold+.34*thick)*(.16+.84*fringe)*smoothstep(.02,.10,q.z);
            }
            else
            {
                // 毒潭的暗处是积液，颜色只在较薄的膜和蚀坑边缘略露出来。
                float3 q = i.material;
                float film = volumeNoise(q*float3(3.4,3.4,8.0) + float3(-.09,.06,0)*_WorldTime);
                float grain = volumeNoise(q*float3(12,12,16));
                float rim = smoothstep(.67,.98,i.surface.y + (grain-.5)*.20);
                float thick = saturate(i.surface.x*2.5);
                if (_Kind < 23.5)
                {
                    float pits = smoothstep(.30,.55,.68*grain+.32*film);
                    base *= (.17+.31*pits+.22*rim)*max(float3(.7,.72,.53),ambient+.48*direct);
                    base += tint.rgb*(.23*rim+.13*film)*(.52+.48*diffuse) + specular*(.14+.37*pits)*direct;
                    alpha *= .63+.30*thick+.05*rim;
                }
                else
                {
                    base = (base*.13 + tint.rgb*rim*(.47+.24*grain)) * (.87+.23*diffuse);
                    alpha *= .63+.32*rim;
                }
            }
            float contact = formFeedback(i);
            base += (base*.24 + float3(.035,.045,.05)*environmentLight)*contact;
            if (_Kind < .5) base += pow(saturate(dot(n,normalize(light+eye))),16)*contact*.12*direct;
            alpha *= dye.a; // 原版和三维均读取同一姿态的显示淡去。
            return float4(base*alpha,alpha);
        }
        float4 shadeNative(Varying i):SV_Target { return shadeSurface(i,false); }
        float4 shadeWorldfall(Varying i):SV_Target { return shadeSurface(i,true); }
        FormTransparentOutput transparentSurface(Varying i,bool worldfall,float side)
        {
            i.normal *= side;
            float4 color=shadeSurface(i,worldfall);
            if ((_Kind>14.5 && _Kind<15.5) || _Kind>16.5 || (formClosed(i)>.5 && _Kind>=2.5 && _Kind<4.5))
            {
                // 封闭光束、水冰和有厚度的领域使用前后两面，避免同一光程重复加深。
                float alpha=1-sqrt(1-color.a);
                color.rgb *= alpha/max(color.a,.00001); color.a=alpha;
            }
            return formTransparent(color,i.vertex.z);
        }
        FormTransparentOutput nativeTransparent(Varying i,float side:VFACE) { return transparentSurface(i,false,side); }
        FormTransparentOutput worldfallTransparent(Varying i,float side:VFACE) { return transparentSurface(i,true,side); }
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
            #pragma target 4.5 PROCEDURAL_INSTANCING_ON
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:setupForm
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
            #pragma target 4.5 PROCEDURAL_INSTANCING_ON
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:setupForm
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
            #pragma target 4.5 PROCEDURAL_INSTANCING_ON
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:setupForm
            #pragma vertex worldfallVertex
            #pragma fragment worldfallSolid
            ENDCG
        }
        Pass
        {
            Name "NativeTransparent"
            Tags { "LightMode"="CultiwayNativeTransparent" }
            Cull Off ZWrite Off
            Blend 0 One One
            Blend 1 Zero OneMinusSrcAlpha
            CGPROGRAM
            #pragma target 4.0
            #pragma target 4.5 PROCEDURAL_INSTANCING_ON
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:setupForm
            #pragma vertex nativeVertex
            #pragma fragment nativeTransparent
            ENDCG
        }
        Pass
        {
            Name "WorldfallTransparent"
            Tags { "LightMode"="CultiwayWorldfallTransparent" }
            Cull Off ZWrite Off
            Blend 0 One One
            Blend 1 Zero OneMinusSrcAlpha
            CGPROGRAM
            #pragma target 4.0
            #pragma target 4.5 PROCEDURAL_INSTANCING_ON
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:setupForm
            #pragma vertex worldfallVertex
            #pragma fragment worldfallTransparent
            ENDCG
        }
    }
    Fallback Off
}
