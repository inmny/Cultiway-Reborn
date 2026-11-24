using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;

namespace Cultiway.Core.SkillLibV3;

/// <summary>一次真实扣费的报价和本金，不以预估余额差冒充实际支付。</summary>
public readonly struct SkillCastPaymentEntry
{
    /// <summary>实际支付通道，同余额别名只形成一项。</summary>
    public readonly SkillCastResourceAsset Resource;

    /// <summary>本次冻结的实际报价。</summary>
    public readonly double Cost;

    /// <summary>资源所属玩法实际扣下的数量。</summary>
    public readonly double Spent;

    /// <summary>可交给生成材料的本金，不超过报价及实际扣费。</summary>
    public double Principal => Math.Min(Cost, Spent);

    /// <summary>记录本次实际变化。</summary>
    /// <param name="resource">真实通道。</param><param name="cost">已经报价的费用。</param><param name="spent">实际扣费。</param>
    internal SkillCastPaymentEntry(SkillCastResourceAsset resource, double cost, double spent)
    {
        Resource = resource;
        Cost = cost;
        Spent = spent;
    }
}

/// <summary>主线程的一次启动支付；未正式受理便释放时恢复余额，正式提交后不因打断自动退款。</summary>
public sealed class SkillCastPayment : IDisposable
{
    /// <summary>资源通知不能在同一支付尚未受理时重入支付者。</summary>
    private static readonly ConcurrentDictionary<ActorExtend, byte> pending = new();

    /// <summary>原人物及其当时的实际身份。</summary>
    private readonly ActorExtend payer;

    /// <summary>原人物编号，避免对象回收后写入新身份。</summary>
    private readonly long payerId;

    /// <summary>原世界的代数，切世界后不能恢复到新人物或继续受理。</summary>
    private readonly int worldGeneration;

    /// <summary>实际分组和启动前余额。</summary>
    private readonly List<Row> rows = new();

    /// <summary>已经提交或已撤销，不能再次提交或恢复。</summary>
    private bool closed;

    /// <summary>资源写入通知返回后，原人物和原世界仍然有效。</summary>
    private bool IsCurrent => worldGeneration == ModClass.I.SkillV3.CastGeneration && !payer.Base.isRekt() &&
                              payer.Base.id == payerId && !payer.E.IsNull;

    /// <summary>本次是否已经正式受理。</summary>
    public bool IsCommitted { get; private set; }

    /// <summary>来源和实际执行一起确认，先于执行受理的外部通知。</summary>
    internal Action OnCommitted { private get; set; }

    /// <summary>只读的实际扣费结果；空集合表示免费。</summary>
    public IReadOnlyList<SkillCastPaymentEntry> Entries { get; private set; }

    /// <summary>支付正式提交后通知付款人编号和实际扣费凭据，撤销时不发布。</summary>
    public static event Action<long, IReadOnlyList<SkillCastPaymentEntry>> Committed;

    /// <summary>只在取得当前人物支付权之后建立。</summary>
    /// <param name="payer">实际支付者。</param>
    private SkillCastPayment(ActorExtend payer)
    {
        this.payer = payer;
        payerId = payer.Base.id;
        worldGeneration = ModClass.I.SkillV3.CastGeneration;
    }

    /// <summary>只读准入在资源通知重入时同样报告尚不可用。</summary>
    /// <param name="payer">实际支付者。</param>
    public static bool IsPending(ActorExtend payer) => payer != null && pending.ContainsKey(payer);

    /// <summary>同一报价边界检查所有资源的数值和整数规则。</summary>
    /// <param name="payer">实际支付者。</param><param name="resource">实际通道。</param><param name="demand">已计算的需求。</param>
    public static float Quote(ActorExtend payer, SkillCastResourceAsset resource, float demand)
    {
        float cost = resource.Quote(payer, demand);
        if (float.IsNaN(cost) || float.IsInfinity(cost) || cost < 0 ||
            resource.IntegerAmount && (cost != Math.Floor(cost) || (double)cost > int.MaxValue))
            throw new InvalidOperationException("施法资源返回无效报价：" + resource.id);
        return cost;
    }

    /// <summary>只读查询按同一余额合计需求，不预先扣费或分配本金。</summary>
    /// <param name="payer">实际支付者。</param><param name="resources">已解析的通道。</param><param name="mode">选择方式。</param><param name="demand">当前需求。</param>
    public static bool CanPay(ActorExtend payer, IReadOnlyList<SkillCastResourceAsset> resources,
        SkillCastResourceRequirementMode mode, float demand)
    {
        if (payer?.Base == null || payer.Base.isRekt() || IsPending(payer)) return false;
        if (resources.Count == 0) return demand == 0;
        return mode != SkillCastResourceRequirementMode.AllOf || resources.Count == 1
            ? Select(payer, resources, mode == SkillCastResourceRequirementMode.AnyOf, demand).Resource != null
            : Collect(payer, resources, mode, demand, new List<Row>());
    }

    /// <summary>查看与正式扣除相同的报价及余额合计；无需锁定人物，空表表示免费，空值表示当前付不起。</summary>
    internal static IReadOnlyList<SkillCastPaymentEntry> Preview(ActorExtend payer,
        IReadOnlyList<SkillCastResourceAsset> resources, SkillCastResourceRequirementMode mode, float demand)
    {
        if (payer?.Base == null || payer.Base.isRekt() || IsPending(payer)) return null;
        var rows = new List<Row>();
        return Collect(payer, resources, mode, demand, rows)
            ? rows.Select(row => new SkillCastPaymentEntry(row.Resource, row.Cost, 0)).ToArray()
            : null;
    }

