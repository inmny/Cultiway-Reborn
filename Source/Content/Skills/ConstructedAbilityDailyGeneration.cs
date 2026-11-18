using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Core.Persistence;
using Cultiway.Core.Skills;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Core.SkillLibV3.Usage;
using Newtonsoft.Json.Linq;

namespace Cultiway.Content.Skills;

public sealed partial class ConstructedAbilityService
{
    /// <summary>受理时优先研究真实磨损的栏位，只冻结栏位，不保存活的装备对象。</summary>
    private static EquipmentType[] CaptureRepairSlots(Actor actor)
    {
        if (actor.equipment == null) return Array.Empty<EquipmentType>();
        var equipped = actor.equipment.Where(slot => slot.getItem() is Item item && item.isAlive() && item.getActor() == actor).ToArray();
        var worn = equipped.Where(slot => slot.getItem().needRepair()).ToArray();
        return (worn.Length > 0 ? worn : equipped).Select(slot => slot.type).OrderBy(slot => slot).ToArray();
    }

    /// <summary>把实际用途、主体和接触组合为完整资料，继续共用原编译、计费和执行。</summary>
    private static AbilityDefinition GenerateDailyUse(long creator, int seed, ConstructionInspiration inspiration, SynthesisResources resources)
    {
        var random = new Random(unchecked(seed ^ 0x3273abd5));
        if (random.NextDouble() >= .30d) return null;
        var choices = new List<Action<AbilityDefinition>> { Travel };
        if (resources.Resource == "wakan") choices.Add(Focus);
        if (resources.RepairSlots.Length != 0 && resources.InventoryMaterials.Length != 0) choices.Add(Repair);
        foreach (var action in resources.WorldActions.Where(action => action.Id is "nature.grow" or "crop.mature" or "land.cleanse"))
            choices.Add(draft => Work(draft, action.Id));

        var definition = new AbilityDefinition
        {
            Id = "Cultiway.Created." + Guid.NewGuid().ToString("N"), Creator = creator, Seed = seed,
            Resource = resources.Resource, Power = 1f, Lifetime = Range(.24f, .32f), TargetRange = 8f,
            Use = new ConstructionUse { Channels = ActiveAbilityChannel.World, Target = ActiveAbilityTargetMode.Self },
            Binding = new ConstructionBinding { Anchor = AbilityAnchor.Source, Height = .22f },
            Root = Surface("contact", "fireball", inspiration.Elements.wood >= inspiration.Elements.water ? "wood" : "water"),
            Impact = new ConstructionImpact { Targets = SkillUseTargetRelation.Self, DamageScale = 0f }
        };
        choices[random.Next(choices.Count)](definition);
        return definition;

        float Range(float low, float high) => low + (float)random.NextDouble() * (high - low);
        ConstructionNode Surface(string id, string form, string matter) => new()
        {
            Id = id, Form = form, Matter = matter, Scale = Range(.74f, .9f),
            OnContact = ConstructionContactResponse.Continue
        };

        void Travel(AbilityDefinition draft)
        {
            draft.Use.Target = ActiveAbilityTargetMode.Point;
            draft.Binding.FaceTarget = true;
            var entry = Surface("entry", "fireball", draft.Root.Matter);
            var exit = Surface("exit", "fireball", draft.Root.Matter);
            exit.Offset = new[] { 0f, 0f, Range(4f, 5.7f) };
            exit.ContactRole = ConstructionContactRole.Decoration;
            draft.Root = new ConstructionNode { Id = "gates", Form = "pivot", ContactRole = ConstructionContactRole.Decoration,
                Children = new[] { entry, exit } };
            draft.Steps = new[]
            {
                new ConstructionStep { Id = "travel", Operation = new ConstructionOperation
                    { Kind = ConstructionOperationKind.MapActor, MappingRadius = 1f }, Inputs = new()
                    { ["part"] = new() { Node = "root/entry" }, ["exit"] = new() { Node = "root/exit" } } }
            };
        }

        void Focus(AbilityDefinition draft)
        {
            draft.Impact.Statuses = new[]
            {
                new ConstructionStatus { Id = StatusEffects.CultivationFocus.id, Duration = Range(4f, 6.5f), Potency = 1f,
                    Stats = new() { [BaseStatses.MeditationFocus.id] = Range(.65f, .95f) } }
            };
        }

        void Repair(AbilityDefinition draft)
        {
            var material = resources.InventoryMaterials[random.Next(resources.InventoryMaterials.Length)];
            draft.Root.Matter = "iron";
            draft.Inventory = new[] { new ConstructionInventoryInput { Id = "repair", Shape = material.Shape, Count = 1 } };
            draft.Impact.EquipmentRepair = new ConstructionEquipmentRepair
            {
                Slot = resources.RepairSlots[random.Next(resources.RepairSlots.Length)], MaterialInput = "repair", Durability = random.Next(4, 10)
            };
        }

        void Work(AbilityDefinition draft, string action)
        {
            draft.Power = 0f;
            draft.TargetRange = Range(10f, 18f);
            draft.Use.Target = ActiveAbilityTargetMode.Point;
            draft.Binding = new ConstructionBinding { Anchor = AbilityAnchor.Target };
            bool round = random.Next(2) == 0;
            draft.Root = Surface("ground", round ? "fireball" : "sword", action == "land.cleanse" ? "water" : "wood");
            draft.Root.Scale = Range(1f, 1.8f);
            if (!round) draft.Root.Rotation = new[] { 0f, 90f, 0f };
            draft.Impact.WorldEffects = new[]
            {
                new ConstructionWorldEffect { Action = action, MaximumTiles = random.Next(8, 19) }
            };
        }
    }

    /// <summary>只给新规则读实际用途命名；旧请求的原名算法保持原样。</summary>
    private static string DailyUseName(CompiledAbility plan)
    {
        if (plan.Use.Channels != ActiveAbilityChannel.World) return null;
        if (TravelPlan(plan)) return "踏隙双门";
        if (MeditationFocusPlan(plan)) return "凝神吐纳诀";
        if (OwnEquipmentRepairRule(plan) != null) return "固器修复诀";
        if (plan.Body != null || !plan.HasWorldEffects || plan.HasWorldMaterials || plan.HasBarrier || plan.HasRecovery ||
            plan.ActorImpacts.Any(impact => impact.HasActorEffect)) return null;
        return plan.Impact.WorldEffects.FirstOrDefault()?.Action.Id switch
        {
            "nature.grow" => "催生地息诀",
            "crop.mature" => "养禾催熟诀",
            "land.cleanse" => "净土解秽诀",
            _ => null
        };
    }
}

/// <summary>原样保留已有请求；此前受理的规则不补写新装备资料。</summary>
internal sealed class CreationV34ToV35 : ISaveMigration
{
    public int FromVersion => 34;
    public JObject Apply(JObject data) => data;
}
