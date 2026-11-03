using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using Cultiway.Content.Skills;
using Cultiway.Core.Skills;
using HarmonyLib;
using UnityEngine;

namespace Cultiway.Content.Compatibility.Worldfall;

/// <summary>从实际建筑和模型采集步法阻挡，不读取镜头附近的通行缓存。</summary>
internal sealed partial class WorldfallBodyObstacles
{
    /// <summary>外部根、场景和原有图片缓存。</summary>
    private readonly PropertyInfo root;

    /// <summary>场景、图片与建筑记录的固定字段。</summary>
    private readonly FieldInfo scene, sprites;

    /// <summary>外部建筑记录类型及其字段，只在安装时查找名称。</summary>
    private readonly Type nearType;

    /// <summary>已有建筑元数据的固定字段。</summary>
    private readonly Dictionary<string, FieldInfo> fields = new();

    /// <summary>取得实际图片尺寸、废墟、固定比例及地基的原有入口。</summary>
    private readonly MethodInfo updateSprite, ruinModel, keptScale, groundBase, groundPitch, stageOf, siteAt;

    /// <summary>原有模型族、模型、朝向和镜像，不按名称另造形状。</summary>
    private readonly Func<Building, string> family;

    /// <summary>模型族的基础名称。</summary>
    private readonly Func<string, string> baseFamily;

    /// <summary>实际朝向与镜像。</summary>
    private readonly Func<Building, int> facing;

    /// <summary>模型角度。</summary>
    private readonly Func<int, float> heading;

    /// <summary>外部实际四向变换，地基取样与显示保持一致。</summary>
    private readonly Func<Vector2, int, Vector2> turnPoint;

    /// <summary>废墟实际使用的固定镜像种子。</summary>
    private readonly Func<int, int, uint> hash;

    /// <summary>已有模型是否镜像。</summary>
    private readonly Func<Building, bool> mirrored;

    /// <summary>已绑定的模型和表面资料读取。</summary>
    private readonly Func<string, object> modelOf;

    /// <summary>模型的具名浮点资料。</summary>
    private readonly Func<object, string, float, float> infoFloat;

    /// <summary>模型的具名字符串资料。</summary>
    private readonly Func<object, string, string> info;

    /// <summary>模型水平宽度。</summary>
    private readonly Func<object, float> widthOf;

    /// <summary>模型真实包络，不以原版格数猜测三维体积。</summary>
    private readonly Func<object, Vector3> minOf, maxOf;

    /// <summary>按身体实际高度截取模型，使用外部已有碰撞面缓存。</summary>
    private readonly Func<object, float, float, float, float, float, float, bool, object> shapeOf;

    /// <summary>碰撞面距离，查询点不再反射或装箱数组。</summary>
    private readonly Func<object, Vector2, float> distance;

    /// <summary>原始记录仅留在主线程，换世界时一起释放。</summary>
    private readonly Dictionary<Building, Entry> entries = new();

    /// <summary>本轮活动区域内的实际建筑。</summary>
    private readonly List<Entry> active = new();

    /// <summary>一座建筑的实际记录及本轮固定摆放。</summary>
    private sealed class Entry
    {
        /// <summary>实际建筑；移除或对象池换人后立即停止使用旧模型。</summary>
        internal Building Owner;

        /// <summary>池化对象当前身份。</summary>
        internal long Id;

        /// <summary>独立记录、当前模型与原站立模型，不写入外部视图列表。</summary>
        internal object Near, Model, Standing;

        /// <summary>本轮位置、模型比例与朝向。</summary>
        internal float X, Y, Z, Scale, Heading;

        /// <summary>自然树木的水平比例可与高度不同。</summary>
        internal float HorizontalScale;

        /// <summary>没有模型时沿用外部矩形或圆形阻挡，不凭图片制造屋面。</summary>
        internal object Fallback;

        /// <summary>原图片可见上端，用于限制基础阻挡的实际高度。</summary>
        internal float Top;

        /// <summary>实际左右镜像。</summary>
        internal bool Mirror;

        /// <summary>模型水平外包，用于跳过远处建筑。</summary>
        internal Rect Bounds;

        /// <summary>记录变化时重采集，避免每帧为整个世界重复准备图片和模型。</summary>
        internal BuildingAsset Asset;

        /// <summary>上轮实际图片。</summary>
        internal Sprite Sprite;

        /// <summary>上轮地表及格子。</summary>
        internal AbilityHeightField Terrain;

        /// <summary>上轮实际格子。</summary>
        internal WorldTile Tile;

