using System.Globalization;
using Cultiway.Abstract;
using Cultiway.Content.Artifacts;
using Cultiway.Content.Artifacts.Baibao;
using Cultiway.Content.Components;
using Cultiway.Content.Extensions;
using Cultiway.Content.Libraries;
using Cultiway.Content.Sects;
using Cultiway.Content.UI.CreatureInfoPages;
using Cultiway.Content.Utils;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Wanfa;
using Cultiway.Core.WorldTools;
using Cultiway.UI;
using Cultiway.UI.CreatureInfoPages;
using Cultiway.UI.Prefab;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using NeoModLoader.General;
using UnityEngine;

namespace Cultiway.Content.UI;

[Dependency(typeof(GodPowers), typeof(BaseStatses))]
public class Manager : ICanInit
{
    private const string WanfaPavilionIcon = "cultiway/icons/world_tools/iconWanfaPavilion";
    private const string DiliujiangIcon = "cultiway/icons/world_tools/iconDiliujiang";
    private const string ElementRootRainIcon = "cultiway/icons/world_tools/iconElementRootRain";
    private const string CharacterPanelWakanTitle = "Cultiway.UI.CharacterPanel.Wakan";
    private const string CharacterPanelWakanDescription = "Cultiway.UI.CharacterPanel.Wakan Description";
    private const string CharacterPanelSpiritTitle = "Cultiway.UI.CharacterPanel.Spirit";
    private const string CharacterPanelSpiritDescription = "Cultiway.UI.CharacterPanel.Spirit Description";

    public static Manager Instance { get; private set; }
    public PowerButton WanfaGrantButton { get; private set; }
    public PowerButton BaibaoArchiveButton { get; private set; }
    public PowerButton BaibaoGrantButton { get; private set; }
    public PowerButton UpgradeRainButton { get; private set; }
    public PowerButton ElementRootRainButton { get; private set; }

    private PowerButton _magicWebButton;
    private PowerButton _wanfaPavilionButton;

