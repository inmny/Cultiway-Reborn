using System;
using System.Linq;
using Cultiway.Core.Skills;
using Cultiway.UI.Prefab;

namespace Cultiway.UI;

public sealed partial class WindowWanfaConstructionEditor
{
    /// <summary>共用动作的固定时点；名称由编译器保留。</summary>
    private static readonly string[] builtInBodyMarkers = { "start", "active", "recover", "release", "end" };

    /// <summary>当前可用于关系的固定时点及自定义姿态标记。</summary>
    private string[] BodyMarkerChoices() => builtInBodyMarkers.Concat(session.Draft.Body.Keys.Where(k => k.Id != null).Select(k => k.Id)).ToArray();

    /// <summary>本地化固定时点，自定义标记保留作者原名。</summary>
    /// <param name="marker">动作内的稳定标记。</param>
    private static string BodyMarkerName(string marker) => builtInBodyMarkers.Contains(marker) ? T("ConnectionMarker." + marker) : marker;

    /// <summary>对每处已有姿态点编辑标记，不另建计时。</summary>
    /// <param name="body">正在编辑的身体。</param>
    private void PoseMarkerRows(BodyActionDefinition body)
    {
        Row("BodyPoseMarkers", T("BodyPoseMarkersHint"));
        for (int i = 0; i < body.Keys.Length; i++)
        {
            int index = i;
            BodyMotionKey key = body.Keys[i];
            var row = rows.GetNext();
            row.Setup(string.Format(T("BodyKey"), i + 1), key.At.ToString("0.###"), "", true, null);
            var input = UiElements.CreateInput(row.UseInlineControls(176f), "BodyPoseMarker", key.Id ?? "", T("BodyPoseMarker"), 172f, 22f);
            input.onEndEdit.AddListener(text =>
            {
                string name = text.Trim();
                if (!refreshing && name != (key.Id ?? "")) EditBodyMotion(next => BodyConstructionEdits.RenamePose(next, index, name));
            });
        }
    }
}
