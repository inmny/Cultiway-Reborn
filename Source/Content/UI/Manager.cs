using Cultiway.Abstract;
using Cultiway.Content.Components;
using Cultiway.Content.UI.CreatureInfoPages;
using Cultiway.UI;
using Cultiway.Utils.Extension;
using NeoModLoader.General;

namespace Cultiway.Content.UI;

public class Manager : ICanInit
{
    public void Init()
    {
        WindowNewCreatureInfo.RegisterPage(nameof(XianBasePage), a => a.GetExtend().HasComponent<XianBase>(),
            XianBasePage.Setup, XianBasePage.Show);
        WindowNewCreatureInfo.RegisterPage(nameof(JindanPage), a => a.GetExtend().HasComponent<Jindan>(),
            JindanPage.Setup, JindanPage.Show);

        WindowWorldWakan.CreateAndInit($"Cultiway.UI.{nameof(WindowWorldWakan)}");
        Cultiway.UI.Manager.AddButton(TabButtonType.WORLD,
            PowerButtonCreator.CreateWindowButton(
                $"Cultiway.UI.{nameof(WindowWorldWakan)} Title",
                $"Cultiway.UI.{nameof(WindowWorldWakan)}",
                SpriteTextureLoader.getSprite("cultiway/icons/iconWakan")
            )
        );
    }
}