    public void Init()
    {
        Instance = this;
        WindowNewCreatureInfo.RegisterPage(nameof(CultisysOverviewPage),
            a => Cultisyses.HasAnyCultisys(a.GetExtend()),
            CultisysOverviewPage.Setup, CultisysOverviewPage.Show);
        WindowNewCreatureInfo.RegisterPage(nameof(XianBasePage), a => a.GetExtend().HasComponent<XianBase>(),
            XianBasePage.Setup, XianBasePage.Show);
        WindowNewCreatureInfo.RegisterPage(nameof(JindanPage), a => a.GetExtend().HasComponent<Jindan>(),
            JindanPage.Setup, JindanPage.Show);
        WindowNewCreatureInfo.RegisterPage(nameof(YuanyingPage), a => a.GetExtend().HasComponent<Yuanying>(),
            YuanyingPage.Setup, YuanyingPage.Show);
        WindowNewCreatureInfo.RegisterPage(nameof(CultibookPage), a=>a.GetExtend().HasCultibook(), CultibookPage.Setup, CultibookPage.Show);
        WindowNewCreatureInfo.RegisterPage(nameof(ElixirPage), a=> a.GetExtend().HasMaster<ElixirAsset>(), ElixirPage.Setup, ElixirPage.Show);
        WindowNewCreatureInfo.RegisterPage(nameof(SectPage), a=> a.GetExtend().sect !=null, SectPage.Setup, SectPage.Show);
        WindowNewCreatureInfo.RegisterPage(nameof(ArtifactPage),
            a => a.GetExtend().HasEquippedArtifacts(),
            ArtifactPage.Setup, ArtifactPage.Show, ArtifactPage.GetTitle);
        CharacterPanelExtensions.RegisterProgressBar("cultiway_wakan",
            a => a.GetExtend().HasCultisys<Xian>(),
            ReadWakanPanelValue);
        CharacterPanelExtensions.RegisterProgressBar("cultiway_spirit",
            a => a.GetExtend().HasCultisys<Magic>(),
            ReadSpiritPanelValue);
        PossessionStatusEffectsUi.Ensure();

        WindowWorldWakan.CreateAndInit($"Cultiway.UI.{nameof(WindowWorldWakan)}");
        Cultiway.UI.Manager.AddButton(TabButtonType.WORLD,
            PowerButtonCreator.CreateWindowButton(
                $"Cultiway.UI.{nameof(WindowWorldWakan)} Title",
                $"Cultiway.UI.{nameof(WindowWorldWakan)}",
                SpriteTextureLoader.getSprite("cultiway/icons/iconWakan")
            )
        );
        Cultiway.UI.Manager.AddButton(TabButtonType.WORLD,
            PowerButtonCreator.CreateGodPowerButton(
                GodPowers.ExtendGeoRegion.id,
                SpriteTextureLoader.getSprite("cultiway/icons/iconExtendGeoRegion")
            )
        );
        Cultiway.UI.Manager.AddButton(TabButtonType.WORLD,
            PowerButtonCreator.CreateGodPowerButton(
                GodPowers.RemoveGeoRegion.id,
                SpriteTextureLoader.getSprite("cultiway/icons/iconRemoveGeoRegion")
            )
        );
        Cultiway.UI.Manager.AddButton(TabButtonType.RACE,
            PowerButtonCreator.CreateGodPowerButton(
                GodPowers.EasternHuman.id,
                SpriteTextureLoader.getSprite("cultiway/icons/races/iconEasternHuman")
            )
        );
        Cultiway.UI.Manager.AddButton(TabButtonType.RACE,
            PowerButtonCreator.CreateGodPowerButton(
                GodPowers.Gui.id,
                SpriteTextureLoader.getSprite("cultiway/icons/races/iconGui")
            )
        );
        Cultiway.UI.Manager.AddButton(TabButtonType.RACE,
            PowerButtonCreator.CreateGodPowerButton(
                GodPowers.EasternHumanSkin.id,
                SpriteTextureLoader.getSprite("cultiway/icons/traits/iconCultureSkin")
            )
        );
        Cultiway.UI.Manager.InsertWallButton(
            GodPowers.EasternHumanWall,
            "ui/Icons/iconWallEasternHuman"
        );

        SpecialItemTooltip.RegisterSetupAction((tooltip, type, entity) =>
        {
            if (entity.TryGetComponent(out Elixir elixir))
            {
                var desc = elixir.Type.description_key;
                if (LM.Has(desc))
                {
                    desc = LM.Get(desc);
                }
                tooltip.Tooltip.addDescription($"\n{desc}");
            }
            if (entity.TryGetComponent(out Talisman talisman))
            {
                var skill_container = talisman.SkillContainer;
                //tooltip.Tooltip.name.text = skill_asset.GetName();
                tooltip.Tooltip.addDescription("\n");
                tooltip.Tooltip.addDescription(skill_container.ToString());
            }
            if (entity.TryGetComponent(out Artifact _))
            {
                AppendArtifactControlDetails(tooltip, entity);
            }

            Sect ownerSect = SectTreasureInventory.FindOwner(entity);
            if (ownerSect != null)
            {
                string ownerLabel = "Cultiway.Sect.Treasure.Owner".Localize();
                tooltip.Tooltip.addDescription($"\n{ownerLabel}: {ownerSect.data.name}");
                Actor borrower = SectTreasureService.GetBorrower(entity);
                if (borrower != null)
                {
                    string borrowerLabel = "Cultiway.Sect.Treasure.Borrower".Localize();
                    tooltip.Tooltip.addDescription($"\n{borrowerLabel}: {borrower.getName()}");
                }
            }
        });
    }