        /// <summary>上轮建筑状态及实际朝向。</summary>
        internal int Progress, Turn;

        /// <summary>上轮废墟与施工标记。</summary>
        internal bool Ruin, Site;
    }

    /// <summary>绑定已核对的外部入口；结构变化由可选模组安装边界报告。</summary>
    /// <param name="assembly">实际加载的外部模组。</param>
    internal WorldfallBodyObstacles(Assembly assembly)
    {
        // 依次绑定外部记录、制作资料及碰撞入口；频繁查询的几何调用最后编成直接委托。
        Type main = assembly.GetType("FirstPerson.WorldBoxMod", true),
            collector = assembly.GetType("FirstPerson.SceneCollector", true),
            models = assembly.GetType("FirstPerson.Models", true),
            turns = assembly.GetType("FirstPerson.Towns.Facing", true),
            mesh = assembly.GetType("FirstPerson.Core.MeshModel", true),
            colliders = assembly.GetType("FirstPerson.Colliders", true);
        nearType = collector.GetNestedType("NearBuilding", BindingFlags.NonPublic);
        foreach (FieldInfo field in nearType.GetFields(BindingFlags.Instance | BindingFlags.Public |
                                                       BindingFlags.NonPublic))
            fields.Add(field.Name, field);
        root = AccessTools.Property(main, "Instance");
        scene = AccessTools.Field(main, "_scene");
        sprites = AccessTools.Field(main, "_sprites");
        updateSprite = AccessTools.Method(collector, "UpdateBuildingSprite");
        ruinModel = AccessTools.Method(collector, "RuinModel");
        keptScale = AccessTools.Method(collector, "KeptScale");
        Type ground = assembly.GetType("FirstPerson.Core.BuildingGround", true);
        groundBase = AccessTools.Method(ground, "Base");
        groundPitch = AccessTools.Method(ground, "Pitch");
        Type site = assembly.GetType("FirstPerson.Core.BuildSite", true);
        stageOf = AccessTools.Method(site, "StageOf");
        siteAt = AccessTools.Method(site, "At", new[] { mesh, typeof(int), typeof(bool) });
        family = (Func<Building, string>)Delegate.CreateDelegate(typeof(Func<Building, string>),
            AccessTools.Method(models, "BuildingFamily", new[] { typeof(Building) }));
        baseFamily = (Func<string, string>)Delegate.CreateDelegate(typeof(Func<string, string>),
            AccessTools.Method(assembly.GetType("FirstPerson.Towns.TownModels", true), "Base"));
        facing = (Func<Building, int>)Delegate.CreateDelegate(typeof(Func<Building, int>),
            AccessTools.Method(turns, "Of"));
        heading = (Func<int, float>)Delegate.CreateDelegate(typeof(Func<int, float>),
            AccessTools.Method(turns, "Heading"));
        turnPoint = (Func<Vector2, int, Vector2>)Delegate.CreateDelegate(typeof(Func<Vector2, int, Vector2>),
            AccessTools.Method(turns, "Turn"));
        hash = (Func<int, int, uint>)Delegate.CreateDelegate(typeof(Func<int, int, uint>),
            AccessTools.Method(assembly.GetType("FirstPerson.Core.TerrainModel", true), "Hash"));
        mirrored = (Func<Building, bool>)Delegate.CreateDelegate(typeof(Func<Building, bool>),
            AccessTools.Method(models, "Mirrored"));
        var name = Expression.Parameter(typeof(string), "name");
        modelOf = Expression.Lambda<Func<string, object>>(Expression.Convert(Expression.Call(
            AccessTools.Method(models, "Get", new[] { typeof(string) }), name), typeof(object)), name).Compile();
        var value = Expression.Parameter(typeof(object), "model");
        var fallback = Expression.Parameter(typeof(float), "fallback");
        var cast = Expression.Convert(value, mesh);
        infoFloat = Expression.Lambda<Func<object, string, float, float>>(Expression.Call(cast,
            AccessTools.Method(mesh, "InfoFloat"), name, fallback), value, name, fallback).Compile();
        info = Expression.Lambda<Func<object, string, string>>(Expression.Call(cast,
            AccessTools.Method(mesh, "Info"), name), value, name).Compile();
        Expression min = Expression.Field(cast, "Min"), max = Expression.Field(cast, "Max");
        widthOf = Expression.Lambda<Func<object, float>>(Expression.Subtract(Expression.Field(max, "X"),
            Expression.Field(min, "X")), value).Compile();
        minOf = ReadVector(min);
        maxOf = ReadVector(max);

        Func<object, Vector3> ReadVector(Expression vector) => Expression.Lambda<Func<object, Vector3>>(
                Expression.New(typeof(Vector3).GetConstructor(new[] { typeof(float), typeof(float), typeof(float) }),
                    Expression.Field(vector, "X"), Expression.Field(vector, "Y"), Expression.Field(vector, "Z")), value)
            .Compile();

        // 碰撞查询不构造反射参数数组，实际模型及高度带仍交给外部已有计算。
        var args = new List<ParameterExpression> { value };
        foreach (string key in new[] { "x", "y", "heading", "scale", "lo", "hi" })
            args.Add(Expression.Parameter(typeof(float), key));
        args.Add(Expression.Parameter(typeof(bool), "mirror"));
        var callArgs = new List<Expression> { cast };
        for (int i = 1; i < args.Count; i++) callArgs.Add(args[i]);
        MethodInfo fromModel = AccessTools.Method(colliders, "FromModel");
        shapeOf = Expression.Lambda<Func<object, float, float, float, float, float, float, bool, object>>(
            Expression.Convert(Expression.Call(fromModel, callArgs), typeof(object)), args).Compile();
        var point = Expression.Parameter(typeof(Vector2), "point");
        var normal = Expression.Variable(typeof(Vector2), "normal");
        distance = Expression.Lambda<Func<object, Vector2, float>>(Expression.Block(new[] { normal },
            Expression.Call(AccessTools.Method(colliders, "Distance"), Expression.Convert(value, fromModel.ReturnType),
                point, normal)), value, point).Compile();
        BindSurfaces(assembly);
        BindPlacement(assembly, fromModel.ReturnType);
    }

