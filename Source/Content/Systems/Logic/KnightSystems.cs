using System.Collections.Generic;
using Cultiway.Const;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using NeoModLoader.api.attributes;

namespace Cultiway.Content.Systems.Logic;

/// <summary>
/// 每月为合格士兵掷骰觉醒为骑士。
/// 资格：职业∈{士兵,领袖,国王}、种族白名单含 Knight、未觉醒、非修仙。
/// 注意：NewCultisys 会 AddComponent，属结构性变更，不能在 query 循环内执行——
/// 先在循环内收集候选，循环外统一授予。
/// </summary>
public sealed class KnightAcquisitionSystem : QuerySystem<ActorBinder>
{
    private static readonly List<Actor> _candidates = new();
    private float _timer = TimeScales.SecPerMonth;

    public KnightAcquisitionSystem()
    {
        Filter.WithoutAnyTags(Tags.Get<TagRecycle>());
    }

    protected override void OnUpdate()
    {
        _timer -= Tick.deltaTime;
        if (_timer > 0f) return;
        _timer = TimeScales.SecPerMonth;

        _candidates.Clear();
        Query.ForEachComponents(([Hotfixable](ref ActorBinder binder) =>
        {
            var actor = binder.Actor;
            if (actor.isRekt()) return;
            if (!(actor.is_profession_warrior || actor.is_profession_leader || actor.is_profession_king)) return;

            var ae = actor.GetExtend();
            // 与修仙互斥；已是骑士则跳过
            if (ae.HasCultisys<Xian>() || ae.HasCultisys<Knight>()) return;
            // 种族白名单（仅人/兽人/矮人/精灵）
            if (!Cultisyses.GetAvailableCultisysIds(ae).Contains(nameof(Cultisyses.Knight))) return;
            if (!Randy.randomChance(KnightSetting.AcquisitionChancePerMonth)) return;

            _candidates.Add(actor);
        }));

        // 结构性变更必须在 query 循环外执行
        for (int i = 0; i < _candidates.Count; i++)
        {
            var ae = _candidates[i].GetExtend();
            ae.NewCultisys(Cultisyses.Knight);
            ModClass.I.WorldRecord.CheckAndLogFirstLevelup(Cultisyses.Knight.id, ae, ref ae.GetCultisys<Knight>());
        }
        _candidates.Clear();
    }
}

/// <summary>
/// 每月检查斗气蓄满的骑士并尝试突破。
/// 突破成败都会清空斗气，月度节流天然构成突破冷却（约 1 个月一次），避免一场战斗连升多级。
/// 注意：TryAdvanceNaturally 内部首次升级会 AddComponent(PowerLevel)，属结构性变更，
/// 必须在 query 循环外执行——先收集斗气蓄满者，循环外统一结算。
/// </summary>
public sealed class KnightBreakthroughSystem : QuerySystem<Knight, ActorBinder>
{
    private static readonly List<Actor> _pending = new();
    private float _timer = TimeScales.SecPerMonth;

    public KnightBreakthroughSystem()
    {
        Filter.AllComponents(ComponentTypes.Get<Knight>());
        Filter.WithoutAnyTags(Tags.Get<TagRecycle>());
    }

    protected override void OnUpdate()
    {
        _timer -= Tick.deltaTime;
        if (_timer > 0f) return;
        _timer = TimeScales.SecPerMonth;

        _pending.Clear();
        Query.ForEachComponents(([Hotfixable](ref Knight knight, ref ActorBinder binder) =>
        {
            var actor = binder.Actor;
            if (actor.isRekt()) return;

            var maxVigor = actor.stats[BaseStatses.MaxVigor.id];
            if (maxVigor <= 0f || knight.vigor < maxVigor - 0.1f) return;

            _pending.Add(actor);
        }));

        for (int i = 0; i < _pending.Count; i++)
        {
            var ae = _pending[i].GetExtend();
            Cultisyses.Knight.TryAdvanceNaturally(ae);
        }
        _pending.Clear();
    }
}
