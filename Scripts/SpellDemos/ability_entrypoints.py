"""任务 0.03 的现有调用路径；只供开发清点，不参与游戏注册或施放。"""

import json
import re
from collections import Counter
from pathlib import Path

from build_ability_inventory import CATEGORIES, PROCESSES, PROJECT, enrich, inventory_references, locale_index, source_index


FIELDS = dict(learning="学习", casting="施法", payment="支付", action="动作", effect="作用",
              display="显示", manual="人工使用", autonomous="自主使用", persistence="存档")
CORE = "Source/Core/"
CONTENT = "Source/Content/"
SKILL = CORE + "SkillLibV3/"
SAVE = CONTENT + "Persistence/"
ABILITIES = CONTENT + "ActiveAbilities/"
NEW = CONTENT + "Skills/"
ACTIVE = SKILL + "ActiveAbilities/ActiveAbilityService.cs"
MANUAL = CORE + "ControlledCultivatorSkillControls.cs::ActiveAbilityService.TryUse"
AI = CORE + "Combat/Tactical/CombatActionProviders.cs::ActiveAbilityService.TryUse"
OLD_SAVE = SAVE + "WorldObjectSaveService.cs::SkillBlueprintExporter"
PARTS = SAVE + "WorldObjectSaveService.Parts.cs"


def at(text, *sources):
    """同一格保留通俗说明及可定位的实际源码。"""
    return dict(text=text, sources=list(sources))


LEARNED = {
    "learning": at("法网、卷轴学习或万法阁授予，经统一学习服务建立已学关系。",
                   SKILL + "SkillOwnershipService.cs::Learn(", CONTENT + "Magic/MagicWebStudyService.cs",
                   CONTENT + "Magic/MagicScrollStudyService.cs", SKILL + "Wanfa/WanfaGrantSession.cs"),
    "casting": at("已学提供者形成计划，启动旧技能序列。", SKILL + "ActiveAbilities/LearnedSkillActiveAbilityProvider.cs::public bool TryUse",
                  SKILL + "Manager.cs::public bool StartSkillSequence"),
    "payment": at("按实际容器、词条、计划和资源绑定报价；序列按来源选择预付或逐发支付。资产默认资源不等于本次实际扣费。",
                  SKILL + "SkillCastCost.cs::public static bool TryPay", SKILL + "Casting/SkillCastResourceResolver.cs",
                  SKILL + "Systems/LogicSkillCastSequenceSystem.cs::SkillCastCost.TryPayStep"),
    "action": at("旧轨迹和技能实体推进；普通法术没有统一人物发力动作。身体载体要求不等于已经制作角色动画。",
                 SKILL + "Systems/LogicTrajectorySystem.cs", SKILL + "Systems/LogicSkillTravelSystem.cs"),
    "effect": at("对象碰撞、落点及周期分别进入命中和结构化效果；单项回调及效果见下表。",
                 SKILL + "SkillHitResolver.cs", SKILL + "Effects/SkillEffectResolver.cs",
                 SKILL + "Systems/LogicSkillPeriodicEffectSystem.cs"),
    "display": at("旧空间表现目录及模型绘制，另有动画和世界反馈；不能视为已迁入共同构造。",
                  CONTENT + "Visuals/SkillSpatialVisualCatalog.cs", CONTENT + "Visuals/SkillSpatialVisual.cs",
                  SKILL + "Systems/RenderSkillWorldVisualSystem.cs"),
    "manual": at("附体轮换同时接受 Combat 或 World；按来源、载体和目标条件调用共同主动入口。", MANUAL, ACTIVE),
    "autonomous": at("战斗候选按通道、准备条件和权重选择；世界通道还须具体需求规划。",
                     AI, ACTIVE + "::public static int CollectAiCandidates", "Source/WorldboxCombatActions.cs"),
    "persistence": at("objects 保存蓝图、已学顺序、关系及冷却；来源授予容器按编号重建。飞行实体和本次施放序列不保存。",
                      OLD_SAVE, PARTS + '::Component<SkillCooldownRuntime>', SAVE + "WorldObjectSaveService.cs::GrantedSkillAsset"),
}

CONSTRUCTION = {
    "learning": at("正式 Learn、活动创法、书籍及功法传承写入构造知识；基础动作仅登记不等于自然学会。",
                   NEW + "ConstructedAbilityService.cs::public static void Learn", NEW + "ConstructedAbilityCreation.cs",
                   NEW + "ConstructedAbilityTransmission.cs", NEW + "ConstructedAbilityInheritance.cs"),
    "casting": at("共同提供者核对已学知识并启动已编译构造。", NEW + "ConstructedAbilityService.cs::public bool TryUse",
                  NEW + "ConstructedAbilityService.cs::private static bool Start"),
    "payment": at("实际资源通道报价及支付，运行保留真实材料本金；预付来源不重复扣费。",
                  NEW + "ConstructedAbilityService.cs::private static bool Start", NEW + "ConstructionResources.cs"),
    "action": at("共同身体、持物、阶段及过程；真实姿态与接触区随动作采样。",
                 NEW + "BodyActionBinding.cs", CORE + "Skills/BodyMotion.cs"),
    "effect": at("共同接触、屏障、世界原理及结果，由实际玩法拥有永久事实。",
                 NEW + "ConstructedAbilityContacts.cs", NEW + "ConstructedAbilityWorldContacts.cs"),
    "display": at("共同状态发布形体及身体姿态，原版和 Worldfall 分别绘制。",
                  NEW + "ConstructedAbilityPresentation.cs", CORE + "Visuals/Forms/FormNativeRenderSystem.cs",
                  CONTENT + "Compatibility/Worldfall/FormWorldfallRenderer.cs", CONTENT + "Compatibility/Worldfall/BodyWorldfallRenderer.cs"),
    "manual": at("共同能力栏入口受当前能力通道、身体及目标限制。", MANUAL, NEW + "ConstructedAbilityService.cs::GetChannels"),
    "autonomous": at("战斗提供者选择已学构造；日常需求及城市工程分别提交正式施放。",
                     AI, NEW + "ConstructedAbilityWorldNeeds.cs", CONTENT + "CollectiveProjects/MagicUtilityProjectExecutor.cs"),
    "persistence": at("ability_constructions 保存定义及升级，ability_knowledge 保存掌握、使用与冷却；短时施放不续播，世界结果另存。",
                      NEW + "ConstructedAbilityService.cs::ability_constructions", NEW + "ConstructedAbilityService.cs::ability_knowledge",
                      NEW + "ConstructedAbilityCreation.cs::ability_creation"),
}

