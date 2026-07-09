using System.Globalization;
using System.Linq;
using Cultiway.Abstract;
using Cultiway.Content.Components;
using Cultiway.Content.Extensions;
using Cultiway.Content.Libraries;
using Cultiway.Content.UI.CreatureInfoPages;
using Cultiway.UI;
using Cultiway.UI.Prefab;
using Cultiway.Utils.Extension;
using NeoModLoader.General;
using UnityEngine;

namespace Cultiway.Content.UI;

[Dependency(typeof(GodPowers), typeof(BaseStatses))]
public class Manager : ICanInit
{
    private const string CharacterPanelWakanTitle = "Cultiway.UI.CharacterPanel.Wakan";
    private const string CharacterPanelWakanDescription = "Cultiway.UI.CharacterPanel.Wakan Description";
    private const string CharacterPanelSpiritTitle = "Cultiway.UI.CharacterPanel.Spirit";
    private const string CharacterPanelSpiritDescription = "Cultiway.UI.CharacterPanel.Spirit Description";

    public void Init()
    {
        WindowNewCreatureInfo.RegisterPage(nameof(XianBasePage), a => a.GetExtend().HasComponent<XianBase>(),
            XianBasePage.Setup, XianBasePage.Show);
        WindowNewCreatureInfo.RegisterPage(nameof(JindanPage), a => a.GetExtend().HasComponent<Jindan>(),
            JindanPage.Setup, JindanPage.Show);
        WindowNewCreatureInfo.RegisterPage(nameof(YuanyingPage), a => a.GetExtend().HasComponent<Yuanying>(),
            YuanyingPage.Setup, YuanyingPage.Show);
        WindowNewCreatureInfo.RegisterPage(nameof(CultibookPage), a=>a.GetExtend().HasCultibook(), CultibookPage.Setup, CultibookPage.Show);
        WindowNewCreatureInfo.RegisterPage(nameof(ElixirPage), a=> a.GetExtend().HasMaster<ElixirAsset>(), ElixirPage.Setup, ElixirPage.Show);
        WindowNewCreatureInfo.RegisterPage(nameof(SectPage), a=> a.GetExtend().sect !=null, SectPage.Setup, SectPage.Show);
        WindowNewCreatureInfo.RegisterPage(nameof(SkillPage), a=> a.GetExtend().all_skills.Count > 0, SkillPage.Setup, SkillPage.Show);
        WindowNewCreatureInfo.RegisterPage(nameof(ArtifactPage),
            a => a.GetExtend().GetItems().Any(x => x.HasComponent<Artifact>()),
            ArtifactPage.Setup, ArtifactPage.Show);
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
                // 第一阶段效果属性未实现，tooltip 仅占位提示
                tooltip.Tooltip.addDescription("\n法器（未觉醒）");
            }
        });
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
