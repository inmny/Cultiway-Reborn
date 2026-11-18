using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Core.Persistence;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Core.SkillLibV3.Usage;
using Cultiway.Core.Skills;
using Newtonsoft.Json.Linq;

namespace Cultiway.Content.Skills;

public sealed partial class ConstructedAbilityService
{
    /// <summary>新规则只从已有的真实接触及冻结材料中挑一项，不改变此前的抽取。</summary>
    private static AbilityDefinition GenerateApplication(AbilityDefinition definition, int seed, SynthesisResources resources)
    {
        var random = new Random(unchecked(seed ^ 0x5413aa72));
        if (random.Next(2) != 0) return definition;
        var choices = new List<(string Name, Func<AbilityDefinition> Apply)>();
        AddApplicationChoices(definition, ValidateDefinition(definition, cache: false), resources, random, choices);
        return choices.Count == 0 ? definition : choices[random.Next(choices.Count)].Apply();
    }

    /// <summary>每种改变先收集兼容的接触，避免部位多的法术反复抽到同一种改变。</summary>
    private static void AddApplicationChoices(AbilityDefinition draft, CompiledAbility plan, SynthesisResources resources,
        Random random, List<(string Name, Func<AbilityDefinition> Apply)> choices)
    {
        var sites = new List<(ConstructionImpact Impact, Action<ConstructionImpact> Set)>();
        foreach (var part in ConstructionContactEdits.Parts(draft.Root))
            if (plan.Nodes.Any(node => node.DeclarationPath == part.Path && node.AffectsActors && node.Resource.Contacts.Length > 0 &&
                                      (!node.BodyWindow || part.Node.Impact != null)))
                sites.Add((part.Node.Impact ?? draft.Impact, value => part.Node.Impact = value));
        if (plan.Body?.Construction.HasTouchRelations == true)
        {
            var body = draft.Body;
            var common = body.Impact ?? draft.Impact;
            if (body.Stages?.Length > 0)
                foreach (var stage in body.Stages) sites.Add((stage.Impact ?? common, value => stage.Impact = value));
            else sites.Add((common, value => body.Impact = value));
        }

        var friendly = sites.Where(site => site.Impact?.Targets is SkillUseTargetRelation.Friendly or SkillUseTargetRelation.Self).ToArray();
        Add("指定解除", friendly.Where(site => site.Impact.Restoration?.RemoveStatus == null &&
                                               site.Impact.Restoration?.Target != ConstructionRestorationTarget.Controller), impact =>
        {
            impact.Restoration ??= new ConstructionRestoration();
            impact.Restoration.RemoveStatus = new ConstructionStatusRemoval
            {
                Id = random.Next(2) == 0 ? StatusEffects.BodyStrain.id : StatusEffects.BodyExposure.id,
                Maximum = random.Next(1, 3)
            };
        });
        Add("抓持解救", friendly.Where(site => site.Impact.Targets == SkillUseTargetRelation.Friendly &&
                                               site.Impact.ReleaseGrab == ConstructionGrabRelease.None &&
                                               (plan.Use.Target != ActiveAbilityTargetMode.Self || !site.Impact.SelectedTargetOnly)),
            impact => impact.ReleaseGrab = ConstructionGrabRelease.Rescue);

        var inventory = draft.Inventory ?? Array.Empty<ConstructionInventoryInput>();
        if (resources != null && inventory.Length < 16 && inventory.Sum(input => input.Count) < 64)
        {
            var materials = InventoryChoices(draft, resources);
            if (materials.Length > 0)
                Add("装备修复", friendly.Where(site => site.Impact.EquipmentRepair == null), impact =>
                {
                    string id = Enumerable.Range(0, inventory.Length + 1).Select(index => "material_" + index)
                        .First(candidate => inventory.All(input => input.Id != candidate));
                    draft.Inventory = inventory.Concat(new[]
                    {
                        new ConstructionInventoryInput { Id = id, Count = 1, Shape = materials[random.Next(materials.Length)].Shape }
                    }).ToArray();
                    impact.EquipmentRepair = new ConstructionEquipmentRepair
                    {
                        Slot = EquipmentType.Armor, MaterialInput = id, Durability = random.Next(4, 9)
                    };
                });
        }

        Add("讯号条件", friendly.Where(site => (site.Impact.ConditionalContacts?.Length ?? 0) < 8), impact =>
        {
            impact.Signal ??= new ConstructionSignal
            {
                Kind = random.Next(2) == 0 ? ConstructionSignalKind.Warning : ConstructionSignalKind.Rally,
                Duration = random.Next(2, 5)
            };
            string status = impact.Signal.Kind == ConstructionSignalKind.Warning ? StatusEffects.SignalWarning.id : StatusEffects.SignalRally.id;
            impact.ConditionalContacts = (impact.ConditionalContacts ?? Array.Empty<ConstructionConditionalContact>()).Concat(new[]
            {
                new ConstructionConditionalContact
                {
                    RequiredStatus = status, MaximumHealthRatio = .75f,
                    Outcome = new ConstructionContactOutcome { Restoration = new ConstructionRestoration { Health = random.Next(4, 9) } }
                }
            }).ToArray();
        });

        void Add(string name, IEnumerable<(ConstructionImpact Impact, Action<ConstructionImpact> Set)> candidates,
            Action<ConstructionImpact> change)
        {
            var eligible = candidates.ToArray();
            if (eligible.Length == 0) return;
            choices.Add((name, () =>
            {
                var selected = eligible[random.Next(eligible.Length)];
                var impact = JObject.FromObject(selected.Impact).ToObject<ConstructionImpact>();
                change(impact);
                selected.Set(impact);
                return draft;
            }));
        }
    }
}

/// <summary>旧正式请求继续使用已保存的规则、原术和材料，无需补写或重抽。</summary>
internal sealed class CreationV32ToV33 : ISaveMigration
{
    public int FromVersion => 32;

    public JObject Apply(JObject data) => data;
}