ARTIFACT = {
    "learning": at("炼器组合生成能力实例，实际装备及控制状态决定可用性；不作为普通法术学习。",
                   CONTENT + "Artifacts/ArtifactAbilityComposer.cs", CONTENT + "Artifacts/ActiveAbilities/ArtifactActiveAbilityProvider.cs::public void Collect"),
    "casting": at("主动提供者调用资产 TryUseActive；被动由事件派发及生命周期推进。",
                  CONTENT + "Artifacts/ActiveAbilities/ArtifactActiveAbilityProvider.cs::public bool TryUse",
                  CONTENT + "Libraries/ArtifactAbilityAsset.cs::TryUseActive", CONTENT + "Artifacts/ArtifactAbilityDispatcher.cs::public static int Dispatch"),
    "payment": at("启动、维持和事件资源由该能力生命周期 Resource 及具体回调处理；参数表不表示每项都会收费。",
                  CONTENT + "Artifacts/ArtifactAbilityLifecycle.cs", CONTENT + "Libraries/ArtifactAbilityAsset.cs::ArtifactAbilityResourceHandler"),
    "action": at("借用真实器物、独立运动、部署或具体能力回调；各能力未统一为共同构造。",
                 CONTENT + "Artifacts/ArtifactSkillExecutions.cs", CONTENT + "Systems/Logic/ArtifactAbilityLifecycleSystem.cs"),
    "effect": at("单项主动、事件及生命周期回调产生实际作用，见下表。", CONTENT + "Artifacts/ArtifactAbilityDispatcher.cs"),
    "display": at("器物本体、部署/显化及能力各自空间表现；具体显示仍归该能力回调。",
                  CONTENT + "Artifacts/ArtifactAbilities.Visuals.cs", CONTENT + "Systems/Logic/ArtifactManifestationSystem.cs"),
    "manual": at("仅主动且通道许可的实例进入能力栏；被动没有独立按钮。", MANUAL,
                 CONTENT + "Artifacts/ActiveAbilities/ArtifactActiveAbilityProvider.cs::GetChannels"),
    "autonomous": at("主动须战斗通道及权重；被动随实际事件、控制状态和维持条件触发。",
                     AI, CONTENT + "Artifacts/ArtifactAbilityDispatcher.cs", CONTENT + "Systems/Logic/ArtifactAbilityLifecycleSystem.cs"),
    "persistence": at("objects 保存能力实例、参数、状态、充能、冷却、器物与关系；NormalizeAbilities 清掉旧短时执行句柄，保留独立部署资料。",
                      PARTS + '::Component<ArtifactAbilitySet>', PARTS + "::NormalizeAbilities"),
}

KNIGHT = dict(LEARNED,
    learning=at("流派及骑士等级解锁后，TryLearnTechnique 通过学习服务取得专属容器。",
                CONTENT + "KnightCombat/KnightTechniqueSkills.cs::private static bool TryLearnTechnique"),
    casting=at("专属提供者调用具体战技 TryUse；不是普通已学提供者。",
               CONTENT + "KnightCombat/KnightTechniqueAbilityProvider.cs::public bool TryUse"),
    payment=at("专属入口先 TryPayStep，再调用战技并开始冷却；内部执行沿用预付，不二次收费。",
               CONTENT + "KnightCombat/KnightTechniqueAbilityProvider.cs::SkillCastCost.TryPayStep",
               CONTENT + "KnightCombat/KnightTechniqueSkills.cs"),
    action=at("具体战技回调、装备武器动作轨迹及架势状态；九项仍走旧专属执行。",
              CONTENT + "KnightCombat/KnightTechniqueSkills.cs", CONTENT + "KnightCombat/KnightTechniqueStatuses.cs"),
    persistence=at("objects 中的专属蓝图、流派掌握、状态及武器关系；TryRestoreSavedContainer 重建受限专属容器。",
                   OLD_SAVE, CONTENT + "KnightCombat/KnightTechniqueSkills.cs::TryRestoreSavedContainer", PARTS + '::Component<SpecializedActiveAbility>'))

ORGAN = dict(LEARNED,
    learning=at("身体表达形成器官等级；按器官/妖丹引用共用来源容器，不转成普通已学关系。",
                CONTENT + "CreatureCompositions/Services/CreatureExpressionService.cs", CONTENT + "CreatureCompositions/ActiveAbilities/CreatureOrganSkillRegistry.cs"),
    casting=at("器官提供者或妖丹提供者调用 CastSkillV3，经旧施法序列出手。",
               CONTENT + "CreatureCompositions/ActiveAbilities/CreaturePhenotypeActiveAbilityProvider.cs::public bool TryUse",
               CONTENT + "Yao/YaoCoreActiveAbilityProvider.cs::public bool TryUse", CORE + "ActorExtend.cs::public bool CastSkillV3"),
    payment=at("实际来源容器按 SkillCastCost 支付；身体维持另外由器官维持系统处理，不能用默认法术资源推断妖力费用。",
               SKILL + "SkillCastCost.cs", CONTENT + "Systems/Logic/CreatureOrganUpkeepSystem.cs", CONTENT + "Yao/YaoResourceService.cs"),
    action=at("来源定位可从器官槽位发出，技能仍用旧轨迹；器官本身未统一为共同身体战技。",
              CONTENT + "CreatureCompositions/ActiveAbilities/CreatureOrganCastingPosition.cs", SKILL + "Systems/LogicTrajectorySystem.cs"),
    effect=at("器官主动走所引用技能；被动由器官事件派发和具体效果族处理。",
              CONTENT + "CreatureCompositions/Combat/CreatureOrganEffectDispatcher.cs", CONTENT + "Yao/YaoOrganEffects.cs"),
    display=at("身体组合外观与引用法术空间表现分开维护，不把换器官外观视为法术迁移。",
               CONTENT + "CreatureCompositions/Visuals/CreatureAppearanceService.cs", CONTENT + "Visuals/SkillSpatialVisual.cs"),
    persistence=at("yao 保存妖兽成长与身体，objects 保存相关器官状态与来源容器编号；加载重新表达并编译身体。",
                   CONTENT + "Yao/YaoPersistence.cs", SAVE + "WorldObjectSaveService.cs::OrganGrantedSkill", PARTS + '::Component<CreatureEffectStatusSource>'))

FORMATION = dict(LEARNED,
    learning=at("修炼成果组合保存原子快照，效果解析按族及等级合并后授予实际容器。",
                CONTENT + "Libraries/CoreFormationComposer.cs", CONTENT + "CoreFormationEffectResolver.cs"),
    casting=at("主动由核心形成提供者启动；被动从伤害、击杀、施放完成或固定推进派生。",
               ABILITIES + "CoreFormationActiveAbilityProvider.cs::public bool TryUse", CONTENT + "CoreFormationSkillBridge.cs"),
    payment=at("主动走普通序列及容器资源；被动 Trigger 通过预付来源提交，不另扣一次普通施法费用。",
               ABILITIES + "CoreFormationActiveAbilityProvider.cs::CasterResources", CONTENT + "CoreFormationEffectHandlers.cs::SkillCastFundingSource.Prepaid"),
    action=at("形成专属旧容器、轨迹及持续状态，没有统一角色动作构造。",
              CONTENT + "CoreFormationSkills.cs", CONTENT + "Systems/Logic/CoreFormationEffectSystem.cs"),
    effect=at("形成桥接按事件与最终伤害阶段分发到单项 Handle，可能再产生技能或更新状态。",
              CONTENT + "CoreFormationSkillBridge.cs", CONTENT + "CoreFormationEffectHandlers.cs"),
    display=at("旧形成技能空间模型、循环状态与事件反馈；不等于共同构造已接入。",
               CONTENT + "CoreFormationSkills.cs", CONTENT + "CoreFormationStatusFactory.cs", CONTENT + "Visuals/SkillSpatialVisual.cs"),
    manual=at("形成主动声明 Combat，可经附体轮换手动释放；被动没有独立按钮，不能按仅有 World 通道来判断手动可用性。",
              ABILITIES + "CoreFormationActiveAbilityProvider.cs::ActiveAbilityChannel.Combat", MANUAL),
    persistence=at("cultivation 保存形成成果与原子；objects 按稳定编号重建来源容器，并保存形成状态及冷却。",
                   SAVE + "CharacterSaveService.cs::CaptureCultivation", PARTS + '::Component<CoreFormationEffectState>', OLD_SAVE))

