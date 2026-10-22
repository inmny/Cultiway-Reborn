using System;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>只读数值地表上的有限浅层流体；每条边同时记入两格，体积不按部件数复制。</summary>
internal sealed partial class AbilityFlow
{
    /// <summary>显示和作用共用的最小有效水深占格宽比例。</summary>
    internal const float DryFraction = .002f;
    /// <summary>二维格间传播的稳定步长系数。</summary>
    private const float StableFraction = .2f;
    /// <summary>初始高压和高速输入按每秒240步估算格宽下限。</summary>
    private const float InitialStep = 1f / 240f;
    /// <summary>本过程固定的共同参数。</summary>
    internal readonly CompiledFlow Settings;
    /// <summary>每格水深、横向动量、纵向动量。</summary>
    internal readonly Vector3[] Water;

    /// <summary>世界容量暂不能扩展时，在本次完整推进中反射边界。</summary>
    private bool closedBoundary;
    /// <summary>上一完整状态及本步增量，均由本过程独占。</summary>
    private readonly Vector3[] delta;
    /// <summary>已经采样的格底高度。</summary>
    internal readonly float[] Ground;
    /// <summary>网格左下边缘。</summary>
    internal Vector2 Origin;
    /// <summary>实际格宽。</summary>
    internal float Cell;
    /// <summary>进入流散后仍在本区域中的体积。</summary>
    internal double Volume;
    /// <summary>已经外流及蒸散的体积，不能再次分配给后段。</summary>
    internal double Escaped, Evaporated;
    /// <summary>是否已经从实际形体建立数值层。</summary>
    internal bool Started;
    /// <summary>实际质量中心及平均水平速度，供连接、引导和结束交接使用。</summary>
    internal Vector3 Centre, Velocity;
    /// <summary>从原形转换时的最后真实姿态，保留该时点之前的有效路径。</summary>
    internal Matrix4x4 SourceTransform;
    /// <summary>地表版本，只在变化时重新采样。</summary>
    private AbilityHeightField terrain;
    /// <summary>每次发布属于本过程的独立标识。</summary>
    internal readonly object VisualOwner = new();

    /// <summary>在施放受理时分配有上限的工作数组，预测与恢复不临时分配。</summary>
    /// <param name="settings">已检查的参数。</param>
    internal AbilityFlow(CompiledFlow settings)
    {
        Settings = settings; int count = settings.Cells * settings.Cells;
        Water = new Vector3[count]; delta = new Vector3[count]; Ground = new float[count];
    }

    /// <summary>按实际投影范围分配已存在的体积，不添加第二份初始材料。</summary>
    /// <param name="position">实际来源中心。</param>
    /// <param name="extent">来源投影的半宽。</param>
    /// <param name="scale">实际尺寸。</param>
    /// <param name="volume">已经交接的体积。</param>
    /// <param name="velocity">来源实际运动。</param>
    /// <param name="field">同轮共同地表。</param>
    internal void Start(Vector3 position, Vector2 extent, float scale, double volume, Vector3 velocity, AbilityHeightField field)
    {
        int n = Settings.Cells;
        // 计算区域是最低范围。极密或极快的输入扩大格宽，避免同一帧为微小范围计算无数次压力传播。
        float pressureCell = (float)Math.Sqrt(Math.Sqrt(Settings.Gravity * volume) * InitialStep / StableFraction);
        Cell = Mathf.Max(.001f, Mathf.Max(Settings.Size * scale / n, pressureCell));
        Cell = Mathf.Max(Cell, Mathf.Max(Mathf.Abs(velocity.x), Mathf.Abs(velocity.y)) * InitialStep / StableFraction);
        Origin = (Vector2)position - Vector2.one * Cell * n * .5f;
        Started = true; Centre = position; terrain = null; RefreshGround(field, true); Escaped = Evaporated = 0;
        double weight = 0;
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
        {
            Vector2 p = Origin + new Vector2(x + .5f, y + .5f) * Cell - (Vector2)position;
            float d = p.x * p.x / Mathf.Max(Cell * Cell, extent.x * extent.x) + p.y * p.y / Mathf.Max(Cell * Cell, extent.y * extent.y);
            float h = Mathf.Max(0, 1f - d); Water[y * n + x] = new Vector3(h, 0, 0); weight += h;
        }
        double factor = volume / (weight * Cell * Cell);
        for (int i = 0; i < Water.Length; i++) { float h = (float)(Water[i].x * factor); Water[i] = new Vector3(h, h * velocity.x, h * velocity.y); }
        Recount();
    }

