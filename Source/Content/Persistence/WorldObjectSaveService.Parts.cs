using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cultiway.Abstract;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Components;
using Friflo.Engine.ECS;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Cultiway.Utils.Extension;
using Cultiway.Content.CreatureCompositions.Components;
using Cultiway.Content.KnightCombat;

namespace Cultiway.Content.Persistence;

/// <summary>明确列出进入世界存档的玩法资料；新增玩法在这里登记稳定编号。</summary>
internal static partial class WorldObjectSaveService
{
    /// <summary>先登记物件资料，关系在第二次恢复时才生效。</summary>
    private static void RegisterParts()
    {
        Component<EntityName>("name");
        Component<SpecialItem>("item");
        Component<ItemShape>("item.shape", value => Require(ModClass.L.ItemShapeLibrary.get(value.shape_id) != null, "物品形状已移除"));
        Component<ItemIconData>("item.icon");
        Component<ItemLevel>("item.level");
        Component<ItemCreation>("item.creation");
        Component<ElementRoot>("item.root");
        Component<Jindan>("item.core");
        Component<Cultibook>("cultibook");
        Component<Elixirbook>("elixirbook");
        Component<Skillbook>("skillbook");
        Component<ConstructionBook>("skillbook.construction", value => Require(
            Cultiway.Content.Skills.ConstructedAbilityService.Contains(value.AbilityId), "书中构造已移除"));
        Component<Talisman>("talisman");
        Component<MagicScroll>("scroll");
        Component<Artifact>("artifact");
        Component<ArtifactAtomData>("artifact.atoms");
        Component<ArtifactMaterialData>("artifact.materials");
        Component<ArtifactAppearance>("artifact.appearance");
        Component<ArtifactAbilitySet>("artifact.abilities", value => { foreach (var ability in value.abilities) Require(Libraries.Manager.ArtifactAbilityLibrary.get(ability.ability_id) != null, "法宝能力已移除"); });
        Component<ArtifactControlProfile>("artifact.control");
        Component<ArtifactAttunement>("artifact.attunement");
        Component<ArtifactStorageState>("artifact.storage");
        Component<ArtifactSpiritState>("artifact.spirit");
        Component<ArtifactDeployment>("artifact.deployment");
        Component<ArtifactManifestation>("artifact.manifestation");
        Component<ArtifactBody>("artifact.body");
        Component<ArtifactSpatialAttackMotion>("artifact.spatial-motion");
        Component<ArtifactIndependentMotion>("artifact.independent-motion");
        Component<ArtifactSummon>("artifact.summon");
        Component<ArtifactSpiritAvatar>("artifact.spirit-avatar");
        Component<ArtifactVehiclePassenger>("artifact.passenger");
        Component<SectTreasureLoan>("sect.loan");
        Component<MasterApprenticeState>("master");
        Component<ActorCultibookState>("cultibook.practice");
        Component<MagicStudyState>("magic.study");
        Component<MagicScrollStudyState>("magic.scroll-study");
        Component<SkillCooldownRuntime>("skill.cooldowns");
        Component<SkillBlueprintOrigin>("skill.origin");
        Component<SpecializedActiveAbility>("skill.specialized");
        Component<StatusComponent>("status", value => Require(ModClass.L.StatusEffectLibrary.get(value.id) != null, "状态定义已移除"));
        Component<StatusOverwriteStats>("status.stats");
        Component<StatusStatsMultiplier>("status.multiplier");
        Component<StatusPotency>("status.potency");
        Component<StatusTickState>("status.tick");
        Component<StatusAnimationState>("status.animation");
        // 粒子只需重建发射能力，不延续按画面时间推进的发射计时。
        Component<StatusParticleState>("status.particle", normalize: _ => new StatusParticleState());
        Component<CreatureEffectStatusSource>("status.organ-source");
        Component<KnightBoundWeaponStatus>("status.knight-weapon", value => Require(
            value.Weapon != null && value.Technique != null, "战技状态的武器或定义已移除"));
        Component<CoreFormationEffectState>("status.formation");
        Component<AliveTimer>("life.elapsed");
        Component<AliveTimeLimit>("life.duration");
        Component<Position>("position");
        Component<Rotation>("rotation");
        Component<Scale>("scale");
        Component<CraftSession>("craft.session");
        Component<CraftingElixir>("craft.elixir", value => Require(
            Libraries.Manager.ElixirLibrary.get(value.elixir_id) != null, "半成品丹方已移除"));
        Component<CraftingArtifact>("craft.artifact");
        Component<CraftWaste>("craft.waste");
        Component<YuanyingSoulState>("yuanying.soul");
        Component<YuanyingSeed>("yuanying.seed");
        Component<YuanshenRuntimeState>("yuanshen.runtime");
        Component<YuanshenSoulCarrierState>("yuanshen.soul-carrier");
        Component<YuanshenNodeState>("yuanshen.node", value => Require(
            World.world.units.get(value.owner_actor_id)?.GetExtend().HasComponent<Yuanshen>() == true, "元神节点所属人物或成果已不存在"));
        Component<YuanshenNodeTask>("yuanshen.node-task");
        Component<ArtifactYuanshenControl>("yuanshen.artifact-control");
        Component<YuanshenAnchorState>("yuanshen.anchor", value => Require(
            World.world.units.get(value.owner_actor_id) != null && World.world.buildings.get(value.building_id) != null,
            "元神设施所属人物或建筑已不存在"));
        Component<YuanshenAnchorNetworkRuntime>("yuanshen.network");
        Component<YuanshenAdvancedNodeState>("yuanshen.advanced-node");
        Component<YuanshenDharmaAppearance>("yuanshen.dharma");
        Component<YuanshenAnchorResidence>("yuanshen.anchor-residence");
        Component<YuanshenAnchorTransitState>("yuanshen.transit");
        Component<YuanshenBodilessTransitState>("yuanshen.bodiless-transit");
        Component<YuanshenAvatarPreparationState>("yuanshen.avatar-preparation");
        Component<BodylessYuanshenState>("yuanshen.bodyless");
        Component<YuanshenBodyConsent>("yuanshen.consent");
        Component<YuanshenPossessionState>("yuanshen.possession");
        Component<YuanshenBodyTransferLock>("yuanshen.body-lock");
        Component<YuanshenBodyRecoveryRuntime>("yuanshen.body-recovery");
        Component<YuanshenReconstructionState>("yuanshen.reconstruction");
        Component<YuanshenArtifactAnchorState>("yuanshen.artifact-anchor");
        Component<YuanshenArtifactAnchorBinding>("yuanshen.anchor-binding");
        Component<YuanshenArtifactRescueCooldown>("yuanshen.rescue-cooldown");
        Tag<TimedCultivationTag>("cultivation.timed-tags");
        Component<XianBreakthroughState>("cultivation.breakthrough");
        Component<Elixir>("elixir", value => Require(Libraries.Manager.ElixirLibrary.get(value.elixir_id) != null, "丹方已移除"));
        Component<ArtifactAbilityRuntime>("artifact.runtime", normalize: NormalizeAbilities);
        Tag<TagIngredient>("ingredient");
        Tag<TagElixirDataGain>("elixir.data-gain");
        Tag<TagElixirStatusGain>("elixir.status-gain");
        Tag<TagElixirRestore>("elixir.restore");
        Tag<TagUncompleted>("craft.uncompleted");
        Tag<TagOccupied>("craft.occupied");
        Tag<TagConsumed>("craft.consumed");
        Tag<TagSuspendAliveTimeLimit>("life.suspended");
        Link<InventoryRelation>("inventory");
        Link<StatusRelation>("status");
        Link<SkillMasterRelation>("skill");
        Link<MasterApprenticeRelation>("apprentice");
        Link<EquippedArtifactRelation>("equipped");
        Link<SectTreasureRelation>("sect-owner");
        Link<CraftOccupyingRelation>("craft-ingredient");
        Link<MagicSpellKnowledgeRelation>("magic-knowledge");
        Link<ArtifactSectInstallationRelation>("sect-installation");
        Link<ArtifactSpiritAvatarRelation>("spirit-avatar");
        Link<ArtifactVehicleRelation>("vehicle");
        Link<ArtifactVehiclePassengerRelation>("passenger");
    }

