using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cultiway.Core;
using Cultiway.Core.SkillLibV3;
using Newtonsoft.Json;

namespace Cultiway.Content.Skills;

/// <summary>一种实际原资源在材料共同份额中的数量，不把不同资源相加。</summary>
public sealed class MaterialFundingEntry
{
    /// <summary>实际支付通道的稳定编号。</summary>
    public string Resource { get; }
    /// <summary>每一份材料预算包含的此种原资源。</summary>
    public double Rate { get; }
    /// <summary>只保存编号和比例，不保存人物或资源对象。</summary>
    /// <param name="resource">实际原资源。</param><param name="rate">每份数量。</param>
    [JsonConstructor]
    public MaterialFundingEntry(string resource, double rate) { Resource = resource; Rate = rate; }
}

/// <summary>一份材料的不可变资源组成；拆分、流散、破碎和损耗共用原有的守恒份额。</summary>
public sealed class MaterialFunding
{
    /// <summary>实际组成，外部不能修改共用数组。</summary>
    public IReadOnlyList<MaterialFundingEntry> Entries { get; }
    /// <summary>存档恢复只建立数值，所属通道在正式恢复边界验证。</summary>
    /// <param name="entries">各实际资源及每份数量。</param>
    [JsonConstructor]
    public MaterialFunding(MaterialFundingEntry[] entries) => Entries = Array.AsReadOnly(entries ?? throw new InvalidDataException("材料缺少支付组成"));

    /// <summary>用最大一项本金作共同份额单位，单资源仍保持原数量；从不合计异种资源。</summary>
    /// <param name="payment">本次真实扣费。</param><param name="budget">交给数值运行的共同份额。</param>
    internal static MaterialFunding FromPayment(SkillCastPayment payment, out double budget)
    {
        budget = payment.Entries.Count == 0 ? 0 : payment.Entries.Max(entry => entry.Principal);
        double unit = budget;
        return new MaterialFunding(payment.Entries.Where(entry => entry.Principal > 0)
            .Select(entry => new MaterialFundingEntry(entry.Resource.id, entry.Principal / unit)).ToArray());
    }

    /// <summary>同一材料剩余份额分别换回各原资源，没有跨资源兑换。</summary>
    /// <param name="shares">已确认的剩余或已收束份额。</param>
    internal Dictionary<string, double> Totals(double shares) => Entries.ToDictionary(entry => entry.Resource, entry => entry.Rate * shares, StringComparer.Ordinal);

    /// <summary>原单资源整数规则保留；多资源的共同份额本身不提前舍入。</summary>
    /// <param name="shares">数值运行已收束份额。</param>
    internal double ConfirmedShares(double shares) => Entries.Count == 1 && Entries[0].Rate == 1 && ConstructionResources.IsInteger(Entries[0].Resource)
        ? Math.Floor(shares + 1e-8) : shares;

    /// <summary>共用过程的已完整返还份额取各通道中最少的一项，实际逐资源结果另行发布。</summary>
    /// <param name="results">所属玩法已经确认的实际入账。</param>
    internal double GrantedShares(IReadOnlyList<MaterialCreditResult> results) => Entries.Count == 0 ? 0 :
        Entries.Min(entry => results.First(result => result.Resource == entry.Resource).Granted / entry.Rate);

    /// <summary>读取边界核对组成和可恢复本金，不重新报价。</summary>
    /// <param name="maximum">这份记录最大仍可承载的共同份额。</param>
    internal void Validate(double maximum)
    {
        if (maximum > 0 && Entries.Count == 0 || Entries.Count > 16 || Entries.Any(entry => entry == null || !Finite(entry.Rate) || entry.Rate <= 0 || entry.Rate > 1) ||
            Entries.Select(entry => entry.Resource).Distinct(StringComparer.Ordinal).Count() != Entries.Count)
            throw new InvalidDataException("材料支付组成无效或重复");
        foreach (var entry in Entries)
        {
            var channel = ConstructionResources.Validate(entry.Resource, true);
            if (channel == null || !Finite(maximum * entry.Rate) || channel.IntegerAmount && maximum * entry.Rate > int.MaxValue)
                throw new InvalidDataException("材料的原资源本金越界");
        }
    }

