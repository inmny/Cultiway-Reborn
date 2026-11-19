using System;
using System.IO;
using Cultiway.Core.Skills;
using UnityEngine;

namespace Cultiway.UI;

/// <summary>收藏与分享的两处窗口使用同一实际原因和提示。</summary>
internal static class ConstructionShareNotice
{
    internal static string Text(string key) => LocalizedTextManager.getText("Cultiway.Construction." + key);

    internal static void Show(string key) => WorldTip.showNow(Text(key), false, "top", 3f);

    /// <summary>向系统剪贴板复制已确认的构造，不读取人物余额和世界状态。</summary>
    internal static void Copy(AbilityDefinition definition)
    {
        GUIUtility.systemCopyBuffer = ConstructionShare.Write(definition);
        Show("ShareCopied");
    }

    /// <summary>仅捕获资料拒绝及本机文件错误，实际程序异常照常可见。</summary>
    internal static void Run(Action action)
    {
        try { action(); }
        catch (ConstructionShareException error) { Show(error.Reason); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
        {
            ConstructionIssue issue = ConstructionIssue.From(error);
            string message = issue?.ResourceKind == null ? string.Format(Text("Invalid"), error.Message) :
                string.Format(Text("IssueResource"), Text("IssueResource." + issue.ResourceKind), issue.ResourceId, issue.Path);
            WorldTip.showNow(message, false, "top", 5f);
        }
    }
}
