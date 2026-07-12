namespace Cultiway.Core;

public delegate bool ExternalMagicActionPrepareCheck(ActorExtend caster, BaseSimObject target);
public delegate int ExternalMagicActionWeightMultiplierResolver(ActorExtend caster, BaseSimObject target);

/// <summary>
/// 将 Content 提供的外部魔法动作接入 Core 战斗决策。
/// 可准备判断用于保留并接近目标，当前权重倍率用于即时可用性与动作池构建。
/// </summary>
public sealed class ExternalMagicActionProvider
{
    public CombatActionAsset Action { get; }
    public ExternalMagicActionPrepareCheck CanPrepare { get; }
    public ExternalMagicActionWeightMultiplierResolver ResolveCurrentWeightMultiplier { get; }

    public ExternalMagicActionProvider(CombatActionAsset action, ExternalMagicActionPrepareCheck canPrepare,
        ExternalMagicActionWeightMultiplierResolver resolveCurrentWeightMultiplier)
    {
        Action = action;
        CanPrepare = canPrepare;
        ResolveCurrentWeightMultiplier = resolveCurrentWeightMultiplier;
    }
}