    /// <summary>只采集实际活动区域；步法预测仍在主线程，工作线程不读取这些外部对象。</summary>
    internal void Install()
    {
        ConstructedAbilityService.CaptureBodyObstacles = Capture;
        BodyLocomotion.ObstacleAllowed = Allowed;
        BodyLocomotion.SurfaceHeight = Surface;
        BodyLocomotion.SurfaceContact = Contact;
        BodyLocomotion.SavedSurface = SavedSurface;
        BodyLocomotion.CeilingHeight = Ceiling;
        Cultiway.Patch.PatchMapBox.RegisterActionOnClearWorld(Clear);
    }

    /// <summary>换世界不保留旧建筑、池化身份或活动碰撞面。</summary>
    private void Clear()
    {
        entries.Clear();
        active.Clear();
        columns.Clear();
        surfaceFrames = Array.Empty<SurfaceFrame>();
    }

    /// <summary>刷新建筑元数据及实际地基，不开启场景绘制或触发房屋进入。</summary>
    /// <param name="area">本轮共同活动区域。</param><param name="terrain">同轮实际地表。</param>
    private void Capture(Rect area, AbilityHeightField terrain)
    {
        // 先冻结同轮环境，再依实际建筑更新元数据，最后只发布活动范围内的摆放。
        active.Clear();
        object main = root.GetValue(null);
        if (main == null || terrain == null)
        {
            surfaceFrames = Array.Empty<SurfaceFrame>();
            return;
        }

        object collector = scene.GetValue(main), images = sprites.GetValue(main);
        WorldfallVisualBridge.PrepareSprites(images);
        foreach (Building building in World.world.buildings.getSimpleList())
        {
            if (!building.isAlive() || building.chopped || building.current_tile == null) continue;
            int look = classify(building.asset);
            if (look == 1 || look == 4 || !solid(building)) continue;
            string special = specialFamily(building.asset.id), ordinary = family(building);
            WorldTile tile = building.current_tile;
            string kind = special ?? ordinary ?? (look == 0 ? treeFamily(building.asset.id,
                    (int)(hash(tile.x * 7, tile.y * 13) >> 4)) :
                building.asset.type == "type_bonfire" || building.asset.id == "bonfire" ? "bonfire" :
                look == 2 ? lumpFamily(building.asset.id) : null);
            object standing = kind == null ? null : modelOf(kind);
            if (!entries.TryGetValue(building, out Entry entry) || entry.Id != building.id)
            {
                entry = new Entry
                    { Id = building.id, Owner = building, Near = Activator.CreateInstance(nearType, true) };
                entries[building] = entry;
            }

            Sprite sprite = building.checkSpriteToRender();
            bool ruin = building.isRuin(), site = building.isUnderConstruction();
            int progress = site ? building.getConstructionProgress() : -1, turn = facing(building);
            if ((entry.Model != null || entry.Fallback != null) && entry.Standing == standing &&
                entry.Asset == building.asset &&
                entry.Sprite == sprite && entry.Terrain == terrain &&
                entry.Tile == building.current_tile && entry.Progress == progress && entry.Turn == turn &&
                entry.Ruin == ruin && entry.Site == site)
            {
                if (entry.Bounds.Overlaps(area)) active.Add(entry);
                continue;
            }

            if (entry.Tile != building.current_tile) Set(entry.Near, "DockHeading", float.NaN);
            entry.Asset = building.asset;
            entry.Sprite = sprite;
            entry.Terrain = terrain;
            entry.Tile = building.current_tile;
            entry.Progress = progress;
            entry.Turn = turn;
            entry.Ruin = ruin;
            entry.Site = site;
            entry.Model = null;
            entry.Fallback = null;
            entry.Standing = standing;
            // 状态发生变化才重新取得图片、施工阶段或废墟；未变化的建筑保留已采集结果。
            object near = entry.Near;
            string priorFamily = Get<string>(near, "MeshFamily");
            if (priorFamily != null && priorFamily != kind) Set(near, "ModelScale", 0f);
            Set(near, "Building", building);
            Set(near, "MeshFamily", kind);
            Set(near, "Look", Enum.ToObject(fields["Look"].FieldType, look));
            Set(near, "Angle", (hash(tile.x, tile.y) & 1023) / 1023f * Mathf.PI * 2f);
            BuildingFundament baseTiles = building.asset.fundament;
            float depth = baseTiles == null ? 1 : Mathf.Max(1, baseTiles.top + baseTiles.bottom + 1);
            float offset = baseTiles == null ? 0 : (baseTiles.top - baseTiles.bottom) * .5f;
            Set(near, "DepthTiles", depth);
            Set(near, "CenterOffset", offset);
            updateSprite.Invoke(collector, new[] { near, images });
            if (Get<object>(near, "Image") == null || !Get<bool>(near, "Solid")) continue;
            if (standing == null)
            {
                // 外部没有三维模型时仍有原生基础阻挡；不把图片上端当作可站立屋面。
                float width = Get<float>(near, "Width"), y = tile.posV3.y + (look == 3 ? offset : 0);
                float radius = Get<float>(near, "Radius");
                entry.X = tile.posV3.x;
                entry.Y = y;
                entry.Z = terrain.Sample(entry.X, y);
                entry.Top = entry.Z + Get<float>(near, "Height") * visible(Get<object>(near, "Image"));
                entry.Fallback = fallbackShape(entry.X, y, width * .47f, depth * .47f, radius, look == 3);
                entry.Bounds = look == 3
                    ? new Rect(entry.X - width * .47f, y - depth * .47f, width * .94f, depth * .94f)
                    : new Rect(entry.X - radius, y - radius, radius * 2, radius * 2);
            }
            else
            {
                if (ordinary == null || special != null)
                    PlaceNatural(entry, collector, kind, look, special != null, terrain);
                else
                {
                    object model = ruin ? ruinModel.Invoke(null, new[] { near }) ?? standing : standing;
                    if (site)
                    {
                        object stages = modelOf("site_" + kind) ?? modelOf("site_" + baseFamily(kind));
                        if (stages == null) continue;
                        object stage = stageOf.Invoke(null,
                            new object[]
                                { building.getConstructionProgress(), building.asset.construction_progress_needed });
                        model = siteAt.Invoke(null, new[] { stages, stage, false });
                    }

                    float width = Get<float>(near, "Width"), heldWidth = Get<float>(near, "StandingWidth");
                    if ((site || ruin && info(model, "ruin") == "1") && heldWidth > 0) width = heldWidth;
                    object sizeModel = site ? standing : model;
                    float scale = (float)keptScale.Invoke(null, new object[]
                    {
                        near, building, ruin,
                        width / Mathf.Max(.5f, infoFloat(sizeModel, "width", widthOf(sizeModel))), false
                    });
                    float x = building.current_tile.posV3.x, y = building.current_tile.posV3.y + offset;
                    float ground = terrain.Sample(x, y), dx = width * .4f, dy = depth * .4f;
                    float bottom = Mathf.Min(ground,
                        Mathf.Min(Mathf.Min(terrain.Sample(x - dx, y - dy), terrain.Sample(x + dx, y - dy)),
                            Mathf.Min(terrain.Sample(x - dx, y + dy), terrain.Sample(x + dx, y + dy))));
                    float footing = infoFloat(sizeModel, "footing", 0);
                    float angle = heading(turn);
                    if (footing > 0)
                    {
                        Vector2 front = new Vector2(x, y) + turnPoint(new Vector2(0, -dy), turn);
                        bottom = info(sizeModel, "kind") == "tent"
                            ? (float)groundPitch.Invoke(null, new object[] { bottom, ground })
                            : (float)groundBase.Invoke(null,
                                new object[] { bottom, terrain.Sample(front.x, front.y), ground, footing, scale });
                    }

                    bool dock = info(sizeModel, "kind") == "docks";
                    if (dock)
                    {
                        float dockHeading = Get<float>(near, "DockHeading");
                        if (float.IsNaN(dockHeading)) Set(near, "DockHeading", dockHeading = TowardsWater(main, x, y));
                        angle = dockHeading;
                        bottom = Mathf.Max(0, Mathf.Min(terrain.Sample(x, y), .6f));
                    }

                    entry.Model = model;
                    entry.X = x;
                    entry.Y = y;
                    entry.Z = bottom - .05f;
                    entry.Scale = entry.HorizontalScale = scale;
                    entry.Heading = angle;
                    entry.Mirror = ruin && info(model, "ruin") == "1" && !dock
                        ? ((hash(building.current_tile.x * 5 + 1, building.current_tile.y * 7 + 3) >> 9) & 1) != 0
                        : !ruin && footing > 0 && mirrored(building);
                }

                Vector3 min = minOf(entry.Model), max = maxOf(entry.Model);
                float extent = Mathf.Max(new Vector2(min.x, min.y).magnitude, new Vector2(max.x, max.y).magnitude) *
                               entry.HorizontalScale;
                extent = Mathf.Max(extent, new Vector2(min.x, max.y).magnitude * entry.HorizontalScale,
                    new Vector2(max.x, min.y).magnitude * entry.HorizontalScale);
                entry.Bounds = new Rect(entry.X - extent, entry.Y - extent, extent * 2, extent * 2);
            }

            if (entry.Bounds.Overlaps(area)) active.Add(entry);
        }

        PublishSurfaces();
    }

