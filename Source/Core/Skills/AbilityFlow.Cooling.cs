using System;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>熔流在同一格中保存可移动材料及冷却后留下的承托面。</summary>
internal sealed partial class AbilityFlow
{
    /// <summary>只有明确开启冷却的数值层才保存这些量。</summary>
    private readonly CoolingState cooling;
    /// <summary>可移动材料的实际体积，不包含仍在同一层的固定壳。</summary>
    internal double LiquidVolume;
    /// <summary>固定壳所含的体积，计入本部位的总量。</summary>
    internal double CrustVolume;
    /// <summary>累计散热和破壳，不把相互转移的热量记作散失。</summary>
    internal double HeatLost => cooling?.Lost ?? 0;
    /// <summary>累计由实际热流推回可流动状态的壳体积。</summary>
    internal double CrustBroken => cooling?.Broken ?? 0;
    /// <summary>本层现在储存的可用热量，包括维持液态所需的份额。</summary>
    internal double ThermalEnergy
    {
        get
        {
            if (cooling == null) return 0d;
            double heat = 0d;
            foreach (float value in cooling.Heat) heat += value;
            return heat * Cell * Cell;
        }
    }
    /// <summary>最近真实破口的地图位置。</summary>
    internal Vector3 LastCrack => cooling?.Crack ?? Vector3.zero;
    /// <summary>渲染快照读取固定壳，内部数组只由数值过程改动。</summary>
    internal float[] Crust => cooling?.Crust;
    /// <summary>与热液分开发布的固定壳网格身份。</summary>
    internal object CrustOwner => cooling;

    /// <summary>液面下的实际承托由地面和冷壳共同构成。</summary>
    /// <param name="cell">格序号。</param>
    internal float FlowBed(int cell) => Ground[cell] + (cooling?.Crust[cell] ?? 0f);

    /// <summary>真实剩余热量达到液态门槛后，才有余焰强度。</summary>
    /// <param name="cell">格序号。</param>
    internal float Glow(int cell) => cooling == null || Water[cell].x <= Cell * DryFraction ? 0f :
        Mathf.Clamp01((cooling.Heat[cell] / Water[cell].x - 1f) / Settings.Cooling.Heat);

    /// <summary>只把最初随这份液料带来的能量入账一次。</summary>
    private void BeginCooling()
    {
        if (cooling == null) return;
        Array.Clear(cooling.Crust, 0, cooling.Crust.Length);
        cooling.Lost = cooling.Broken = 0d;
        cooling.Crack = Vector3.zero;
        for (int i = 0; i < Water.Length; i++) cooling.Heat[i] = Water[i].x * (1f + Settings.Cooling.Heat);
    }

    /// <summary>按已经算出的实际液体通量搬运热量，不由显示速度或固定圈选传播。</summary>
    /// <param name="left">流边第一格。</param><param name="right">流边第二格。</param>
    /// <param name="depth">从第一格流向第二格的带符号液深。</param>
    private void TransportHeat(int left, int right, float depth)
    {
        int from = depth >= 0f ? left : right, to = depth >= 0f ? right : left;
        if (from < 0 || Water[from].x < .000001f) return;
        float heat = Mathf.Abs(depth) * cooling.Heat[from] / Water[from].x;
        cooling.Change[from] -= heat;
        if (to >= 0) cooling.Change[to] += heat;
    }

    /// <summary>每格先扣除实际散热，不能保持液态的材料按体积交给承托面。</summary>
    /// <param name="cell">实际格序号。</param><param name="step">有效世界秒。</param>
    /// <param name="retained">流体蒸散后实际留存比例。</param>
    private void CoolCell(int cell, float step, float retained)
    {
        if (cooling == null) return;
        Vector3 wet = Water[cell];
        float incoming = Mathf.Max(0f, cooling.Heat[cell] + cooling.Change[cell]);
        float kept = wet.x <= .00000001f ? 0f :
            Mathf.Max(0f, Mathf.Min(incoming * retained, wet.x * (1f + Settings.Cooling.Heat)) - Settings.Cooling.Loss * step);
        cooling.Lost += (incoming - kept) * Cell * Cell;
        cooling.Heat[cell] = kept;
        if (kept >= wet.x) return;
        cooling.Crust[cell] += wet.x - kept;
        Water[cell] = kept > 0f ? wet * (kept / wet.x) : Vector3.zero;
    }

