using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>实际地块连通与本轮地表的只读输入，工作线程不访问游戏对象。</summary>
public sealed class PropagationGround
{
    /// <summary>地块中心的一或零分别表示可传力地面和水隙。</summary>
    internal readonly AbilityHeightField Land;
    /// <summary>同一轮使用的实际地表，原版为空零平面。</summary>
    internal readonly AbilityHeightField Terrain;
    /// <summary>绑定一份已冻结地块和地表。</summary>
    /// <param name="land">连通资料。</param><param name="terrain">实际高度。</param>
    internal PropagationGround(AbilityHeightField land, AbilityHeightField terrain) { Land = land; Terrain = terrain; }
    /// <summary>地图内真实可传力地块。</summary><param name="x">横格。</param><param name="y">纵格。</param>
    internal bool Contains(int x, int y) => x >= 0 && y >= 0 && x < Land.Width && y < Land.Height && Land.Sample(x + .5f, y + .5f) > .5f;
    /// <summary>地块中心实际高度。</summary><param name="x">横格。</param><param name="y">纵格。</param>
    internal float Height(int x, int y) => Terrain?.Sample(x + .5f, y + .5f) ?? 0f;
}

/// <summary>共享只读路线与少量单次推进状态；预测撤销只撤销本步新增的断路记录。</summary>
internal sealed class AbilityPropagation
{
    /// <summary>一格真实到达的地面和路程，不以直线距离代替绕行。</summary>
    internal readonly struct Cell
    {
        /// <summary>地块坐标及已到达上游在路线中的位置。</summary>
        internal readonly int X, Y, Parent;
        /// <summary>实际路程、到达时间及余下强度倍率。</summary>
        internal readonly float Distance, At, Strength;
        /// <summary>复制已经确认的路线。</summary>
        /// <param name="x">真实横格。</param><param name="y">真实纵格。</param>
        /// <param name="parent">上游路线位置。</param><param name="distance">实际路程。</param>
        /// <param name="speed">实际传播速度。</param><param name="attenuation">每格路程的衰减。</param>
        internal Cell(int x, int y, int parent, float distance, float speed, float attenuation)
        {
            X = x; Y = y; Parent = parent; Distance = distance;
            // 只向后对齐，避免微小地面起伏为每一格制造整场重算；时间及路程的精度上限同时成立。
            double tick = Math.Min(1d / 240d, 1d / (16d * speed));
            At = (float)(Math.Ceiling((double)distance / speed / tick) * tick);
            Strength = Mathf.Exp(-distance * attenuation);
        }
    }

    /// <summary>只在实际开始时建立的有序路线，之后不改写。</summary>
    internal readonly Cell[] Route;
    /// <summary>同一已检查参数及实际开始年龄。</summary>
    internal readonly CompiledPropagation Settings;
    /// <summary>实际开始年龄及输入尺寸。</summary>
    internal readonly float StartedAt, Scale;
    /// <summary>稳定地块编号到路线位置，不为每次命中遍历整张地图。</summary>
    private readonly Dictionary<int, int> index = new();
    /// <summary>实际地块行宽。</summary>
    internal readonly int Width;
    /// <summary>已经断开的分支和可撤销的新增记录。</summary>
    private readonly HashSet<int> blocked = new();
    /// <summary>断路的确认顺序，回退只移除本步新增内容。</summary>
    private readonly List<int> blockedJournal = new();
    /// <summary>已经到达的路线边界及本次预测起点。</summary>
    private int cursor, savedCursor, savedJournal;
    /// <summary>当前过程年龄及预测前态。</summary>
    internal float Age;
    /// <summary>预测前态的过程年龄。</summary>
    private float savedAge;
    /// <summary>地面连通变化时重新核对本步已经到达的作用面。</summary>
    private PropagationGround ground;
    /// <summary>预测前态的地面。</summary>
    private PropagationGround savedGround;

