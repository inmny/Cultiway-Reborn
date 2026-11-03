using System;
using System.Linq.Expressions;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Cultiway.Content.Compatibility.Worldfall;

/// <summary>自然物、特殊物件和船坞的实际摆放；不调用绘制或向外部视图登记碰撞。</summary>
internal sealed partial class WorldfallBodyObstacles
{
    /// <summary>外部实际外观分类和实体阻挡规则。</summary>
    private Func<BuildingAsset, int> classify;

    /// <summary>草花等非实体装饰不参与身体阻挡。</summary>
    private Func<Building, bool> solid;

    /// <summary>特殊物件与自然物的原有模型选择。</summary>
    private Func<string, string> specialFamily, lumpFamily;

    /// <summary>按实际格子选择树种变体。</summary>
    private Func<string, int, string> treeFamily;

    /// <summary>图片实际可见高度。</summary>
    private Func<object, float> visible;

    /// <summary>图片实际不透明宽度。</summary>
    private Func<object, float> opaqueWidth;

    /// <summary>自然模型的比例规则。</summary>
    private Func<string, bool> naturalPalette;

    /// <summary>石块和灌木原有高度比例。</summary>
    private Func<string, float> lumpHeight;

    /// <summary>原有焦树模型选择，仅改独立记录，不运行烟雾等显示。</summary>
    private MethodInfo burnt;

    /// <summary>当前世界的实际水域标记，只在主线程读取。</summary>
    private Func<object, byte[]> waterFlags;

    /// <summary>无模型物件沿用原有矩形或圆形碰撞。</summary>
    private Func<float, float, float, float, float, bool, object> fallbackShape;

    /// <summary>只在安装边界解析原有入口，不在每个碰撞点反射。</summary>
    /// <param name="assembly">实际外部程序集。</param><param name="collider">原有基础碰撞类型。</param>
    private void BindPlacement(Assembly assembly, Type collider)
    {
        Type style = assembly.GetType("FirstPerson.AssetStyle", true),
            models = assembly.GetType("FirstPerson.Models", true),
            palette = assembly.GetType("FirstPerson.Core.NaturePalette", true),
            image = assembly.GetType("FirstPerson.Core.SpriteImage", true),
            main = assembly.GetType("FirstPerson.WorldBoxMod", true);
        var asset = Expression.Parameter(typeof(BuildingAsset), "asset");
        classify = Expression.Lambda<Func<BuildingAsset, int>>(Expression.Convert(Expression.Call(
            AccessTools.Method(style, "Classify"), asset), typeof(int)), asset).Compile();
        solid = (Func<Building, bool>)Delegate.CreateDelegate(typeof(Func<Building, bool>),
            AccessTools.Method(style, "IsSolid"));
        specialFamily = (Func<string, string>)Delegate.CreateDelegate(typeof(Func<string, string>),
            AccessTools.Method(models, "SpecialFamily"));
        lumpFamily = (Func<string, string>)Delegate.CreateDelegate(typeof(Func<string, string>),
            AccessTools.Method(models, "LumpFamily"));
        treeFamily = (Func<string, int, string>)Delegate.CreateDelegate(typeof(Func<string, int, string>),
            AccessTools.Method(models, "TreeFamily"));
        naturalPalette = (Func<string, bool>)Delegate.CreateDelegate(typeof(Func<string, bool>),
            AccessTools.Method(palette, "Handles"));
        lumpHeight = (Func<string, float>)Delegate.CreateDelegate(typeof(Func<string, float>),
            AccessTools.Method(palette, "LumpHeight"));
        var value = Expression.Parameter(typeof(object), "value");
        visible = Expression.Lambda<Func<object, float>>(Expression.Property(Expression.Convert(value, image),
            "VisibleFraction"), value).Compile();
        opaqueWidth = Expression.Lambda<Func<object, float>>(Expression.Call(
            AccessTools.Method(models, "OpaqueWidth"), Expression.Convert(value, image)), value).Compile();
        burnt = AccessTools.Method(assembly.GetType("FirstPerson.SceneCollector", true), "Burnt");
        Expression cache = Expression.Field(Expression.Field(Expression.Convert(value, main),
            AccessTools.Field(main, "_sampler")), "Cache");
        waterFlags = Expression.Lambda<Func<object, byte[]>>(Expression.Field(cache, "Flags"), value).Compile();
        var x = Expression.Parameter(typeof(float), "x");
        var y = Expression.Parameter(typeof(float), "y");
        var width = Expression.Parameter(typeof(float), "width");
        var depth = Expression.Parameter(typeof(float), "depth");
        var radius = Expression.Parameter(typeof(float), "radius");
        var box = Expression.Parameter(typeof(bool), "box");
        fallbackShape = Expression.Lambda<Func<float, float, float, float, float, bool, object>>(
            Expression.Convert(Expression.MemberInit(Expression.New(collider),
                    Expression.Bind(collider.GetField("X"), x), Expression.Bind(collider.GetField("Y"), y),
                    Expression.Bind(collider.GetField("HalfW"), width),
                    Expression.Bind(collider.GetField("HalfD"), depth),
                    Expression.Bind(collider.GetField("Radius"), radius),
                    Expression.Bind(collider.GetField("Box"), box)),
                typeof(object)), x, y, width, depth, radius, box).Compile();
    }