STATUS = {
    "learning": at("不单独学习，由实际法术、物品或玩法施加。", CORE + "Libraries/StatusEffectAsset.cs::public Entity NewEntity"),
    "casting": at("不提供独立施法入口，由持有者状态关系维护。", "Source/Abstract/IHasStatus.cs"),
    "payment": at("状态自身没有统一施法费；来源创建及维持另按来源规则。", CORE + "Systems/Logic/StatusTickSystem.cs"),
    "action": at("状态随持有者和寿命推进；骑士绑定武器及形成状态有各自状态系统。",
                 CORE + "Systems/Logic/StatusTickSystem.cs", CONTENT + "Systems/Logic/KnightTechniqueStatusSystem.cs"),
    "effect": at("属性覆盖、倍率及可选 TickSettings.Action；本条真实回调及属性见下表。",
                 CORE + "Libraries/StatusEffectAsset.cs::TickSettings", CORE + "Systems/Logic/StatusTickSystem.cs::tick.Action"),
    "display": at("状态动画、粒子和可选空间标记，不声明具有实际屏障或攻击体积。",
                  CORE + "Systems/Render/RenderStatusAnimationSystem.cs", CORE + "Systems/Render/RenderStatusParticleSystem.cs"),
    "manual": at("没有独立能力按钮；人工施法仅能通过其来源。", "Source/Abstract/IHasStatus.cs"),
    "autonomous": at("属性及状态周期自动推进；不进入主动能力候选。", CORE + "Systems/Logic/StatusTickSystem.cs"),
    "persistence": at("objects 保存状态编号、持有关系、寿命及 tick/形成/骑士附属状态；丹药动态定义先恢复后恢复状态。",
                      PARTS + '::Component<StatusComponent>', PARTS + '::Component<StatusTickState>', SAVE + "WorldObjectSaveService.cs::CaptureDefinitions"),
}

ELIXIR = dict(STATUS,
    learning=at("配方材料、品质及种子选择效果原子，生成实际丹药行为；原子不单独学习。",
                CONTENT + "Libraries/ElixirEffectComposer.cs", CONTENT + "Libraries/ElixirEffectGenerator.cs"),
    casting=at("食用实际丹药后执行其生成行为，不经过主动法术提供者。", CONTENT + "Libraries/ElixirEffectGenerator.cs"),
    payment=at("消耗真实丹药；炼制资源归炼丹流程，永久收益及恢复不重复走普通施法收费。",
               CONTENT + "Libraries/ElixirEffectGenerator.cs", CONTENT + "Patch/PatchAboutElixir.cs"),
    effect=at("StatusGain、DataGain 或 Restore 分支；永久属性、特质及一次性操作和临时状态不同。",
              CONTENT + "Libraries/ElixirEffectGenerator.cs::GenerateElixirActions"),
    manual=at("背包使用丹药调用 TryConsumeElixir；不进入主动法术轮换。",
              "Source/UI/CreatureInfoPages/InventoryPage.cs::TryConsumeElixir", CONTENT + "Extensions/ActorExtendTools.cs::TryConsumeElixir"),
    autonomous=at("实际食用行为触发；生成的临时状态再按状态系统自动推进。", CONTENT + "Patch/PatchAboutElixir.cs"),
    persistence=at("objects 定义目录保存丹药及生成状态，人物永久收益归 character 或原版特质；临时状态归 objects。",
                   SAVE + "WorldObjectSaveService.cs::CaptureDefinitions", SAVE + "CharacterSaveService.cs::PermanentStats", PARTS + '::Tag<TagElixirStatusGain>'))

CITY = dict(CONSTRUCTION,
    learning=at("工程不单独学习，执行者必须实际掌握符合需求的旧术或构造。",
                CONTENT + "CollectiveProjects/MagicUtilitySpellResolver.cs"),
    casting=at("城市规划、认领后由工程执行器提交正式主动能力，完成依真实结果验收。",
               CONTENT + "CollectiveProjects/CityMagicUtilityProjectPlanner.cs", CONTENT + "CollectiveProjects/MagicUtilityProjectExecutor.cs::ActiveAbilityService.TryUse"),
    payment=at("选中能力按其本身收费；工程不替能力重复支付，永久改造额度由工程维护。",
               CONTENT + "CollectiveProjects/MagicUtilityProjectContent.cs::RatePolicy", CONTENT + "CollectiveProjects/MagicUtilityProjectExecutor.cs"),
    action=at("随实际选中来源；规划足迹不等于人物动作或已发生作用。", CONTENT + "CollectiveProjects/MagicUtilitySpellResolver.cs"),
    effect=at("需求原理对应地块变化，由结果事件及原版事实验收。", CONTENT + "CollectiveProjects/CityMagicUtilityProjectModel.cs"),
    manual=at("工程没有独立法术按钮；可手动使用其所需能力，但不等于完成城市工程。", CONTENT + "CollectiveProjects/MagicUtilityProjectContent.cs"),
    autonomous=at("城市需求发起、选执行者及目标，再进入能力入口。", CONTENT + "CollectiveProjects/CityMagicUtilityProjectPlanner.cs"),
    persistence=at("collective-projects 保存工程、目标及完成历史；能力掌握、永久地形和天气分别保存，不续播瞬时施放。",
                   SAVE + "CollectiveProjectSaveContent.cs::collective-projects"))

ROUTES = {"learned": LEARNED, "construction": CONSTRUCTION, "artifact": ARTIFACT,
          "knight": KNIGHT, "organ": ORGAN, "formation": FORMATION, "status": STATUS,
          "elixir": ELIXIR, "city": CITY}

