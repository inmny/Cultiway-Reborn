using System.Collections.Generic;
using UnityEngine;

namespace Cultiway.Core.Visuals;

/// <summary>保存有限的物质运动姿态；两个视角使用同一浪形、根系和岩体。</summary>
internal static class SpatialMaterialGeometry
{
    /// <summary>一次翻卷到落下的浪形，所有施放共用。</summary>
    internal static readonly SpatialVisualMesh[] Waves = BuildWaves();
    /// <summary>细藤采用连续圆截面，连接处保留相同口径。</summary>
    internal static readonly SpatialVisualMesh[] VineSegments = BuildVineSegments();
    /// <summary>留出接缝的球面护甲，区别于通用圆环。</summary>
    internal static readonly SpatialVisualMesh ShieldPetal = BuildShieldPetal();
    /// <summary>四种分枝方向的根系生长姿态。</summary>
    internal static readonly SpatialVisualMesh[][] Roots = BuildRoots();
    /// <summary>宽底、断顶且不对称的共用岩块。</summary>
    internal static readonly SpatialVisualMesh[] Rocks = BuildRocks();
    /// <summary>薄水锋不断破开，避免闭合为一块椭圆水片。</summary>
    internal static readonly SpatialVisualMesh[] WaterCuts = BuildCuts(SpatialSurface.Water);
    /// <summary>剑气使用斜切锋面和不对称的断尾。</summary>
    internal static readonly SpatialVisualMesh[] SwordCuts = BuildCuts(SpatialSurface.Metal);
    /// <summary>阴刃采用几段错开的冷色裂片。</summary>
    internal static readonly SpatialVisualMesh[] YinCuts = BuildCuts(SpatialSurface.Energy);
    /// <summary>岩块裂开后各碎块在原岩体中的固定位置。</summary>
    internal static readonly Vector3[][] FragmentOrigins = new Vector3[4][];
    /// <summary>四种岩块各自的十二块完整实体，收尾时独立掉落。</summary>
    internal static readonly SpatialVisualMesh[][] RockFragments = BuildRockFragments();
    /// <summary>落水后留下的低矮水面，不使用旋转浪峰表示静水。</summary>
    internal static readonly SpatialVisualMesh Pool = BuildPool();
    /// <summary>连续下落水幕，区别于横向重复的浪峰。</summary>
    internal static readonly SpatialVisualMesh Curtain = BuildCurtain();

    /// <summary>按完整过程选择姿态，不创建新模型或写入永久缓存。</summary>
    /// <param name="frames">有限姿态。</param><param name="progress">零到一的进度。</param>
    internal static SpatialVisualMesh Sample(SpatialVisualMesh[] frames, float progress) =>
        frames[Mathf.RoundToInt(Mathf.Clamp01(progress) * (frames.Length - 1))];