    /// <summary>保存金额只能是有限非负数。</summary>
    /// <param name="value">外部金额。</param>
    internal static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0;
}

/// <summary>一项实际回收，金额始终属于所注明的原资源。</summary>
internal readonly struct MaterialCreditResult
{
    /// <summary>原资源编号。</summary>
    internal readonly string Resource;
    /// <summary>本轮新增请求与实际入账。</summary>
    internal readonly double Requested, Granted;
    /// <summary>只由真实入账入口建立。</summary>
    /// <param name="resource">原资源。</param><param name="requested">本轮请求。</param><param name="granted">实际入账。</param>
    internal MaterialCreditResult(string resource, double requested, double granted) { Resource = resource; Requested = requested; Granted = granted; }
}

/// <summary>每种资源单独保留已确认金额和精度余额，供施放与世界材料共用。</summary>
public sealed class MaterialRecoveryBook
{
    /// <summary>实际原资源编号对应的累计，不保存其他通道的换算金额。</summary>
    public Dictionary<string, MaterialRecoveryBalance> Entries = new(StringComparer.Ordinal);

    /// <summary>主线程逐通道确认累计并入账，容量不足的部分依原规则损失。</summary>
    /// <param name="totals">每种资源已经收束的累计数量。</param><param name="owner">有效原支付者，失效时为空。</param>
    /// <param name="current">原世界仍然有效，资源通知换世界后停止本批写入。</param>
    internal MaterialCreditResult[] Credit(Dictionary<string, double> totals, ActorExtend owner, Func<bool> current)
    {
        var results = new List<MaterialCreditResult>(totals.Count);
        long provider = owner?.Base?.id ?? 0;
        foreach (var pair in totals)
        {
            if (!current()) break;
            if (owner != null && (owner.Base.isRekt() || owner.Base.id != provider || owner.E.IsNull)) owner = null;
            if (!Entries.TryGetValue(pair.Key, out var balance)) Entries.Add(pair.Key, balance = new MaterialRecoveryBalance());
            double total = ConstructionResources.IsInteger(pair.Key) ? Math.Floor(pair.Value + 1e-8) : pair.Value;
            double request = Math.Max(0, total - balance.Confirmed);
            balance.Confirmed = Math.Max(balance.Confirmed, total);
            double available = request + balance.Fraction;
            balance.Fraction = 0;
            if (ConstructionResources.IsInteger(pair.Key)) available = Math.Min(available, int.MaxValue);
            double granted = owner == null || available <= 0 ? 0 : MaterialRecoveryCredit.Grant(owner, pair.Key, available, out balance.Fraction);
            results.Add(new MaterialCreditResult(pair.Key, request, granted));
        }
        return results.ToArray();
    }

    /// <summary>外部资料只检查真实累计，不因当前报价变化修改历史。</summary>
    /// <param name="totals">当前实际可回收的各资源累计上限。</param>
    internal void Validate(Dictionary<string, double> totals)
    {
        if (Entries == null || Entries.Count > 65536) throw new InvalidDataException("材料回收账本缺失或越界");
        foreach (var pair in Entries)
            if (!totals.TryGetValue(pair.Key, out double total) || pair.Value == null || !MaterialFunding.Finite(pair.Value.Confirmed) ||
                !MaterialFunding.Finite(pair.Value.Fraction) || pair.Value.Fraction > pair.Value.Confirmed || pair.Value.Confirmed > total + Math.Max(1e-7, total * 1e-12))
                throw new InvalidDataException("材料原资源累计及精度余额无效");
    }
}

/// <summary>一种原资源的已确认累计，读档不重复领取。</summary>
public sealed class MaterialRecoveryBalance
{
    /// <summary>已经正式确认的请求和精度未能表示的余额。</summary>
    public double Confirmed, Fraction;
}
