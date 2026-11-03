using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using Cultiway.Content.Skills;
using HarmonyLib;
using UnityEngine;

namespace Cultiway.Content.Compatibility.Worldfall;

/// <summary>从同一实际模型查询可站立表面和顶棚，不使用外部限时的视图摆放列表。</summary>
internal sealed partial class WorldfallBodyObstacles
{
    /// <summary>主线程登记不可变模型的数值计算；同模型仅首次求值一次，不在查询线程访问字典。</summary>
    private readonly Dictionary<object, Lazy<object>> columns = new();

    /// <summary>一次性发布完整摆放数组，原版并行下落只读取自己的这份快照。</summary>
    private volatile SurfaceFrame[] surfaceFrames = Array.Empty<SurfaceFrame>();

    /// <summary>不含建筑或图片对象的固定摆放；列资料仅从外部不可变网格数字生成。</summary>
    private readonly struct SurfaceFrame
    {
        /// <summary>不可变模型版本，仅用于主线程比较。</summary>
        internal readonly object Model;

        /// <summary>稳定建筑身份，工作线程不读取原建筑对象。</summary>
        internal readonly long Owner;

        /// <summary>同一模型只执行一次的纯数值计算。</summary>
        internal readonly Lazy<object> Column;

        /// <summary>本次摆放的完整数值。</summary>
        internal readonly float X, Y, Z, Scale, HorizontalScale, Heading;

        /// <summary>实际镜像。</summary>
        internal readonly bool Mirror;

        /// <summary>世界水平包络。</summary>
        internal readonly Rect Bounds;

        /// <summary>只在主线程从实际建筑冻结摆放。</summary>
        /// <param name="entry">主线程记录。</param><param name="column">纯数字列资料。</param>
        internal SurfaceFrame(Entry entry, Lazy<object> column)
        {
            Owner = entry.Id;
            Model = entry.Model;
            Column = column;
            X = entry.X;
            Y = entry.Y;
            Z = entry.Z;
            Scale = entry.Scale;
            HorizontalScale = entry.HorizontalScale;
            Heading = entry.Heading;
            Mirror = entry.Mirror;
            Bounds = entry.Bounds;
        }

        /// <summary>未发生变化时复用整份数组，不为静止屋面每帧分配相同快照。</summary>
        /// <param name="entry">本轮主线程记录。</param>
        internal bool Matches(Entry entry) => Owner == entry.Id && ReferenceEquals(Model, entry.Model) &&
                                              X == entry.X && Y == entry.Y &&
                                              Z == entry.Z && Scale == entry.Scale &&
                                              HorizontalScale == entry.HorizontalScale && Heading == entry.Heading &&
                                              Mirror == entry.Mirror && Bounds == entry.Bounds;
    }

    /// <summary>原有模型到列资料的直接入口。</summary>
    private Func<object, float, object> columnsOf;

    /// <summary>查询实际支撑高度，未取得支撑时返回负无穷。</summary>
    private Func<object, float, float, float, float, float, float, float> supportOn;

    /// <summary>查询身体上方的实际顶棚，无顶棚时返回正无穷。</summary>
    private Func<object, float, float, float, float, float> ceilingOn;

    /// <summary>先绑定模型列资料，再将带输出参数的查询编成无反射参数数组的委托。</summary>
    /// <param name="assembly">实际外部程序集。</param>
    private void BindSurfaces(Assembly assembly)
    {
        // 实际表面算法仍由外部提供，这里只转换原类型与高度返回值。
        Type model = assembly.GetType("FirstPerson.Core.MeshModel", true),
            columnType = assembly.GetType("FirstPerson.Core.ColumnField", true);
        var value = Expression.Parameter(typeof(object), "value");
        var cell = Expression.Parameter(typeof(float), "cell");
        columnsOf = Expression.Lambda<Func<object, float, object>>(Expression.Convert(Expression.Call(
                AccessTools.Method(columnType, "OfModel"), Expression.Convert(value, model), cell), typeof(object)),
            value, cell).Compile();
        var x = Expression.Parameter(typeof(float), "x");
        var y = Expression.Parameter(typeof(float), "y");
        var radius = Expression.Parameter(typeof(float), "radius");
        var maximum = Expression.Parameter(typeof(float), "maximum");
        var tolerance = Expression.Parameter(typeof(float), "tolerance");
        var keep = Expression.Parameter(typeof(float), "keep");
        var z = Expression.Variable(typeof(float), "z");
        var kind = Expression.Variable(typeof(byte), "kind");
        MethodInfo support = AccessTools.Method(columnType, "Support", new[]
        {
            typeof(float), typeof(float), typeof(float), typeof(float), typeof(float), typeof(float),
            typeof(float).MakeByRefType(), typeof(byte).MakeByRefType()
        });
        supportOn = Expression.Lambda<Func<object, float, float, float, float, float, float, float>>(
            Expression.Block(new[] { z, kind }, Expression.Condition(Expression.Call(
                    Expression.Convert(value, columnType),
                    support, x, y, radius, maximum, tolerance, keep, z, kind), z,
                Expression.Constant(float.NegativeInfinity))), value, x, y, radius, maximum, tolerance, keep).Compile();
        ceilingOn = Expression.Lambda<Func<object, float, float, float, float, float>>(Expression.Call(
                Expression.Convert(value, columnType), AccessTools.Method(columnType, "CeilingAt"), x, y, radius,
                maximum),
            value, x, y, radius, maximum).Compile();
    }

