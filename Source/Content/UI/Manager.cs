using Cultiway.Abstract;
using Cultiway.Content.Components;
using Cultiway.Content.Extensions;
using Cultiway.Content.Libraries;
using Cultiway.Content.UI.CreatureInfoPages;
using Cultiway.UI;
using Cultiway.UI.Prefab;
using Cultiway.Utils.Extension;
using NeoModLoader.General;

namespace Cultiway.Content.UI;

[Dependency(typeof(GodPowers))]
public class Manager : ICanInit
{
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
        });
    }
}