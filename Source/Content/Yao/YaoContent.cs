using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cultiway.Abstract;
using Cultiway.Content.CreatureCompositions.Combat;
using Cultiway.Content.CreatureCompositions.Libraries;
using Cultiway.Content.CreatureCompositions.Visuals;
using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Core.Semantics;
using Newtonsoft.Json;
using UnityEngine;

namespace Cultiway.Content.YaoBeasts;

/// <summary>
///     妖兽玩法的静态内容登记：身体槽位、身体方案、固定形态、器官、效果类别与启灵模板。
///     妖丹方向与血脉分别在 <see cref="YaoCorePatterns" /> 与 <see cref="YaoBloodlines" /> 登记。
/// </summary>
[Dependency(typeof(Libraries.Manager))]
public sealed class YaoContent : ICanInit, ICanReload
{
    /// <summary>妖兽八槽基线的身体位置编号。</summary>
    public static class Slots
    {
        /// <summary>表被：鳞甲、皮毛、羽毛。</summary>
        public const string Surface = "yao.slot.surface";

        /// <summary>头颅：瞳、角、巨口。</summary>
        public const string Head = "yao.slot.head";

        /// <summary>吐息腺：毒腺、狐火腺、真火灵窍。</summary>
        public const string Breath = "yao.slot.breath";

        /// <summary>四肢：爪、翼。</summary>
        public const string Limbs = "yao.slot.limbs";

        /// <summary>代谢：肺、胃、再生组织。</summary>
        public const string Metabolism = "yao.slot.metabolism";

        /// <summary>灵窍：镇岳、吞噬、风水等灵窍。</summary>
        public const string Spirit = "yao.slot.spirit";

        /// <summary>尾脊：尾冠、蛇尾。</summary>
        public const string Tail = "yao.slot.tail";

        /// <summary>丹心：涅槃心、噬战心等核心器官。</summary>
        public const string Heart = "yao.slot.heart";
    }

    /// <summary>登记全部妖兽静态定义；世界清理不影响静态内容。</summary>
    public void Init()
    {
        RegisterSlots();
        RegisterBodyPlans();
        RegisterMorphs();
        RegisterOrgans();
        RegisterEffectFamilies();
        RegisterVisuals();
        YaoSpeciesTemplates.Initialize();
        YaoCorePatterns.Initialize();
        YaoBloodlines.Initialize();
    }

    private static void RegisterSlots()
    {
        AddSlot(Slots.Surface, CreatureOrganCategoryMask.Surface, 2, "body", true);
        // 头容量 3：按本槽位注释"瞳、角、巨口"三件套设计
        AddSlot(Slots.Head, CreatureOrganCategoryMask.Perception | CreatureOrganCategoryMask.NaturalWeapon, 3, "head", true);
        AddSlot(Slots.Breath, CreatureOrganCategoryMask.Breath, 1, "head", false);
        AddSlot(Slots.Limbs, CreatureOrganCategoryMask.Locomotion, 2, "body", false);
        AddSlot(Slots.Metabolism, CreatureOrganCategoryMask.Metabolism, 2, "body", false);
        AddSlot(Slots.Spirit, CreatureOrganCategoryMask.Spirit, 1, "aura", false);
        AddSlot(Slots.Tail, CreatureOrganCategoryMask.Appendage, 1, "tail", false);
        AddSlot(Slots.Heart, CreatureOrganCategoryMask.Spirit | CreatureOrganCategoryMask.Metabolism, 1, "aura", false);
    }

    private static void RegisterBodyPlans()
    {
        // 蛇形：真身基础方案；暂时不用的槽位保持为空。
        AddPlan("yao.serpentine", 10,
            Slots.Surface, Slots.Head, Slots.Breath, Slots.Limbs, Slots.Metabolism, Slots.Spirit, Slots.Tail, Slots.Heart);

        // 兽形：狐、龟、重兽等四足真身。
        AddPlan("yao.quadruped", 12,
            Slots.Surface, Slots.Head, Slots.Breath, Slots.Limbs, Slots.Metabolism, Slots.Spirit, Slots.Tail, Slots.Heart);

        // 龙形：蛟龙血脉顶点后的完整真身。
        AddPlan("yao.dragon", 14,
            Slots.Surface, Slots.Head, Slots.Breath, Slots.Limbs, Slots.Metabolism, Slots.Spirit, Slots.Tail, Slots.Heart);

        // 人形：化形后的固定人身。
        AddPlan("yao.human", 8,
            Slots.Surface, Slots.Head, Slots.Breath, Slots.Limbs, Slots.Metabolism, Slots.Spirit, Slots.Tail, Slots.Heart);
    }

