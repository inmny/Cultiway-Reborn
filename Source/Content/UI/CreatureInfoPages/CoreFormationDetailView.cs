using Cultiway.Core;
using Cultiway.UI;
using Cultiway.UI.Prefab;
using Cultiway.Utils.Extension;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.Content.UI.CreatureInfoPages;

/// <summary>金丹与元婴详情页共用的紧凑字段布局。</summary>
internal sealed class CoreFormationDetailView : MonoBehaviour
{
    /// <summary>构成之前的名称、强度与阶段文本。</summary>
    private Text headerText;

    /// <summary>与特殊效果信息按钮同行的构成文本。</summary>
    private Text atomsText;

    /// <summary>构成之后的元素、法术与演化文本。</summary>
    private Text footerText;

    /// <summary>打开当前角色实际形成效果说明的图标按钮。</summary>
    private Button effectsButton;

    /// <summary>在原版角色信息页的 246 像素内容宽度内创建共享布局。</summary>
    public static CoreFormationDetailView Create(CreatureInfoPage page)
    {
        var view = page.gameObject.AddComponent<CoreFormationDetailView>();
        GameObject root = UiLayout.Create(page.transform, "CoreFormationDetail", false, 246f, 208f, 1f);
        view.headerText = UiElements.CreateText(root.transform, "Header", string.Empty, 246f, 42f, 8,
            TextAnchor.UpperLeft, verticalOverflow: VerticalWrapMode.Overflow);

        GameObject atomsRow = UiLayout.Create(root.transform, "Atoms", true, 246f, 18f, 2f,
            TextAnchor.MiddleLeft);
        view.atomsText = UiElements.CreateText(atomsRow.transform, "Text", string.Empty, 226f, 18f, 8,
            TextAnchor.MiddleLeft, verticalOverflow: VerticalWrapMode.Overflow);
        view.effectsButton = UiElements.CreateIconButton(atomsRow.transform, "Effects", UiIcons.Info,
            18f, 18f, null, 3f);

        view.footerText = UiElements.CreateText(root.transform, "Footer", string.Empty, 246f, 72f, 8,
            TextAnchor.UpperLeft, verticalOverflow: VerticalWrapMode.Overflow);
        UiTooltip.Set(view.effectsButton.gameObject, "Cultiway.CoreFormation.Page.Effects.Title",
            "Cultiway.CoreFormation.Page.Effects.Empty");
        return view;
    }

    /// <summary>刷新字段文本及信息按钮当前角色对应的动态效果详情。</summary>
    public void SetContent(
        ActorExtend actor,
        string header,
        string atoms,
        string footer)
    {
        headerText.text = header;
        atomsText.text = atoms;
        footerText.text = footer;
        UiTooltip.Set(effectsButton.gameObject, "Cultiway.CoreFormation.Page.Effects.Title".Localize(),
            CoreFormationEffectPresentation.BuildTooltip(actor));
    }
}
