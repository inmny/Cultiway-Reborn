using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI;

/// <summary>滚动区和目录共用的空状态文本。</summary>
internal sealed class UiEmptyState
{
    public Text Text { get; }

    public UiEmptyState(Transform parent, string value, float width, float height)
    {
        Text = UiElements.CreateText(parent, "Empty", value, width, height, 7, TextAnchor.MiddleCenter);
        Text.raycastTarget = false;
    }

    public void SetVisible(bool visible)
    {
        Text.gameObject.SetActive(visible);
    }
}
