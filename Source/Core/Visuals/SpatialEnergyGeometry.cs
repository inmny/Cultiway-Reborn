using UnityEngine;

namespace Cultiway.Core.Visuals;

/// <summary>精细能量曲面与流派主体；只在首次使用时建立，两个视角共享原始几何。</summary>
internal static class SpatialEnergyGeometry
{
    /// <summary>平滑火核及灵力核心。</summary>
    internal static readonly SpatialVisualMesh Orb = Ellipsoid("energy_orb", 10, 20, false);
    /// <summary>保持体积与软边的云雾主体，区别于描边和流线。</summary>
    internal static readonly SpatialVisualMesh Cloud = SpatialModelLoader.Load("cloud",SpatialSurface.Mist);
    /// <summary>带起伏轮廓和尖尾的火舌。</summary>
    internal static readonly SpatialVisualMesh Flame = SpatialModelLoader.Load("flame",SpatialSurface.Flame);
    /// <summary>逐渐变细的弯曲能量带。</summary>
    internal static readonly SpatialVisualMesh Stream = BuildStream();
    /// <summary>带薄刃、剑脊和剑格的长剑。</summary>
    internal static readonly SpatialVisualMesh Sword = BuildSword();
    /// <summary>多层断面与不对称岩峰。</summary>
    internal static readonly SpatialVisualMesh Peak = SpatialModelLoader.Load("peak",SpatialSurface.Stone);
    /// <summary>有六面晶脊的冰晶。</summary>
    internal static readonly SpatialVisualMesh Crystal = BuildCrystal();
    /// <summary>卷起并收尖的宽面浪脊。</summary>
    internal static readonly SpatialVisualMesh Wave = SpatialModelLoader.Load("wave",SpatialSurface.Water);
    /// <summary>持续风柱的螺旋流。</summary>
    internal static readonly SpatialVisualMesh Helix = BuildHelix();
    /// <summary>平面一百八十度的渐细弧带。</summary>
    internal static readonly SpatialVisualMesh Crescent = BuildCrescent();
    /// <summary>分叉主干与细电丝。</summary>
    internal static readonly SpatialVisualMesh[] Bolts = BuildBolts();
    /// <summary>空间盘绕的龙形，保留头、角、须与脊鳍。</summary>
    internal static readonly SpatialVisualMesh Dragon = SpatialModelLoader.Load("dragon",SpatialSurface.Energy);
    /// <summary>有头肩、躯干、双臂和掌指的半透明法相。</summary>
    internal static readonly SpatialVisualMesh Avatar = SpatialModelLoader.Load("avatar",SpatialSurface.Energy);
    /// <summary>法相可摆动的圆截面手臂。</summary>
    internal static readonly SpatialVisualMesh Limb = BuildLimb();
    /// <summary>法相可随真实掌击转动的掌与五指。</summary>
    internal static readonly SpatialVisualMesh Palm = BuildPalm();
    /// <summary>吞摄法域的宽带漏斗，保留逐渐收束的负空间。</summary>
    internal static readonly SpatialVisualMesh Vortex = BuildVortex();

