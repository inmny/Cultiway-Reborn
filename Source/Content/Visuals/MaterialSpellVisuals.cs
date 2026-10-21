using System.Collections.Generic;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.Visuals;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content.Visuals;

/// <summary>水、木、土与切削的物质运动；只读取真实执行，不产生命中。</summary>
internal static class MaterialSpellVisuals
{
    /// <summary>地表技能已经实际经过的有限节点。</summary>
    private static readonly Dictionary<Entity, GroundPath> paths = new();
    /// <summary>本帧已经完成收尾的路径。</summary>
    private static readonly List<Entity> finished = new();
    /// <summary>当前路径记录数，供回收验证读取。</summary>
    internal static int PathCount => paths.Count;

    /// <summary>在执行体建立时登记根系和土脊，普通境界也采用相同运动。</summary>
    /// <param name="entity">真实执行体。</param>
    internal static void Register(Entity entity)
    {
        SkillEntityAsset asset = entity.GetComponent<SkillEntity>().Asset;
        if (asset != SkillEntities.SpiritVine && asset != SkillEntities.EarthRidge) return;
        paths.Add(entity, new GroundPath
        {
            Entity = entity, Wood = asset == SkillEntities.SpiritVine,
            Radius = Core.SkillLibV3.Utils.SkillEffectRadius.Resolve(entity, Mathf.Max(.25f, asset.ImpactProfile.EffectRadius))
        });
    }

    /// <summary>每帧记录实际位置，再绘制留在原处的根系或岩段；换镜头不重复采样。</summary>
    /// <param name="output">共同模型。</param><param name="details">装饰预算。</param>
    internal static void AppendGroundPaths(List<SpatialVisualMeshInstance> output, ref int details)
    {
        double now = World.world.getCurWorldTime();
        finished.Clear();
        foreach (var pair in paths)
        {
            GroundPath path = pair.Value;
            bool live = path.Entity.IsAvailable() && !path.Entity.Tags.Has<TagRecycle>();
            if (live && path.Entity.Tags.Has<TagInactive>()) continue;
            if (live)
            {
                Vector3 point = path.Entity.GetComponent<Position>().value; point.z = .035f;
                path.Head = point;
                float spacing = Mathf.Max(.65f, path.Radius * (path.Wood ? .28f : .48f));
                if (path.Nodes.Count == 0 || Vector3.Distance(point, path.Nodes[path.Nodes.Count - 1].Position) >= spacing)
                {
                    if (path.Nodes.Count == 64) path.Nodes.RemoveAt(0);
                    path.Nodes.Add(new GroundNode { Position = point, At = now });
                }
            }
            else if (path.Ended < 0d) path.Ended = now;
            float ending = path.Ended < 0d ? 0f : (float)(now - path.Ended);
            if (ending > 1.5f) { finished.Add(pair.Key); continue; }
            for (int i = 0; i < path.Nodes.Count + (path.Wood ? 1 : 0); i++)
            {
                GroundNode node = i < path.Nodes.Count ? path.Nodes[i] : new GroundNode { Position = path.Head, At = now - .22d };
                if (!SpatialVisualEffects.IsRelevant(node.Position, path.Radius * 2f)) continue;
                Vector3 delta = i > 0 ? node.Position - path.Nodes[i - 1].Position : Vector3.right;
                float age = (float)(now - node.At);
                if (path.Wood)
                {
                    if (i == 0) continue;
                    float growth = Mathf.Clamp01(age / .35f), fade = 1f - Mathf.Clamp01((ending - 1.1f) / .4f);
                    Quaternion heading = Heading(delta);
                    if (delta.sqrMagnitude < .0001f) continue;
                    float thickness = path.Radius * (.42f - Mathf.Min(.2f, i * .009f));
                    SpatialVisualDraw.Mesh(output, SpatialEnergyGeometry.Limb, path.Nodes[i - 1].Position,
                        Quaternion.FromToRotation(Vector3.forward, delta), new Vector3(thickness, thickness, delta.magnitude),
                        new Color(.52f,.36f,.19f,fade), false, surface: SpatialSurface.Wood);
                    if (i % 5 == 2 && i < path.Nodes.Count)
                    {
                        SpatialVisualDraw.Mesh(output, SpatialMaterialGeometry.Sample(SpatialMaterialGeometry.Roots[i % 4], growth),
                            node.Position, heading * Quaternion.Euler(0,0,i%2==0?55f:-65f),
                            new Vector3(path.Radius*1.2f,path.Radius*1.4f,path.Radius*1.4f),new Color(1,1,1,fade),false);
                        if(details>0)
                        {
                            details--;
                            SpatialVisualDraw.Mesh(output,SpatialVisualGeometry.Leaf,node.Position+Vector3.forward*path.Radius*.18f,
                                heading*Quaternion.Euler(25,0,i*37),Vector3.one*path.Radius*.22f*growth,new Color(.27f,.5f,.16f,fade),false);
                        }
                    }
                }
                else DrawEarthRise(output, node.Position, Heading(delta), path.Radius, age, Mathf.Max(ending,age-1f), i, ref details);
            }
        }
        foreach (Entity entity in finished) paths.Remove(entity);
    }