    /// <summary>从实际重叠的后到热流打开薄壳。先检查压力与接触面积，再用余热软化破口；铁料按原账归回流动层。</summary>
    /// <param name="donor">送来热量和冲量的实际熔流。</param><param name="step">本次有效世界秒。</param>
    internal bool BreakWith(AbilityFlow donor, float step)
    {
        if (cooling == null || donor.cooling == null || !Started || !donor.Started || Cell <= 0 || donor.Cell <= 0 ||
            donor.LiquidVolume <= 0d || CrustVolume <= 0d) return false;
        int n = Settings.Cells, fromN = donor.Settings.Cells;
        float area = Cell * Cell, fromArea = donor.Cell * donor.Cell;
        double old = cooling.Broken;
        for (int i = 0; i < donor.Water.Length; i++)
        {
            Vector3 liquid = donor.Water[i];
            float extra = donor.cooling.Heat[i] - liquid.x;
            if (liquid.x < donor.Cell * DryFraction || extra <= 0f) continue;
            Vector2 velocity = new(liquid.y / liquid.x, liquid.z / liquid.x);
            float push = liquid.x * velocity.sqrMagnitude; // 静止热池不会冒充冲击。
            if (push < Settings.Cooling.BreakPressure) continue;
            Vector2 at = donor.Origin + new Vector2(i % fromN, i / fromN) * donor.Cell;
            Vector2 end = at + Vector2.one * donor.Cell;
            int xmin = Mathf.Max(0, Mathf.FloorToInt((at.x - Origin.x) / Cell));
            int ymin = Mathf.Max(0, Mathf.FloorToInt((at.y - Origin.y) / Cell));
            int xmax = Mathf.Min(n, Mathf.CeilToInt((end.x - Origin.x) / Cell));
            int ymax = Mathf.Min(n, Mathf.CeilToInt((end.y - Origin.y) / Cell));
            float impulseDepth = liquid.x;
            for (int y = ymin; y < ymax; y++) for (int x = xmin; x < xmax; x++)
            {
                int j = y * n + x;
                float shell = cooling.Crust[j];
                if (shell < Cell * DryFraction) continue;
                float bottom = donor.FlowBed(i), top = Ground[j] + shell;
                if (bottom >= top || bottom + liquid.x <= Ground[j]) continue;
                float retained = impulseDepth / liquid.x;
                float excessPush = push * retained * retained - Settings.Cooling.BreakPressure * (1f + 8f * shell / Cell);
                if (excessPush <= 0f) continue;
                Vector2 point = Origin + new Vector2(x, y) * Cell;
                float overlap = (Mathf.Min(end.x, point.x + Cell) - Mathf.Max(at.x, point.x)) *
                                (Mathf.Min(end.y, point.y + Cell) - Mathf.Max(at.y, point.y));
                float depth = Mathf.Min(shell, Mathf.Min(Mathf.Min(extra, impulseDepth) * fromArea / area,
                    excessPush * step * overlap / area));
                if (depth <= .000001f) continue;
                float used = depth * area / fromArea;
                extra -= used;
                impulseDepth -= used;
                donor.cooling.Heat[i] -= used;
                cooling.Crust[j] -= depth;
                cooling.Heat[j] += depth;
                Water[j] += new Vector3(depth, depth * velocity.x, depth * velocity.y);
                cooling.Broken += depth * area;
                cooling.Crack = new Vector3(point.x + .5f * Cell, point.y + .5f * Cell, top);
            }
            // 两边的格宽可以不同，按相同地图面积折算后从后到热流扣除冲量。
            if (impulseDepth < liquid.x) donor.Water[i] = new Vector3(liquid.x, impulseDepth * velocity.x, impulseDepth * velocity.y);
        }

        if (cooling.Broken == old) return false;
        Recount(); donor.Recount();
        return true;
    }

    /// <summary>预测、回退和确认保留相同的热量、冷壳和破口记录。</summary>
    /// <param name="target">预先分配的同配置数值层。</param>
    private void CopyCooling(AbilityFlow target)
    {
        target.LiquidVolume = LiquidVolume;
        target.CrustVolume = CrustVolume;
        if (cooling == null) return;
        Array.Copy(cooling.Crust, target.cooling.Crust, cooling.Crust.Length);
        Array.Copy(cooling.Heat, target.cooling.Heat, cooling.Heat.Length);
        target.cooling.Lost = cooling.Lost;
        target.cooling.Broken = cooling.Broken;
        target.cooling.Crack = cooling.Crack;
    }

    /// <summary>冻结铁料、液态热储与共用临时通量。</summary>
    private sealed class CoolingState
    {
        /// <summary>每格的固定厚度、余热、当步热通量。</summary>
        internal readonly float[] Crust, Heat, Change;
        /// <summary>已经散失的热量、重新破开的真实铁料。</summary>
        internal double Lost, Broken;
        /// <summary>最近破口的实际位置。</summary>
        internal Vector3 Crack;
        /// <summary>按有限场地预留数值及复用工作数组。</summary>
        /// <param name="count">固定格数。</param>
        internal CoolingState(int count) { Crust = new float[count]; Heat = new float[count]; Change = new float[count]; }
    }
}
