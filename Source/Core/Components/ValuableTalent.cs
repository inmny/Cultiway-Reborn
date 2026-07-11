using Cultiway.Utils;
using Friflo.Engine.ECS;
using UnityEngine;
using Friflo.Json.Fliox;
using UnityEngine.PlayerLoop;

namespace Cultiway.Core.Components;

/// <summary>
/// 角色的先天资质。包括各种基础资质。
/// </summary>
public struct ValuableTalent : IComponent
{
    public float DivineSense { get; private set;}
    [Ignore]
    public BaseStats Stats { get; }

    public static ValuableTalent Roll()
    {
        return new ValuableTalent(Mathf.Abs(RdUtils.NextStdNormal()));
    }
    public ValuableTalent(float divineSense, bool update = true)
    {
        DivineSense = divineSense;

        Stats = new();
        if (update)
        {
            Update();
        }
    }
    public override string ToString()
    {
        return $"DivineSense: {DivineSense:F1}";
    }
    private void Update()
    {
        Stats.clear();
        Stats[nameof(WorldboxGame.BaseStats.DivineSense)] = DivineSense * 100;
        Stats["Mod" + nameof(WorldboxGame.BaseStats.DivineSense)] = Mathf.Exp(DivineSense);
    }
    public void UpdateValue(ValuableTalent other)
    {
        DivineSense = other.DivineSense;
        Update();
    }
}