    /// <summary>单项或任选仍按声明顺序，跳过当前不合格的任选通道。</summary>
    private static (SkillCastResourceAsset Resource, float Cost) Select(ActorExtend payer,
        IReadOnlyList<SkillCastResourceAsset> resources, bool any, float demand)
    {
        foreach (var resource in resources)
        {
            if (resource == null) throw new ArgumentException("支付通道不能缺失", nameof(resources));
            if (resource.IsAvailable(payer))
            {
                float cost = Quote(payer, resource, demand);
                if (resource.ReadAmount(payer) >= cost) return (resource, cost);
            }
            if (!any) break;
        }
        return default;
    }

    /// <summary>预览和扣费共用报价顺序；全部支付先按真实余额合计。</summary>
    private static bool Collect(ActorExtend payer, IReadOnlyList<SkillCastResourceAsset> resources,
        SkillCastResourceRequirementMode mode, float demand, List<Row> rows)
    {
        if (resources.Count == 0) return demand == 0;
        if (mode != SkillCastResourceRequirementMode.AllOf || resources.Count == 1)
        {
            var selection = Select(payer, resources, mode == SkillCastResourceRequirementMode.AnyOf, demand);
            if (selection.Resource == null) return false;
            rows.Add(new Row { Resource = selection.Resource, Cost = selection.Cost });
            return true;
        }
        foreach (var resource in resources)
        {
            if (resource == null) throw new ArgumentException("支付通道不能缺失", nameof(resources));
            if (!resource.IsAvailable(payer)) return false;
            float cost = Quote(payer, resource, demand);
            var existing = rows.FirstOrDefault(row => row.Resource.BalanceId == resource.BalanceId);
            if (existing == null) rows.Add(new Row { Resource = resource, Cost = cost });
            else existing.Cost += cost;
        }
        return rows.All(row => row.Resource.ReadAmount(payer) >= row.Cost);
    }

    /// <summary>一次冻结报价，先核对所有余额，再实际扣费；失败没有部分收费。</summary>
    /// <param name="payer">实际支付者。</param><param name="resources">按来源声明顺序排列的通道，免费使用空集合。</param>
    /// <param name="mode">单项、任选或全部。</param><param name="demand">本次每个通道的实际需求。</param>
    public static SkillCastPayment TryBegin(ActorExtend payer, IReadOnlyList<SkillCastResourceAsset> resources,
        SkillCastResourceRequirementMode mode, float demand)
    {
        if (resources == null || !Enum.IsDefined(typeof(SkillCastResourceRequirementMode), mode) ||
            float.IsNaN(demand) || float.IsInfinity(demand) || demand < 0 || resources.Count == 0 && demand != 0)
            throw new ArgumentException("支付通道、选择方式或需求无效");
        if (payer?.Base == null || payer.Base.isRekt() || !pending.TryAdd(payer, 0)) return null;
        var payment = new SkillCastPayment(payer);
        bool ready = false;
        try
        {
            if (!Collect(payer, resources, mode, demand, payment.rows)) return null;
            foreach (var row in payment.rows)
            {
                // 前一个通道的通知可以使来源失效；未正式开始时由同一支付撤销已扣部分。
                if (!row.Resource.IsAvailable(payer) || !(row.Resource.ReadAmount(payer) >= row.Cost)) return null;
                row.Before = row.Resource.ReadAmount(payer);
                if (row.Cost > 0)
                {
                    row.Debited = true;
                    row.Resource.SpendAmount(payer, row.Cost);
                }

                if (!payment.IsCurrent) return null;
                row.Spent = Math.Min(row.Before, Math.Max(0, row.Before - row.Resource.ReadAmount(payer)));
            }

            if (mode == SkillCastResourceRequirementMode.AllOf &&
                resources.Any(resource => !resource.IsAvailable(payer))) return null;
            if (!payment.IsCurrent) return null;
            payment.Entries = Array.AsReadOnly(payment.rows
                .Select(row => new SkillCastPaymentEntry(row.Resource, row.Cost, row.Spent)).ToArray());
            ready = true;
            return payment;
        }
        finally
        {
            if (!ready) payment.Dispose();
        }
    }

    /// <summary>所有创建及来源控制就绪后正式受理，之后的结束不能撤销已经支付的费用。</summary>
    public void Commit()
    {
        if (closed) throw new InvalidOperationException("本次启动支付已经结束");
        IsCommitted = true;
        closed = true;
        pending.TryRemove(payer, out _);
        OnCommitted?.Invoke();
        Committed?.Invoke(payerId, Entries);
    }

    /// <summary>未受理时反向恢复本次真实扣费，不按回收容量截掉原本已有的余额。</summary>
    public void Dispose()
    {
        if (closed) return;
        closed = true;
        try
        {
            if (!IsCurrent) return;
            for (int i = rows.Count - 1; i >= 0; i--)
                if (rows[i].Debited)
                    rows[i].Resource.RestoreAmount(payer, rows[i].Before);
        }
        finally
        {
            pending.TryRemove(payer, out _);
        }
    }

    /// <summary>单个真实余额的本次启动状态，不进入长期存档。</summary>
    private sealed class Row
    {
        /// <summary>实际通道。</summary>
        internal SkillCastResourceAsset Resource;

        /// <summary>总费用、扣费前余额及实际扣费。</summary>
        internal double Cost, Before, Spent;

        /// <summary>只有确实调用扣费的项才需要撤销。</summary>
        internal bool Debited;
    }
}
