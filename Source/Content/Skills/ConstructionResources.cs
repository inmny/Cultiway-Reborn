using System;
using System.IO;
using Cultiway.Core;
using Cultiway.Core.SkillLibV3;

namespace Cultiway.Content.Skills;

/// <summary>构造与已有资源登记共用真实可用性、报价、余额和容量，不生成替代资源。</summary>
internal static class ConstructionResources
{
    /// <summary>首轮正式资料的短名称仍指向原通道；新资料也可直接使用长期资源编号。</summary>
    /// <param name="id">保存的资源名称或长期编号。</param>
    internal static SkillCastResourceAsset Resolve(string id) => id switch
    {
        "none" => null,
        "wakan" => SkillCastResources.Wakan,
        "vigor" => SkillCastResources.Vigor,
        "mana" => SkillCastResources.NativeMana,
        "stamina" => SkillCastResources.Stamina,
        "yao_power" => SkillCastResources.YaoPower,
        null => null,
        _ => ModClass.I.SkillV3.CastResourceLib.get(id)
    };

    /// <summary>保存边界拒绝缺失通道，免费构造仍只允许零费用。</summary>
    /// <param name="id">保存的资源编号。</param>
    /// <param name="recovery">是否需要把材料收回此资源。</param>
    internal static void Validate(string id, bool recovery)
    {
        if (id == "none") return;
        SkillCastResourceAsset resource = Resolve(id);
        if (resource == null || recovery && resource.MaximumAmount == null)
            throw new InvalidDataException("缺少施法资源或实际回收容量：" + id);
    }

    /// <summary>按当前支付者实时换算；需求已经由能力构造确定，不重复添加旧容器范围。</summary>
    /// <param name="caster">当前支付者。</param><param name="id">实际资源。</param>
    /// <param name="demand">构造的当前需求。</param>
    internal static float Quote(ActorExtend caster, string id, float demand)
    {
        if (id == "none") return 0f;
        SkillCastResourceAsset resource = Resolve(id);
        float cost = resource.Quote(caster, demand);
        if (float.IsNaN(cost) || float.IsInfinity(cost) || cost < 0f ||
            resource.IntegerAmount && (cost != Math.Floor(cost) || (double)cost > int.MaxValue))
            throw new InvalidOperationException("施法资源返回无效报价：" + id);
        return cost;
    }

    /// <summary>必须具有真实通道，即使费用为零也不能凭空获得另一体系的资源。</summary>
    /// <param name="caster">当前支付者。</param><param name="id">实际资源。</param>
    /// <param name="demand">当前需求。</param>
    internal static bool CanPay(ActorExtend caster, string id, float demand)
    {
        if (id == "none") return true;
        SkillCastResourceAsset resource = Resolve(id);
        return resource.IsAvailable(caster) && resource.ReadAmount(caster) >= Quote(caster, id, demand);
    }

    /// <summary>以实际可表示的余额差确定材料本金，不能把被浮点精度吞掉的费用变成可回收资源。</summary>
    /// <param name="caster">当前支付者。</param><param name="id">本次资源。</param>
    /// <param name="cost">本次确定的实际费用。</param>
    internal static double Budget(ActorExtend caster, string id, float cost)
    {
        if (id == "none") return 0d;
        SkillCastResourceAsset resource = Resolve(id);
        double before = resource.ReadAmount(caster);
        double after = resource.IntegerAmount ? before - cost : (float)(before - cost);
        return Math.Min(cost, Math.Max(0d, before - after));
    }

    /// <summary>正式受理后只调用资源所属玩法的写入，不伪造人物体系。</summary>
    /// <param name="caster">已确认可支付的人物。</param><param name="id">本次实际资源。</param>
    /// <param name="cost">本次已经确定的费用。</param>
    internal static void Pay(ActorExtend caster, string id, float cost)
    {
        if (id == "none") return;
        SkillCastResourceAsset resource = Resolve(id);
        resource.SpendAmount(caster, cost);
    }

    /// <summary>旧短名称与长期编号可以指向同一通道，实际需求据此判断能否回收。</summary>
    /// <param name="a">第一个保存编号。</param><param name="b">第二个保存编号。</param>
    internal static bool Same(string a, string b) => Resolve(a) == Resolve(b);

    /// <summary>整数规则由实际资源登记维护，不根据保存字符串猜测。</summary>
    /// <param name="id">已验证的资源编号。</param>
    internal static bool IsInteger(string id) => Resolve(id)?.IntegerAmount == true;
}