    /// <summary>身体候选点按实际高度截取模型，允许已经重叠的人物向外离开。</summary>
    /// <param name="from">已确认脚下。</param><param name="to">候选脚下。</param><param name="height">实际体高。</param>
    private bool Allowed(Vector3 from, Vector3 to, float height)
    {
        foreach (Entry entry in active)
        {
            if (!entry.Owner.isAlive() || entry.Owner.id != entry.Id || entry.Owner.chopped) continue;
            float radius = height * .16f;
            Rect bounds = entry.Bounds;
            bounds.xMin -= radius;
            bounds.xMax += radius;
            bounds.yMin -= radius;
            bounds.yMax += radius;
            if (!bounds.Contains(to)) continue;
            float next = Distance(entry, to, height);
            if (next < radius && next < Distance(entry, from, height) - .000001f) return false;
        }

        return true;
    }

    /// <summary>真实身体所占高度带，第一次遇到模型也同步生成阻挡，不等镜头预热。</summary>
    /// <param name="entry">实际摆放。</param><param name="point">脚下。</param><param name="height">实际体高。</param>
    private float Distance(Entry entry, Vector3 point, float height)
    {
        if (entry.Fallback != null)
            return point.z + height * .2f >= entry.Top || point.z + height * 1.04f <= entry.Z
                ? float.PositiveInfinity
                : distance(entry.Fallback, point);
        float lo = Mathf.Floor((point.z - entry.Z + height * .2f) * 20) / (20 * entry.Scale);
        float hi = Mathf.Ceil((point.z - entry.Z + height * 1.04f) * 20) / (20 * entry.Scale);
        object shape = shapeOf(entry.Model, entry.X, entry.Y, entry.Heading, entry.HorizontalScale, lo, hi,
            entry.Mirror);
        return distance(shape, point);
    }

    /// <summary>读取已解析的外部字段。</summary>
    /// <typeparam name="T">实际字段类型。</typeparam><param name="value">外部记录。</param><param name="name">字段。</param>
    private T Get<T>(object value, string name) => (T)fields[name].GetValue(value);

    /// <summary>写入独立采集记录，外部视图记录保持原样。</summary>
    /// <param name="target">独立记录。</param><param name="name">字段。</param><param name="value">实际值。</param>
    private void Set(object target, string name, object value) => fields[name].SetValue(target, value);
}