    /// <summary>飞行中的短时施法会话在读档后结束；充能、冷却与部署继续保存。</summary>
    /// <param name="runtime">当前运行状态。</param>
    private static ArtifactAbilityRuntime NormalizeAbilities(ArtifactAbilityRuntime runtime)
    {
        runtime.abilities = (ArtifactAbilityRuntimeEntry[])(runtime.abilities ?? Array.Empty<ArtifactAbilityRuntimeEntry>()).Clone();
        for (int i = 0; i < runtime.abilities.Length; i++)
        {
            ref var ability = ref runtime.abilities[i];
            ability.active_execution = default;
            if (ability.activity_kind == ArtifactAbilityActivityKind.SkillExecution)
            {
                ability.activity_kind = ArtifactAbilityActivityKind.None;
                ability.activity_until = 0;
                ability.has_activity_position = false;
                ability.has_activity_direction = false;
            }
        }
        return runtime;
    }

    /// <summary>保存人物掌握的知识，不保存资产对象引用。</summary>
    /// <param name="actor">当前人物。</param>
    private static KnowledgeSaveData CaptureKnowledge(ActorExtend actor) => new()
    {
        Cultibooks = actor.GetAllMaster<CultibookAsset>().ToDictionary(entry => entry.Item1.id, entry => entry.Item2),
        Elixirs = actor.GetAllMaster<ElixirAsset>().ToDictionary(entry => entry.Item1.id, entry => entry.Item2),
        Bloodlines = actor.GetAllMaster<BloodlineAsset>().ToDictionary(entry => entry.Item1.id, entry => entry.Item2)
    };