    /// <summary>自然物按原图尺寸和原模型比例摆放；树的水平收窄不改变竖向高度。</summary>
    /// <param name="entry">当前独立记录。</param><param name="collector">原有元数据读取者。</param>
    /// <param name="kind">实际模型族。</param><param name="look">原有外观分类。</param>
    /// <param name="special">是否为特殊物件。</param><param name="terrain">共同实际地表。</param>
    private void PlaceNatural(Entry entry, object collector, string kind, int look, bool special,
        Core.Skills.AbilityHeightField terrain)
    {
        object near = entry.Near, model = entry.Standing;
        float x = entry.Owner.current_tile.posV3.x,
            y = entry.Owner.current_tile.posV3.y + (special ? Get<float>(near, "CenterOffset") : 0);
        float width = Get<float>(near, "Width"),
            depth = Get<float>(near, "DepthTiles"),
            pictureHeight = Get<float>(near, "Height");
        object image = Get<object>(near, "Image");
        bool charred = false;
        if (look == 0 && entry.Ruin)
        {
            object[] args = { near, entry.Owner, kind };
            charred = (bool)burnt.Invoke(collector, args);
            if (charred) model = modelOf((string)args[2]) ?? model;
        }

        float scale, horizontal, z;
        if (special)
        {
            bool vent = kind == "nature_volcano" || kind == "nature_geyser" || kind == "nature_geyser_acid";
            scale = vent
                ? 1
                : (float)keptScale.Invoke(null, new object[]
                {
                    near, entry.Owner, false,
                    Mathf.Min(
                        pictureHeight * visible(image) / Mathf.Max(.2f, infoFloat(model, "height", maxOf(model).z)),
                        width * opaqueWidth(image) / Mathf.Max(.2f, infoFloat(model, "width", widthOf(model)))),
                    true
                });
            float dx = vent
                ? infoFloat(model, "radius", Mathf.Max(maxOf(model).x, maxOf(model).y)) * .55f
                : width * .3f;
            float dy = vent ? dx : depth * .3f;
            z = BaseHeight(terrain, x, y, dx, dy) - (vent ? .1f : .05f);
            horizontal = scale;
            entry.Heading = -Mathf.PI * .5f;
        }
        else
        {
            bool fire = kind == "bonfire";
            float maxHeight = maxOf(model).z;
            scale = fire ? width / 2.1f :
                look == 0 ? pictureHeight * visible(image) / Mathf.Max(.5f, maxHeight) :
                pictureHeight * visible(image) * (naturalPalette(kind) ? lumpHeight(kind) : 1.35f) /
                Mathf.Max(.3f, maxHeight);
            WorldTile tile = entry.Owner.current_tile;
            if (!fire) scale *= .9f + .2f * ((hash(tile.x, tile.y) >> 12) & 255) / 255f;
            horizontal = look == 0 && pictureHeight <= 15.5f ? scale * .79999995f : scale;
            z = terrain.Sample(x, y);
            entry.Heading = Get<float>(near, "Angle");
        }

        entry.Model = model;
        entry.X = x;
        entry.Y = y;
        entry.Z = z;
        entry.Scale = scale;
        entry.HorizontalScale = horizontal;
        entry.Mirror = false;
    }

    /// <summary>同原绘制入口，取中心及四角中的最低地基。</summary>
    /// <param name="terrain">本轮地表。</param><param name="x">中心横坐标。</param><param name="y">中心纵坐标。</param>
    /// <param name="dx">水平取样偏移。</param><param name="dy">纵深取样偏移。</param>
    private static float BaseHeight(Core.Skills.AbilityHeightField terrain, float x, float y, float dx, float dy) =>
        Mathf.Min(terrain.Sample(x, y), Mathf.Min(
            Mathf.Min(terrain.Sample(x - dx, y - dy), terrain.Sample(x + dx, y - dy)),
            Mathf.Min(terrain.Sample(x - dx, y + dy), terrain.Sample(x + dx, y + dy))));

    /// <summary>复用原有十六向水域评分，查询当前世界标记，不依赖镜头的船坞缓存。</summary>
    /// <param name="main">当前外部根。</param><param name="x">船坞中心横坐标。</param><param name="y">船坞中心纵坐标。</param>
    private float TowardsWater(object main, float x, float y)
    {
        byte[] flags = waterFlags(main);
        int best = -1;
        float result = -Mathf.PI * .5f;
        for (int i = 0; i < 16; i++)
        {
            float angle = i * Mathf.PI / 8f;
            int score = 0;
            for (int j = 2; j <= 9; j++)
            {
                int tx = (int)(x + Mathf.Cos(angle) * j), ty = (int)(y + Mathf.Sin(angle) * j);
                if (tx >= 0 && ty >= 0 && tx < MapBox.width && ty < MapBox.height &&
                    (flags[ty * MapBox.width + tx] & 1) != 0)
                    score += 10 - j;
            }

            if (score <= best) continue;
            best = score;
            result = angle;
        }

        return result;
    }
}