    public void InitWanfa(WanfaPavilionService service)
    {
        WindowActorTargetFilter.CreateAndInit(WindowActorTargetFilter.Id, WindowActorTargetFilter.WindowSize);
        WindowNewCreatureInfo.RegisterPage(nameof(Cultiway.UI.CreatureInfoPages.SkillPage),
            actor => actor.GetExtend().GetLearnedSkillsInOrder().Count > 0,
            Cultiway.UI.CreatureInfoPages.SkillPage.Setup, Cultiway.UI.CreatureInfoPages.SkillPage.Show);

        WindowWanfaPavilion.CreateAndInit(WindowWanfaPavilion.Id, WindowWanfaPavilion.WindowSize);
        WindowWanfaSkillEditor.CreateAndInit(WindowWanfaSkillEditor.Id, WindowWanfaSkillEditor.WindowSize);
        WindowWanfaGrantConflict.CreateAndInit(WindowWanfaGrantConflict.Id);
        WindowMagicWebBrowser.CreateAndInit(WindowMagicWebBrowser.Id, WindowMagicWebBrowser.WindowSize);

        _magicWebButton = PowerButtonCreator.CreateWindowButton(
            $"{WindowMagicWebBrowser.Id} Title", WindowMagicWebBrowser.Id,
            SpriteTextureLoader.getSprite("ui/icons/iconMana"));
        _wanfaPavilionButton = PowerButtonCreator.CreateWindowButton(
            $"{WindowWanfaPavilion.Id} Title", WindowWanfaPavilion.Id,
            SpriteTextureLoader.getSprite(WanfaPavilionIcon));
        UiPowerButtonAdapter.ApplyWorldToolConfigStyle(_wanfaPavilionButton);
        WanfaGrantButton = PowerButtonCreator.CreateGodPowerButton(
            WorldboxGame.GodPowers.WanfaGrant.id, SpriteTextureLoader.getSprite(WanfaPavilionIcon));

        service.TestCastRequested += draft => WanfaTestCastSession.Enter(draft, WanfaGrantButton);
        service.GrantConflictRequested += WindowWanfaGrantConflict.Enqueue;
        service.GrantConflictsCleared += WindowWanfaGrantConflict.ClearPending;
        service.TestCastCompleted += WindowWanfaSkillEditor.ResumeAfterTestCast;
        service.WorldStateClearing += WindowWanfaSkillEditor.ClearWorldState;
    }

    /// <summary>初始化百宝阁目录、炼制、收录窗口与两种地图交互工具。</summary>
    public void InitBaibao(BaibaoPavilionService service)
    {
        WindowBaibaoPavilion.CreateAndInit(WindowBaibaoPavilion.Id, WindowBaibaoPavilion.WindowSize);
        WindowBaibaoForge.CreateAndInit(WindowBaibaoForge.Id, WindowBaibaoForge.WindowSize);
        WindowBaibaoArchive.CreateAndInit(WindowBaibaoArchive.Id, WindowBaibaoArchive.WindowSize);

        PowerButton pavilionButton = PowerButtonCreator.CreateWindowButton(
            $"{WindowBaibaoPavilion.Id} Title", WindowBaibaoPavilion.Id,
            SpriteTextureLoader.getSprite(BaibaoUiIcons.Pavilion));
        UiPowerButtonAdapter.ApplyWorldToolConfigStyle(pavilionButton);
        BaibaoGrantButton = PowerButtonCreator.CreateGodPowerButton(
            WorldboxGame.GodPowers.BaibaoGrant.id, SpriteTextureLoader.getSprite(BaibaoUiIcons.Pavilion));
        BaibaoArchiveButton = PowerButtonCreator.CreateGodPowerButton(
            WorldboxGame.GodPowers.BaibaoArchive.id, SpriteTextureLoader.getSprite(BaibaoUiIcons.Archive));

        Cultiway.UI.Manager.AddButtonPair(TabButtonType.WORLD, _magicWebButton, BaibaoArchiveButton);
        Cultiway.UI.Manager.AddButtonPair(TabButtonType.WORLD, _wanfaPavilionButton, WanfaGrantButton);
        Cultiway.UI.Manager.AddButtonPair(TabButtonType.WORLD, pavilionButton, BaibaoGrantButton);
        service.ArchiveRequested += WindowBaibaoArchive.Open;
    }

