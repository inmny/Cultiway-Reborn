using System;
using System.Linq;
using System.Collections.Generic;
using Cultiway.Core.Skills;
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
    internal static SkillCastResourceAsset Validate(string id, bool recovery)
    {
        if (id == "none") return null;
        SkillCastResourceAsset resource = Resolve(id);
        if (resource == null) throw new InvalidDataException("Resource：未登记实际施法资源：" + id);
        if (recovery && resource.MaximumAmount == null)
            throw new InvalidDataException("Resource：资源" + id + "没有实际回收容量，不能用于本构造的材料回收");
        return resource;
    }

    /// <summary>按当前支付者实时换算；需求已经由能力构造确定，不重复添加旧容器范围。</summary>
    /// <param name="caster">当前支付者。</param><param name="id">实际资源。</param>
    /// <param name="demand">构造的当前需求。</param>
    internal static float Quote(ActorExtend caster, string id, float demand)
    {
        if (id == "none") return 0f;
        return SkillCastPayment.Quote(caster, Resolve(id), demand);
    }

    /// <summary>必须具有真实通道，即使费用为零也不能凭空获得另一体系的资源。</summary>
    /// <param name="caster">当前支付者。</param><param name="id">实际资源。</param>
    /// <param name="demand">当前需求。</param>
    internal static bool CanPay(ActorExtend caster, string id, float demand)
    {
        if (SkillCastPayment.IsPending(caster)) return false;
        if (id == "none") return true;
        SkillCastResourceAsset resource = Resolve(id);
        return resource.IsAvailable(caster) && resource.ReadAmount(caster) >= Quote(caster, id, demand);
    }

    /// <summary>准备完成后才实际扣费，未正式开始时释放即可撤销。</summary>
    /// <param name="caster">当前支付者。</param><param name="id">实际资源。</param><param name="demand">当前构造需求。</param>
    internal static SkillCastPayment TryBegin(ActorExtend caster, string id, float demand) =>
        SkillCastPayment.TryBegin(caster, id == "none" ? Array.Empty<SkillCastResourceAsset>() : new[] { Resolve(id) },
            SkillCastResourceRequirementMode.Single, demand);

    /// <summary>把主通道与额外通道解析成同一支付输入，简单构造也走此入口。</summary>
    /// <param name="definition">已完成资料检查的构造。</param>
    internal static SkillCastResourceAsset[] Channels(AbilityDefinition definition) => definition.Resource == "none"
        ? Array.Empty<SkillCastResourceAsset>()
        : new[] { definition.Resource }.Concat(definition.AdditionalResources).Select(Resolve).ToArray();

    /// <summary>登记边界检查全部通道，需求允许小数，整数规则由本次实际报价执行。</summary>
    /// <param name="definition">当前完整资料。</param><param name="recovery">是否产生可回收材料。</param>
    internal static void Validate(AbilityDefinition definition, bool recovery)
    {
        if (definition.Resource == "none" && definition.Cost != 0) throw new InvalidDataException("Cost：无消耗构造须为零需求");
        foreach (string id in new[] { definition.Resource }.Concat(definition.AdditionalResources)) Validate(id, recovery);
        if (Channels(definition).GroupBy(channel => channel.id).Any(group => group.Count() > 1))
            throw new InvalidDataException("AdditionalResources：同一通道不能重复声明");
    }

    /// <summary>可用预览与真实启动共用通道顺序及支付方式。</summary>
    /// <param name="caster">支付者。</param><param name="definition">当前构造。</param>
    internal static bool CanPay(ActorExtend caster, AbilityDefinition definition) =>
        SkillCastPayment.CanPay(caster, Channels(definition), definition.ResourceMode, definition.Cost);

    /// <summary>所有实际报价和扣费由共用支付凭据维护。</summary>
    /// <param name="caster">支付者。</param><param name="definition">当前构造。</param>
    internal static SkillCastPayment TryBegin(ActorExtend caster, AbilityDefinition definition) =>
        SkillCastPayment.TryBegin(caster, Channels(definition), definition.ResourceMode, definition.Cost);

    /// <summary>日常需求只读预估当前可选费用；正式支付仍单独冻结其报价。</summary>
    /// <param name="caster">当前支付者。</param><param name="definition">已验证的资料。</param>
    internal static Dictionary<string, double> PreviewCosts(ActorExtend caster, AbilityDefinition definition)
    {
        var costs = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var channel in Channels(definition))
        {
            if (!channel.IsAvailable(caster)) continue;
            float cost = SkillCastPayment.Quote(caster, channel, definition.Cost);
            if (definition.ResourceMode == SkillCastResourceRequirementMode.AnyOf && channel.ReadAmount(caster) < cost) continue;
            costs[channel.id] = cost;
            if (definition.ResourceMode != SkillCastResourceRequirementMode.AllOf) break;
        }
        return costs;
    }

    /// <summary>旧短名称与长期编号可以指向同一通道，实际需求据此判断能否回收。</summary>
    /// <param name="a">第一个保存编号。</param><param name="b">第二个保存编号。</param>
    internal static bool Same(string a, string b) => Resolve(a) == Resolve(b);

    /// <summary>整数规则由实际资源登记维护，不根据保存字符串猜测。</summary>
    /// <param name="id">已验证的资源编号。</param>
    internal static bool IsInteger(string id) => Resolve(id)?.IntegerAmount == true;
}