    private static void RegisterMorphs()
    {
        AddMorph("yao.serpentine.base", "yao.serpentine", "snake", CreatureLocomotionKind.Amphibious, new[] { "beast", "serpentine" }, rigId: "yao.rig.snake");
        AddMorph("yao.serpentine.awakened", "yao.serpentine", "snake", CreatureLocomotionKind.Amphibious, new[] { "beast", "serpentine", "awakened" }, 1, "yao.rig.snake");
        AddMorph("yao.wolf.base", "yao.quadruped", "wolf", CreatureLocomotionKind.Ground, new[] { "beast", "quadruped", "awakened" }, rigId: "yao.rig.wolf");
        AddMorph("yao.bear.base", "yao.quadruped", "bear", CreatureLocomotionKind.Ground, new[] { "beast", "quadruped", "awakened" }, 1, "yao.rig.bear");
        AddMorph("yao.turtle.base", "yao.quadruped", "turtle", CreatureLocomotionKind.Amphibious, new[] { "beast", "quadruped", "awakened" }, rigId: "yao.rig.turtle");
        AddMorph("yao.fox.base", "yao.quadruped", "fox", CreatureLocomotionKind.Ground, new[] { "beast", "quadruped", "awakened" }, rigId: "yao.rig.fox");
        AddMorph("yao.crocodile.base", "yao.quadruped", "crocodile", CreatureLocomotionKind.Amphibious, new[] { "beast", "quadruped", "awakened" }, 1, "yao.rig.crocodile");
        AddMorph("yao.frog.base", "yao.quadruped", "frog", CreatureLocomotionKind.Amphibious, new[] { "beast", "quadruped", "awakened" }, rigId: "yao.rig.frog");
        AddMorph("yao.rabbit.base", "yao.quadruped", "rabbit", CreatureLocomotionKind.Ground, new[] { "beast", "quadruped", "awakened" }, rigId: "yao.rig.rabbit");
        AddMorph("yao.rat.base", "yao.quadruped", "rat", CreatureLocomotionKind.Ground, new[] { "beast", "quadruped", "awakened" }, rigId: "yao.rig.rat");
        AddMorph("yao.chicken.base", "yao.quadruped", "chicken", CreatureLocomotionKind.Ground, new[] { "beast", "quadruped", "awakened" }, rigId: "yao.rig.chicken");
        AddMorph("yao.sheep.base", "yao.quadruped", "sheep", CreatureLocomotionKind.Ground, new[] { "beast", "quadruped", "awakened" }, rigId: "yao.rig.sheep");
        AddMorph("yao.penguin.base", "yao.quadruped", "penguin", CreatureLocomotionKind.Amphibious, new[] { "beast", "quadruped", "awakened" }, rigId: "yao.rig.penguin");
        AddMorph("yao.dragon.base", "yao.dragon", "dragon", CreatureLocomotionKind.Flying, new[] { "beast", "serpentine", "dragon", "awakened" }, 2, "yao.rig.dragon");
        AddMorph("yao.human.base", "yao.human", "Cultiway.EasternHuman", CreatureLocomotionKind.Ground, new[] { "humanoid", "awakened" }, rigId: "yao.rig.human");
    }