# 每个命令列出真实调用的领域方法，取消/返回也不假定支付为普通法术费用。
COMMANDS = {
    "content.yuanshen_travel": {
        "travel_guarded": ["YuanshenTravelService.TryTravelTo"], "travel_balanced": ["YuanshenTravelService.TryTravelTo"],
        "travel_full": ["YuanshenTravelService.TryTravelTo"], "return": ["YuanshenTravelService.RequestReturn"]},
    "content.yuanshen_soul": {"strike": ["YuanshenNodeCombatService.TrySoulStrike"]},
    "content.yuanshen_thought": {
        "create": ["YuanshenThoughtService.TryCreateThought"], "focus": ["YuanshenThoughtService.TryGetOwnedAtPoint", "YuanshenThoughtService.TryFocus"],
        "move_focused": ["YuanshenThoughtService.TryAssignMove"], "guard_focused": ["YuanshenThoughtService.TryAssignGuard"],
        "follow_focused": ["YuanshenThoughtService.TryAssignFollow"], "track_focused": ["YuanshenNodeLockService.TryGetLockedNear", "YuanshenThoughtService.TryAssignLockedNodeTracking"],
        "return_focused": ["YuanshenThoughtService.RequestReturn"], "return_all": ["YuanshenThoughtService.RequestAllThoughtsReturn"],
        "control_artifact": ["Artifacts.ArtifactYuanshenControlService.TryBindNextEquippedArtifact"], "release_artifact": ["Artifacts.ArtifactYuanshenControlService.ReleaseFocusedArtifact"]},
    "content.yuanshen_body_recovery": {
        "bind_anchor": ["YuanshenArtifactAnchorService.TryBindNext"], "unbind_anchor": ["YuanshenArtifactAnchorService.Unbind"],
        "rest_anchor": ["YuanshenArtifactAnchorService.TryRestAtAnchor"], "offer_body": ["YuanshenBodyRecoveryService.TryOfferBody"],
        "possess": ["YuanshenBodyRecoveryService.TryStartPossession"], "accept_body": ["YuanshenBodyRecoveryService.TryStartPossession"],
        "cancel_possession": ["YuanshenBodyRecoveryService.CancelPossession"], "reconstruct": ["YuanshenBodyRecoveryService.TryStartReconstruction"],
        "cancel_reconstruction": ["YuanshenBodyRecoveryService.CancelReconstruction"]},
    "content.yuanshen_advanced": {
        "dharma": ["YuanshenAdvancedNodeService.TryCreateDharmaForm"], "return_dharma": ["YuanshenAdvancedNodeService.RequestRoleReturn"],
        "prepare_avatar": ["YuanshenAdvancedNodeService.TryStartAvatarPreparation"], "cancel_avatar": ["YuanshenAdvancedNodeService.CancelAvatarPreparation"],
        "return_avatar": ["YuanshenAdvancedNodeService.RequestRoleReturn"], "manifest": ["YuanshenAdvancedNodeService.TryCreateManifestation"],
        "return_manifest": ["YuanshenAdvancedNodeService.RequestRoleReturn"], "consecrate_sect": ["YuanshenAnchorNetworkService.TryConsecrate"],
        "consecrate_altar": ["YuanshenAnchorNetworkService.TryConsecrate"], "select_anchor": ["YuanshenAnchorNetworkService.TrySelectForConnection"],
        "connect_anchor": ["YuanshenAnchorNetworkService.TryConnectSelected"], "offer_incense": ["YuanshenAnchorNetworkService.TryOfferIncense"],
        "dismantle_anchor": ["YuanshenAnchorNetworkService.TryDismantle"], "transit": ["YuanshenAdvancedNodeService.TryTransitFocused"],
        "engage": ["YuanshenAdvancedNodeService.TryAssignFocusedEngage"]},
}

ROUTES["utility"] = dict(LEARNED,
    casting=at("原学习、计划和支付后，在实际发射时转换；支持分支 StartPrepaid，不支持分支保留旧实体执行。",
               SKILL + "Systems/LogicSkillCastSequenceSystem.cs", NEW + "UtilityConstructionMigration.cs::internal static bool Emit"),
    action=at("支持的旧轨迹转换为共同区域；未支持的词条/轨迹仍走旧轨迹。", NEW + "UtilityConstructionMigration.cs::private static AbilityDefinition Convert"),
    effect=CONSTRUCTION["effect"],
    display=at("已转换区域不需要独立显示网格，实际地形、植被、麦田、云继续由原版显示；旧分支保持旧表现。",
               NEW + "UtilityConstructionMigration.cs", "Content/Forms/world_domain.json"),
    persistence=at("原已学容器仍按 objects 蓝图恢复；转换定义不写入已学目录。原版地块及 constructed_weather 保存独立天气。",
                   OLD_SAVE, NEW + "ConstructedRainClouds.cs::constructed_weather"))

ROUTES["weapon"] = dict(LEARNED,
    learning=at("筑基御器来源提供实际武器容器，不作为普通学习法术。", CONTENT + "WeaponControl/WeaponControlSkills.cs::SourceGranted"),
    casting=at("御器提供者准备真实武器及动态计划，QueueSkillSequence 排队。", CONTENT + "WeaponControl/WeaponControlSkills.cs::public bool TryUse"),
    payment=at("每次实际出手付费，PaymentTiming.PerEmission；不能一次预付后又逐发扣费。",
               CONTENT + "WeaponControl/WeaponControlSkills.cs::SkillCastPaymentTiming.PerEmission"),
    action=at("实际武器按种类进行近战运动或远程投射物代理；仍为原御器实现。",
              CONTENT + "WeaponControl/WeaponControlSkills.cs", CONTENT + "Combat/EquippedWeaponTrajectories.cs"),
    effect=at("原武器实际攻击及投射物规则，不用通用光球替代武器作用。", CONTENT + "WeaponControl/WeaponControlSkills.cs"),
    persistence=at("objects 保存来源及武器关系；短时御器会话不恢复，物品与长期掌握继续恢复。", OLD_SAVE, PARTS))

ROUTES["consumable"] = dict(LEARNED,
    learning=at("制作符箓或卷轴时封存真实技能载荷；持有物品即可进入对应来源条件，不再复制一套法术。",
                CONTENT + "Behaviours/BehCraftTalisman.cs", CONTENT + "Behaviours/BehCraftMagicScroll.cs"),
    casting=at("一次性来源形成计划并以 Prepaid 启动；成功受理后删除本次物品。",
               CONTENT + "Artifacts/ActiveAbilities/ConsumableSkillActiveAbilityProviders.cs::public bool TryUse"),
    payment=at("消耗物品和其中已封存的强度；使用 Prepaid，不再扣一次施法者普通资源。",
               CONTENT + "Artifacts/ActiveAbilities/ConsumableSkillActiveAbilityProviders.cs::SkillCastFundingSource.Prepaid",
               CONTENT + "Artifacts/ActiveAbilities/ConsumableSkillActiveAbilityProviders.cs::handle.Source.DeleteEntity"),
    display=at("真实纸张显出与燃解由 TalismanVfxManager；载荷继续使用该法术表现。", CONTENT + "Visuals/TalismanVfxManager.cs"),
    persistence=at("objects 保存实际物品组件及封存技能关系；已成功消耗的物品不重新生成。", PARTS, OLD_SAVE))

