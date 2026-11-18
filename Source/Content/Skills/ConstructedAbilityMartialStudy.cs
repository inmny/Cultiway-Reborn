using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Content.Combat;
using Cultiway.Core;
using Cultiway.Core.Persistence;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Core.Skills;
using Cultiway.Core.Visuals.Forms;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Cultiway.Content.Skills;

public sealed partial class ConstructedAbilityService
{
    /// <summary>按实际来源挑选发力物，再配合接触面和落脚构造连续姿态。排队后不再取样人物。</summary>
    /// <param name="actor">受理时的真实人物。</param>
    /// <param name="seed">此次固定种子。</param>
    /// <param name="inspiration">受理时固定的倾向。</param>
    /// <param name="parts">能绑定的实际器官、骨段及命名足。</param>
    private static AbilityDefinition CaptureMartialStudy(ActorExtend actor, int seed, ConstructionInspiration inspiration,
        (string[] Effectors, string[] Feet) parts)
    {
        var random = new System.Random(unchecked(seed ^ 0x52ed3a17));
        Actor source = actor.Base;
        if (random.NextDouble() >= .30d || source.isFlying() || source.isAffectedByLiquid() || source.under_forces ||
            SkillCasterContextService.Resolve(actor).Kind != SkillCarrierKind.Physical)
            return null;

        // 用真实手持物的类型筛选已适配模型，再选一个可作用的面或尖端，不登记另一件武器。
        string weapon = null, grip = null, contact = null;
        if (source.asset.is_humanoid && source.hasWeapon() && EquippedWeaponVisualService.IsCurrent(source, source.getWeapon()))
        {
            string group = source.getWeaponAsset().group_id;
            var forms = FormResources.Ids.OrderBy(id => id, StringComparer.Ordinal)
                .Where(id => FormResources.Load(id).Description.Held.Groups.Contains(group)).ToArray();
            if (forms.Length > 0)
            {
                string id = forms[random.Next(forms.Length)];
                var form = FormResources.Load(id);
                var grips = form.Description.Grips.Where(value => form.Grip(value.Id).Fits(form.Description.Held.Scale,
                    .035f * CompiledBodyAction.HandScale, .08f * CompiledBodyAction.HandScale)).ToArray();
                if (grips.Length > 0 && form.Contacts.Length > 0)
                {
                    weapon = id;
                    grip = grips[random.Next(grips.Length)].Id;
                    var faces = form.Contacts.Select(value => value.Id.Split('/')[0]).Distinct(StringComparer.Ordinal).ToArray();
                    contact = faces[random.Next(faces.Length)];
                }
            }
        }

        var effectors = new List<string>(parts.Effectors);
        if (source.asset.is_humanoid)
        {
            effectors.AddRange(new[] { "hand.left", "elbow.left", "shoulder.left", "head" });
            if (!source.hasWeapon()) effectors.AddRange(new[] { "hand.right", "elbow.right", "shoulder.right" });
        }

        if (weapon == null && effectors.Count == 0) return null;
        string effector = weapon == null ? effectors[random.Next(effectors.Count)] : "hand.right";
        var selected = new BodyPartSelection(effector);
        bool thrust = contact == "tip" || contact == null && random.NextDouble() < .28d + .45d * inspiration.Aggression;
        var kind = thrust
            ? random.Next(2) == 0 ? BodyActionKind.Thrust : BodyActionKind.Pierce
            : random.Next(4) switch
            {
                0 => BodyActionKind.Sweep, 1 => BodyActionKind.Cleave, 2 => BodyActionKind.Lift, _ => BodyActionKind.Press
            };
        var draft = BodyActionCatalog.Create("Cultiway.Creation.MartialInput", "近身发力", new BodyActionRequest
        {
            Effector = effector, Kind = kind, WeaponForm = weapon, Grip = grip ?? "grip", Contact = contact,
            Seconds = Range(.85f, 1.15f), Strength = Range(.8f, 1.1f), Amplitude = Range(.83f, 1.12f),
            Plane = Range(-9f, 9f), Arc = thrust ? Range(32f, 56f) : Range(65f, 102f)
        });
        draft.Resource = "stamina";
        draft.Body.MatchDistance = false;
        draft.Use = new ConstructionUse { Target = ActiveAbilityTargetMode.Object, Channels = ActiveAbilityChannel.Combat };

        // 接触关系与动作时钟一起保存，运行时仍检查真实形状和原目标。
        var motion = draft.Body;
        motion.Construction ??= new BodyConstructionDefinition
        {
            Parts = new[] { new BodyPartInput { Id = effector, Part = effector },
                new BodyPartInput { Id = "target", Subject = BodyPartSubject.Target, Part = "surface" } },
            Relations = new[] { new BodyRelationDefinition { Id = "touch", Kind = BodyRelationKind.Touch,
                From = effector, To = "target" } }
        };
        string[] feet = selected.HasPartRotation ? parts.Feet : null;
        if (feet == null || feet.Length >= 2)
        {
            bool left = random.Next(2) == 0;
            float distance = Range(.19f, .36f);
            float side = random.Next(3) == 0 ? 0f : Range(.06f, .135f) * (left ? -1f : 1f);
            AddStudyStep(motion, feet, left, distance, side, Range(.07f, .105f));
        }

        var plan = ValidateDefinition(draft, cache: false);
        if (!BodyActionBinding.CanBind(actor, plan.Body)) return null;
        if (BodyActionBinding.TryCreate(source, plan.Body, Vector3.right, BodyFoot(source) + Vector3.right,
                SpatialActorPose.Height(source)) == null) return null;
        return draft;

        float Range(float low, float high) => low + (high - low) * (float)random.NextDouble();
    }

