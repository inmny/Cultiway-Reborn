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
        struct Input { float4 vertex:POSITION; float3 normal:NORMAL; float2 uv:TEXCOORD0; float4 controls:TEXCOORD1; float4 weights:TEXCOORD2; float2 surface:TEXCOORD3; float4 build:TEXCOORD4; UNITY_VERTEX_INPUT_INSTANCE_ID };
        struct Varying
        {
            float4 vertex:SV_POSITION; float3 world:TEXCOORD0; float3 normal:TEXCOORD1; float2 uv:TEXCOORD2;
            float3 eye:TEXCOORD3; float3 light:TEXCOORD4; float2 surface:TEXCOORD5; float buildAt:TEXCOORD6; float3 material:TEXCOORD7;
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
            o.buildAt = v.build.w;
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
        // 绑定空间连续取样；同一接缝两侧、平移、转向与骨架变形均保留原材料位置。
        float volumeNoise(float3 p)
        {
            float z=floor(p.z),blend=frac(p.z); blend=blend*blend*(3-2*blend);
            return lerp(waterNoise(p.xy+z*float2(13.1,7.7)),waterNoise(p.xy+(z+1)*float2(13.1,7.7)),blend);
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
                float layers=volumeNoise(i.material*.8+float3(.015,-.04,.008)*_WorldTime);
                float depth = 1-exp(-thickness*(.63+.14*layers));
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
                float crystal = .65*volumeNoise(i.material*8)+.35*volumeNoise(i.material*2.2);
                float frost = smoothstep(.52,.82,crystal)*.5;
                float thickness = max(.025,i.surface.x)/max(.2,facing);
                float layers=volumeNoise(i.material*.9);
                float depth = 1-exp(-thickness*(.58+.24*layers));
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
                // 透明边缘和内部密度只改变雾的明暗，不画球壳轮廓。
                float density=volumeNoise(i.material*1.6+float3(.025,-.03,.055)*_WorldTime);
                float detail=volumeNoise(i.material*4.5+float3(-.018,.02,.025)*_WorldTime);
                base*=((.42+.32*density)*ambient+.24*diffuse*direct);
                base+=tint.rgb*detail*.09;
                // 体积前后两侧的光程相同；不能让原版投影的双面方向把整个体积裁成透明。
                alpha*=pow(abs(dot(n,eye)),.7)*(.42+.58*density);
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
            else
            {
                // 岩层色随制作坐标固定；厚层缓慢变色，矿粒只在近处补出不均匀的粗面。
                float3 q = i.material;
                float warp = .25 * volumeNoise(q * float3(1.8, 3.0, .75));
                float layer = smoothstep(.12, .86, .5 + .5 * sin((q.z + warp) * 11.5 + q.x * .8));
                float grains = volumeNoise(q * float3(8, 18, 10));
                float3 mineral = lerp(float3(.66, .62, .57), float3(1.22, 1.18, 1.04), layer);
                base *= mineral * (.76 + .43 * grains) * (max(float3(.57, .57, .57), .63 * ambient) + diffuse * .46 * direct);
            }
            float contact = formFeedback(i);
            base += (base*.24 + float3(.035,.045,.05)*environmentLight)*contact;
            if (_Kind < .5) base += pow(saturate(dot(n,normalize(light+eye))),16)*contact*.12*direct;
            return float4(base*alpha,alpha);
        }
        float4 shadeNative(Varying i):SV_Target { return shadeSurface(i,false); }
        float4 shadeWorldfall(Varying i):SV_Target { return shadeSurface(i,true); }
        FormTransparentOutput transparentSurface(Varying i,bool worldfall,float side)
        {
            i.normal *= side;
            float4 color=shadeSurface(i,worldfall);
            if ((_Kind>14.5 && _Kind<15.5) || (formClosed(i)>.5 && _Kind>=2.5 && _Kind<4.5))
            {
                // 封闭光束及有体积的水冰使用前后两面，不把同一光程重复算成两份。
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