    /// <summary>原版书籍保留其承载的功法、丹方和熟练程度。</summary>
    /// <param name="book">承载知识的原版书册。</param>
    private static KnowledgeSaveData CaptureKnowledge(BookExtend book) => new()
    {
        Cultibooks = book.GetAllMaster<CultibookAsset>().ToDictionary(entry => entry.Item1.id, entry => entry.Item2),
        Elixirs = book.GetAllMaster<ElixirAsset>().ToDictionary(entry => entry.Item1.id, entry => entry.Item2),
        Bloodlines = book.GetAllMaster<BloodlineAsset>().ToDictionary(entry => entry.Item1.id, entry => entry.Item2)
    };

    /// <summary>原版计算人物属性前重新掌握知识。</summary>
    /// <param name="actor">当前人物。</param>
    /// <param name="data">待保存或恢复的资料。</param>
    private static void RestoreKnowledge(ActorExtend actor, KnowledgeSaveData data)
    {
        if (data == null) return;
        RestoreMastery(data.Cultibooks, Libraries.Manager.CultibookLibrary, actor.Master);
        RestoreMastery(data.Elixirs, Libraries.Manager.ElixirLibrary, actor.Master);
        RestoreMastery(data.Bloodlines, Libraries.Manager.BloodlineLibrary, actor.Master);
    }

    /// <summary>恢复书籍承载的知识，不触发赠书或贡献奖励。</summary>
    /// <param name="book">承载知识的原版书册。</param>
    /// <param name="data">待保存或恢复的资料。</param>
    private static void RestoreKnowledge(BookExtend book, KnowledgeSaveData data)
    {
        if (data == null) return;
        RestoreMastery(data.Cultibooks, Libraries.Manager.CultibookLibrary, book.Master);
        RestoreMastery(data.Elixirs, Libraries.Manager.ElixirLibrary, book.Master);
        RestoreMastery(data.Bloodlines, Libraries.Manager.BloodlineLibrary, book.Master);
    }

    /// <summary>按条目恢复知识，删除的定义只影响对应一项。</summary>
    /// <param name="entries">按稳定编号排列的知识与熟练程度。</param>
    /// <param name="library">当前使用的知识定义库。</param>
    /// <param name="master">重新绑定知识与熟练程度的方法。</param>
    private static void RestoreMastery<T>(Dictionary<string, float> entries, AssetLibrary<T> library, Action<T, float> master)
        where T : Asset, IDeleteWhenUnknown
    {
        foreach (var entry in entries)
            Attempt(entry.Key, () =>
            {
                T asset = library.get(entry.Key);
                Require(asset != null, "知识定义已移除");
                master(asset, entry.Value);
            });
    }

    /// <summary>动态资产只采集公开资料；其中技能引用也加入同一物件集合。</summary>
    private static DynamicDefinitionSaveData CaptureDefinitions()
    {
        JsonSerializer serializer = JsonSerializer.Create(Json.Settings);
        return new DynamicDefinitionSaveData
        {
            Cultibooks = Libraries.Manager.CultibookLibrary.GetDynamicAssets().OrderBy(asset => asset.id, StringComparer.Ordinal).Select(asset => JObject.FromObject(asset, serializer)).ToList(),
            Elixirs = Libraries.Manager.ElixirLibrary.GetDynamicAssets().OrderBy(asset => asset.id, StringComparer.Ordinal).Select(asset => JObject.FromObject(asset, serializer)).ToList(),
            Bloodlines = Libraries.Manager.BloodlineLibrary.GetDynamicAssets().OrderBy(asset => asset.id, StringComparer.Ordinal).Select(asset => JObject.FromObject(asset, serializer)).ToList()
        };
    }

