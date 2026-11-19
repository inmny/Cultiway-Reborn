using System.Linq;
using Cultiway.Core.SkillLibV3.Wanfa;
using Cultiway.Core.Skills;

namespace Cultiway.UI;

public sealed partial class WindowWanfaConstructionEditor
{
    /// <summary>只分享或收藏已确认修订；窗口可继续保留本次未提交的编辑。</summary>
    private void ShowSavedConstructionActions()
    {
        AbilityDefinition saved = access.Read(session.SourceId);
        WanfaPavilionService store = WanfaPavilionService.Instance;
        bool favorite = store.ConstructionFavorites.Any(item => item.Id == saved.Id && item.Revision == saved.Revision);
        menu.Show(T("ShareSavedConstruction"), new[]
        {
            new UiOptionMenuOption
            {
                Label = T(favorite ? "ShareRemoveFavorite" : "ShareFavorite"), IconPath = UiIcons.Favorite,
                Select = () => ConstructionShareNotice.Run(() =>
                {
                    store.SetConstructionFavorite(saved, !favorite);
                    ConstructionShareNotice.Show(favorite ? "ShareFavoriteRemoved" : "ShareFavorited");
                })
            },
            new UiOptionMenuOption
            {
                Label = T("ShareCopyText"),
                Select = () => ConstructionShareNotice.Run(() => ConstructionShareNotice.Copy(saved))
            }
        });
    }
}