    private static void RegisterOrgans()
    {
        // ===== 第 2 阶段：灵蛇先天器官 =====
        AddOrgan("yao.scale.basic", CreatureOrganCategoryMask.Surface, Slots.Surface, planTags: "beast")
            .WithSemantics(SkillSemantics.Element.Earth, 1f)
            .WithRank(1, 2, stats: [Stat("armor", 2f)], visuals: ["yao.layer.pattern_scale"]);
        AddOrgan("yao.venom.fang", CreatureOrganCategoryMask.NaturalWeapon, Slots.Head, morphTags: "serpentine")
            .WithSemantics(SkillSemantics.Element.Poison, 1f)
            .WithRank(1, 3, stats: [Stat("damage", 2f)], skills: ["Cultiway.PoisonNeedle"], effects: [("yao.venom", 1)],
                bodyOutlet: CreatureOrganBodyOutlet.Mouth);
        AddOrgan("yao.spirit.sense", CreatureOrganCategoryMask.Perception, Slots.Head)
            .WithSemantics(SkillSemantics.Element.Wind, 1f)
            .WithRank(1, 2, stats: [Stat("accuracy", 5f)]);
        AddOrgan("yao.fang.basic", CreatureOrganCategoryMask.NaturalWeapon, Slots.Head)
            .WithSemantics(SkillSemantics.Element.Earth, 1f)
            .WithRank(1, 3, stats: [Stat("damage", 3f)]);

        // ===== 第 3 阶段：吞噬炼化候选 =====
        AddOrgan("yao.lung.aquatic", CreatureOrganCategoryMask.Metabolism, Slots.Metabolism)
            .WithSemantics(SkillSemantics.Element.Water, 1f)
            .WithRank(1, 3, stats: [Stat("speed", -1f)]);
        AddOrgan("yao.venom.gland.enhanced", CreatureOrganCategoryMask.Breath, Slots.Breath, prerequisite: "yao.venom.fang")
            .WithSemantics(SkillSemantics.Element.Poison, 1f)
            .WithRank(2, 5, stats: [Stat("damage", 4f)], skills: ["Cultiway.PoisonNeedle"], effects: [("yao.venom", 2)],
                bodyOutlet: CreatureOrganBodyOutlet.Mouth);
        AddOrgan("yao.scale.fine", CreatureOrganCategoryMask.Surface, Slots.Surface, prerequisite: "yao.scale.basic")
            .WithSemantics(SkillSemantics.Element.Earth, 1f)
            .WithRank(2, 5, stats: [Stat("armor", 6f), Stat("speed", -2f)], visuals: ["yao.layer.pattern_scale_cold"]);
        AddOrgan("yao.regen.low", CreatureOrganCategoryMask.Metabolism, Slots.Metabolism)
            .WithSemantics(SkillSemantics.Element.Wood, 1f)
            .WithRank(1, 4, effects: [("yao.regen", 1)]);

        // ===== 第 5/6 阶段：蛟龙 =====
        AddOrgan("yao.scale.jiaolong", CreatureOrganCategoryMask.Surface, Slots.Surface)
            .WithSemantics(SkillSemantics.Element.Water, 1f)
            .WithRank(2, 6, stats: [Stat("armor", 8f), Stat("speed", -1f)], visuals: ["yao.layer.pattern_scale_cold"])
            .WithRank(3, 9, stats: [Stat("armor", 14f), Stat("speed", -2f)], visuals: ["yao.layer.pattern_scale_cold"]);
        AddOrgan("yao.lung.cloud", CreatureOrganCategoryMask.Metabolism, Slots.Metabolism)
            .WithSemantics(SkillSemantics.Element.Water, 1f)
            .WithRank(2, 5, stats: [Stat("speed", 3f)], skills: ["Cultiway.WaterBlade"], bodyOutlet: CreatureOrganBodyOutlet.Mouth);
        AddOrgan("yao.horn.thunder", CreatureOrganCategoryMask.Perception, Slots.Head)
            .WithSemantics(SkillSemantics.Element.Lightning, 1f)
            .WithRank(2, 5, stats: [Stat("damage", 5f)], skills: ["Cultiway.ChainLightning"],
                visuals: ["yao.layer.horn_thunder"], castingLayerId: "yao.layer.horn_thunder");
        AddOrgan("yao.eye.dragon", CreatureOrganCategoryMask.Perception, Slots.Head)
            .WithSemantics(SkillSemantics.Element.Lightning, 1f)
            .WithRank(2, 4, stats: [Stat("accuracy", 12f)]);

        // ===== 月狐 =====
        AddOrgan("yao.fur.moonlight", CreatureOrganCategoryMask.Surface, Slots.Surface)
            .WithSemantics(SkillSemantics.Element.Ice, 1f)
            .WithRank(2, 5, stats: [Stat("armor", 4f), Stat("speed", 2f)], visuals: ["yao.layer.pattern_moon"]);
        AddOrgan("yao.crown.tails", CreatureOrganCategoryMask.Appendage, Slots.Tail)
            .WithSemantics(SkillSemantics.Element.Ice, 1f)
            .WithRank(1, 3, stats: [Stat("speed", 2f)], visuals: ["yao.layer.tails1"])
            .WithRank(3, 6, stats: [Stat("speed", 4f), Stat("damage", 2f)], visuals: ["yao.layer.tails3"])
            // 六尾占八点，给狐形必需的表被和感知各留两点，保证这条成长路线可达。
            .WithRank(6, 8, stats: [Stat("speed", 6f), Stat("damage", 5f)], effects: [("yao.nine_tail", 6)], visuals: ["yao.layer.tails6"]);
        AddOrgan("yao.eye.illusion", CreatureOrganCategoryMask.Perception, Slots.Head)
            .WithSemantics(SkillSemantics.Element.Ice, 1f)
            .WithRank(2, 4, stats: [Stat("accuracy", 8f), Stat("diplomacy", 3f)], visuals: ["yao.layer.eye_glow"]);
        AddOrgan("yao.gland.foxfire", CreatureOrganCategoryMask.Breath, Slots.Breath)
            .WithSemantics(SkillSemantics.Element.Fire, 1f)
            .WithRank(2, 5, stats: [Stat("damage", 4f)], skills: ["Cultiway.FoxfireBreath"], bodyOutlet: CreatureOrganBodyOutlet.Mouth);

        // ===== 金乌与凤凰 =====
        AddOrgan("yao.vent.truefire", CreatureOrganCategoryMask.Breath, Slots.Breath)
            .WithSemantics(SkillSemantics.Element.Fire, 1f)
            .WithRank(2, 5, stats: [Stat("damage", 5f)], skills: ["Cultiway.FireBlade"], bodyOutlet: CreatureOrganBodyOutlet.Mouth);
        AddOrgan("yao.feather.fire", CreatureOrganCategoryMask.Surface, Slots.Surface)
            .WithSemantics(SkillSemantics.Element.Fire, 1f)
            .WithRank(2, 5, stats: [Stat("armor", 5f), Stat("speed", 3f)], visuals: ["yao.layer.pattern_fire"]);
        AddOrgan("yao.heart.nirvana", CreatureOrganCategoryMask.Spirit, Slots.Heart)
            .WithSemantics(SkillSemantics.Element.Fire, 1f)
            .WithRank(1, 6, effects: [("yao.nirvana", 1)]);

        // ===== 玄武 =====
        AddOrgan("yao.shell.black", CreatureOrganCategoryMask.Surface, Slots.Surface)
            .WithSemantics(SkillSemantics.Element.Earth, 1f)
            .WithRank(2, 7, stats: [Stat("armor", 16f), Stat("speed", -4f)], effects: [("yao.turtle_stance", 2)], visuals: ["yao.layer.shell"]);
        AddOrgan("yao.lung.darkwater", CreatureOrganCategoryMask.Metabolism, Slots.Metabolism)
            .WithSemantics(SkillSemantics.Element.Water, 1f)
            .WithRank(2, 4, stats: [Stat("speed", 1f)], skills: ["Cultiway.WaterOrb"], bodyOutlet: CreatureOrganBodyOutlet.Mouth);
        AddOrgan("yao.tail.snake", CreatureOrganCategoryMask.Appendage, Slots.Tail)
            .WithSemantics(SkillSemantics.Element.Earth, 1f)
            .WithRank(2, 4, stats: [Stat("damage", 3f)]);
        AddOrgan("yao.spirit.mountaineer", CreatureOrganCategoryMask.Spirit, Slots.Spirit)
            .WithSemantics(SkillSemantics.Element.Earth, 1f)
            .WithRank(2, 5, stats: [Stat("armor", 6f), Stat("speed", -2f)], visuals: ["yao.layer.pattern_scale_cold"]);

        // ===== 饕餮 =====
        AddOrgan("yao.mouth.abyss", CreatureOrganCategoryMask.NaturalWeapon, Slots.Head)
            .WithSemantics(SkillSemantics.Element.Entropy, 1f)
            .WithRank(2, 6, stats: [Stat("damage", 6f)]);
        AddOrgan("yao.stomach.devour", CreatureOrganCategoryMask.Metabolism, Slots.Metabolism)
            .WithSemantics(SkillSemantics.Element.Entropy, 1f)
            .WithRank(2, 5, effects: [("yao.gluttony", 2)]);
        AddOrgan("yao.spirit.consume", CreatureOrganCategoryMask.Spirit, Slots.Spirit)
            .WithSemantics(SkillSemantics.Element.Entropy, 1f)
            .WithRank(2, 4);

        // ===== 鲲鹏 =====
        AddOrgan("yao.stomach.void", CreatureOrganCategoryMask.Metabolism, Slots.Metabolism)
            .WithSemantics(SkillSemantics.Element.Wind, 1f)
            .WithRank(2, 4, stats: [Stat("speed", 3f)]);
        AddOrgan("yao.wing.fupeng", CreatureOrganCategoryMask.Locomotion, Slots.Limbs)
            .WithSemantics(SkillSemantics.Element.Wind, 1f)
            .WithRank(2, 6, stats: [Stat("speed", 6f)], visuals: ["yao.layer.wing_fupeng"]);
        AddOrgan("yao.spirit.windwater", CreatureOrganCategoryMask.Spirit, Slots.Spirit)
            .WithSemantics(SkillSemantics.Element.Wind, 1f)
            .WithRank(2, 4, stats: [Stat("speed", 2f)], skills: ["Cultiway.WindBlade"], bodyOutlet: CreatureOrganBodyOutlet.Torso);

        // ===== 白泽 =====
        AddOrgan("yao.eye.insight", CreatureOrganCategoryMask.Perception, Slots.Head)
            .WithSemantics(SkillSemantics.Element.Pos, 1f)
            .WithRank(2, 4, stats: [Stat("accuracy", 10f), Stat("intelligence", 2f)]);
        AddOrgan("yao.horn.evilspotter", CreatureOrganCategoryMask.Perception, Slots.Head)
            .WithSemantics(SkillSemantics.Element.Pos, 1f)
            .WithRank(2, 4, stats: [Stat("damage", 3f), Stat("armor", 3f)], visuals: ["yao.layer.horn_evilspotter"]);
        AddOrgan("yao.spirit.memory", CreatureOrganCategoryMask.Spirit, Slots.Spirit)
            .WithSemantics(SkillSemantics.Element.Pos, 1f)
            .WithRank(2, 4, stats: [Stat("intelligence", 4f)]);

        // ===== 穷奇 =====
        AddOrgan("yao.wing.fierce", CreatureOrganCategoryMask.Locomotion, Slots.Limbs)
            .WithSemantics(SkillSemantics.Element.Wind, 1f)
            .WithRank(2, 5, stats: [Stat("speed", 5f)], visuals: ["yao.layer.wing_fierce"]);
        AddOrgan("yao.claw.windrift", CreatureOrganCategoryMask.Locomotion, Slots.Limbs)
            .WithSemantics(SkillSemantics.Element.Wind, 1f)
            .WithRank(2, 4, stats: [Stat("damage", 4f), Stat("attack_speed", 0.2f)]);
        AddOrgan("yao.heart.warbeast", CreatureOrganCategoryMask.Spirit, Slots.Heart)
            .WithSemantics(SkillSemantics.Element.Wind, 1f)
            .WithRank(2, 4, stats: [Stat("damage", 3f), Stat("armor", -2f)]);
    }