    /// <summary>地面改变只改变承托，不凭空改变水量。</summary>
    /// <param name="field">共同地表。</param>
    /// <param name="force">首次建立必须完整采样。</param>
    internal void RefreshGround(AbilityHeightField field, bool force = false)
    {
        if (!force && ReferenceEquals(field, terrain)) return;
        terrain = field; int n = Settings.Cells;
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            Ground[y * n + x] = field?.Sample(Origin.x + (x + .5f) * Cell, Origin.y + (y + .5f) * Cell) ?? 0f;
    }

    /// <summary>先按传播速度拆步，再计算静水重构通量，最后施加阻力和真实蒸散。</summary>
    /// <param name="seconds">本次共同世界时间。</param>
    internal void Advance(float seconds) => AdvanceBounded(seconds, false);

    /// <summary>世界格网暂不能接纳时，保留材料并对边界施加回压。</summary>
    /// <param name="seconds">共同世界步长。</param><param name="closeBoundary">本次推进是否反射边界。</param>
    internal void AdvanceBounded(float seconds, bool closeBoundary)
    {
        closedBoundary = closeBoundary;
        int n = Settings.Cells;
        while (seconds > 0)
        {
            float speed = .0001f;
            foreach (Vector3 u in Water)
                if (u.x > .000001f) speed = Mathf.Max(speed, Mathf.Max(Mathf.Abs(u.y), Mathf.Abs(u.z)) / u.x + Mathf.Sqrt(Settings.Gravity * u.x));
            float dt = Mathf.Min(seconds, StableFraction * Cell / speed); Array.Clear(delta, 0, delta.Length);
            for (int y = 0; y < n; y++) for (int x = 0; x <= n; x++) Edge(x == 0 ? -1 : y * n + x - 1, x == n ? -1 : y * n + x, false, dt);
            for (int y = 0; y <= n; y++) for (int x = 0; x < n; x++) Edge(y == 0 ? -1 : (y - 1) * n + x, y == n ? -1 : y * n + x, true, dt);
            float friction = Mathf.Exp(-Settings.Drag * dt), retained = Mathf.Exp(-Settings.Evaporation * dt);
            for (int i = 0; i < Water.Length; i++)
            {
                Vector3 next = Water[i] + delta[i]; next.x = Mathf.Max(0f, next.x);
                Evaporated += next.x * (1d - retained) * Cell * Cell;
                next *= retained; next.y *= friction; next.z *= friction;
                if (next.x < .000001f) next.y = next.z = 0;
                Water[i] = next;
            }
            seconds = Mathf.Max(0f, seconds - dt);
        }
        Recount();
    }

    /// <summary>一条共用边只计算一次，水量通量成对记录，地形项只改变动量。</summary>
    /// <param name="left">左格或下格，负值为边界。</param>
    /// <param name="right">右格或上格，负值为边界。</param>
    /// <param name="vertical">是否交换两个水平坐标。</param>
    /// <param name="dt">稳定步长。</param>
    private void Edge(int left, int right, bool vertical, float dt)
    {
        Vector3 a = left < 0 ? Vector3.zero : Water[left], b = right < 0 ? Vector3.zero : Water[right];
        float za = Ground[left < 0 ? right : left], zb = Ground[right < 0 ? left : right];
        if (vertical) { a = new Vector3(a.x, a.z, a.y); b = new Vector3(b.x, b.z, b.y); }
        if (!Settings.OpenBoundary || closedBoundary)
        {
            if (left < 0) a = new Vector3(b.x, -b.y, b.z);
            if (right < 0) b = new Vector3(a.x, -a.y, a.z);
        }
        float ua = a.x > .000001f ? a.y / a.x : 0, va = a.x > .000001f ? a.z / a.x : 0;
        float ub = b.x > .000001f ? b.y / b.x : 0, vb = b.x > .000001f ? b.z / b.x : 0;
        float bed = Mathf.Max(za, zb), ha = Mathf.Max(0f, a.x + za - bed), hb = Mathf.Max(0f, b.x + zb - bed), g = Settings.Gravity;
        Vector3 l = new(ha, ha * ua, ha * va), r = new(hb, hb * ub, hb * vb);
        float speed = Mathf.Max(Mathf.Abs(ua) + Mathf.Sqrt(g * ha), Mathf.Abs(ub) + Mathf.Sqrt(g * hb));
        Vector3 flux = .5f * (new Vector3(l.y, l.y * ua + .5f * g * ha * ha, l.y * va) + new Vector3(r.y, r.y * ub + .5f * g * hb * hb, r.y * vb) - speed * (r - l));
        if (!closedBoundary && Settings.OpenBoundary && (left < 0 && flux.x > 0 || right < 0 && flux.x < 0)) flux.x = 0;
        float factor = dt / Cell;
        Vector3 fl = flux + Vector3.up * (.5f * g * (a.x * a.x - ha * ha));
        Vector3 fr = flux + Vector3.up * (.5f * g * (b.x * b.x - hb * hb));
        if (vertical) { fl = new Vector3(fl.x, fl.z, fl.y); fr = new Vector3(fr.x, fr.z, fr.y); }
        if (left >= 0) delta[left] -= fl * factor;
        if (right >= 0) delta[right] += fr * factor;
        if (left < 0) Escaped -= flux.x * dt * Cell;
        if (right < 0) Escaped += flux.x * dt * Cell;
    }