    /// <summary>预先建立一次隆起、翻卷、崩落和铺开的完整形状变化。</summary>
    private static SpatialVisualMesh[] BuildWaves()
    {
        SpatialVisualMesh[] frames = new SpatialVisualMesh[48];
        for (int frame = 0; frame < frames.Length; frame++)
        {
            float phase = frame / 47f * Mathf.PI * 2f;
            SpatialVisualMeshBuilder builder = new();
            for (int side = 0; side < 28; side++)
            for (int section = 0; section < 24; section++)
            {
                float y = side / 14f - 1f, nextY = (side + 1) / 14f - 1f;
                float t = section / 24f, nextT = (section + 1) / 24f;
                float foam = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.76f, 1f, t)) * (.65f + .35f * Mathf.Sin(y * 17f + phase));
                Color water = Color.Lerp(new Color(.1f, .5f, .62f), new Color(.91f, .99f, 1), foam);
                water.a = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0, .12f, 1f - Mathf.Abs(y))) * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0, .12f, t)) * .86f;
                builder.AddFace(new[] { Point(y, t), Point(y, nextT), Point(nextY, nextT), Point(nextY, t) }, water);
            }
            frames[frame] = builder.Build("breaking_wave_" + frame, SpatialSurface.Water);
            SmoothWater(frames[frame]);
            Vector3 Point(float y, float t) => WavePoint(y, t, frame / 47f);
        }
        return frames;
    }

    /// <summary>同一浪面从低伏到立起、卷唇再落下；泡沫也读取这个实际表面。</summary>
    /// <param name="y">横跨浪头的位置。</param><param name="t">从浪背到浪唇的位置。</param><param name="progress">本次浪头的生命进度。</param>
    internal static Vector3 WavePoint(float y, float t, float progress)
    {
        float rise = Mathf.SmoothStep(0, 1, Mathf.Clamp01(progress / .3f));
        float fall = Mathf.SmoothStep(0, 1, Mathf.Clamp01((progress - .5f) / .5f));
        float height = .08f + .92f * rise * (1 - fall);
        float curl = Mathf.SmoothStep(0, 1, Mathf.Clamp01((progress - .12f) / .58f));
        float edge = Mathf.Pow(Mathf.Max(0, 1 - y * y), .55f);
        float lip = Mathf.Clamp01((t - .64f) / .36f);
        // 浪头中央领先、两端收回，不保持一面齐整的直墙。
        float x = -.9f + t * .98f + Mathf.Sin(lip * Mathf.PI * (.35f + .53f * curl)) * .36f;
        x += .8f * (1 - y * y) * t;
        float z = Mathf.Sin(t * Mathf.PI * .7f) * .85f - lip * lip * (.08f + curl * .3f);
        float uneven = 1 + .12f * Mathf.Sin(y * 9 + progress * 5) + .05f * Mathf.Sin(y * 21 - progress * 4);
        return new Vector3(x, y, .025f + z * edge * height * uneven);
    }

    /// <summary>生成端点口径一致的圆截面细藤，分段处不使用方木。</summary>
    private static SpatialVisualMesh[] BuildVineSegments()
    {
        var models = new SpatialVisualMesh[4];
        for (int variant = 0; variant < models.Length; variant++)
        {
            SpatialVisualMeshBuilder builder = new();
            for (int section = 0; section < 12; section++)
            for (int side = 0; side < 8; side++)
            {
                float t = section / 12f, next = (section + 1) / 12f;
                float a = side * Mathf.PI / 4, b = (side + 1) * Mathf.PI / 4;
                builder.AddFace(new[] { Point(t, a), Point(next, a), Point(next, b), Point(t, b) }, new Color(.35f, .44f, .14f));
            }
            models[variant] = builder.Build("living_vine_" + variant, SpatialSurface.Wood);
            Vector3 Point(float t, float a) => new(t, Mathf.Sin(t * Mathf.PI * 2) * .22f + Mathf.Cos(a) * .46f,
                .42f + Mathf.Sin(t * Mathf.PI) * (.35f + variant * .08f) + Mathf.Sin(a) * .46f);
        }
        return models;
    }

    /// <summary>生成有弧度的护甲片，缝隙让人物与来袭方向保持清楚。</summary>
    private static SpatialVisualMesh BuildShieldPetal()
    {
        SpatialVisualMeshBuilder builder = new();
        for (int row = 0; row < 7; row++)
        for (int column = 0; column < 5; column++)
        {
            float u = column / 5f, v = row / 7f;
            builder.AddFace(new[] { Point(u, v), Point(u + .2f, v), Point(u + .2f, v + 1f / 7), Point(u, v + 1f / 7) }, Color.white);
        }
        return builder.Build("shield_petal", SpatialSurface.Energy);
        Vector3 Point(float u, float v)
        {
            float azimuth = (u - .5f) * .88f, elevation = -.12f + v * 1.43f;
            return new Vector3(Mathf.Cos(azimuth) * Mathf.Cos(elevation), Mathf.Sin(azimuth) * Mathf.Cos(elevation), Mathf.Sin(elevation));
        }
    }

    /// <summary>让相邻浪面共用平滑受光和泡沫过渡，避免宽浪出现棋盘状硬面。</summary>
    /// <param name="mesh">刚建立的共用浪体。</param>
    private static void SmoothWater(SpatialVisualMesh mesh)
    {
        Dictionary<Vector3, (Vector3 normal, Color color, int count)> sums = new();
        for (int i = 0; i < mesh.Vertices.Length; i++)
        {
            Vector3 point = mesh.Vertices[i];
            sums.TryGetValue(point, out var sum);
            sums[point] = (sum.normal + mesh.Normals[i], sum.color + (Color)mesh.VertexColors[i], sum.count + 1);
        }
        for (int i = 0; i < mesh.Vertices.Length; i++)
        {
            var sum = sums[mesh.Vertices[i]];
            mesh.Normals[i] = sum.normal.normalized;
            mesh.VertexColors[i] = sum.color / sum.count;
        }
    }

    /// <summary>预先沿曲线长出根系，已经形成的部分不再被整体拉伸。</summary>
    private static SpatialVisualMesh[][] BuildRoots()
    {
        SpatialVisualMesh[][] variants = new SpatialVisualMesh[4][];
        for (int variant = 0; variant < variants.Length; variant++)
        {
            variants[variant] = new SpatialVisualMesh[32];
            for (int frame = 0; frame < 32; frame++)
            {
                float growth = Mathf.Max(.006f, frame / 31f);
                SpatialVisualMeshBuilder builder = new();
                Tube(builder, RootPoint, growth, .105f, new Color(.54f, .38f, .21f));
                for (int branch = 0; branch < 3; branch++)
                {
                    float at = .27f + branch * .23f;
                    if (growth <= at) continue;
                    int branchIndex = branch;
                    Vector3 start = RootPoint(at);
                    float reach = Mathf.Clamp01((growth - at) / .25f);
                    Tube(builder, BranchPoint, reach, .049f * (1f - at * .5f), new Color(.47f, .32f, .16f));
                    Vector3 BranchPoint(float t) => start + new Vector3(t * .27f,
                        (branchIndex % 2 == 0 ? 1f : -1f) * t * (.23f + variant * .025f),
                        Mathf.Sin(t * Mathf.PI * .8f) * .18f);
                }
                variants[variant][frame] = builder.Build("growing_root_" + variant + "_" + frame, SpatialSurface.Wood);
            }
            Vector3 RootPoint(float t) => new(t, Mathf.Sin(t * 5.5f + variant * .9f) * .09f * t,
                .04f + Mathf.Sin(t * Mathf.PI) * (.17f + variant * .025f) + Mathf.Pow(t, 5f) * .09f);
        }
        return variants;
    }

    /// <summary>沿逐渐显露的固定路径建立粗根与尖端。</summary>
    /// <param name="builder">目标模型。</param><param name="path">固定路径。</param><param name="growth">生长进度。</param>
    /// <param name="radius">根部半径。</param><param name="color">木色。</param>
    private static void Tube(SpatialVisualMeshBuilder builder, System.Func<float, Vector3> path, float growth, float radius, Color color)
    {
        for (int section = 0; section < 12; section++)
        for (int side = 0; side < 7; side++)
        {
            float t = growth * section / 12f, next = growth * (section + 1) / 12f;
            float angle = side * Mathf.PI * 2f / 7f, nextAngle = (side + 1) * Mathf.PI * 2f / 7f;
            builder.AddFace(new[] { Point(t, angle), Point(t, nextAngle), Point(next, nextAngle), Point(next, angle) }, color);
        }
        Vector3 Point(float t, float angle)
        {
            Vector3 tangent = (path(Mathf.Min(1f, t + .005f)) - path(Mathf.Max(0f, t - .005f))).normalized;
            Vector3 right = Vector3.Cross(tangent, Vector3.forward).normalized;
            Vector3 up = Vector3.Cross(right, tangent).normalized;
            float thickness = radius * Mathf.Pow(Mathf.Max(.001f, 1f - t), .65f) * Mathf.Clamp01((growth - t) * 25f + .03f);
            return path(t) + (right * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * thickness;
        }
    }

    /// <summary>岩块以大断面和宽底表达重量，不使用细长尖锥。</summary>
    private static SpatialVisualMesh[] BuildRocks()
    {
        SpatialVisualMesh[] rocks = new SpatialVisualMesh[4];
        for (int variant = 0; variant < rocks.Length; variant++)
        {
            SpatialVisualMeshBuilder builder = new();
            for (int level = 0; level < 4; level++)
            for (int side = 0; side < 9; side++)
            {
                Color color = Color.Lerp(new Color(.48f, .45f, .38f), new Color(.79f, .75f, .62f), (side + level + variant) % 4 / 3f);
                builder.AddFace(new[] { Point(level, side), Point(level, side + 1), Point(level + 1, side + 1), Point(level + 1, side) }, color);
            }
            Vector3[] top = new Vector3[9];
            for (int i = 0; i < top.Length; i++) top[i] = Point(4, i);
            builder.AddFace(top, new Color(.74f, .7f, .58f));
            rocks[variant] = builder.Build("fractured_rock_" + variant, SpatialSurface.Stone);
            Vector3 Point(int level, int side)
            {
                float angle = side % 9 * Mathf.PI * 2f / 9f;
                float radius = (1f - level * .17f) * (1f + Mathf.Sin(side % 9 * 5.1f + variant) * .17f);
                return new Vector3(Mathf.Cos(angle) * radius + level * .055f,
                    Mathf.Sin(angle) * radius, level * .25f + (level == 0 ? 0f : Mathf.Sin(side % 9 * 3.7f + variant) * .045f));
            }
        }
        return rocks;
    }

    /// <summary>把切削限制在窄而破碎的前锋，形状随年龄改变，不再形成闭合的圆润主体。</summary>
    /// <param name="material">水、剑气或阴刃，各自采用不同的边缘和断口。</param>
    private static SpatialVisualMesh[] BuildCuts(SpatialSurface material)
    {
        SpatialVisualMesh[] frames = new SpatialVisualMesh[24];
        bool water = material == SpatialSurface.Water, yin = material == SpatialSurface.Energy;
        for (int frame = 0; frame < frames.Length; frame++)
        {
            SpatialVisualMeshBuilder builder = new();
            float phase = frame / 23f;
            for (int i = 0; i < 32; i++)
            {
                if (yin && (i % 11 == 0 || i % 11 == 1) || water && i % 13 == 0) continue;
                float y = i / 16f - 1f, next = (i + 1) / 16f - 1f;
                Color color = water ? new Color(.55f,.86f,.9f) : yin ? new Color(.65f,.79f,.84f) : Color.white;
                builder.AddFace(new[] { Point(y,false,1),Point(y,true,1),Point(next,true,1),Point(next,false,1) },color);
                builder.AddFace(new[] { Point(next,false,-1),Point(next,true,-1),Point(y,true,-1),Point(y,false,-1) },color);
                builder.AddFace(new[] { Point(y,true,-1),Point(next,true,-1),Point(next,true,1),Point(y,true,1) },color);
            }
            frames[frame] = builder.Build((water?"water_cut_":yin?"yin_cut_":"sword_cut_")+frame,
                water?SpatialSurface.Water:SpatialSurface.Energy);
            ApplyCutTransparency(frames[frame], material, phase);
            Vector3 Point(float y, bool front, int face)
            {
                float edge = 1f-Mathf.Abs(y)*.15f;
                float leading = water ? y*.10f + Mathf.Sin(y*12f-phase*7f)*.025f : yin ? .07f*Mathf.Floor(y*5f) : y*.16f;
                float width = water ? .62f+.3f*Mathf.Pow(Mathf.Sin(y*5f-phase*3f),2f) : yin ? .45f+.3f*Mathf.Repeat(y*3f,1f) : .55f+.5f*(y+1f)*.5f;
                return new Vector3(leading-(front?0f:width*edge),y,
                    (water?Mathf.Sin(y*8f-phase*5f)*.025f:yin?Mathf.Sin(y*13f)*.035f:0f)+face*.012f*edge);
            }
        }
        return frames;
    }

    /// <summary>切面前锋清楚、后侧逐渐透开，保留面状质感而不形成椭圆轮廓。</summary>
    /// <param name="mesh">刚建立的切面。</param><param name="material">切削材料。</param><param name="phase">形变进度。</param>
    private static void ApplyCutTransparency(SpatialVisualMesh mesh,SpatialSurface material,float phase)
    {
        bool water=material==SpatialSurface.Water,yin=material==SpatialSurface.Energy;
        for(int i=0;i<mesh.Vertices.Length;i++)
        {
            Vector3 point=mesh.Vertices[i];float y=point.y;
            float leading=water?y*.10f+Mathf.Sin(y*12f-phase*7f)*.025f:yin?.07f*Mathf.Floor(y*5f):y*.16f;
            float width=water?.62f+.3f*Mathf.Pow(Mathf.Sin(y*5f-phase*3f),2f):yin?.45f+.3f*Mathf.Repeat(y*3f,1f):.55f+.5f*(y+1f)*.5f;
            float alpha=Mathf.Clamp01(1f-(leading-point.x)/(width*(1f-Mathf.Abs(y)*.15f)));
            Color color=mesh.VertexColors[i];color.a=alpha;mesh.VertexColors[i]=color;
        }
    }

    /// <summary>沿层理与三个方向把原岩体拆成完整碎块，保持刚性并围绕各自位置掉落。</summary>
    private static SpatialVisualMesh[][] BuildRockFragments()
    {
        SpatialVisualMesh[][] variants = new SpatialVisualMesh[4][];
        for (int variant=0;variant<4;variant++)
        {
            variants[variant]=new SpatialVisualMesh[12];FragmentOrigins[variant]=new Vector3[12];
            for(int level=0;level<4;level++)
            for(int sector=0;sector<3;sector++)
            {
                int index=level*3+sector;
                float angle=(sector*3+1.5f)*Mathf.PI*2f/9f;
                Vector3 origin=new(Mathf.Cos(angle)*.32f*(1f-level*.17f)+level*.055f,
                    Mathf.Sin(angle)*.32f*(1f-level*.17f),level*.25f);
                FragmentOrigins[variant][index]=origin;
                SpatialVisualMeshBuilder builder=new();
                Color stone=new(.64f,.60f,.5f);
                for(int side=sector*3;side<sector*3+3;side++)
                    builder.AddFace(new[]{Point(level,side),Point(level,side+1),Point(level+1,side+1),Point(level+1,side)},
                        Color.Lerp(new Color(.48f,.45f,.38f),new Color(.79f,.75f,.62f),(side+level+variant)%4/3f));
                int first=sector*3,last=first+3;
                Vector3 low= new Vector3(level*.055f,0,level*.25f)-origin;
                Vector3 high=new Vector3((level+1)*.055f,0,(level+1)*.25f)-origin;
                builder.AddFace(new[]{low,Point(level,first),Point(level+1,first),high},stone);
                builder.AddFace(new[]{Point(level,last),low,high,Point(level+1,last)},stone);
                builder.AddFace(new[]{high,Point(level+1,first),Point(level+1,first+1),Point(level+1,first+2),Point(level+1,last)},
                    level==3?new Color(.74f,.7f,.58f):stone);
                builder.AddFace(new[]{low,Point(level,last),Point(level,last-1),Point(level,last-2),Point(level,first)},stone);
                variants[variant][index]=builder.Build("rock_fragment_"+variant+"_"+index,SpatialSurface.Stone);
                Vector3 Point(int row,int side)
                {
                    float a=side%9*Mathf.PI*2f/9f;
                    float r=(1f-row*.17f)*(1f+Mathf.Sin(side%9*5.1f+variant)*.17f);
                    return new Vector3(Mathf.Cos(a)*r+row*.055f,Mathf.Sin(a)*r,
                        row*.25f+(row==0?0f:Mathf.Sin(side%9*3.7f+variant)*.045f))-origin;
                }
            }
        }
        return variants;
    }

    /// <summary>沿不规则岸边建立低矮水面，材质自身提供缓慢流动。</summary>
    private static SpatialVisualMesh BuildPool()
    {
        SpatialVisualMeshBuilder builder = new();
        const int sides = 48, rings = 8;
        for (int ring = 0; ring < rings; ring++)
        for (int side = 0; side < sides; side++)
        {
            float inside = ring / (float)rings, outside = (ring + 1f) / rings;
            float first = side * Mathf.PI * 2f / sides, next = (side + 1f) * Mathf.PI * 2f / sides;
            Color water = Color.Lerp(new Color(.19f, .5f, .57f), new Color(.62f, .8f, .8f), Mathf.Pow(outside, 5f) * .45f);
            water.a = 1f - Mathf.Pow(outside, 8f) * .55f;
            builder.AddFace(new[] { Point(inside, first), Point(outside, first), Point(outside, next), Point(inside, next) }, water);
        }
        SpatialVisualMesh mesh = builder.Build("settled_water_pool", SpatialSurface.Water);
        SmoothWater(mesh);
        return mesh;
        Vector3 Point(float radius, float angle)
        {
            float edge = .94f + Mathf.Sin(angle * 3f) * .035f + Mathf.Sin(angle * 7f + 1f) * .02f;
            return new Vector3(Mathf.Cos(angle) * radius * edge, Mathf.Sin(angle) * radius * edge,
                .1f + Mathf.Sin(angle * 3f) * radius * radius * .025f);
        }
    }

    /// <summary>水幕横向连成整面，底部稍展开。</summary>
    private static SpatialVisualMesh BuildCurtain()
    {
        SpatialVisualMeshBuilder builder = new();
        for (int x = 0; x < 28; x++)
        for (int z = 0; z < 8; z++)
        {
            float a = x / 28f - .5f, b = (x + 1) / 28f - .5f, low = z / 8f, high = (z + 1) / 8f;
            builder.AddFace(new[] { Point(a, low), Point(b, low), Point(b, high), Point(a, high) }, new Color(.3f, .73f, .8f));
        }
        return builder.Build("falling_water_curtain", SpatialSurface.Water);
        Vector3 Point(float x, float z) => new(x, Mathf.Sin(x * 31f) * .025f + (1f - z) * (1f - z) * .16f, z);
    }
}