    private static void RegisterEffectFamilies()
    {
        CreatureOrganEffectFamilies.Register(new CreatureOrganEffectFamily(
            "yao.venom", CreatureOrganEventMask.DamageResolved, YaoOrganEffects.OnVenom));
        CreatureOrganEffectFamilies.Register(new CreatureOrganEffectFamily(
            "yao.regen", CreatureOrganEventMask.Upkeep, YaoOrganEffects.OnRegeneration));
        CreatureOrganEffectFamilies.Register(new CreatureOrganEffectFamily(
            "yao.turtle_stance", CreatureOrganEventMask.Adaptation, YaoOrganEffects.OnTurtleStance));
        CreatureOrganEffectFamilies.Register(new CreatureOrganEffectFamily(
            "yao.gluttony", CreatureOrganEventMask.Kill, YaoOrganEffects.OnGluttony));
        CreatureOrganEffectFamilies.Register(new CreatureOrganEffectFamily(
            YaoNirvanaService.EffectId, CreatureOrganEventMask.Survival, YaoOrganEffects.OnNirvana));
        CreatureOrganEffectFamilies.Register(new CreatureOrganEffectFamily(
            "yao.nine_tail", CreatureOrganEventMask.Survival, YaoOrganEffects.OnNineTailSubstitute));
    }