    /// <summary>沿经纬建立圆润核心；火舌沿高度收尖并加入固定轮廓变化。</summary>
    /// <param name="name">模型名。</param><param name="rings">纬线数。</param><param name="sides">经线数。</param><param name="flame">是否为火舌。</param>
    /// <param name="surface">非火焰主体的表面质感。</param>
    private static SpatialVisualMesh Ellipsoid(string name, int rings, int sides, bool flame,SpatialSurface surface=SpatialSurface.Energy)
    {
        SpatialVisualMeshBuilder b = new();
        for (int j = 0; j < rings; j++)
        for (int i = 0; i < sides; i++)
        {
            float t = j / (float)rings, next = (j + 1f) / rings;
            float a = i * Mathf.PI * 2f / sides, c = (i + 1f) * Mathf.PI * 2f / sides;
            float shade = flame ? .65f + .35f * (1f - t) : .72f + .28f * Mathf.Sin(t * Mathf.PI);
            b.AddFace(new[] { Point(t,a), Point(t,c), Point(next,c), Point(next,a) }, new Color(shade,shade,shade,1f));
        }
        return b.Build(name,flame?SpatialSurface.Flame:surface);

        Vector3 Point(float t, float a)
        {
            float radius = Mathf.Sin(t * Mathf.PI) * (flame ? (1f - t * .58f) * (1f + .13f * Mathf.Sin(a * 3f + t * 7f)) : 1f);
            return new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, flame ? t * 2.2f : t * 2f - 1f);
        }
    }

    /// <summary>在独立空间曲线上建立两张相交流带，保持侧面可见。</summary>
    private static SpatialVisualMesh BuildStream()
    {
        SpatialVisualMeshBuilder b = new();
        for (int plane = 0; plane < 2; plane++)
        for (int i = 0; i < 32; i++)
        {
            float t = i / 32f, next = (i + 1f) / 32f;
            Vector3 a = Path(t), c = Path(next);
            Vector3 side = plane == 0 ? Vector3.right : Vector3.up;
            float w = Mathf.Pow(Mathf.Sin(t * Mathf.PI), .7f) * .28f * (1f - t * .6f);
            float nw = Mathf.Pow(Mathf.Sin(next * Mathf.PI), .7f) * .11f * (1f - next * .6f);
            b.AddFace(new[] { a-side*w,a+side*w,c+side*nw,c-side*nw }, Color.white);
        }
        return b.Build("energy_stream",SpatialSurface.Mist);
        Vector3 Path(float t) => new(Mathf.Sin(t * Mathf.PI * 2f) * .13f, Mathf.Sin(t * Mathf.PI) * .12f, t);
    }

    /// <summary>剑身的窄脊和刃缘分开着色，沿高度方向指向剑尖。</summary>
    private static SpatialVisualMesh BuildSword()
    {
        SpatialVisualMeshBuilder b = new();
        Vector3 tip = new(0,0,1f), left = new(-.085f,0,.2f), right = new(.085f,0,.2f);
        Vector3 spine = new(0,.035f,.25f), back = new(0,-.035f,.25f);
        b.AddFace(new[]{left,spine,tip}, new Color(.78f,.88f,1f));
        b.AddFace(new[]{spine,right,tip}, Color.white);
        b.AddFace(new[]{right,back,tip}, new Color(.75f,.8f,.9f));
        b.AddFace(new[]{back,left,tip}, Color.white);
        SpatialVisualGeometry.AddRod(b,new Vector3(-.15f,0,.18f),new Vector3(.15f,0,.18f),.045f,new Color(.82f,.65f,.3f));
        SpatialVisualGeometry.AddRod(b,Vector3.zero,new Vector3(0,0,.19f),.065f,new Color(.3f,.37f,.46f));
        return b.Build("energy_sword");
    }

    /// <summary>有晶脊与长尖端的冰体。</summary>
    private static SpatialVisualMesh BuildCrystal()
    {
        SpatialVisualMeshBuilder b=new();
        for(int i=0;i<6;i++)
        {
            float a=i*Mathf.PI/3f,c=(i+1)*Mathf.PI/3f;
            Vector3 p=new(Mathf.Cos(a)*.35f,Mathf.Sin(a)*.35f,.18f),q=new(Mathf.Cos(c)*.35f,Mathf.Sin(c)*.35f,.18f);
            Vector3 up=Vector3.forward*.75f;
            Color color=i%2==0?Color.white:new Color(.58f,.78f,.9f);
            b.AddFace(new[]{p,q,q+up,p+up},color);
            b.AddFace(new[]{p+up,q+up,Vector3.forward*1.7f},color);
        }
        return b.Build("energy_crystal",SpatialSurface.Ice);
    }

    /// <summary>由地面向上渐收的两圈螺旋流带。</summary>
    private static SpatialVisualMesh BuildHelix()
    {
        SpatialVisualMeshBuilder b=new();
        for(int i=0;i<96;i++)
        {
            float t=i/96f,n=(i+1)/96f;
            Vector3 a=Point(t),c=Point(n);float w=.1f*Mathf.Sin(t*Mathf.PI);
            b.AddFace(new[]{a-Vector3.forward*w,a+Vector3.forward*w,c+Vector3.forward*w,c-Vector3.forward*w},Color.white);
        }
        return b.Build("energy_helix",SpatialSurface.Mist);
        Vector3 Point(float t)
        {
            float a=t*Mathf.PI*4f,r=.25f+.75f*t;
            return new Vector3(Mathf.Cos(a)*r,Mathf.Sin(a)*r,t);
        }
    }

    /// <summary>弧带的边缘沿长度渐细，不出现等粗的折线框。</summary>
    private static SpatialVisualMesh BuildCrescent()
    {
        SpatialVisualMeshBuilder b=new();
        for(int i=0;i<64;i++)
        {
            float a=i*Mathf.PI/64f,c=(i+1)*Mathf.PI/64f;
            float w=.16f*Mathf.Sin(a);
            b.AddFace(new[]{Point(a,1f-w),Point(a,1f+w),Point(c,1f+w),Point(c,1f-w)},Color.white);
        }
        return b.Build("energy_crescent",SpatialSurface.Energy);
        Vector3 Point(float a,float radius)=>new(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius,0);
    }

    /// <summary>固定种子的雷干、分叉与细电丝，不消耗战斗随机数。</summary>
    private static SpatialVisualMesh[] BuildBolts()
    {
        var output=new SpatialVisualMesh[8];
        for(int seed=0;seed<output.Length;seed++)
        {
            SpatialVisualMeshBuilder b=new();Vector3 previous=Vector3.zero;
            for(int i=1;i<=24;i++)
            {
                float t=i/24f;Vector3 point=new(Mathf.Sin(i*7.13f+seed*2.47f)*.045f,Mathf.Sin(i*4.91f+seed)*.03f,t);
                if(i==24)point=Vector3.forward;
                SpatialVisualGeometry.AddRod(b,previous,point,.022f*(1f-t*.4f),Color.white);
                if(i%5==0)
                {
                    Vector3 mid=point+new Vector3(Mathf.Sin(i+seed)*.17f,.06f,-.08f);
                    Vector3 end=mid+new Vector3(Mathf.Sin(i+seed)*.1f,-.06f,-.12f);
                    SpatialVisualGeometry.AddRod(b,point,mid,.008f,new Color(.7f,.8f,1f));
                    SpatialVisualGeometry.AddRod(b,mid,end,.0035f,new Color(.7f,.8f,1f));
                }
                previous=point;
            }
            output[seed]=b.Build("energy_bolt_"+seed);
        }
        return output;
    }

    /// <summary>光滑肢体采用八面圆截面，区别于碎屑用的三棱细线。</summary>
    /// <param name="builder">目标模型。</param><param name="start">起点。</param><param name="end">终点。</param><param name="width">直径。</param><param name="color">颜色。</param>
    private static void AddTube(SpatialVisualMeshBuilder builder,Vector3 start,Vector3 end,float width,Color color)
    {
        Vector3 direction=(end-start).normalized;
        Vector3 side=Vector3.Cross(direction,Mathf.Abs(direction.z)<.9f?Vector3.forward:Vector3.up).normalized*width*.5f;
        Vector3 up=Vector3.Cross(direction,side).normalized*width*.5f;
        for(int i=0;i<8;i++)
        {
            float a=i*Mathf.PI*.25f,c=(i+1)*Mathf.PI*.25f;
            Vector3 p=side*Mathf.Cos(a)+up*Mathf.Sin(a),q=side*Mathf.Cos(c)+up*Mathf.Sin(c);
            builder.AddFace(new[]{start+p,start+q,end+q,end+p},color);
        }
    }

    /// <summary>手臂沿局部高度轴伸展，关节位置由真实动作决定。</summary>
    private static SpatialVisualMesh BuildLimb()
    {
        SpatialVisualMeshBuilder builder=new();AddTube(builder,Vector3.zero,Vector3.forward,1f,Color.white);
        return builder.Build("energy_limb",SpatialSurface.Energy);
    }

    /// <summary>掌面圆润而手指分开，远景仍保留方向。</summary>
    private static SpatialVisualMesh BuildPalm()
    {
        SpatialVisualMeshBuilder builder=new();AddEllipsoid(builder,Vector3.zero,new Vector3(.06f,.035f,.075f),Color.white);
        for(int i=0;i<5;i++)AddTube(builder,new Vector3((i-2)*.024f,0,-.04f),new Vector3((i-2)*.027f,0,-.15f),.02f,Color.white);
        return builder.Build("energy_palm",SpatialSurface.Energy);
    }

    /// <summary>三圈宽流带从真实法域边界收束到器口附近，区别于普通描边圆环。</summary>
    private static SpatialVisualMesh BuildVortex()
    {
        SpatialVisualMeshBuilder builder=new();
        for(int i=0;i<128;i++)for(int band=0;band<3;band++)
        {
            float t=i/128f,n=(i+1)/128f,left=(band-1.5f)*.12f,right=(band-.5f)*.12f;
            builder.AddFace(new[]{Point(t,left),Point(t,right),Point(n,right),Point(n,left)},new Color(1,1,1,band==1?.7f:.15f));
        }
        return builder.Build("energy_vortex",SpatialSurface.Mist);
        Vector3 Point(float t,float width)
        {
            float angle=t*Mathf.PI*6f,radius=1f-t*.92f+width;
            return new Vector3(Mathf.Cos(angle)*radius,Mathf.Sin(angle)*radius,t*.55f);
        }
    }

    /// <summary>法相的头、肩、掌与足采用曲面轮廓，不再拼成空心圆管。</summary>
    /// <param name="builder">目标模型。</param><param name="center">中心。</param><param name="scale">各轴半径。</param><param name="color">表面颜色。</param>
    private static void AddEllipsoid(SpatialVisualMeshBuilder builder,Vector3 center,Vector3 scale,Color color)
    {
        for(int j=0;j<8;j++)for(int i=0;i<16;i++)
        {
            float t=j*Mathf.PI/8f,n=(j+1)*Mathf.PI/8f,a=i*Mathf.PI/8f,c=(i+1)*Mathf.PI/8f;
            builder.AddFace(new[]{Point(t,a),Point(t,c),Point(n,c),Point(n,a)},color);
        }
        Vector3 Point(float t,float a)=>center+Vector3.Scale(new Vector3(Mathf.Sin(t)*Mathf.Cos(a),Mathf.Sin(t)*Mathf.Sin(a),Mathf.Cos(t)),scale);
    }
}