    /// <summary>先建立真实入口，再按最短地面路径有限展开；耗量上限在展开时就限制规模。</summary>
    /// <param name="settings">已检查参数。</param><param name="surface">真实介质。</param>
    /// <param name="origin">实际接触点。</param><param name="forward">实际出手前向。</param>
    /// <param name="scale">输入的共同尺寸。</param><param name="force">实际余力。</param><param name="at">开始年龄。</param>
    internal AbilityPropagation(CompiledPropagation settings, PropagationGround surface, Vector3 origin, Vector3 forward, float scale, float force, float at)
    {
        Settings = settings; ground = surface; StartedAt = at; Scale = scale;
        Width = surface?.Land.Width ?? 0;
        var route = new List<Cell>();
        int sx = Mathf.FloorToInt(origin.x), sy = Mathf.FloorToInt(origin.y);
        if (surface == null || !surface.Contains(sx, sy) ||
            Mathf.Abs(origin.z - surface.Height(sx, sy)) > .02f) { Route = route.ToArray(); return; }
        Vector2 direction = ((Vector2)forward).normalized;
        var queue = new SortedSet<(float Distance, int Id, int Parent)>();
        var distances = new Dictionary<int, float>();
        var visited = new HashSet<int>();
        int seed = sx + sy * Width;
        distances.Add(seed, 0f); queue.Add((0f, seed, -1));
        int maximum = Math.Min(settings.MaximumCells, (int)Math.Min(settings.MaximumCells, Math.Floor(force / settings.CellCost)));
        while (queue.Count > 0 && route.Count < maximum)
        {
            var next = queue.Min; queue.Remove(next);
            if (!visited.Add(next.Id)) continue;
            int x = next.Id % Width, y = next.Id / Width, parent = route.Count;
            index.Add(next.Id, parent);
            route.Add(new Cell(x, y, next.Parent, next.Distance, settings.Speed * scale, settings.Attenuation / scale));
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                int nx = x + dx, ny = y + dy, id = nx + ny * Width;
                if (!surface.Contains(nx, ny) || visited.Contains(id) ||
                    dx != 0 && dy != 0 && (!surface.Contains(nx, y) || !surface.Contains(x, ny))) continue;
                Vector2 radial = new(nx - sx, ny - sy);
                if (radial.sqrMagnitude > 0f && Vector2.Dot(radial.normalized, direction) < settings.MinimumDot) continue;
                float rise = surface.Height(nx, ny) - surface.Height(x, y);
                if (Mathf.Abs(rise) > settings.MaximumStep * scale) continue;
                if (dx != 0 && dy != 0 &&
                    (Mathf.Abs(surface.Height(nx, y) - surface.Height(x, y)) > settings.MaximumStep * scale ||
                     Mathf.Abs(surface.Height(x, ny) - surface.Height(x, y)) > settings.MaximumStep * scale)) continue;
                float distance = next.Distance + Mathf.Sqrt(dx * dx + dy * dy + rise * rise);
                if (distance > settings.Range * scale || distances.TryGetValue(id, out float prior) && prior <= distance) continue;
                distances[id] = distance; queue.Add((distance, id, parent));
            }
        }
        Route = route.ToArray();
    }

    /// <summary>下个到达或回落边界，不把整张世界的终点当成本帧要处理的工作。</summary>
    internal float NextBoundary
    {
        get
        {
            float next = cursor < Route.Length ? Route[cursor].At : float.PositiveInfinity;
            next = Mathf.Min(next, Future(Settings.Seconds * .5f));
            return Mathf.Min(next, Future(Settings.Seconds));

            float Future(float offset)
            {
                int i = Lower(Age - offset);
                while (i < Route.Length && Route[i].At + offset <= Age) i++;
                return i < Route.Length ? Route[i].At + offset : float.PositiveInfinity;
            }
        }
    }
    /// <summary>实际自然结束年龄。</summary>
    internal float Until => Route.Length == 0 ? 0f : Route[Route.Length - 1].At + Settings.Seconds;

    /// <summary>只在新到达格扣除实际沿途作用量；已经断路的后继不产生波面。</summary>
    /// <param name="age">本轮过程年龄。</param><param name="current">本轮真实地面。</param>
    internal float Advance(float age, PropagationGround current)
    {
        Age = age; ground = current;
        float spent = 0f;
        while (cursor < Route.Length && Route[cursor].At <= age)
        {
            int i = cursor++; Cell cell = Route[i];
            bool valid = current.Contains(cell.X, cell.Y) && (cell.Parent < 0 || !blocked.Contains(cell.Parent));
            if (valid && cell.Parent >= 0)
            {
                Cell parent = Route[cell.Parent];
                valid = current.Contains(parent.X, parent.Y) &&
                    Mathf.Abs(current.Height(cell.X, cell.Y) - current.Height(parent.X, parent.Y)) <= Settings.MaximumStep * Scale;
                if (valid && cell.X != parent.X && cell.Y != parent.Y)
                    valid = current.Contains(cell.X, parent.Y) && current.Contains(parent.X, cell.Y) &&
                        Mathf.Abs(current.Height(cell.X, parent.Y) - current.Height(parent.X, parent.Y)) <= Settings.MaximumStep * Scale &&
                        Mathf.Abs(current.Height(parent.X, cell.Y) - current.Height(parent.X, parent.Y)) <= Settings.MaximumStep * Scale;
            }
            if (!valid) { blocked.Add(i); blockedJournal.Add(i); }
            else spent += Settings.CellCost;
        }
        return spent;
    }

    /// <summary>按到达时间二分定位仍在回落中的局部波面。</summary>
    /// <param name="at">最早保留的到达时间。</param>
    internal int Lower(float at)
    {
        int a = 0, b = Route.Length;
        while (a < b) { int mid = (a + b) / 2; if (Route[mid].At < at) a = mid + 1; else b = mid; }
        return a;
    }
    /// <summary>实际到达且仍有波厚的局部范围。</summary>
    internal int First => Lower(Age - Settings.Seconds);
    /// <summary>已经到达的末端，不包含本项。</summary>
    internal int End => cursor;
    /// <summary>读取当前真实波厚，断路和已经消退的地块不显示或作用。</summary>
    /// <param name="i">路线位置。</param>
    internal float Height(int i)
    {
        Cell cell = Route[i];
        float t = (Age - cell.At) / Settings.Seconds;
        return !Reachable(i) || t < 0f || t > 1f ? 0f :
            Settings.Height * Scale * cell.Strength * Mathf.Max(0f, Mathf.Sin(Mathf.PI * t));
    }
    /// <summary>当前仍由实际介质承接的路线，不以零厚度伪装断路。</summary><param name="i">路线位置。</param>
    internal bool Reachable(int i) => Route[i].Distance >= Settings.InnerRadius * Scale &&
        !blocked.Contains(i) && ground.Contains(Route[i].X, Route[i].Y);
    /// <summary>取得真实路线处的衰减，不以人物直线距离猜测。</summary>
    /// <param name="id">地块稳定编号。</param>
    internal float Strength(int id) => index.TryGetValue(id, out int i) ? Route[i].Strength : 0f;
    /// <summary>保存轻量推进位置及断路日志边界。</summary>
    internal void Save() { savedCursor = cursor; savedAge = Age; savedJournal = blockedJournal.Count; savedGround = ground; }
    /// <summary>撤销本步改变，路线和整张地图不复制。</summary>
    internal void Restore()
    {
        cursor = savedCursor; Age = savedAge; ground = savedGround;
        for (int i = blockedJournal.Count - 1; i >= savedJournal; i--) { blocked.Remove(blockedJournal[i]); blockedJournal.RemoveAt(i); }
    }
}
