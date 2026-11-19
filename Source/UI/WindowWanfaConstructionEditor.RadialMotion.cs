using System;
using System.IO;
using System.Linq;
using Cultiway.Core.Skills;
using Cultiway.Core.SkillLibV3.Usage;
using Cultiway.UI.Prefab;

namespace Cultiway.UI;

/// <summary>范围推拉编辑使用已有真实运动，不把主体接触面当作作用半径。</summary>
public sealed partial class WindowWanfaConstructionEditor
{
    /// <summary>先建立完整数值候选，通过共同检查后才修改草稿与撤销记录。</summary>
    /// <param name="node">当前草稿中的实际部位。</param>
    private void RadialMotionRows(ConstructionNode node)
    {
        var row = Row("RadialMotion", T("RadialMotionHint"));
        var toggle = UiElements.CreateIconToggle(row.UseInlineControls(32f), "RadialMotion", UiIcons.Options,
            node.RadialMotion != null, 28f, 22f);
        toggle.onValueChanged.AddListener(enabled => Commit(enabled ? new ConstructionRadialMotion
        {
            Targets = (node.Impact ?? session.Draft.Impact)?.Targets ?? SkillUseTargetRelation.Hostile
        } : null));
        UiTooltip.Set(toggle, "Cultiway.Construction.RadialMotion", "Cultiway.Construction.RadialMotionHint");
        if (node.RadialMotion == null) return;
        Value("RadialRadius", node.RadialMotion.Radius, (next, value) => next.Radius = value);
        Value("RadialSpeed", node.RadialMotion.Speed, (next, value) => next.Speed = value);
        Value("RadialInterval", node.RadialMotion.Interval, (next, value) => next.Interval = value);
        ContactInteger("RadialGroundItems", node.RadialMotion.GroundItemLimit,
            value => Edit(next => next.GroundItemLimit = value), draftChange: false);
        Choice("RadialTargets", T("Relation." + node.RadialMotion.Targets), () => menu.Show(T("RadialTargets"),
            new[] { SkillUseTargetRelation.Hostile, SkillUseTargetRelation.Friendly, SkillUseTargetRelation.Self }
                .Select(relation => new UiOptionMenuOption
                {
                    Label = T("Relation." + relation), Selected = node.RadialMotion.Targets == relation,
                    Select = () => Edit(next => next.Targets = relation)
                }).ToArray()));

        // 先检查完整的运动资料再写草稿，任何一栏修改都保留其他内容。
        void Value(string key, float value, Action<ConstructionRadialMotion, float> set)
        {
            var input = Row(key);
            FloatInput(input.UseInlineControls(104f), key, value, next => Edit(candidate => set(candidate, next)),
                true, 100f, draftChange: false);
        }
        void Edit(Action<ConstructionRadialMotion> edit)
        {
            var current = node.RadialMotion;
            var next = new ConstructionRadialMotion
            {
                Radius = current.Radius, Speed = current.Speed, Interval = current.Interval, Targets = current.Targets,
                GroundItemLimit = current.GroundItemLimit
            };
            edit(next);
            Commit(next);
        }
        void Commit(ConstructionRadialMotion candidate)
        {
            try
            {
                if (candidate != null) _ = new CompiledConstructionRadialMotion(candidate, "编辑范围推拉");
                Change(() => node.RadialMotion = candidate);
            }
            catch (InvalidDataException error)
            {
                Refresh();
                state.text = string.Format(T("Invalid"), error.Message);
            }
        }
    }
}
