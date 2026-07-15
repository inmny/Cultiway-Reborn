using System.Collections.Generic;
using UnityEngine.UI;

namespace Cultiway.UI;

/// <summary>统一管理一组互斥按钮的中性灰选中态。</summary>
internal sealed class UiSegmentedTabs
{
    private readonly List<Button> _buttons = new();

    public void Add(Button button)
    {
        _buttons.Add(button);
    }

    public void AddRange(IEnumerable<Button> buttons)
    {
        _buttons.AddRange(buttons);
    }

    public void SetSelected(int index)
    {
        for (int i = 0; i < _buttons.Count; i++) UiStateStyle.SetSelected(_buttons[i], i == index);
    }

    public void SetSelected(Button selected)
    {
        for (int i = 0; i < _buttons.Count; i++) UiStateStyle.SetSelected(_buttons[i], _buttons[i] == selected);
    }
}