    /// <summary>初始化帝流浆配置窗口，并在世界页加入成对的配置和投放按钮。</summary>
    public void InitUpgradeRain()
    {
        UpgradeRainService.Initialize();
        WindowUpgradeRainConfig.CreateAndInit(WindowUpgradeRainConfig.Id, WindowUpgradeRainConfig.WindowSize);
        PowerButton configButton = PowerButtonCreator.CreateWindowButton(
            $"{WindowUpgradeRainConfig.Id} Title", WindowUpgradeRainConfig.Id,
            SpriteTextureLoader.getSprite(DiliujiangIcon));
        UiPowerButtonAdapter.ApplyWorldToolConfigStyle(configButton);
        UpgradeRainButton = PowerButtonCreator.CreateGodPowerButton(
            WorldboxGame.GodPowers.UpgradeRain.id, SpriteTextureLoader.getSprite(DiliujiangIcon));
        Cultiway.UI.Manager.AddButtonPair(TabButtonType.WORLD, configButton, UpgradeRainButton);
    }

    /// <summary>初始化灵根雨配置窗口，并在世界页加入成对的配置和投放按钮。</summary>
    public void InitElementRootRain()
    {
        ElementRootRainService.Initialize();
        WindowElementRootRainConfig.CreateAndInit(WindowElementRootRainConfig.Id,
            WindowElementRootRainConfig.WindowSize);
        PowerButton configButton = PowerButtonCreator.CreateWindowButton(
            $"{WindowElementRootRainConfig.Id} Title", WindowElementRootRainConfig.Id,
            SpriteTextureLoader.getSprite(ElementRootRainIcon));
        UiPowerButtonAdapter.ApplyWorldToolConfigStyle(configButton);
        ElementRootRainButton = PowerButtonCreator.CreateGodPowerButton(
            WorldboxGame.GodPowers.ElementRootRain.id, SpriteTextureLoader.getSprite(ElementRootRainIcon));
        Cultiway.UI.Manager.AddButtonPair(TabButtonType.WORLD, configButton, ElementRootRainButton);
    }