    /// <summary>恢复当前定义，丹方由现有生成器重新建立行为与药效状态。</summary>
    /// <param name="data">待保存或恢复的资料。</param>
    private static void RestoreDefinitions(DynamicDefinitionSaveData data)
    {
        JsonSerializer serializer = JsonSerializer.Create(Json.Settings);
        foreach (JObject item in data.Cultibooks)
            Attempt(item.Value<string>("id"), () =>
            {
                CultibookAsset asset = item.ToObject<CultibookAsset>(serializer);
                Require(!string.IsNullOrEmpty(asset.id) && asset.FinalStats != null &&
                    Libraries.Manager.CultivateMethodLibrary.get(asset.CultivateMethodId) != null, "功法定义无效或修炼方式已移除");
                Libraries.Manager.CultibookLibrary.AddDynamic(asset);
            });
        foreach (JObject item in data.Elixirs)
            Attempt(item.Value<string>("id"), () =>
            {
                ElixirAsset asset = item.ToObject<ElixirAsset>(serializer);
                Require(!string.IsNullOrEmpty(asset.id) && asset.ingredients?.Length > 0, "丹方定义无效");
                ElixirEffectGenerator.GenerateElixirActions(asset);
                Libraries.Manager.ElixirLibrary.AddDynamic(asset);
            });
        foreach (JObject item in data.Bloodlines)
            Attempt(item.Value<string>("id"), () => Libraries.Manager.BloodlineLibrary.AddDynamic(item.ToObject<BloodlineAsset>(serializer)));
    }

    /// <summary>在外部资料入口报告已经失效的定义。</summary>
    /// <param name="valid">外部资料是否有效。</param>
    /// <param name="message">失效资料的具体原因。</param>
    private static void Require(bool valid, string message)
    {
        if (!valid) throw new InvalidDataException(message);
    }

    /// <summary>读取后重建元神解析表和表现，缓存从稳定状态重新得到。</summary>
    private static void RestoreRuntimeIndexes()
    {
        YuanshenAnchorNetworkService.RestoreWorldObjects();
        foreach (Entity entity in ModClass.I.W.Query<YuanshenNodeState>().Entities.ToEntityList())
            YuanshenSaveVisuals.RestoreNode(entity);
    }

    /// <summary>半成品保留原材料和进度，读档后作为普通炼制继续，不复用已结束的界面命令编号。</summary>
    private static void RestoreCraftTasks()
    {
        foreach (Entity item in ModClass.I.W.Query<SpecialItem>().Entities.ToEntityList())
        {
            if (!item.Tags.Has<TagConsumed>()) continue;
            bool occupied = false;
            foreach (Entity owner in item.GetIncomingLinks<CraftOccupyingRelation>().Entities)
            { occupied = true; break; }
            // 对应半成品因定义失效而丢弃时，剩余材料重新可用。
            if (!occupied) item.RemoveTag<TagConsumed>();
        }
        foreach (Entity item in ModClass.I.W.Query<CraftSession>().Entities.ToEntityList())
        {
            ref CraftSession session = ref item.GetComponent<CraftSession>();
            Actor actor = World.world.units.get(session.actor_id);
            if (actor == null) continue;
            session.order_id = 0;
            actor.setTask(session.process == CraftProcessType.Alchemy
                ? ActorTasks.CraftElixir.id : ActorTasks.CraftArtifact.id, true, false, false);
        }
    }

    /// <summary>先结算人物与宗门装备加成，再结算引用驾驭者属性的器灵、召唤物和命魂载体。</summary>
    private static void RestoreDependentStats()
    {
        var actors = Loaded.Values.Where(entity => !entity.IsNull && entity.HasComponent<ActorBinder>())
            .OrderBy(entity => entity.HasComponent<ArtifactSpiritAvatar>() || entity.HasComponent<ArtifactSummon>() ||
                               entity.HasComponent<YuanshenSoulCarrierState>()).ToArray();
        foreach (Entity entity in actors) entity.GetComponent<ActorBinder>().AE.MarkCultiwayStatsDirty();
        foreach (Entity entity in actors) entity.GetComponent<ActorBinder>().Actor.updateStats();
    }
}
