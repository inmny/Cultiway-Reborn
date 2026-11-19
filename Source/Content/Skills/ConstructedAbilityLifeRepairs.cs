using System.Collections.Generic;
using Cultiway.Content.Combat;
using Cultiway.Core.Visuals.Forms;
using UnityEngine;

namespace Cultiway.Content.Skills;

public sealed partial class ConstructedAbilityService
{
    /// <summary>实收的恢复留下短暂组织或枝芽，不再进行结算。</summary>
    private static readonly List<LifeRepair> lifeRepairs = new(32);

    /// <summary>按现有规则恢复生命，实际超过恢复前的生命值才显示接合。</summary>
    /// <param name="target">已确认的受术人物。</param><param name="amount">该段可使用的治疗量。</param>
    internal static void RestoreLivingActor(Actor target, float amount)
    {
        int before = target.getHealth();
        long id = target.id, generation = worldGeneration;
        CombatResourceEffects.RestoreHealth(target, amount);
        if (generation == worldGeneration && target.data?.id == id && target.getHealth() > before)
            AddLifeRepair(target, false, target.getHealth() - before, World.world.getCurWorldTime());
    }

    /// <summary>只在原植被真实恢复后绘制木芯与枝芽。</summary>
    /// <param name="plant">原受损植被。</param><param name="amount">实际恢复量。</param><param name="time">实际世界接触时间。</param>
    internal static void AddPlantRepair(Building plant, int amount, double time) => AddLifeRepair(plant, true, amount, time);

    /// <summary>每个对象仅保留最近一次确认，避免重复接触堆出不透明光团。</summary>
    /// <param name="target">真实受术对象。</param><param name="plant">是否为原版植被。</param>
    /// <param name="amount">实际变化量。</param><param name="time">本次实际结算时点。</param>
    private static void AddLifeRepair(BaseSimObject target, bool plant, int amount, double time)
    {
        for (int i = 0; i < lifeRepairs.Count; i++)
            if (lifeRepairs[i].Target == target && lifeRepairs[i].Id == target.id)
            {
                lifeRepairs.RemoveAt(i);
                break;
            }
        if (lifeRepairs.Count == 32) lifeRepairs.RemoveAt(0);
        lifeRepairs.Add(new LifeRepair(target, target.id, plant, amount, time));
    }

    /// <summary>组织在人物身上向内合拢，枝芽在同一株植被根部向外长出。</summary>
    /// <param name="now">本轮确认完成的世界时间。</param>
    private void AppendLifeRepairs(double now)
    {
        for (int i = lifeRepairs.Count - 1; i >= 0; i--)
        {
            LifeRepair repair = lifeRepairs[i];
            BaseSimObject target = repair.Target;
            float phase = (float)((now - repair.At) / (repair.Plant ? .88f : .72f));
            if (phase >= 1f || target.isRekt() || !target.isAlive() || target.id != repair.Id)
            {
                lifeRepairs.RemoveAt(i);
                continue;
            }
            if (phase < 0f) continue;

            Vector3 point;
            float height;
            if (repair.Plant)
            {
                if (target.current_tile == null || ((Building)target).chopped) continue;
                point = target.current_tile.posV3;
                height = 2.45f;
            }
            else
            {
                var actor = (Actor)target;
                point = SpatialActorPose.Foot(actor);
                height = SpatialActorPose.Height(actor);
            }
            point.z += terrain?.Sample(point.x, point.y) ?? 0f;
            float grow = Mathf.SmoothStep(.22f, 1f, phase * 3.1f);
            float leave = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.45f, 1f, phase));
            float strength = .78f + .24f * Mathf.Sqrt(Mathf.Clamp01(4f * repair.Amount / target.getMaxHealth()));
            float spread = grow * strength * (repair.Plant ? 1f : 1f - .24f * leave);
            Vector3 scale = new(height * spread, height * spread, height * grow * .85f);
            var pose = Matrix4x4.TRS(point, Quaternion.identity, scale);
            var form = FormResources.Load(repair.Plant ? "life_wood_graft" : "life_tissue_join");
            visuals.Add(new FormVisualInstance(form, pose, dye: new Color(1f, 1f, 1f, 1f - leave), terrain: terrain));
        }
    }

    /// <summary>短暂画面引用原对象，附带身份以免对象复用时错画。</summary>
    private readonly struct LifeRepair
    {
        /// <summary>实际受益者与本次受术时的身份。</summary>
        internal readonly BaseSimObject Target;
        /// <summary>结算时的原版对象编号。</summary>
        internal readonly long Id;
        /// <summary>本次是否修复活木。</summary>
        internal readonly bool Plant;
        /// <summary>实际补回的生命量。</summary>
        internal readonly int Amount;
        /// <summary>确认恢复的世界时间。</summary>
        internal readonly double At;

        /// <summary>保留当前可见结果，不保存到世界资料里。</summary>
        /// <param name="target">实际对象。</param><param name="id">实际身份。</param><param name="plant">是否修木。</param>
        /// <param name="amount">实际恢复。</param><param name="at">世界时点。</param>
        internal LifeRepair(BaseSimObject target, long id, bool plant, int amount, double at)
        {
            Target = target;
            Id = id;
            Plant = plant;
            Amount = amount;
            At = at;
        }
    }
}