    /// <summary>载入与离线预览共用的逐物种外观及组合规则。</summary>
    private static void RegisterVisuals()
    {
        string path = Path.Combine(ModClass.I.GetDeclaration().FolderPath,
            "Content", "CreatureVisuals", "catalog.json");
        var catalog = JsonConvert.DeserializeObject<CreatureAppearanceCatalog>(File.ReadAllText(path));
        foreach (CreatureVisualRigAsset rig in catalog.Rigs)
        {
            var library = Content.Libraries.Manager.CreatureVisualRigLibrary;
            if (library.has(rig.id)) library.get(rig.id).CompatibleActorAssetIds = rig.CompatibleActorAssetIds;
            else library.add(rig);
        }
        foreach (CreatureVisualLayerAsset layer in catalog.Layers)
        {
            if (string.IsNullOrWhiteSpace(layer.Channel) || layer.FramesByRig.Count == 0)
                throw new InvalidOperationException($"器官外观缺少位置或动画资源：{layer.id}");
            var library = Content.Libraries.Manager.CreatureVisualLayerLibrary;
            if (!library.has(layer.id)) library.add(layer);
            else
            {
                CreatureVisualLayerAsset existing = library.get(layer.id);
                existing.Channel = layer.Channel;
                existing.Priority = layer.Priority;
                existing.OccludesChannels = layer.OccludesChannels;
                existing.Depth = layer.Depth;
                existing.ClipToBody = layer.ClipToBody;
                existing.IconPath = layer.IconPath;
                existing.FramesByRig = layer.FramesByRig;
            }
        }
    }

