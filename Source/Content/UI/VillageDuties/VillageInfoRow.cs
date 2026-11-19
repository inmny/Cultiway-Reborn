using System;
using Cultiway.Abstract;
using Cultiway.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.Content.UI.VillageDuties;

/// <summary>复用人员和编制条目，说明文字与固定操作区分开。</summary>
public sealed class VillageInfoRow : APrefabPreview<VillageInfoRow>
{
    /// <summary>标题。</summary>
    private Text title;
    /// <summary>状态与军功说明。</summary>
    private Text detail;
    /// <summary>查看人物或阵列。</summary>
    private Button first;
    /// <summary>名称编辑或关联操作。</summary>
    private Button second;

    /// <summary>解析自己创建的固定节点。</summary>
    protected override void OnInit()
    {
        title = transform.Find("Labels/Title").GetComponent<Text>();
        detail = transform.Find("Labels/Detail").GetComponent<Text>();
        first = transform.Find("First").GetComponent<Button>();
        second = transform.Find("Second").GetComponent<Button>();
    }

    /// <summary>每次复用替换正文和回调，不给整行设置悬浮提示。</summary>
    /// <param name="heading">本行标题。</param>
    /// <param name="body">身份、军功和任务说明。</param>
    /// <param name="firstLabel">主要操作文字。</param>
    /// <param name="onFirst">主要操作。</param>
    /// <param name="secondLabel">次要操作文字。</param>
    /// <param name="onSecond">可选的次要操作。</param>
    internal void Bind(string heading, string body, string firstLabel = null, Action onFirst = null,
        string secondLabel = null, Action onSecond = null)
    {
        Init(); title.text = heading; detail.text = body;
        BindButton(first, firstLabel, onFirst); BindButton(second, secondLabel, onSecond);
    }

    /// <summary>统一更新固定按钮及其动作。</summary>
    /// <param name="button">复用的按钮。</param>
    /// <param name="label">本轮显示的文字。</param>
    /// <param name="action">本轮操作，空操作表示隐藏按钮。</param>
    private static void BindButton(Button button, string label, Action action)
    {
        button.onClick.RemoveAllListeners(); button.gameObject.SetActive(action != null);
        if (action == null) return;
        UiElements.SetButtonLabel(button, label); button.onClick.AddListener(action.Invoke);
    }

    /// <summary>释放关闭窗口后的领域引用。</summary>
    internal void Unbind() { if (first == null) return; first.onClick.RemoveAllListeners(); second.onClick.RemoveAllListeners(); }

    /// <summary>按公共布局创建一次池化条目。</summary>
    private static void _init()
    {
        var m = UiTheme.Current.Metrics;
        var obj = UiLayout.Create(ModClass.I.PrefabLibrary, nameof(VillageInfoRow), true, 500, 52, m.SpacingSm);
        UiListRowChrome.Attach(obj, false);
        var labels = UiLayout.Create(obj.transform, "Labels", false, 392, 48, m.SpacingXs);
        UiElements.CreateText(labels.transform, "Title", "", 392, 16, 8, TextAnchor.UpperLeft, FontStyle.Bold);
        UiElements.CreateText(labels.transform, "Detail", "", 392, 30, 7, TextAnchor.UpperLeft);
        UiElements.CreateButton(obj.transform, "First", "", 48, m.ControlSmall, () => { });
        UiElements.CreateButton(obj.transform, "Second", "", 48, m.ControlSmall, () => { });
        Prefab = obj.AddComponent<VillageInfoRow>();
    }
}
