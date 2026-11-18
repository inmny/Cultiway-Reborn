using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Core.Skills;
using UnityEngine.UI;

namespace Cultiway.UI;

/// <summary>展示原编译器认出的冲突，在现有编辑页定位实际声明。</summary>
public sealed partial class WindowWanfaConstructionEditor
{
    /// <summary>本次检查的两处位置；不参与保存或撤销。</summary>
    private ConstructionIssue constructionIssue;
    /// <summary>只在可定位的错误上出现。</summary>
    private Button locateIssueButton;

    /// <summary>刷新或关闭时移除上一次失败；不把过期位置带到新草稿。</summary>
    private void ClearConstructionIssue()
    {
        constructionIssue = null;
        locateIssueButton.gameObject.SetActive(false);
        UiTooltip.Clear(state.gameObject);
    }

    /// <summary>在原有状态栏和提示中显示错误，已有可用草稿不因候选被拒而失效。</summary>
    /// <param name="error">正式检查的失败。</param>
    private void ShowConstructionIssue(Exception error)
    {
        constructionIssue = ConstructionIssue.From(error);
        state.text = string.Format(T("Invalid"), error.Message);
        state.color = UiTheme.Current.Palette.Error;
        UiTooltip.Set(state.gameObject, T("LocateIssue"), state.text);
        locateIssueButton.gameObject.SetActive(constructionIssue != null &&
            (IssueTarget(constructionIssue.Path).HasValue || IssueTarget(constructionIssue.Related).HasValue));
    }

    /// <summary>单处直接跳转，两处在同一选择菜单中展开。</summary>
    private void LocateIssue()
    {
        var options = new List<UiOptionMenuOption>(2);
        Add(constructionIssue.Path, "IssueCurrent");
        Add(constructionIssue.Related, "IssueRelated");
        if (options.Count == 1) options[0].Select();
        else if (options.Count > 1) menu.Show(T("LocateIssue"), options.ToArray());

        void Add(string location, string label)
        {
            if (IssueTarget(location) is not { } target) return;
            options.Add(new UiOptionMenuOption { Label = string.Format(T(label), location), Select = () =>
            {
                page = target.Page;
                if (target.Part >= 0) part = target.Part;
                if (target.Scope == 0) { operationScope = 0; nativeChoice = target.Operation; }
                else if (target.Scope == 1) { operationScope = 1; stepChoice = target.Operation; }
                Refresh();
            }});
        }
    }

    /// <summary>从原有结构路径找到真正能编辑的位置，不拆解错误正文。</summary>
    /// <param name="location">编译器传递的声明路径。</param>
    private (int Page, int Part, int Scope, int Operation)? IssueTarget(string location)
    {
        if (location == null) return null;
        if (location.StartsWith("Steps/", StringComparison.Ordinal))
        {
            var steps = session.Draft.Steps;
            int index = -1, length = 0;
            for (int i = 0; i < steps.Length; i++)
            {
                string prefix = "Steps/" + steps[i].Id;
                if (prefix.Length > length && (location == prefix || location.StartsWith(prefix + "/", StringComparison.Ordinal)))
                { index = i; length = prefix.Length; }
            }
            if (index < 0) return null;
            ConstructionNode subject = constructionIssue.Declaration != null ? DeclarationNode(constructionIssue.Declaration) :
                StepNode(steps[index], new HashSet<ConstructionStep>());
            return (5, parts.IndexOf(subject), 1, index);
        }

        int operationAt = location.IndexOf("/Operations[", StringComparison.Ordinal);
        if (operationAt >= 0)
        {
            int start = operationAt + "/Operations[".Length, end = location.IndexOf(']', start);
            ConstructionNode node = DeclarationNode(constructionIssue.Declaration ?? location.Substring(0, operationAt));
            if (node == null || end < 0 || !int.TryParse(location.Substring(start, end - start), out int index) ||
                index < 0 || index >= node.Operations.Length) return null;
            return (5, parts.IndexOf(node), 0, index);
        }
        if (location == "root" || location.StartsWith("root/", StringComparison.Ordinal))
            return (0, parts.IndexOf(DeclarationNode(constructionIssue.Declaration ?? location)), -1, -1);
        if (location.StartsWith("Binding/", StringComparison.Ordinal)) return (3, -1, -1, -1);
        if (location.StartsWith("Body/", StringComparison.Ordinal)) return (2, -1, -1, -1);
        return null;
    }
}
