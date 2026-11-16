using System;
using System.Linq;
using Cultiway.Core.Skills;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Settings;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Skills;

/// <summary>将原可导电水和雷法接触展开为普通介质通道及有限分送。</summary>
internal static class MediumContactMigration
{
    /// <summary>原水雷交互明确共用的通道，不根据模型或颜色寻找水体。</summary>
    private const string WaterChannel = "water.conductive";

    /// <summary>先声明实际水部位，再为真实攻击面添加有限雷法分送。</summary>
    /// <param name="definition">已经展开形体、材料和接触的构造。</param><param name="skill">原容器。</param>
    /// <param name="forceScale">接触余量换回原作用强度的比例。</param><param name="includeRoot">爆发载体不提前作用。</param>
    /// <param name="runtimeData">本次元素覆盖，动态雷与固定雷共用实际介质。</param>
    internal static AbilityDefinition Bind(AbilityDefinition definition, Entity skill, float forceScale = 1f, bool includeRoot = true, SkillCastRuntimeData runtimeData = default)
    {
        if (skill.TryGetComponent(out SkillSettings settings) && settings.ConductiveWater)
            definition.Root.MediumChannel = WaterChannel;
        if (NatureConstructionMigration.IsLightning(skill, runtimeData))
            BindNode(definition.Root, skill, forceScale, includeRoot, runtimeData, definition.Impact?.DamageScale ?? 1f);
        return definition;
    }

    /// <summary>原本会实际调用命中作用的部位才传递；同通道共用整次施放的机会。</summary>
    /// <param name="node">原真实部位。</param><param name="skill">原来源。</param>
    /// <param name="forceScale">原强度比例。</param><param name="include">是否已经进入实际作用阶段。</param>
    /// <param name="runtimeData">本次完整伤害组成。</param><param name="defaultDamageScale">省略部位作用时真实继承的全局伤害倍率。</param>
    private static void BindNode(ConstructionNode node, Entity skill, float forceScale, bool include, SkillCastRuntimeData runtimeData, float defaultDamageScale)
    {
        if (include && (node.ContactRole == ConstructionContactRole.Attack && (node.Impact?.DamageScale ?? defaultDamageScale) > 0f ||
            node.ContactRole == ConstructionContactRole.Barrier && node.Impact?.DamageScale > 0f))
        {
            node.Impact ??= new ConstructionImpact();
            var spread = new ConstructionContactSpread
            {
                Medium = new ConstructionSpreadMedium { Channel = WaterChannel, Matter = "water", State = "liquid",
                    MaximumTargets = 3, OncePerCast = true, InheritContactEffects = true, LinkForm = "thunder_channel", LinkRadius = .09f },
                Outcome = new ConstructionContactOutcome { Damage = new[] { new ConstructionContactDamage
                    { PerForce = .25f * forceScale, Element = NatureConstructionMigration.Element(skill, runtimeData) } } },
                BuildingDamagePerForce = .25f * forceScale, VisualSeconds = .22f
            };
            node.Impact.Spreads = (node.Impact.Spreads ?? Array.Empty<ConstructionContactSpread>()).Concat(new[] { spread }).ToArray();
        }
        if (node.Children != null) foreach (ConstructionNode child in node.Children) BindNode(child, skill, forceScale, true, runtimeData, defaultDamageScale);
    }
}
