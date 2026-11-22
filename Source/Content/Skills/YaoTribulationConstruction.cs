using Cultiway.Const;
using Cultiway.Core;
using Cultiway.Core.Skills;
using Cultiway.Core.SkillLibV3.ActiveAbilities;

namespace Cultiway.Content.Skills;

/// <summary>妖兽天劫从固定劫心落雷，圆面与实际点名接触使用同一尺寸。</summary>
internal static class YaoTribulationConstruction
{
    /// <summary>天劫环境落雷的稳定编号。</summary>
    internal const string StrikeId = "Cultiway.Tribulation.Yao.Strike";

    /// <summary>劫程以原世界年的六十分之一计算，不延长原三秒落雷间隔。</summary>
    private const float StrikeAt = 1f / TimeScales.SecPerYear;

    /// <summary>雷光在下一记落雷可以开始前退尽。</summary>
    private const float EndAt = 2.7f / TimeScales.SecPerYear;

    /// <summary>只在落雷的一刻接触，之后留出时间让闪光收束。</summary>
    /// <param name="element">该渡劫者本次真正承受的弱点元素。</param>
    internal static AbilityDefinition Strike(ElementComposition element) => new()
    {
        Id = StrikeId, Name = "妖兽天劫·落雷", Resource = "none",
        Lifetime = EndAt, Cost = 0f, Cooldown = 0f, Power = 1f,
        Use = new ConstructionUse { Channels = ActiveAbilityChannel.World, Target = ActiveAbilityTargetMode.Object },
        Binding = new ConstructionBinding { Anchor = AbilityAnchor.Source },
        Impact = new ConstructionImpact { SelectedTargetOnly = true, DamageScale = 1f, Element = element },
        Root = new ConstructionNode
        {
            Id = "origin", Form = "pivot", ContactRole = ConstructionContactRole.Decoration,
            Children = new[]
            {
                new ConstructionNode
                {
                    Id = "reach", Form = "yao_tribulation_ground", Matter = "lightning", State = "energy",
                    Scale = 4f / 2.5f, Volume = 0f, ContactActorCenter = true, IgnoreFormClashes = true,
                    ActiveFrom = StrikeAt, ActiveUntil = StrikeAt + .01f,
                    Contact = new ConstructionContactPolicy
                    {
                        Mode = ConstructionContactMode.Pulse, Interval = StrikeAt, MaximumPerTarget = 1
                    },
                    Operations = new[]
                    {
                        new ConstructionOperation { Kind = ConstructionOperationKind.Grow, Seconds = StrikeAt },
                        new ConstructionOperation
                        {
                            Kind = ConstructionOperationKind.Grow, Seconds = .01f, TargetScale = .02f,
                            When = new ConstructionCondition
                            {
                                Trigger = ConstructionChangeTrigger.SizeAbove, Threshold = .98f, Delay = .012f
                            }
                        }
                    }
                },
                new ConstructionNode
                {
                    Id = "bolt", Form = "lightning_strike_0", Matter = "lightning", State = "energy",
                    Scale = 1.65f, AppearAt = StrikeAt, ContactRole = ConstructionContactRole.Decoration,
                    Operations = new[]
                    {
                        new ConstructionOperation
                        {
                            Kind = ConstructionOperationKind.Animate, Clip = "dissolve",
                            At = StrikeAt, Seconds = EndAt - StrikeAt
                        }
                    }
                }
            }
        }
    };
}
