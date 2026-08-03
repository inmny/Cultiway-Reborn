using Cultiway.Content.Extensions;
using Cultiway.UI.Prefab;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Content.UI.CreatureInfoPages;

/// <summary>人物信息窗口中的筑基三花五气详情页。</summary>
public sealed class XianBasePage : MonoBehaviour
{
    private FoundationDetailView detailView;

    /// <summary>创建固定尺寸的筑基详情布局。</summary>
    public static void Setup(CreatureInfoPage page)
    {
        var component = page.gameObject.AddComponent<XianBasePage>();
        component.detailView = FoundationDetailView.Create(page);
    }

    /// <summary>使用角色当前筑基组件刷新完成度、比例和强度。</summary>
    public static void Show(CreatureInfoPage page, Actor actor)
    {
        XianBasePage component = page.GetComponent<XianBasePage>();
        component.detailView.SetContent(new FoundationPageModel(actor.GetExtend()));
    }
}
