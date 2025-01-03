using Cultiway.Abstract;
using Cultiway.Content.Components;
using Cultiway.UI;
using Cultiway.Utils.Extension;

namespace Cultiway.Content.UI.CreatureInfoPages;

public class Manager : ICanInit
{
    public void Init()
    {
        WindowNewCreatureInfo.RegisterPage(nameof(XianBasePage), a => a.GetExtend().HasComponent<XianBase>(),
            XianBasePage.Setup, XianBasePage.Show);
    }
}