ROUTES["palm"] = dict(CONSTRUCTION,
    learning=at("身体适配的徒手来源直接提供；不要求把掌击再学习一遍。", CONTENT + "Combat/PalmStrikeCombat.cs::public void Collect"),
    casting=at("旧掌击能力入口调用共同 Start，旧专用 Action 循环已删除。", CONTENT + "Combat/PalmStrikeCombat.cs::public bool TryUse"),
    payment=at("当前正式掌击资料 Cost 为零；伤害和恢复仍按实际身体与普攻规则，其他改造读取自己的费用。",
               "Content/Skills/Body/palm-strike.json", NEW + "ConstructedAbilityService.cs::ConstructionResources.Quote"),
    manual=at("掌击声明 Combat，可在附体轮换中选择并调用；仍需真实身体、敌方人物及动作恢复条件。",
              CONTENT + "Combat/PalmStrikeCombat.cs::ActiveAbilityChannel.Combat", MANUAL))

ROUTES["internal_artifact"] = dict(ARTIFACT,
    learning=at("不独立学习，只由对应法宝能力启动或内部派生。", CONTENT + "Artifacts/ArtifactSkillExecutions.cs"),
    casting=at("旧技能内部执行/剑阵子体，不是另一个主动提供者。", CONTENT + "Artifacts/ArtifactSkillExecutions.cs"),
    payment=at("费用归原法宝生命周期，此内部执行不独立按默认需求收费。", CONTENT + "Artifacts/ArtifactAbilityLifecycle.cs"),
    effect=LEARNED["effect"],
    manual=at("没有独立按钮，人工使用其所属法宝主动能力。", CONTENT + "Artifacts/ActiveAbilities/ArtifactActiveAbilityProvider.cs"),
    autonomous=at("随所属法宝能力推进，不再独立选一次招式。", CONTENT + "Artifacts/ArtifactSkillExecutions.cs"),
    persistence=at("内部执行体不保存；所属器物、能力充能/冷却及部署按 objects 保存。", PARTS + "::NormalizeAbilities"))

ROUTES["raw"] = {
    field: at("纯动画资产，不提供独立学习、施法、支付、伤害、能力栏、AI 或保存入口。" if field != "display" else
              "Manager.SpawnAnim 借用 RawAnim 创建临时动画，结束回收，不算独立法术。",
              SKILL + "Manager.cs::SkillEntityLibrary.RawAnim.NewEntity") for field in FIELDS
}

ROUTES["tribulation"] = dict(LEARNED,
    learning=at("赑风劫阶段建立来源容器，不由人物学习。", CONTENT + "BalefulWindTribulationSkillService.cs"),
    casting=at("世界规则以 StartSourcelessSkillSequence 发出中心和风浪，人物只用于失效判断。",
               CONTENT + "BalefulWindTribulationSkillService.cs::StartSourcelessSkillSequence"),
    payment=at("无施法者序列使用 Prepaid，不扣渡劫人物普通施法资源。", SKILL + "Manager.cs::public Entity StartSourcelessSkillSequence"),
    manual=at("不是可选主动法术，只随渡劫阶段出现。", CONTENT + "BalefulWindTribulationStage.cs"),
    autonomous=at("渡劫系统推进，不经过修士普通抽招。", CONTENT + "Systems/Logic/BalefulWindTribulationSystem.cs"),
    persistence=at("baleful-tribulation 保存劫过程；短时中心和风浪实体不存，读档重建当前过程所需表现。",
                   SAVE + "WorldEnvironmentSaveService.cs::baleful-tribulation", CONTENT + "BalefulWindTribulationSkillService.cs::CaptureWorldSave"))

PROVIDER_ROUTES = {
    "core.learned_skill": "learned", "content.creature_phenotype": "organ", "content.weapon_control": "weapon",
    "content.yao_core": "organ", "content.artifact": "artifact", "content.talisman": "consumable",
    "content.magic_scroll": "consumable", "content.knight_techniques": "knight", "content.core_formation": "formation",
    "content.palm_strike": "palm", "content.constructed": "construction",
}


def command_route(provider):
    """五类元神提供者分别声明控制通道，具体条目另指向真实领域方法。"""
    name = provider["type"].rsplit(".", 1)[-1]
    source = ABILITIES + name + ".cs"
    combat = provider["id"] == "content.yuanshen_soul"
    return {
        "learning": at("由元神层数、载体、聚焦节点、身体和锚点状态提供；不单独学习。", source + "::public void Collect"),
        "casting": at("提供者按条目调用专属元神服务，不进入普通技能序列。", source + "::public bool TryUse"),
        "payment": at("启动、维持、贡献或材料费用由具体领域服务处理；返回、聚焦与取消不假定存在普通施法费。", source + "::public bool TryUse"),
        "action": at("命魂/节点移动、接管、身体恢复或锚点操作，由专属系统推进，见单项方法。", source),
        "effect": at("以本条列出的实际服务方法改变领域状态；不把控制命令伪装为飞行法术。", source + "::public bool TryUse"),
        "display": at("元神载体、锚点及恢复表现由对应服务/系统维护；尚未迁入共同构造显示。",
                      CONTENT + "Persistence/YuanshenSaveVisuals.cs", CONTENT + "Visuals/ActorManifestationVisual.cs"),
        "manual": at("命魂神念攻击声明 Combat|World，可按载体和目标条件使用。" if combat else
                     "声明 World，供玩家世界控制；是否显示由当前条目状态决定。", source + "::GetChannels", MANUAL),
        "autonomous": at("具备 Combat 通道，可参与普通战斗选择；另有节点战斗服务。" if combat else
                         "未声明 Combat 且 AI 权重为零，不参与普通战斗抽招；专属元神决策和任务系统另行推进。",
                         source + "::ResolveAiWeight", CONTENT + "Systems/Logic/YuanshenDecisionSystem.cs",
                         CONTENT + "Systems/Logic/YuanshenNodeTaskSystem.cs"),
        "persistence": at("cultivation 保存元神层数；objects 中 yuanshen.* 部件及关系保存命魂、节点、锚点、夺舍/重塑及高阶状态，读取不重放命令。",
                          SAVE + "CharacterSaveService.cs::PrimordialSoul", PARTS + "::yuanshen.", CONTENT + "Persistence/YuanshenSaveVisuals.cs"),
    }


