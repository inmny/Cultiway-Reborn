using UnityEngine;

namespace Cultiway.UI;

/// <summary>统一管理窗口内模态面板及其所属 CanvasGroup 的交互状态。</summary>
internal sealed class UiModal
{
    public GameObject Panel { get; }
    private readonly CanvasGroup _owner;

    public UiModal(GameObject panel, CanvasGroup owner)
    {
        Panel = panel;
        _owner = owner;
        Panel.SetActive(false);
    }

    public void Show()
    {
        Panel.transform.SetAsLastSibling();
        Panel.SetActive(true);
        _owner.interactable = false;
    }

    public void Hide()
    {
        Panel.SetActive(false);
        _owner.interactable = true;
    }
}
