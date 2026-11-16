using System;
using System.Linq;
using Cultiway.Core.Skills;
using Cultiway.Core.SkillLibV3.Usage;
using Newtonsoft.Json;

namespace Cultiway.UI;

/// <summary>通过普通表单修改目标筛选和真实接触作用。</summary>
public sealed partial class WindowWanfaConstructionEditor
{
    /// <summary>整招默认影响选取与身体，部位可以明确覆盖实际接触规则。</summary>
    private void ImpactPage()
    {
        Row("ImpactRules", T("ImpactRulesHint"));
        Toggle("CustomImpact", session.Draft.Impact != null,
            value => session.Draft.Impact = value ? new ConstructionImpact() : null);
        if (session.Draft.Impact != null) ImpactRows(session.Draft.Impact, "");
        else Row("DefaultImpact", T("DefaultImpactHint"));
        if (session.Draft.Body != null)
        {
            Row("BodyImpactRules", T("BodyImpactHint"));
            Toggle("BodyImpact", session.Draft.Body.Impact != null, value => session.Draft.Body.Impact = value
                ? JsonConvert.DeserializeObject<ConstructionImpact>(
                    JsonConvert.SerializeObject(session.Draft.Impact ?? new ConstructionImpact()))
                : null);
            if (session.Draft.Body.Impact != null) ImpactRows(session.Draft.Body.Impact, "Body");
            ContactPolicyRows(session.Draft.Body.Contact, null, "Body");
            BodyForceResponseRows(session.Draft.Body);
            BodyStageImpact(session.Draft.Body);
        }

        if (parts.Count == 0) return;
        ConstructionNode node = parts[part];
        ActivationRows(node);
        MotionRows(node);
        if (node.ContactRole == ConstructionContactRole.Decoration)
        {
            Row("NoActorImpact", T("NoActorImpactHint"));
            return;
        }

        ConnectionText("NodeContactGroup", node.ContactGroup ?? "", value => node.ContactGroup = string.IsNullOrWhiteSpace(value) ? null : value);
        ContactPolicyRows(node.Contact, node, "Node");
        ImpactOption("Node", "OnContact", node.OnContact, value => node.OnContact = value);
        Toggle("NodeImpact", node.Impact != null, value =>
        {
            ConstructionImpact basis = session.Draft.Impact ?? new ConstructionImpact();
            node.Impact = value
                ? JsonConvert.DeserializeObject<ConstructionImpact>(JsonConvert.SerializeObject(basis))
                : null;
        });
        if (node.Impact != null) ImpactRows(node.Impact, "Node");
    }

    /// <summary>同一规则用于整招及生成部位；关闭覆盖时直接恢复继承。</summary>
    /// <param name="impact">独立草稿中的接触作用。</param>
    /// <param name="prefix">整招和部位表单名称。</param>
    private void ImpactRows(ConstructionImpact impact, string prefix)
    {
        Choice(prefix + "Targets", T("Relation." + impact.Targets), () => menu.Show(T(prefix + "Targets"),
            new[] { SkillUseTargetRelation.Hostile, SkillUseTargetRelation.Friendly, SkillUseTargetRelation.Self }
                .Select(relation => new UiOptionMenuOption
                {
                    Label = T("Relation." + relation), Selected = impact.Targets == relation,
                    Select = () => Change(() => impact.Targets = relation)
                }).ToArray()));
        Number(prefix + "DamageScale", impact.DamageScale, value => impact.DamageScale = value);
        ContactEffectRows(impact, prefix);
        ContactResultRows(impact, prefix);
        Toggle(prefix + "DirectedImpulse", impact.Impulse != null, value =>
        {
            impact.Impulse = value ? new float[3] : null;
            if (value)
            {
                impact.Force = 0f;
                impact.Launch = false;
            }
            else impact.ForceRouting = null;
        });
        if (impact.Impulse == null) Number(prefix + "Force", impact.Force, value => impact.Force = value);
        else
            for (int i = 0; i < 3; i++)
            {
                int axis = i;
                Number(prefix + new[] { "ImpulseForward", "ImpulseSide", "ImpulseUp" }[i], impact.Impulse[i],
                    value => impact.Impulse[axis] = value);
            }

        if (impact.Impulse != null)
        {
            Toggle(prefix + "ForceRouting", impact.ForceRouting != null, value => impact.ForceRouting = value
                ? NewForceRouting(ConstructionForceDestination.Contacts, .5f)
                : null);
            if (impact.ForceRouting != null) ForceRoutingRows(impact.ForceRouting);
        }

        Number(prefix + "SlideSeconds", impact.SlideSeconds, value =>
        {
            impact.SlideSeconds = value;
            if (value > 0f)
            {
                impact.Launch = false;
                if (impact.Impulse != null) impact.Impulse[2] = 0f;
            }
        });
        if (impact.Impulse == null && impact.SlideSeconds == 0f)
        {
            Number(prefix + "Lift", impact.Lift, value => impact.Lift = value);
            Toggle(prefix + "Launch", impact.Launch, value => impact.Launch = value);
        }

        Number(prefix + "StaggerSeconds", impact.StaggerSeconds, value => impact.StaggerSeconds = value);
        Number(prefix + "BindSeconds", impact.BindSeconds, value => impact.BindSeconds = value);
        Number(prefix + "StrainSeconds", impact.StrainSeconds, value => impact.StrainSeconds = value);
        Number(prefix + "ExposureSeconds", impact.ExposureSeconds, value => impact.ExposureSeconds = value);
        Choice(prefix + "ForceOrigin", T("ForceOrigin." + impact.Origin), () => menu.Show(T(prefix + "ForceOrigin"),
            Enum.GetValues(typeof(ConstructionForceOrigin)).Cast<ConstructionForceOrigin>().Select(origin =>
                new UiOptionMenuOption
                {
                    Label = T("ForceOrigin." + origin), Selected = impact.Origin == origin,
                    Select = () => Change(() => impact.Origin = origin)
                }).ToArray()));
    }
}