def process_routes():
    """无独立技能资产的过程仍列完整适用范围，明确未保存的短时工作。"""
    result = {}
    for owner, _, _, _, file in PROCESSES:
        result["process:" + owner] = {
            field: at("专用玩法过程，非可学习或可独立施放的法术；具体工作由该服务维护。", file)
            for field in FIELDS
        }
    result["process:Cultiway.Content.BalefulWindTribulationSkillService"] = dict(ROUTES["tribulation"])
    result["process:Cultiway.Content.Skills.ConstructedAbilityService"] = dict(CONSTRUCTION)
    result["process:Cultiway.Content.Libraries.ElixirEffectGenerator"] = dict(ELIXIR)
    yao = result["process:Cultiway.Content.YaoBeasts.YaoTribulationService"]
    yao.update(
        learning=at("妖修达到进阶门槛由渡劫阶段触发，不学习一个天劫技能。", CONTENT + "Yao/YaoTribulationService.cs::public static bool TryStart"),
        casting=at("YaoTribulationSystem 调用 Update，直接提交天劫伤害和成功/退却/碎丹结果。", CONTENT + "Yao/YaoTribulationService.cs::public static void Update"),
        payment=at("没有普通施法费用，渡劫直接改变生命、证据与妖丹结果。", CONTENT + "Yao/YaoTribulationService.cs::DealDamage"),
        action=at("按渡劫时间与阶段推进，不借用人物招式动作。", CONTENT + "Systems/Logic/YaoTribulationSystem.cs"),
        effect=at("CombatDamageEffects.DealDamage 及结劫领域结果。", CONTENT + "Yao/YaoTribulationService.cs::DealDamage"),
        display=at("原天劫命中与领域反馈，不属于共同法术显示。", CONTENT + "Yao/YaoTribulationService.cs"),
        manual=at("没有独立天劫施放按钮；只能由实际进阶流程进入。", CONTENT + "Yao/YaoTribulationService.cs::YaoTribulationStage"),
        autonomous=at("妖修进阶与渡劫系统自动推进。", CONTENT + "Systems/Logic/YaoTribulationSystem.cs"),
        persistence=at("yao-tribulation 保存世界渡劫记录，yao 保存人物渡劫/妖丹资料。",
                       SAVE + "WorldEnvironmentSaveService.cs::yao-tribulation", CONTENT + "Yao/YaoPersistence.cs"))
    for owner, kind in [("SectLectureService", "讲法"), ("SectScriptureStudyService", "读书"), ("SectTreasureService", "宝库")]:
        file = CONTENT + "Sects/" + owner + ".cs"
        route = result["process:Cultiway.Content.Sects." + owner]
        route.update(
            learning=at("宗门成员通过既有" + kind + "安排取得知识或物品，不新增独立法术资产。", file),
            casting=at("由已有宗门行为调用专用服务，未进入共同施法运行。", file),
            payment=at("按宗门权限、贡献及物品归属处理，详见实际服务；不重复扣普通法术费。", file),
            action=at("沿用原版读书、讲经或物品转移行为，没有法术发力动作。", file),
            effect=at("改变功法了解度、实际读书目标或借用物品；物品能力仍按所属来源执行。", file),
            display=at("沿用宗门及原版行为显示，没有独立法术特效入口。", file),
            manual=at("专用宗门操作及规则；没有统一主动能力按钮，不能视为已接入自由施法。", file),
            autonomous=at("宗门规划及行为按候选、贡献和权限执行。", file),
            persistence=at("sects 保存组织，objects 保存知识、书籍及物品借用关系；不重放一次讲法或扣费。",
                           SAVE + "WorldEnvironmentSaveService.cs::sects", SAVE + "WorldObjectSaveService.Parts.cs"))
    vein = result["process:Cultiway.Content.SpiritVeins.SpiritVeinManager"]
    vein.update(
        learning=at("自然世界资料，不属于人物已学能力。", CONTENT + "SpiritVeins/SpiritVeinManager.cs"),
        casting=at("生成、月度演化和地形重流；没有独立施法请求。", CONTENT + "SpiritVeins/SpiritVeinMonthlySystem.cs"),
        payment=at("环境灵气供给及消耗归原环境玩法，不走技能支付。", CONTENT + "SpiritVeins/SpiritVeinManager.Simulation.cs"),
        action=at("世界地形与灵气场演化，没有人物出招动作。", CONTENT + "SpiritVeins/SpiritVeinManager.Reflow.cs"),
        effect=at("更新灵脉、聚灵地与实际灵气场。", CONTENT + "SpiritVeins/SpiritVeinManager.Simulation.cs"),
        display=at("地图叠加及信息展示；当前没有可施放阵法主体。", CONTENT + "SpiritVeins/SpiritVeinManager.cs"),
        manual=at("没有人物主动能力按钮；不能据此声称已有聚灵阵法术。", CONTENT + "SpiritVeins/SpiritVeinManager.cs"),
        autonomous=at("世界生成、月度系统与稳定地形变化触发。", CONTENT + "SpiritVeins/SpiritVeinMonthlySystem.cs"),
        persistence=at("spirit-veins 保存拓扑与演化，wakan 保存天地灵气。", SAVE + "WorldEnvironmentSaveService.cs::spirit-veins"))
    portal = result["process:Cultiway.Content.Systems.Logic.TeleportArraySystem"]
    file = CONTENT + "Systems/Logic/TeleportArraySystem.cs"
    portal.update(
        learning=at("已建成传送阵提供通行，不由人物学习。", file + "::RebuildGraph"),
        casting=at("门户请求、集客和充能后批量转移，由设施系统维护。", file + "::PortalManager.SnapshotRequests"),
        payment=at("设施当前按人数、等待与请求时限判定，不走普通技能资源支付。", file + "::RequiredPassengers"),
        action=at("充能、批量转移及移动恢复，由设施过程推进。", file + "::TeleportChargeDuration"),
        effect=at("改变实际人物位置及通行状态。", file),
        display=at("充能与传送脉冲由原设施实现，未转成共同构造主体。", file + "::TeleportPulseInterval"),
        manual=at("没有独立人物施法条目，随既有通行请求使用。", file),
        autonomous=at("寻路和门户请求进入设施排队，自动触发批量转移。", file + "::PortalManager.SnapshotRequests"),
        persistence=at("建筑随原版保存、门户图重建；本系统 _states/_pendingTeleports 未登记为长期保存，不声明排队或充能恢复。",
                       file + "::_pendingTeleports", file + "::ResetForNewWorld"))
    return result


def entry_overrides(entry, profile):
    """来源资料和被动执行明确写不适用，不套用同组主动招式的入口。"""
    kind = entry["kind"]
    if kind in ("器官等级", "器官被动效果", "妖丹神通来源", "修炼形成原子", "修炼形成效果"):
        active = (kind == "修炼形成效果" and entry["active"] or
                  kind == "器官等级" and bool(entry["skills"]) or kind == "妖丹神通来源")
        if kind == "修炼形成效果" and active:
            return {}
        sources = CATEGORIES[kind][3] or [CONTENT + "CoreFormationAtoms.cs"]
        label = "来源提供实际子能力，自己不单独施放；子能力入口见引用关系。" if active else "这是来源资料或被动效果，没有独立主动施放及按钮。"
        automatic = ("按实际事件 " + entry["events"] + " 消费此效果，不独立抽取主动招式。" if "events" in entry else
                     "来源资料随实际身体/形成成果编译；由所列子能力和被动效果自动使用。")
        return dict(casting=at(label, *sources), manual=at(label, *sources), autonomous=at(automatic, *sources))
    if entry["id"] == "Cultiway.ArtifactAbility.ArtifactRiding":
        return dict(action=at("实际法宝载具与乘客运动；不创建普通攻击序列。", CONTENT + "Artifacts/ArtifactVehicleService.cs"),
                    effect=at("vehicle_use 提供速度及载客量，由载具服务和飞行系统消费。", CONTENT + "Artifacts/ArtifactVehicleService.cs",
                              CONTENT + "Systems/Logic/ArtifactVehicleFlightSystem.cs"),
                    display=at("真实器物与乘客跟随，尾迹由法宝表现规则生成。", CONTENT + "Artifacts/ArtifactAbilities.Vehicle.cs"),
                    manual=at("无独立施法按钮；通过法宝载具与通行行为使用。", CONTENT + "Artifacts/ArtifactVehicleService.cs"))
    if entry["id"] == "Cultiway.ArtifactAbility.SectGuardianTreasure":
        return dict(effect=at("sect_use 按供奉状态、叠加组及优先级给成员实际属性。", CONTENT + "Artifacts/ArtifactSectService.cs"),
                    autonomous=at("随宗门供奉及成员属性计算消费，不通过战斗抽招。", CONTENT + "Artifacts/ArtifactSectService.cs"),
                    manual=at("无独立施法按钮；已有宗门供奉操作决定此能力是否参与。", CONTENT + "Artifacts/ArtifactSectService.cs"))
    if kind == "法宝能力" and not entry["active"]:
        return {"manual": at("被动能力没有独立按钮；由事件、属性或生命周期触发。", CONTENT + "Artifacts/ArtifactAbilityDispatcher.cs")}
    if kind == "技能资产" and profile in ("formation", "knight"):
        return {"learning": at("当前是所属形成效果或战技的执行资产；取得、手动及自主选择必须通过所属入口，不独立追加一套招式。",
                               CONTENT + ("CoreFormationSkills.cs" if profile == "formation" else "KnightCombat/KnightTechniqueSkills.cs"))}
    return {}


