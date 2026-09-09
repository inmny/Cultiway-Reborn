using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Abstract;
using Cultiway.Content.CreatureCompositions.Combat;
using Cultiway.Content.CreatureCompositions.Libraries;
using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Core.Semantics;
using UnityEngine;

namespace Cultiway.Content.YaoBeasts;

/// <summary>
///     妖兽玩法的静态内容登记：身体槽位、身体方案、固定形态、器官、效果类别与启灵模板。
///     妖丹方向与血脉分别在 <see cref="YaoCorePatterns" /> 与 <see cref="YaoBloodlines" /> 登记。
/// </summary>
[Dependency(typeof(Libraries.Manager))]
public sealed class YaoContent : ICanInit
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
        AddSlot(Slots.Head, CreatureOrganCategoryMask.Perception | CreatureOrganCategoryMask.NaturalWeapon, 2, "head", true);
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
            .WithRank(1, 3, stats: [Stat("damage", 2f)], skills: ["Cultiway.PoisonNeedle"], effects: [("yao.venom", 1)]);
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
            .WithRank(2, 5, stats: [Stat("damage", 4f)], skills: ["Cultiway.PoisonNeedle"], effects: [("yao.venom", 2)]);
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
            .WithRank(2, 5, stats: [Stat("speed", 3f)], skills: ["Cultiway.WaterBlade"]);
        AddOrgan("yao.horn.thunder", CreatureOrganCategoryMask.Perception, Slots.Head)
            .WithSemantics(SkillSemantics.Element.Lightning, 1f)
            .WithRank(2, 5, stats: [Stat("damage", 5f)], skills: ["Cultiway.ChainLightning"], visuals: ["yao.layer.horn_thunder"]);
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
            .WithRank(6, 9, stats: [Stat("speed", 6f), Stat("damage", 5f)], effects: [("yao.nine_tail", 6)], visuals: ["yao.layer.tails6"]);
        AddOrgan("yao.eye.illusion", CreatureOrganCategoryMask.Perception, Slots.Head)
            .WithSemantics(SkillSemantics.Element.Ice, 1f)
            .WithRank(2, 4, stats: [Stat("accuracy", 8f), Stat("diplomacy", 3f)], visuals: ["yao.layer.eye_glow"]);
        AddOrgan("yao.gland.foxfire", CreatureOrganCategoryMask.Breath, Slots.Breath)
            .WithSemantics(SkillSemantics.Element.Fire, 1f)
            .WithRank(2, 5, stats: [Stat("damage", 4f)], skills: ["Cultiway.YinBolt"]);

        // ===== 金乌与凤凰 =====
        AddOrgan("yao.vent.truefire", CreatureOrganCategoryMask.Breath, Slots.Breath)
            .WithSemantics(SkillSemantics.Element.Fire, 1f)
            .WithRank(2, 5, stats: [Stat("damage", 5f)], skills: ["Cultiway.FireBlade"]);
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
            .WithRank(2, 4, stats: [Stat("speed", 1f)], skills: ["Cultiway.WaterOrb"]);
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
            .WithRank(2, 4, stats: [Stat("speed", 2f)], skills: ["Cultiway.WindBlade"]);

        // ===== 白泽 =====
        AddOrgan("yao.eye.insight", CreatureOrganCategoryMask.Perception, Slots.Head)
            .WithSemantics(SkillSemantics.Element.Pos, 1f)
            .WithRank(2, 4, stats: [Stat("accuracy", 10f), Stat("intelligence", 2f)]);
        AddOrgan("yao.horn.evilspotter", CreatureOrganCategoryMask.Perception, Slots.Head)
            .WithSemantics(SkillSemantics.Element.Pos, 1f)
            .WithRank(2, 4, stats: [Stat("damage", 3f), Stat("armor", 3f)]);
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
            "yao.nirvana", CreatureOrganEventMask.Survival, YaoOrganEffects.OnNirvana));
        CreatureOrganEffectFamilies.Register(new CreatureOrganEffectFamily(
            "yao.nine_tail", CreatureOrganEventMask.Survival, YaoOrganEffects.OnNineTailSubstitute));
    }

    /// <summary>登记物种外观骨架与世界图层；锚点是主体精灵包围盒的比例。</summary>
    private static void RegisterVisuals()
    {
        // 四足模板：头在前上、背在上方、尾在后上。
        (string id, string actor)[] quadrupeds =
        {
            ("yao.rig.wolf", "wolf"), ("yao.rig.bear", "bear"), ("yao.rig.turtle", "turtle"),
            ("yao.rig.fox", "fox"), ("yao.rig.crocodile", "crocodile"), ("yao.rig.frog", "frog"),
            ("yao.rig.rabbit", "rabbit"), ("yao.rig.rat", "rat"), ("yao.rig.chicken", "chicken"),
            ("yao.rig.sheep", "sheep"), ("yao.rig.penguin", "penguin"),
        };
        foreach ((string id, string actor) in quadrupeds)
        {
            AddRig(id, actor,
                ("head", 0.30f, 0.35f), ("back", 0f, 0.45f), ("tail", -0.40f, 0.25f));
        }

        // 蛇形细长：头尾分布在身体两端，背中线偏低。
        AddRig("yao.rig.snake", "snake",
            ("head", 0.45f, 0.20f), ("back", 0f, 0.30f), ("tail", -0.45f, 0.15f));
        // 龙形宽大：整体外扩。
        AddRig("yao.rig.dragon", "dragon",
            ("head", 0.40f, 0.35f), ("back", 0f, 0.40f), ("tail", -0.45f, 0.25f));
        // 人形：图层只承担显化表现。
        AddRig("yao.rig.human", "Cultiway.EasternHuman",
            ("head", 0f, 0.45f), ("back", 0f, 0.40f), ("tail", 0f, 0.10f));

        const string organDir = "cultiway/yao/organs/";
        AddLayer("yao.layer.shell", "back", organDir + "shell_black", 1.1f);
        AddLayer("yao.layer.horn_thunder", "head", organDir + "horn_thunder", 0.9f);
        AddLayer("yao.layer.horn_evilspotter", "head", organDir + "horn_evilspotter", 0.9f);
        AddLayer("yao.layer.wing_fupeng", "back", organDir + "wing_fupeng", 1.2f,
            offsetY: 0.05f);
        AddLayer("yao.layer.wing_fierce", "back", organDir + "wing_fierce", 1.1f);
        AddLayer("yao.layer.tails1", "tail", organDir + "tails_rank1", 0.9f);
        AddLayer("yao.layer.tails3", "tail", organDir + "tails_rank3", 1.0f);
        AddLayer("yao.layer.tails6", "tail", organDir + "tails_rank6", 1.1f);
        AddLayer("yao.layer.pattern_scale", "back", organDir + "pattern_scale", 1.4f,
            tint: CreatureLayerTintPolicy.KingdomColor, maskToBody: true);
        AddLayer("yao.layer.pattern_scale_cold", "back", organDir + "pattern_scale", 1.4f,
            tint: CreatureLayerTintPolicy.FixedColor, tintColor: new Color(0.74f, 0.85f, 0.91f),
            maskToBody: true);
        AddLayer("yao.layer.pattern_fire", "back", organDir + "pattern_feather", 1.4f,
            tint: CreatureLayerTintPolicy.FixedColor, tintColor: new Color(0.94f, 0.69f, 0.50f),
            maskToBody: true);
        AddLayer("yao.layer.pattern_moon", "back", organDir + "pattern_feather", 1.4f,
            tint: CreatureLayerTintPolicy.FixedColor, tintColor: new Color(0.85f, 0.88f, 0.94f),
            maskToBody: true);
        AddLayer("yao.layer.eye_glow", "head", organDir + "eye_glow", 0.35f,
            tint: CreatureLayerTintPolicy.Glow);
    }

    /// <summary>登记一个物种骨架；锚点数值是主体精灵包围盒的比例。</summary>
    private static void AddRig(
        string id, string actorAssetId,
        (string name, float forward, float up) head,
        (string name, float forward, float up) back,
        (string name, float forward, float up) tail)
    {
        Content.Libraries.Manager.CreatureVisualRigLibrary.add(new CreatureVisualRigAsset
        {
            id = id,
            CompatibleActorAssetIds = new[] { actorAssetId },
            Anchors = new System.Collections.Generic.Dictionary<string, Vector2>
            {
                { head.name, new Vector2(head.forward, head.up) },
                { back.name, new Vector2(back.forward, back.up) },
                { tail.name, new Vector2(tail.forward, tail.up) },
            },
            LayerOrder = new[] { "body", "head", "tail", "aura" },
        });
    }

    /// <summary>登记一个通配世界图层。</summary>
    private static void AddLayer(
        string id, string anchor, string spritePath, float scale,
        float offsetX = 0f, float offsetY = 0f,
        CreatureLayerTintPolicy tint = CreatureLayerTintPolicy.None, Color tintColor = default,
        bool maskToBody = false)
    {
        Content.Libraries.Manager.CreatureVisualLayerLibrary.add(new CreatureVisualLayerAsset
        {
            id = id,
            RigCompatibility = Array.Empty<string>(),
            Channel = null,
            FramesByBaseFrame = Array.Empty<CreatureLayerFrame>(),
            WildcardSpritePath = spritePath,
            Anchor = anchor,
            Offset = new Vector2(offsetX, offsetY),
            Scale = scale,
            TintPolicy = tint,
            TintColor = tintColor == default ? Color.white : tintColor,
            MaskToBody = maskToBody,
        });
    }

    // ===== 启灵模板 =====

    /// <summary>按物种登记启灵真身的固定内容。</summary>
    public static class YaoSpeciesTemplates
    {
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

            string adaptedMorphId = $"yao.adapted.{speciesId}";
            if (Content.Libraries.Manager.CreatureMorphLibrary.get(adaptedMorphId) == null)
            {
                ActorAsset asset = AssetManager.actor_library.get(speciesId);
                if (asset == null) return false;

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

            return YaoFormPlanService.TryCreateTrueForm(actor, "yao.quadruped", adaptedMorphId, Scales(), Sense());
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
            string[] skills = null, (string familyId, int rank)[] effects = null, string[] visuals = null)
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
