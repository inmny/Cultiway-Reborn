using Cultiway.Core.EventSystem;
using Cultiway.Core.EventSystem.Events;

namespace Cultiway.Content.Systems.Logic;

/// <summary>
/// 将核心技能系统的施法完成事件交给魔法知识成长规则处理。
/// </summary>
public sealed class MagicSpellCastCompletedEventSystem : GenericEventSystem<SkillCastCompletedEvent>
{
    protected override int MaxEventsPerUpdate => 256;

    protected override void HandleEvent(SkillCastCompletedEvent evt)
    {
        MagicSpellProgressionRules.RecordCompletedCast(evt.Caster, evt.SkillContainer, evt.EmittedCount);
    }
}
