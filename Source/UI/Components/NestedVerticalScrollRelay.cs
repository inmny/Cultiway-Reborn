using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Cultiway.UI.Components;

/// <summary>当内层竖向滚动区到达边界时，将继续滚动的输入转交给最近的外层滚动区。</summary>
internal sealed class NestedVerticalScrollRelay : MonoBehaviour, IScrollHandler
{
    private ScrollRect _local;
    private ScrollRect _parent;

    /// <summary>给滚动内容所属的 ScrollRect 安装边界转发组件。</summary>
    public static void Attach(Transform content)
    {
        if (content?.parent?.parent == null) return;
        var root = content.parent.parent.gameObject;
        if (root.GetComponent<NestedVerticalScrollRelay>() == null)
            root.AddComponent<NestedVerticalScrollRelay>();
    }

    private void Awake()
    {
        _local = GetComponent<ScrollRect>();
        var candidates = GetComponentsInParent<ScrollRect>(true);
        for (var i = 0; i < candidates.Length; i++)
        {
            if (candidates[i] == _local) continue;
            _parent = candidates[i];
            break;
        }
    }

    public void OnScroll(PointerEventData eventData)
    {
        if (_local == null || _parent == null || eventData == null) return;
        var contentHeight = _local.content == null ? 0f : _local.content.rect.height;
        var viewportHeight = _local.viewport == null ? 0f : _local.viewport.rect.height;
        var cannotScrollLocally = contentHeight <= viewportHeight + 0.5f;
        var towardTop = eventData.scrollDelta.y > 0f;
        var atTop = _local.verticalNormalizedPosition >= 0.999f;
        var atBottom = _local.verticalNormalizedPosition <= 0.001f;
        if (cannotScrollLocally || towardTop && atTop || !towardTop && atBottom)
            _parent.OnScroll(eventData);
    }
}