    /// <summary>原版重载图片后重新读取组合目录，释放旧的合成帧。</summary>
    public void OnReload()
    {
        CreatureAppearanceService.InvalidateResources();
        RegisterVisuals();
    }

    // ===== 启灵模板 =====

    /// <summary>按物种登记启灵真身的固定内容。</summary>
    public static class YaoSpeciesTemplates
    {
        /// <summary>启灵雨为没有专用模板的原版物种建立的身体编号前缀。</summary>
        public const string AdaptedMorphPrefix = "yao.adapted.";

        private static readonly
            System.Collections.Generic.Dictionary<string, (string bodyPlan, string morph, YaoOrganRecord[] organs)> templates = new();

        /// <summary>按原物种读取启灵模板；未登记的物种不能启灵。</summary>
        public static bool TryGet(string speciesId, out string bodyPlan, out string morph, out YaoOrganRecord[] organs)
        {
            bodyPlan = null;
            morph = null;
            organs = null;
            if (!templates.TryGetValue(speciesId, out var template)) return false;
            bodyPlan = template.bodyPlan;
            morph = template.morph;
            organs = template.organs;
            return true;
        }

        /// <summary>用物种模板为刚启灵的动物建立真身。</summary>
        public static bool TryCreateTrueForm(string speciesId, ActorExtend actor)
        {
            if (!TryGet(speciesId, out string bodyPlan, out string morph, out YaoOrganRecord[] organs)) return false;
            return YaoFormPlanService.TryCreateTrueForm(actor, bodyPlan, morph, organs);
        }

        /// <summary>登记物种模板；仅由内容初始化调用。</summary>
        public static void Register(string speciesId, string bodyPlan, string morph, params YaoOrganRecord[] organs)
        {
            templates[speciesId] = (bodyPlan, morph, organs);
        }

        /// <summary>登记全部可启灵物种的模板；肉食物种额外获得兽牙。</summary>
        internal static void Initialize()
        {

            // 蛇形：保留毒牙特色。
            Register("snake", "yao.serpentine", "yao.serpentine.awakened",
                Scales(), new YaoOrganRecord
                {
                    SlotId = YaoContent.Slots.Head,
                    OrganId = "yao.venom.fang",
                    Rank = 1,
                    Origin = YaoOrganOrigin.Innate,
                }, Sense());

            // 四足与小型动物：统一使用兽形方案。
            Register("wolf", "yao.quadruped", "yao.wolf.base", Scales(), Fang(), Sense());
            Register("bear", "yao.quadruped", "yao.bear.base", Scales(), Fang(), Sense());
            Register("fox", "yao.quadruped", "yao.fox.base", Scales(), Fang(), Sense());
            Register("crocodile", "yao.quadruped", "yao.crocodile.base", Scales(), Fang(), Sense());
            Register("rat", "yao.quadruped", "yao.rat.base", Scales(), Fang(), Sense());
            Register("turtle", "yao.quadruped", "yao.turtle.base", Scales(), Sense());
            Register("frog", "yao.quadruped", "yao.frog.base", Scales(), Sense());
            Register("rabbit", "yao.quadruped", "yao.rabbit.base", Scales(), Sense());
            Register("chicken", "yao.quadruped", "yao.chicken.base", Scales(), Sense());
            Register("sheep", "yao.quadruped", "yao.sheep.base", Scales(), Sense());
            Register("penguin", "yao.quadruped", "yao.penguin.base", Scales(), Sense());

            // 龙形单位直接使用龙形真身。
            Register("dragon", "yao.dragon", "yao.dragon.base", Scales(), Fang(), Sense());
        }