    private static void AppendArtifactControlDetails(SpecialItemTooltip tooltip, Entity artifact)
    {
        Entity owner = default;
        foreach (Entity candidate in artifact.GetIncomingLinks<EquippedArtifactRelation>().Entities)
        {
            owner = candidate;
            break;
        }

        long ownerId = 0;
        float preparedLoad;
        float operatingLoad;
        if (!owner.IsNull)
        {
            ActorExtend actor = owner.GetComponent<ActorBinder>().AE;
            ownerId = actor.Base.data.id;
            EquippedArtifactRelation relation = owner.GetRelation<EquippedArtifactRelation, Entity>(artifact);
            ArtifactLoadoutState loadout = actor.GetArtifactLoadoutState();
            var divineSense = actor.Base.stats[WorldboxGame.BaseStats.DivineSense.id];
            tooltip.Tooltip.addDescription("\n");
            tooltip.Tooltip.addLineText("状态", relation.state.GetName(), pLocalize: false);
            tooltip.Tooltip.addLineText(
                "控制",
                relation.mode.GetName() + (relation.locked ? "（锁定）" : string.Empty),
                pLocalize: false);
            tooltip.Tooltip.addLineText(
                "驾驭",
                $"{loadout.prepared_load + loadout.operating_load:0.#}/{divineSense:0.#}",
                pLocalize: false);
            tooltip.Tooltip.addLineText(
                "分念",
                $"{loadout.used_threads}/{ArtifactControlRules.GetThreadCapacity(divineSense)}",
                pLocalize: false);
        }
        else
        {
            if (artifact.TryGetComponent(out ArtifactAttunement attunement)) ownerId = attunement.owner_actor_id;
            tooltip.Tooltip.addDescription("\n");
            tooltip.Tooltip.addLineText("状态", "未装备", pLocalize: false);
        }

        ArtifactControlRules.ResolveLoads(artifact, ownerId, out preparedLoad, out operatingLoad, out _);
        tooltip.Tooltip.addLineText(
            "神识负荷",
            $"准备 {preparedLoad:0.#} / 运转 {operatingLoad:0.#}",
            pLocalize: false);
        ArtifactMaterialData materialData = artifact.GetComponent<ArtifactMaterialData>();
        tooltip.Tooltip.addLineText("炼材", materialData.ingredient_count.ToString(), pLocalize: false);
        tooltip.Tooltip.addLineText("稳定", $"{materialData.stability * 100f:0.#}%", pLocalize: false);
        ArtifactAtomData atomData = artifact.GetComponent<ArtifactAtomData>();
        ArtifactAtomEntry[] atoms = atomData.entries ?? [];
        for (int i = 0; i < atoms.Length; i++)
        {
            ArtifactAtomAsset atom = Libraries.Manager.ArtifactAtomLibrary.get(atoms[i].atom_id);
            string name = atom.name_stems.Length > 0 ? atom.name_stems[0] : atom.id;
            tooltip.Tooltip.addLineText(name, $"{atoms[i].strength:0.##}", pLocalize: false);
        }
        if (artifact.TryGetComponent(out ArtifactAttunement currentAttunement))
        {
            tooltip.Tooltip.addLineText("祭炼", $"{currentAttunement.mastery:0.#}%", pLocalize: false);
        }
        ArtifactAbilitySet abilitySet = artifact.GetComponent<ArtifactAbilitySet>();
        for (int i = 0; i < abilitySet.abilities.Length; i++)
        {
            ArtifactAbilityInstance ability = abilitySet.abilities[i];
            ArtifactAbilityAsset asset = Libraries.Manager.ArtifactAbilityLibrary.get(ability.ability_id);
            if (asset == null) continue;
            tooltip.Tooltip.addLineText(asset.GetName(), asset.GetDescription(ability), pLocalize: false);
        }
    }

    private static CharacterPanelProgressBarState ReadWakanPanelValue(Actor actor)
    {
        var ae = actor.GetExtend();
        ref var xian = ref ae.GetCultisys<Xian>();
        float current = Mathf.Max(0f, xian.wakan);
        float max = Mathf.Max(0f, actor.stats[BaseStatses.MaxWakan.id]);

        return new CharacterPanelProgressBarState(
            current,
            max,
            "cultiway/icons/iconWakan",
            CharacterPanelWakanTitle,
            CharacterPanelWakanDescription,
            $"{FormatWholeNumber(current)} / {FormatWholeNumber(max)}",
            new Color(0f, 0.62f, 0.78f, 1f)
        );
    }

    private static CharacterPanelProgressBarState ReadSpiritPanelValue(Actor actor)
    {
        var ae = actor.GetExtend();
        ref var magic = ref ae.GetCultisys<Magic>();
        float current = Mathf.Max(0f, magic.spirit);
        float max = Mathf.Max(0f, actor.stats[BaseStatses.MaxSpirit.id]);

        return new CharacterPanelProgressBarState(
            current,
            max,
            "cultiway/icons/iconMagic",
            CharacterPanelSpiritTitle,
            CharacterPanelSpiritDescription,
            $"{FormatWholeNumber(current)} / {FormatWholeNumber(max)}",
            new Color(0.55f, 0.35f, 0.85f, 1f)
        );
    }

    private static string FormatWholeNumber(float value)
    {
        return Mathf.FloorToInt(value).ToString(CultureInfo.InvariantCulture);
    }
}