    /// <summary>用双精度统计实际剩余体积。</summary>
    private void Recount()
    {
        double total = 0, px = 0, py = 0, pz = 0, qx = 0, qy = 0; int n = Settings.Cells;
        for (int i = 0; i < Water.Length; i++)
        {
            Vector3 u = Water[i]; total += u.x; qx += u.y; qy += u.z;
            px += u.x * (Origin.x + (i % n + .5f) * Cell); py += u.x * (Origin.y + (i / n + .5f) * Cell); pz += u.x * (Ground[i] + u.x * .5f);
        }
        Volume = total * Cell * Cell;
        if (total > 0) { Centre = new Vector3((float)(px / total), (float)(py / total), (float)(pz / total)); Velocity = new Vector3((float)(qx / total), (float)(qy / total), 0); }
        else Velocity = Vector3.zero;
    }

    /// <summary>明确的水平投送改变整体动量，保留水流内部速度差和实际水量。</summary>
    /// <param name="target">新的平均水平速度。</param>
    internal void SetVelocity(Vector3 target)
    {
        Vector3 shift = target - Velocity;
        for (int i = 0; i < Water.Length; i++) { Water[i].y += Water[i].x * shift.x; Water[i].z += Water[i].x * shift.y; }
        Velocity = new Vector3(target.x, target.y);
    }

    /// <summary>整个共同主体按接触表面反射水平动量，地面承受竖直分量。</summary>
    /// <param name="normal">真实表面方向。</param><param name="surface">表面运动。</param>
    internal void Reflect(Vector3 normal, Vector3 surface)
    {
        for (int i = 0; i < Water.Length; i++)
        {
            Vector3 u = Water[i]; if (u.x <= .000001f) continue;
            Vector3 v = Vector3.Reflect(new Vector3(u.y / u.x, u.z / u.x, 0) - surface, normal) + surface;
            Water[i].y = u.x * v.x; Water[i].z = u.x * v.y;
        }
        Recount();
    }

    /// <summary>已转化回能的体积，与外流、蒸散分别记账。</summary>
    internal double Recovered;

    /// <summary>收束降低真实水深及相应动量，不能只隐藏仍在作用的水面。</summary>
    /// <param name="ratio">本次步骤后保留的比例。</param>
    internal void Recover(float ratio)
    {
        double before = Volume;
        for (int i = 0; i < Water.Length; i++)
        {
            Water[i] *= ratio;
            if (Budget != null) Budget[i] *= ratio;
        }

        Recount(); Recovered += before - Volume;
    }

    /// <summary>预测前态独立保存数组，不让旧状态引用正在修改的水深。</summary>
    /// <param name="target">预先分配的目标。</param>
    internal void CopyTo(AbilityFlow target)
    {
        Array.Copy(Water, target.Water, Water.Length); Array.Copy(Ground, target.Ground, Ground.Length);
        target.Origin = Origin; target.Cell = Cell; target.Volume = Volume; target.Escaped = Escaped; target.Evaporated = Evaporated; target.Recovered = Recovered;
        target.Transferred = Transferred;
        if (Budget != null)
        {
            target.Budget ??= new double[Budget.Length];
            Array.Copy(Budget, target.Budget, Budget.Length);
        }
        else target.Budget = null;

        target.Started = Started; target.terrain = terrain;
        target.Centre = Centre; target.Velocity = Velocity;
        target.SourceTransform = SourceTransform;
    }
}