        private static YaoOrganRecord Scales() => new()
        {
            SlotId = YaoContent.Slots.Surface,
            OrganId = "yao.scale.basic",
            Rank = 1,
            Origin = YaoOrganOrigin.Innate,
        };

        private static YaoOrganRecord Sense() => new()
        {
            SlotId = YaoContent.Slots.Head,
            OrganId = "yao.spirit.sense",
            Rank = 1,
            Origin = YaoOrganOrigin.Innate,
        };

        private static YaoOrganRecord Fang() => new()
        {
            SlotId = YaoContent.Slots.Head,
            OrganId = "yao.fang.basic",
            Rank = 1,
            Origin = YaoOrganOrigin.Innate,
        };

        /// <summary>
        ///     启灵雨不做物种过滤：已登记的物种走登记模板；
        ///     未登记的物种动态补一具通用兽形固定形态（复用该物种自己的动画资产）。
        /// </summary>
        public static bool TryCreateAdaptedTrueForm(string speciesId, ActorExtend actor)
        {
            if (TryGet(speciesId, out string bodyPlan, out string morph, out YaoOrganRecord[] organs))
                return YaoFormPlanService.TryCreateTrueForm(actor, bodyPlan, morph, organs);

            string adaptedMorphId = EnsureAdaptedMorph(speciesId);
            return adaptedMorphId != null &&
                   YaoFormPlanService.TryCreateTrueForm(actor, "yao.quadruped", adaptedMorphId, Scales(), Sense());
        }

        /// <summary>启灵与读档共用的通用身体登记；只建立定义，不修改任何单位的身体。</summary>
        /// <param name="speciesId">使用原版动画的实际物种编号。</param>
        public static string EnsureAdaptedMorph(string speciesId)
        {
            string adaptedMorphId = AdaptedMorphPrefix + speciesId;
            if (Content.Libraries.Manager.CreatureMorphLibrary.get(adaptedMorphId) == null)
            {
                ActorAsset asset = AssetManager.actor_library.get(speciesId);
                if (asset == null) return null;

                Content.Libraries.Manager.CreatureMorphLibrary.add(new CreatureMorphAsset
                {
                    id = adaptedMorphId,
                    BodyPlanId = "yao.quadruped",
                    ActorAssetId = speciesId,
                    LocomotionKind = asset.flying
                        ? CreatureLocomotionKind.Flying
                        : CreatureLocomotionKind.Ground,
                    LockedSlots = Array.Empty<string>(),
                    AddedSlotCapacity = Array.Empty<CreatureSlotCapacityChange>(),
                    BaseComplexityModifier = 0,
                    VisualRigId = null,
                    Tags = new[] { "beast", "quadruped", "awakened" },
                });
            }

            return adaptedMorphId;
        }
    }

    // ===== 登记辅助 =====

    private static CreatureStatValue Stat(string statId, float value)
    {
        return new CreatureStatValue(statId, value);
    }

    private static void AddSlot(
        string id, CreatureOrganCategoryMask mask, int capacity, string channel, bool required)
    {
        Content.Libraries.Manager.CreatureBodySlotLibrary.add(new CreatureBodySlotAsset
        {
            id = id,
            AcceptedCategoryMask = mask,
            Capacity = capacity,
            SymmetryMode = CreatureSymmetryMode.Single,
            VisualChannel = channel,
            Required = required,
        });
    }

    private static void AddPlan(string id, int complexity, params string[] slotIds)
    {
        Content.Libraries.Manager.CreatureBodyPlanLibrary.add(new CreatureBodyPlanAsset
        {
            id = id,
            SlotIds = slotIds,
            AllowedMorphIds = null,
            BaseComplexityCapacity = complexity,
            MaximumOverlayLayers = 3,
            VisualRigId = null,
            Tags = new[] { "beast" },
        });
    }