def render_entrypoints(payload):
    """先解释共用九字段，再逐项列出应用路径、差异与实际回调。"""
    profiles = payload["profiles"]
    lines = ["# 各能力调用与保存入口", "", "对应 TODO **0.03**，以[全部能力登记表](全部能力登记表.md)的稳定定位符逐项关联。", "",
             f"快照：{payload['generatedAt']}；代码及工具内容指纹：`{payload['version']['sha256']}`。", "",
             f"本次覆盖 {len(payload['entries'])} 项登记、{len(payload['sourceEntrances'])} 个主动来源和 {len(payload['processes'])} 个无独立技能资产的过程。", "",
             "每项先应用所列路径的九个字段，再应用本项的差异覆盖；JSON 保留同样的关联、源文件行和具体回调。被动、来源资料和纯显示辅助写明不适用，不能把存在同组主动入口等同于该项可手动释放。", "",
             "本表证明当前代码的调用与保存位置，不证明每个入口已经通过实机、存档升级、动画质量或 Worldfall 验收。后续实际迁移、性能及画面任务继续保持未完成。", "",
             "## 当前替换边界", "",
             "- 普通战斗法术、骑士战技、法宝、器官及修炼形成技能的大部分执行仍在旧技能或各自专属系统。共用 ActiveAbilityService 只统一选择、校验和请求。",
             "- 八种功能法术在实际发射时尝试转换；不支持的词条、轨迹和无来源执行仍走旧分支。旧学习、收费、容器蓝图保存尚未删除。",
             "- 掌击的旧专用 Action 循环已在 `259495c0` 替换；当前 PalmStrikeCombat 只提交共同 Start。历史专用 OnUpdateGroup/StrikeUnarmed 结算不再存在于该类。",
             "- 三项基础构造使用共同定义、编译、身体和接触；新增共同路径不表示其他来源已迁移。",
             "- 原版及旧专属长期事实继续由所属玩法保存。短时施放不续播，法宝旧执行句柄明确清除；传送阵临时排队和充能没有正式恢复登记。", "",
             "## 全部主动来源", "", "| 来源 | 当前路径 |", "|---|---|"]
    lines += [f"| `{row['id']}` | [路径 `{row['profile']}`](#route-{row['profile']}) |" for row in payload["sourceEntrances"]]
    for key, fields in profiles.items():
        lines += ["", "<a id=\"route-" + key + "\"></a>", "", "## 路径 " + key, "", "| 环节 | 当前行为及入口 |", "|---|---|"]
        for field, label in FIELDS.items():
            cell = fields[field]
            lines.append("| " + label + " | " + cell["text"] + " " + links(cell["sources"]) + " |")
    for kind in Counter(row["kind"] for row in payload["entries"]):
        lines += ["", "## " + kind, "", "| 稳定定位符与名称 | 应用路径 | 本项调用差异 | 当前作用/显示与回调 | 新旧执行状态 |", "|---|---|---|---|---|"]
        for entry in (entry for entry in payload["entries"] if entry["kind"] == kind):
            changes = [FIELDS[field] + "：" + cell["text"] + links(cell["sources"]) for field, cell in entry["overrides"].items()]
            details = entry["details"]
            if "castResources" in details:
                resource = "、".join(details["castResources"] or []) or "未声明默认资源，按来源容器解析"
                changes.append("资产默认资源：" + resource + "；基础需求 " + str(details["baseDemand"]) + "（不是本次扣费）")
                changes.append("默认轨迹：`" + str(entry.get("trajectory", "")) + "`")
            facts = []
            labels = dict(events="触发事件", skills="授予技能", sourceEffects="来源效果", triggerSkill="事件执行",
                          activeSkill="主动执行", upkeep="身体维持", mode="效果规则", effect="效果键", realms="适用境界")
            for name, value in details.get("sourceFacts", {}).items():
                if value not in (None, [], ""):
                    facts.append(labels[name] + "：" + json.dumps(value, ensure_ascii=False))
            if details.get("effects"):
                facts += [effect["id"] + "（" + effect["trigger"] + "，" + effect["relation"] + "）" for effect in details["effects"]]
            if "tick" in details:
                facts.append("状态周期：" + (str(details["interval"]) + " 秒" if details["tick"] else "关闭"))
                facts.append("属性：" + ("、".join(details["stats"]) or "无固定属性"))
            for name in ("channels", "impact", "spatialForm", "spatial", "activation", "target"):
                if details.get(name) is not None: facts.append(name + "：" + str(details[name]))
            if entry["callbacks"]:
                facts += ["`" + ref["symbol"] + "` " + links([ref]) for ref in entry["callbacks"]]
            else:
                facts.append("无本项独立委托；使用路径及声明资料 " + links(entry["declaration"]))
            cells = ["`" + entry["key"] + "` " + entry["name"], "[" + entry["profile"] + "](#route-" + entry["profile"] + ")",
                     "<br>".join(changes) or "按上述完整九字段；具体作用由右列登记决定", "<br>".join(facts), entry["migration"]]
            lines.append("| " + " | ".join(cell.replace("|", "\\|").replace("\n", " ") for cell in cells) + " |")
    lines += ["", "## 无独立技能资产的过程", "", "| 定位符 | 九字段路径 |", "|---|---|"]
    lines += [f"| `{row['key']}` {row['name']} | [完整路径](#route-{row['profile']}) |" for row in payload["processes"]]
    lines += ["", "## 重新生成", "", "```bash",
              "python3 Scripts/SpellDemos/demo_all.py --launch --inventory --output artifacts/skill_rebuild/ability-entrypoints",
              "python3 Scripts/SpellDemos/build_ability_inventory.py artifacts/skill_rebuild/ability-entrypoints/inventory.json --entrypoints",
              "```", "", "生成 `.md` 及同名 `.json`。引用变动、缺失回调、未分组登记和不完整字段会明确失败；不启动施放，不修改玩家保存。", ""]
    return "\n".join(lines)


