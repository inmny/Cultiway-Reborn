using System.Collections.Generic;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Settings;
using Cultiway.Core.Visuals;
using Cultiway.Utils;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using UnityEngine;

namespace Cultiway.Content.Combat;

/// <summary>一次雷法执行最多利用一个有效水域，防止连锁再次触发自己。</summary>
internal struct ConductiveDischarge : IComponent
{
    /// <summary>该执行体已经传导。</summary>
    public bool Used;
}

/// <summary>水法与后续雷法通过真实存续区域承接，位置和敌我判定沿用原执行。</summary>
internal sealed class ConductiveWaterSystem : BaseSystem
{
    /// <summary>实际接触通知，离开原命中查询后统一处理。</summary>
    private static readonly List<(Entity skill, BaseSimObject target)> contacts = new();
    /// <summary>实际产生的水域传导次数，供验收观察。</summary>
    internal static int DischargeCount { get; private set; }

    /// <summary>内容初始化时只注册一次。</summary>
    internal static void Initialize()
    {
        Cultiway.Core.SkillLibV3.Manager.ExecutionConfigured += Configure;
        SkillHitResolver.ContactResolved += OnContact;
        ModClass.I.GeneralLogicSystems.Add(new ConductiveWaterSystem());
    }

    /// <summary>雷法准备一次独立传导机会。</summary>
    /// <param name="entity">实际技能执行体。</param>
    private static void Configure(Entity entity)
    {
        SkillEntity skill = entity.GetComponent<SkillEntity>();
        if (skill.VfxElement == SkillVfxElements.Lightning) entity.AddComponent(new ConductiveDischarge());
    }

    /// <summary>只记录带有传导资格的真实雷法接触。</summary>
    /// <param name="target">已完成接触的目标。</param>
    private static void OnContact(Entity entity, BaseSimObject target)
    {
        if (entity.TryGetComponent(out ConductiveDischarge discharge) && !discharge.Used) contacts.Add((entity, target));
    }

    /// <summary>在同一水域内按距离依次命中最多三个额外敌人，每次为原强度的四分之一。</summary>
    protected override void OnUpdateGroup()
    {
        for (int c = 0; c < contacts.Count; c++)
        {
            var contact = contacts[c]; Entity entity = contact.skill;
            if (!entity.IsAvailable() || contact.target == null || contact.target.isRekt() || entity.GetComponent<ConductiveDischarge>().Used) continue;
            SkillEntity skill = entity.GetComponent<SkillEntity>(); SkillContext context = entity.GetComponent<SkillContext>();
            foreach (Entity field in ModClass.I.W.Query<SkillEntity, SkillContext, Position>().Entities)
            {
                if (field.Tags.Has<TagPrefab>() || field.Tags.Has<TagInactive>() || field.Tags.Has<TagRecycle>() || field.Tags.Has<TagSkillAnimationNoTravelEffects>()) continue;
                SkillEntity fieldSkill = field.GetComponent<SkillEntity>();
                if (!fieldSkill.SkillContainer.TryGetComponent(out SkillSettings settings) || !settings.ConductiveWater) continue;
                SkillContext fieldContext = field.GetComponent<SkillContext>();
                if (fieldContext.ResolveAttackKingdom() != context.ResolveAttackKingdom()) continue;
                Vector3 center = field.GetComponent<Position>().value;
                float radius = Core.SkillLibV3.Utils.SkillEffectRadius.Resolve(field, fieldSkill.Asset.ImpactProfile.EffectRadius);
                if (Vector2.Distance(contact.target.current_position, new Vector2(center.x, center.y)) > radius) continue;
                entity.GetComponent<ConductiveDischarge>().Used = true; DischargeCount++;
                var candidates = new List<BaseSimObject>();
                foreach (var enemy in SkillUtils.IterEnemyInSphere(center, radius, context.SourceObj, context.ResolveAttackKingdom()))
                    if (enemy != contact.target && !enemy.isRekt()) candidates.Add(enemy);
                Vector3 previous = contact.target.GetSimPos();
                for (int i = 0; i < 3 && candidates.Count > 0; i++)
                {
                    candidates.Sort((a, b) => (a.GetSimPos() - previous).sqrMagnitude.CompareTo((b.GetSimPos() - previous).sqrMagnitude));
                    BaseSimObject target = candidates[0]; candidates.RemoveAt(0);
                    SpatialVisualEffects.Link(previous, target.GetSimPos(), new Color(.52f, .82f, 1f, .85f), .22f, .09f);
                    SkillHitResolver.HitTarget(skill.Asset, ref context, skill.SkillContainer, entity, target, false, .25f, applyGroundFx: false, applyStructuredEffects: false);
                    previous = target.GetSimPos();
                }
                break;
            }
        }
        contacts.Clear();
    }

    /// <summary>换世界释放所有等待中的对象引用。</summary>
    internal static void Clear() { contacts.Clear(); DischargeCount = 0; }
}
