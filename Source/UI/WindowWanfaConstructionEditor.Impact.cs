using System;
using System.Linq;
using Cultiway.Core.Skills;
using Cultiway.Core.SkillLibV3.Usage;

namespace Cultiway.UI;

/// <summary>通过普通表单修改目标筛选和真实接触作用。</summary>
public sealed partial class WindowWanfaConstructionEditor
{
    /// <summary>整招默认影响选取与身体，部位可以明确覆盖实际接触规则。</summary>
    private void ImpactPage()
    {
        Row("ImpactRules", T("ImpactRulesHint"));
        Toggle("CustomImpact", session.Draft.Impact != null, value => session.Draft.Impact = value ? new ConstructionImpact() : null);
        if (session.Draft.Impact != null) ImpactRows(session.Draft.Impact, "");
        else Row("DefaultImpact", T("DefaultImpactHint"));
        if (parts.Count == 0) return;
        ConstructionNode node = parts[part];
        ActivationRows(node);
        MotionRows(node);
        if (node.ContactRole != ConstructionContactRole.Attack) { Row("NoActorImpact", T("NoActorImpactHint")); return; }
        Toggle("NodeImpact", node.Impact != null, value =>
        {
            ConstructionImpact basis = session.Draft.Impact ?? new ConstructionImpact();
            node.Impact = value ? new ConstructionImpact { Targets = basis.Targets, DamageScale = basis.DamageScale, Force = basis.Force, Lift = basis.Lift, Origin = basis.Origin } : null;
        });
        if (node.Impact != null) ImpactRows(node.Impact, "Node");
    }

    /// <summary>同一规则用于整招及生成部位；关闭覆盖时直接恢复继承。</summary>
    /// <param name="impact">独立草稿中的接触作用。</param>
    /// <param name="prefix">整招和部位表单名称。</param>
    private void ImpactRows(ConstructionImpact impact, string prefix)
    {
        Choice(prefix + "Targets", T("Relation." + impact.Targets), () => menu.Show(T(prefix + "Targets"),
            new[] { SkillUseTargetRelation.Hostile, SkillUseTargetRelation.Friendly, SkillUseTargetRelation.Self }.Select(relation => new UiOptionMenuOption
            {
                Label = T("Relation." + relation), Selected = impact.Targets == relation,
                Select = () => Change(() => impact.Targets = relation)
            }).ToArray()));
        Number(prefix + "DamageScale", impact.DamageScale, value => impact.DamageScale = value);
        Number(prefix + "Force", impact.Force, value => impact.Force = value);
        Number(prefix + "Lift", impact.Lift, value => impact.Lift = value);
        Choice(prefix + "ForceOrigin", T("ForceOrigin." + impact.Origin), () => menu.Show(T(prefix + "ForceOrigin"),
            Enum.GetValues(typeof(ConstructionForceOrigin)).Cast<ConstructionForceOrigin>().Select(origin => new UiOptionMenuOption
            {
                Label = T("ForceOrigin." + origin), Selected = impact.Origin == origin,
                Select = () => Change(() => impact.Origin = origin)
            }).ToArray()));
    }
}