    /// <summary>宽底岩段只顶起一次，保持后从上部裂开，碎块独立落地。</summary>
    /// <param name="output">模型。</param><param name="center">固定节点。</param><param name="heading">前进方向。</param>
    /// <param name="radius">实际范围。</param><param name="age">该段年龄。</param><param name="ending">执行结束时间。</param>
    /// <param name="seed">固定变化。</param><param name="details">装饰预算。</param>
    internal static void DrawEarthRise(List<SpatialVisualMeshInstance> output, Vector3 center, Quaternion heading,
        float radius, float age, float ending, int seed, ref int details)
    {
        if(ending>1.5f)return;
        float rise = Mathf.SmoothStep(.02f, 1f, Mathf.Clamp01(age / .22f));
        for (int i = 0; i < 3; i++)
        {
            int variant = (i + seed) % 4;
            Vector3 position = center + heading * new Vector3((i % 2) * radius * .09f, (i - 1) * radius * .55f, .015f);
            Quaternion rotation = heading * Quaternion.Euler(0,0,variant * 61f);
            Vector3 size = new(radius*.37f,radius*.33f,radius*(.5f+variant*.09f)*rise);
            DrawRock(output,variant,position,rotation,size,ending);
        }
        float dust = Mathf.Max(1f - Mathf.Clamp01(age / .55f), Mathf.Clamp01((ending-.3f)/.35f)*(1f-Mathf.Clamp01((ending-.9f)/.6f)));
        if (dust <= .01f || details <= 0) return;
        details--;
        SpatialVisualDraw.Mesh(output, SpatialEnergyGeometry.Cloud, center + Vector3.forward * radius * .1f, heading,
            new Vector3(radius * .6f, radius, radius * .22f), new Color(.59f, .53f, .4f, dust * .2f), true, additive: false);
    }