    /// <summary>将一次换步加入发力关键点：起势抬领先足，发力落地，收势再换后足。</summary>
    /// <param name="motion">已生成的单次发力。</param>
    /// <param name="feet">非人身体实际命名足，空值为人形双足。</param>
    /// <param name="left">决定哪一侧先抬足。</param>
    /// <param name="distance">相对体高的累积前移。</param>
    /// <param name="side">相对体高的累积侧移。</param>
    /// <param name="lift">相对体高的抬足量。</param>
    private static void AddStudyStep(BodyActionDefinition motion, string[] feet, bool left, float distance,
        float side, float lift)
    {
        float[] progress = { 0f, .17f, .73f, .94f, 1f };
        motion.Construction.Footwork = new ConstructionPath
        {
            Forward = null,
            Keys = motion.Keys.Select((key, index) => new ConstructionPathKey
                { At = key.At, Point = new[] { distance * progress[index], side * progress[index], 0f } }).ToArray()
        };
        for (int i = 0; i < motion.Keys.Length; i++)
        {
            var support = new BodySupportMotion();
            float first = i == 1 ? lift : 0f, second = i == 3 ? lift : 0f;
            if (feet == null)
            {
                support.LeftFoot[2] = left ? first : second;
                support.RightFoot[2] = left ? second : first;
            }
            else
            {
                support.Parts = feet.Select((foot, index) => (foot, index)).ToDictionary(pair => pair.foot,
                    pair => new[] { 0f, 0f, (pair.index % 2 == 0) == left ? first : second }, StringComparer.Ordinal);
            }
            motion.Keys[i].Support = support;
        }
    }
}

/// <summary>保留旧规则和完整冻结资料，不从读档后的手持物补写动作。</summary>
internal sealed class CreationV35ToV36 : ISaveMigration
{
    /// <summary>上一版正式创法区段。</summary>
    public int FromVersion => 35;
    /// <summary>新动作资料只有新受理请求才产生。</summary>
    /// <param name="data">保存层提供的旧资料副本。</param>
    public JObject Apply(JObject data) => data;
}