def source_ref(location):
    """在开发资料边界确认文件和定位文本，链接到当前真实行。"""
    file, _, needle = location.partition("::")
    path = PROJECT / file
    text = path.read_text(encoding="utf-8-sig")
    offset = text.find(needle)
    if offset < 0:
        raise ValueError("源码定位已失效：" + location)
    return dict(file=file, line=text[:offset].count("\n") + 1, symbol=needle)


def resolve_callback(callback, types, texts):
    """委托含编译器生成的闭包时，定位回其真实制作方法。"""
    owner = callback.split("+")[0] if "+" in callback else callback.rsplit(".", 1)[0]
    generated = re.search(r"<([^<>]+)>b__", callback)
    method = generated[1] if generated else callback.rsplit(".", 1)[-1]
    files = types.get(owner, set())
    pattern = re.compile(r"\b(?:public|private|internal|protected)\s+(?:static\s+)?[^;={}\n]+\b" + re.escape(method) + r"\s*\(")
    matches = []
    for file in sorted(files):
        match = pattern.search(texts[file])
        if match:
            matches.append(dict(file=file, line=texts[file][:match.start()].count("\n") + 1, symbol=callback))
    if not matches:
        raise ValueError("回调未定位到真实声明：" + callback)
    return matches


def route_id(entry):
    """按实际来源选择调用路径，不把可学习标记等同于普通法术。"""
    kind, identifier = entry["kind"], entry["id"]
    if kind == "技能资产":
        if identifier == "raw_anim": return "raw"
        if identifier.startswith("Cultiway.ArtifactSkillExecution."): return "internal_artifact"
        if identifier.startswith("Cultiway.CoreFormation.Skill."): return "formation"
        if identifier.startswith("Cultiway.KnightTechnique."): return "knight"
        if identifier == "Cultiway.WeaponControl": return "weapon"
        if "Tribulation" in identifier: return "tribulation"
        return "utility" if "ConstructedAbilityService" in entry["execution"] else "learned"
    return {"法宝能力": "artifact", "战技定义": "knight", "器官等级": "organ", "器官被动效果": "organ",
            "状态定义": "status", "修炼形成原子": "formation", "修炼形成效果": "formation",
            "妖丹神通来源": "organ", "丹药效果原子": "elixir", "城市功能工程": "city", "身体动作": "palm",
            "构造资料": "construction", "元神功能命令": entry.get("provider")}[kind]


def links(refs):
    """文档引用带源码行号；机器资料另保留定位文字。"""
    return "；".join(f"[{ref['file']}:{ref['line']}](../../{ref['file']}#L{ref['line']})" for ref in refs)


def export_entrypoints(data, destination):
    """每项登记应用完整九字段路径，再附真实回调、差异及旧路径状态。"""
    declarations, types, texts = source_index()
    names = locale_index()
    providers = data["sourceEntrances"]
    references = inventory_references(data)
    routes = dict(ROUTES)
    for provider in providers:
        if provider["id"].startswith("content.yuanshen_"):
            routes[provider["id"]] = command_route(provider)
    routes.update(process_routes())
    profiles = {key: {field: dict(text=value["text"], sources=[source_ref(ref) for ref in value["sources"]])
                      for field, value in fields.items()} for key, fields in routes.items()}
    entries = []
    for raw in data["entries"]:
        entry = enrich(raw, names, declarations, types, texts)
        profile = route_id(entry)
        details = dict(raw.get("entrypoints", {}))
        if raw["kind"] in ("技能资产", "法宝能力", "状态定义") and not details:
            raise ValueError("请重新运行 --inventory 导出当前调用资料：" + raw["key"])
        details["sourceFacts"] = {key: raw[key] for key in
                                 ("events", "skills", "triggerSkill", "activeSkill", "upkeep", "mode", "effect", "realms") if key in raw}
        if raw["kind"] == "器官等级":
            details["sourceFacts"]["sourceEffects"] = raw["effects"]
        callbacks = set(raw.get("callbacks", [])) | set(details.get("callbacks", []))
        callbacks.update(callback for effect in details.get("effects", []) for callback in effect["callbacks"])
        command_refs = []
        if raw["kind"] == "元神功能命令":
            for action in COMMANDS[raw["provider"]][raw["id"]]:
                command_refs.extend(resolve_callback("Cultiway.Content." + action, types, texts))
        elif raw["kind"] == "器官被动效果":
            callbacks.add(raw["execution"])
        specific = [ref for callback in sorted(callbacks) for ref in resolve_callback(callback, types, texts)]
        if command_refs:
            specific.extend(command_refs)
        overrides = {field: dict(text=value["text"], sources=[source_ref(ref) for ref in value["sources"]])
                     for field, value in entry_overrides(raw, profile).items()}
        if command_refs:
            overrides["payment"] = dict(text="不走普通施法收费；本条启动、维持或归返是否改变资源，由所列实际领域方法处理。",
                                        sources=command_refs)
            overrides["effect"] = dict(text="本条实际提交的领域方法，不与同组其他命令混用。", sources=command_refs)
        migration = ("共同执行，旧专用掌击结算已删除" if profile == "palm" else
                     "新增共同构造；不表示其他旧来源已替换" if profile == "construction" else
                     "部分转入共同执行；Convert 返回空仍走旧分支，旧容器保存保留" if profile == "utility" else
                     "纯显示辅助，非迁移对象" if profile == "raw" else
                     "当前专属或旧执行在用，未删除")
        entries.append(dict(key=raw["key"], name=entry["displayName"], kind=raw["kind"], profile=profile,
                            details=details, callbacks=specific, migration=migration, trajectory=raw.get("trajectory"),
                            overrides=overrides,
                            declaration=[source_ref(file) for file in entry["files"]],
                            references=[ref for ref in references if raw["key"] in (ref["owner"], ref["target"])]))
    source_rows = [dict(id=provider["id"], profile=PROVIDER_ROUTES.get(provider["id"], provider["id"]), type=provider["type"])
                   for provider in providers]
    processes = [dict(key="process:" + owner, name=name, profile="process:" + owner)
                 for owner, name, _, _, _ in PROCESSES]
    for row in entries + source_rows + processes:
        if set(profiles[row["profile"]]) != set(FIELDS):
            raise ValueError("调用资料未包含完整九字段：" + str(row))
    payload = dict(schema=1, generatedAt=data["generatedAt"], version=data["version"], profiles=profiles,
                   entries=entries, sourceEntrances=source_rows, processes=processes)
    destination.write_text(render_entrypoints(payload), encoding="utf-8")
    destination.with_suffix(".json").write_text(json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"调用与保存入口：{len(entries)} 项登记、{len(source_rows)} 个来源、{len(processes)} 个过程；{destination}")
    return payload