    /// <summary>完整岩体先保持刚性，再按原来的体积分裂；碎块落地后留作短暂残骸。</summary>
    /// <param name="output">模型。</param><param name="variant">岩体种类。</param><param name="position">固定底部。</param>
    /// <param name="rotation">原始朝向。</param><param name="size">原始尺寸。</param><param name="ending">结束后的世界秒数。</param>
    internal static void DrawRock(List<SpatialVisualMeshInstance> output,int variant,Vector3 position,Quaternion rotation,Vector3 size,float ending)
    {
        if(ending<.2f)
        {
            SpatialVisualDraw.Mesh(output,SpatialMaterialGeometry.Rocks[variant],position,rotation,size,Color.white,false);
            return;
        }
        float alpha=1f-Mathf.Clamp01((ending-1.35f)/.15f);
        for(int fragment=0;fragment<12;fragment++)
        {
            int level=fragment/3,sector=fragment%3;
            float fall=Mathf.Max(0,ending-.2f-(3-level)*.11f-(sector+variant)%3*.025f);
            Vector3 offset=SpatialMaterialGeometry.FragmentOrigins[variant][fragment];
            Vector3 start=position+rotation*Vector3.Scale(offset,size);
            float angle=sector*Mathf.PI*2f/3f+variant*.6f;
            float motion=Mathf.Min(fall,.65f);
            Vector3 scatter=rotation*new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0)*Mathf.Max(size.x,size.y)*motion*.45f;
            SpatialVisualMesh model=SpatialMaterialGeometry.RockFragments[variant][fragment];
            Vector3 point=start+scatter;
            Quaternion turn=rotation*Quaternion.Euler(motion*28f*(sector-1),motion*19f,motion*(variant-1)*17f);
            Vector3 up=Quaternion.Inverse(turn)*Vector3.forward;
            Vector3 extent=Vector3.Scale(model.Bounds.extents,size);
            float minimum=Vector3.Dot(Vector3.Scale(model.Bounds.center,size),up)-Mathf.Abs(up.x)*extent.x-Mathf.Abs(up.y)*extent.y-Mathf.Abs(up.z)*extent.z;
            float ground=position.z-minimum;
            point.z=Mathf.Max(ground,start.z+size.z*(fall*.08f-fall*fall*2.4f));
            SpatialVisualDraw.Mesh(output,model,point,turn,size,new Color(1,1,1,alpha),false);
        }
    }

    /// <summary>水体由隆起到前卷、崩落；飞沫只出现在浪唇与落水处。</summary>
    /// <param name="output">模型。</param><param name="position">真实浪头。</param><param name="direction">实际方向。</param>
    /// <param name="radius">实际范围。</param><param name="age">世界年龄。</param><param name="fade">真实消散比例。</param>
    /// <param name="details">装饰预算。</param>
    internal static void Wave(List<SpatialVisualMeshInstance> output, Vector3 position, Vector3 direction,
        float radius, float age, float fade, ref int details)
    {
        Quaternion heading = Heading(direction);
        position.z = .025f;
        SpatialVisualDraw.Mesh(output, SpatialMaterialGeometry.Sample(SpatialMaterialGeometry.Waves, age / 1.05f), position,
            heading, new Vector3(radius * .7f, radius, radius * .66f), new Color(.85f, .98f, 1, .72f * fade), false);
        for (int i = 0; i < 18 && details > 0; i++)
        {
            float t = age - .5f - i * .018f;
            if (t < 0 || t > .8f) continue;
            details--;
            Vector3 local = new(.2f + t * .95f, Mathf.Sin(i * 2.39996f) * .86f,
                Mathf.Max(.025f, .52f + t * .28f - t * t * 1.6f));
            float size = radius * (.028f + i % 3 * .012f);
            SpatialVisualDraw.Mesh(output, SpatialEnergyGeometry.Orb, position + heading * (local * radius), Quaternion.identity,
                new Vector3(size * 1.7f, size, size * .7f), new Color(.77f, .92f, .94f, (1f - t / .8f) * .7f * fade),
                true, additive: false, surface: SpatialSurface.None);
        }
    }

    /// <summary>木系领域以不等长主根和稀疏枝叶建立主体。</summary>
    /// <param name="output">模型。</param><param name="center">固定根部。</param><param name="radius">实际范围。</param>
    /// <param name="growth">生长进度。</param><param name="fade">收尾透明度。</param>
    internal static void RootCluster(List<SpatialVisualMeshInstance> output, Vector3 center, float radius, float growth, float fade)
    {
        for (int i = 0; i < 5; i++)
        {
            float phase = Mathf.Clamp01(growth * 1.35f - i * .07f);
            SpatialVisualDraw.Mesh(output, SpatialMaterialGeometry.Sample(SpatialMaterialGeometry.Roots[i % 4], phase), center,
                Quaternion.Euler(0, 0, i * 137.5f + 18f), new Vector3(radius * (.66f + i % 3 * .13f), radius * .85f, radius * .9f),
                new Color(1, 1, 1, fade), false);
        }
    }

    /// <summary>分开呈现各种切削材料，实体飞剑仍走原有剑身入口。</summary>
    /// <param name="output">模型。</param><param name="asset">实际技能。</param><param name="position">锋面位置。</param>
    /// <param name="direction">实际方向。</param><param name="body">碰撞体派生尺度。</param><param name="age">年龄。</param>
    /// <param name="fade">消散比例。</param><param name="details">装饰预算。</param>
    internal static void Blade(List<SpatialVisualMeshInstance> output, SkillEntityAsset asset, Vector3 position,
        Vector3 direction, float body, float age, float fade, ref int details, Cultiway.Core.SkillLibV3.Visuals.SkillVfxElementAsset nature = null)
    {
        Quaternion heading = Heading(direction);
        bool fire = nature==SkillVfxElements.Fire || nature==null && asset == SkillEntities.FireBlade;
        bool water = nature==SkillVfxElements.Water || nature==null && asset == SkillEntities.WaterBlade;
        bool wind = nature==SkillVfxElements.Wind || nature==null && asset == SkillEntities.WindBlade;
        bool yin = nature==SkillVfxElements.Neg || nature==null && asset == SkillEntities.YinBlade;
        Color color = fire ? new Color(1, .37f, .07f, fade * .6f) : water ? new Color(.38f, .76f, .82f, fade * .7f) :
            yin ? new Color(.44f, .58f, .68f, fade * .55f) : new Color(.65f, .85f, .92f, fade * .65f);
        if (!wind && !fire)
        {
            var poses=water?SpatialMaterialGeometry.WaterCuts:yin?SpatialMaterialGeometry.YinCuts:SpatialMaterialGeometry.SwordCuts;
            SpatialVisualDraw.Mesh(output,SpatialMaterialGeometry.Sample(poses,age/.9f),position,heading,
                new Vector3(body,body*1.55f,body*.7f),color,!water,additive:false);
        }
        for (int i = 0; i < 9 && details > 0; i++, details--)
        {
            float side = i / 4f - 1f, jitter = Mathf.Sin(i * 3.7f + age * 15f);
            Vector3 offset = new(-side * side * .25f - .15f, side * 1.4f, jitter * .035f);
            Vector3 point = position + heading * (offset * body);
            if (fire)
            {
                if (i % 2 == 1) continue;
                SpatialVisualDraw.Mesh(output, SpatialEnergyGeometry.Flame, point,
                    heading * Quaternion.FromToRotation(Vector3.forward, new Vector3(-1, jitter * .14f, .2f)),
                    new Vector3(body * .35f, body * .32f, body * (.66f + .25f * jitter)), color, true, additive: false);
            }
            else if (water)
            {
                float trail = Mathf.Repeat(age * 1.8f + i * .137f, 1f);
                point -= direction * body * trail;
                SpatialVisualDraw.Mesh(output, SpatialVisualGeometry.Spark, point, heading*Quaternion.Euler(0,0,i*43f),
                    new Vector3(body * .12f, body * .035f, body * .03f) * (1f - trail * .7f),
                    new Color(.67f, .9f, .94f, fade * (1f - trail) * .6f), true, additive: false, surface: SpatialSurface.None);
            }
            else if (wind || yin)
                SpatialVisualDraw.Mesh(output, SpatialEnergyGeometry.Cloud, point - direction * body * .25f, heading,
                    new Vector3(body * .38f, body * .2f, body * .12f),
                    wind ? new Color(.7f, .77f, .69f, fade * .13f) : new Color(.28f, .37f, .48f, fade * .18f), true, additive: false);
        }
    }

    /// <summary>取得地图平面方向，局部横轴为前进方向。</summary>
    /// <param name="direction">实际方向。</param>
    internal static Quaternion Heading(Vector3 direction) => Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);

    /// <summary>退出世界释放路径与执行体引用。</summary>
    internal static void Clear() { paths.Clear(); finished.Clear(); }

    /// <summary>一条地表执行路径，最多保留六十四个实际节点。</summary>
    private sealed class GroundPath
    {
        /// <summary>真实执行体。</summary>
        internal Entity Entity;
        /// <summary>是否为木系根系。</summary>
        internal bool Wood;
        /// <summary>实际作用半径。</summary>
        internal float Radius;
        /// <summary>真实前端，位于最后一个固定节点之后。</summary>
        internal Vector3 Head;
        /// <summary>实际经过的节点。</summary>
        internal readonly List<GroundNode> Nodes = new();
        /// <summary>执行结束的世界时间。</summary>
        internal double Ended = -1d;
    }

    /// <summary>固定在地图上的一次生长或隆起。</summary>
    private struct GroundNode
    {
        /// <summary>实际位置。</summary>
        internal Vector3 Position;
        /// <summary>首次经过的世界时间。</summary>
        internal double At;
    }
}