    private static void AddMorph(
        string id, string bodyPlanId, string actorAssetId, CreatureLocomotionKind locomotion,
        string[] tags = null, int complexityModifier = 0, string rigId = null)
    {
        Content.Libraries.Manager.CreatureMorphLibrary.add(new CreatureMorphAsset
        {
            id = id,
            BodyPlanId = bodyPlanId,
            ActorAssetId = actorAssetId,
            LocomotionKind = locomotion,
            LockedSlots = Array.Empty<string>(),
            AddedSlotCapacity = Array.Empty<CreatureSlotCapacityChange>(),
            BaseComplexityModifier = complexityModifier,
            VisualRigId = rigId,
            Tags = tags,
        });
    }

    private static OrganBuilder AddOrgan(
        string id, CreatureOrganCategoryMask category, string slot,
        string planTags = null, string morphTags = null, string prerequisite = null)
    {
        var organ = new CreatureOrganAsset
        {
            id = id,
            Category = category,
            AllowedBodyPlanTags = planTags == null ? Array.Empty<string>() : new[] { planTags },
            AllowedMorphTags = morphTags == null ? Array.Empty<string>() : new[] { morphTags },
            PrerequisiteOrganIds = prerequisite == null ? Array.Empty<string>() : new[] { prerequisite },
            ConflictOrganIds = Array.Empty<string>(),
            RankIds = Array.Empty<string>(),
            SlotRequirements = Array.Empty<CreatureSlotRequirement>(),
            EffectFamilyIds = Array.Empty<string>(),
            Semantics = new SemanticDescriptor(),
        };
        Content.Libraries.Manager.CreatureOrganLibrary.add(organ);
        return new OrganBuilder(organ, slot);
    }

    /// <summary>器官定义的链式登记器，集中处理等级资产创建。</summary>
    private sealed class OrganBuilder
    {
        private readonly CreatureOrganAsset organ;
        private readonly string slot;
        private readonly List<string> rankIds = new();

        internal OrganBuilder(CreatureOrganAsset organ, string slot)
        {
            this.organ = organ;
            this.slot = slot;
        }

        /// <summary>声明器官提供的一条长期特征。</summary>
        internal OrganBuilder WithSemantics(SemanticAsset semantic, float strength)
        {
            organ.Semantics.contributions = organ.Semantics.contributions
                .Append(new SemanticContribution(semantic.id, strength)).ToArray();
            return this;
        }

        /// <summary>声明器官的一个等级及其属性、技能、效果与世界图层。</summary>
        internal OrganBuilder WithRank(
            int rank, int complexity, CreatureStatValue[] stats = null,
            string[] skills = null, (string familyId, int rank)[] effects = null, string[] visuals = null,
            string castingLayerId = null, CreatureOrganBodyOutlet bodyOutlet = CreatureOrganBodyOutlet.None)
        {
            string rankId = $"{organ.id}.rank{rank}";
            Content.Libraries.Manager.CreatureOrganRankLibrary.add(new CreatureOrganRankAsset
            {
                id = rankId,
                Rank = rank,
                ComplexityCost = complexity,
                StatValues = stats ?? Array.Empty<CreatureStatValue>(),
                SkillContainerIds = skills ?? Array.Empty<string>(),
                EffectRanks = ToEffectRanks(effects),
                VisualLayerIds = visuals ?? Array.Empty<string>(),
                CastingLayerId = castingLayerId,
                BodyOutlet = bodyOutlet,
            });
            rankIds.Add(rankId);
            organ.RankIds = rankIds.ToArray();

            var familyIds = new List<string>(organ.EffectFamilyIds);
            if (effects != null)
            {
                foreach ((string familyId, int _) in effects)
                {
                    if (!familyIds.Contains(familyId)) familyIds.Add(familyId);
                }
            }

            organ.EffectFamilyIds = familyIds.ToArray();
            return this;
        }

        private static CreatureEffectRank[] ToEffectRanks((string familyId, int rank)[] effects)
        {
            if (effects == null || effects.Length == 0) return Array.Empty<CreatureEffectRank>();
            var result = new CreatureEffectRank[effects.Length];
            for (int i = 0; i < effects.Length; i++)
            {
                result[i] = new CreatureEffectRank(effects[i].familyId, effects[i].rank);
            }

            return result;
        }
    }
}