    /// <summary>按原有建筑表面采样密度建立实际模型列资料，不等待显示缓存。</summary>
    /// <param name="entry">本轮实际模型摆放。</param>
    private Lazy<object> Columns(Entry entry)
    {
        if (columns.TryGetValue(entry.Model, out Lazy<object> value)) return value;
        object model = entry.Model;
        Vector3 span = maxOf(model) - minOf(model);
        float cell = Mathf.Max(.1f, Mathf.Max(span.x, span.y) / 220f);
        value = new Lazy<object>(() => columnsOf(model, cell), LazyThreadSafetyMode.ExecutionAndPublication);
        columns.Add(model, value);
        return value;
    }

    /// <summary>主线程准备完整数值摆放，然后一次替换；查询不读取可变列表或游戏对象。</summary>
    private void PublishSurfaces()
    {
        SurfaceFrame[] previous = surfaceFrames;
        bool same = previous.Length == active.Count;
        for (int i = 0; same && i < previous.Length; i++) same = previous[i].Matches(active[i]);
        if (same) return;
        var next = new SurfaceFrame[active.Count];
        for (int i = 0; i < next.Length; i++)
            next[i] = new SurfaceFrame(active[i], active[i].Model == null ? null : Columns(active[i]));
        surfaceFrames = next;
    }

    /// <summary>最高可承托表面，实际返回高度不越过调用者允许的到达高度。</summary>
    /// <param name="point">世界脚下。</param><param name="radius">世界支撑半径。</param>
    /// <param name="maximum">本次能到达的最高表面。</param>
    private float Surface(Vector3 point, float radius, float maximum) => Contact(point, radius, maximum).Height;

    /// <summary>查询高度时一起保留稳定来源，存读档不依赖模型名称或视图记录。</summary>
    /// <param name="point">世界脚下。</param><param name="radius">实际半径。</param>
    /// <param name="maximum">最高可达表面。</param>
    private BodySurfaceContact Contact(Vector3 point, float radius, float maximum)
    {
        float result = float.NegativeInfinity;
        float localHeight = 0;
        long found = 0;
        foreach (SurfaceFrame entry in surfaceFrames)
        {
            if (entry.Column == null || !Near(entry, point, radius)) continue;
            Vector2 local = Local(entry, point);
            float support = supportOn(entry.Column.Value, local.x, local.y, radius / entry.HorizontalScale,
                (maximum - entry.Z) / entry.Scale, .03f / entry.Scale, (point.z - entry.Z) / entry.Scale);
            float z = entry.Z + support * entry.Scale;
            if (z <= maximum + .0001f && z > result)
            {
                result = z;
                found = entry.Owner;
                localHeight = support;
            }
        }

        return new BodySurfaceContact(found, result, localHeight);
    }

    /// <summary>恢复同一建筑内的实际表面，地基变化不被当成屋面消失；变形超出身体余量时拒绝。</summary>
    /// <param name="point">人物的世界平面位置。</param><param name="radius">人物实际支撑半径。</param>
    /// <param name="owner">正式保存的建筑身份。</param><param name="localHeight">正式保存的模型内高度。</param>
    private BodySurfaceContact SavedSurface(Vector2 point, float radius, long owner, float localHeight)
    {
        foreach (SurfaceFrame entry in surfaceFrames)
        {
            if (entry.Owner != owner || entry.Column == null || !Near(entry, point, radius)) continue;
            Vector2 local = Local(entry, point);
            // 半径为体高的0.16，允许的变化沿用体高的0.2，不把其他楼层冒充原承托。
            float tolerance = radius * 1.25f / entry.Scale;
            float support = supportOn(entry.Column.Value, local.x, local.y, radius / entry.HorizontalScale,
                localHeight + tolerance, .03f / entry.Scale, localHeight);
            if (Mathf.Abs(support - localHeight) <= tolerance)
                return new BodySurfaceContact(owner, entry.Z + support * entry.Scale, support);
        }

        return new BodySurfaceContact(0, float.NegativeInfinity);
    }

    /// <summary>实际身体上方最近的模型下表面。</summary>
    /// <param name="point">世界脚下。</param><param name="radius">世界身体半径。</param>
    /// <param name="above">原身体上端，不把原有重叠当作新的上方碰撞。</param>
    private float Ceiling(Vector3 point, float radius, float above)
    {
        float result = float.PositiveInfinity;
        foreach (SurfaceFrame entry in surfaceFrames)
        {
            if (entry.Column == null || !Near(entry, point, radius)) continue;
            Vector2 local = Local(entry, point);
            float z = ceilingOn(entry.Column.Value, local.x, local.y, radius / entry.HorizontalScale,
                (above - entry.Z) / entry.Scale);
            result = Mathf.Min(result, entry.Z + z * entry.Scale);
        }

        return result;
    }

    /// <summary>只用已发布的数值包络筛选，不在原版工作线程读取建筑存活状态。</summary>
    /// <param name="entry">实际摆放。</param><param name="point">世界点。</param><param name="radius">支撑半径。</param>
    private static bool Near(SurfaceFrame entry, Vector3 point, float radius)
    {
        Rect bounds = entry.Bounds;
        bounds.xMin -= radius;
        bounds.xMax += radius;
        bounds.yMin -= radius;
        bounds.yMax += radius;
        return bounds.Contains(point);
    }

    /// <summary>与外部模型摆放相同的逆变换，包含四向朝向和镜像。</summary>
    /// <param name="entry">实际摆放。</param><param name="point">世界点。</param>
    private static Vector2 Local(SurfaceFrame entry, Vector3 point)
    {
        float cos = Mathf.Cos(entry.Heading + Mathf.PI * .5f), sin = Mathf.Sin(entry.Heading + Mathf.PI * .5f);
        float x = point.x - entry.X, y = point.y - entry.Y;
        return new Vector2((x * cos + y * sin) / entry.HorizontalScale * (entry.Mirror ? -1 : 1),
            (-x * sin + y * cos) / entry.HorizontalScale);
    }
}