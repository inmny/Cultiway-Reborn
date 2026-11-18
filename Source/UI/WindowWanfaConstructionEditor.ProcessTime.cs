using System;
using System.Linq;
using Cultiway.Core.Skills;

namespace Cultiway.UI;

/// <summary>实际侵蚀与运行中改速共用时间窗编辑；候选通过前不改草稿。</summary>
public sealed partial class WindowWanfaConstructionEditor
{
    private int timeWindowChoice;

    private void RetimeParameters(ConstructionNode node, ConstructionOperation operation)
    {
        Row("RetimeErosionRules", T("RetimeErosionHint"));
        WorldMatterParameters(operation.WorldMatter, true);
        ProcessTimeRows(node, operation);
    }

    private void ProcessTimeRows(ConstructionNode node, ConstructionOperation operation)
    {
        var windows = Windows(operation);
        bool local = operation.Kind == ConstructionOperationKind.Erode && operation.WorldMatter == null;
        float limit = local ? session.Draft.Lifetime : 1000000000f;
        Row("ProcessTimeRules", T(operation.Kind == ConstructionOperationKind.RetimeErosion ? "RetimeWindowsHint" : "ProcessTimeHint"));
        if (windows.Length < ConstructionProcessWindow.Maximum && NewWindow(windows, limit) is { } window)
            ActionRow("AddProcessWindow", T("AddProcessWindowHint"), () => Edit(list => list.Append(window).ToArray(), window));
        if (windows.Length == 0) return;

        timeWindowChoice = Math.Min(timeWindowChoice, windows.Length - 1);
        int index = timeWindowChoice;
        var selected = windows[index];
        Choice("ProcessWindow", Label(selected, index), () => menu.Show(T("ProcessWindow"),
            windows.Select((item, i) => new UiOptionMenuOption
            {
                Label = Label(item, i), Selected = i == index,
                Select = () => { timeWindowChoice = i; Refresh(); }
            }).ToArray()));
        NumberWindow("ProcessWindowAt", selected.At, (item, value) => item.At = value);
        NumberWindow("ProcessWindowSeconds", selected.Seconds, (item, value) => item.Seconds = value);
        NumberWindow("ProcessWindowRate", selected.Rate, (item, value) => item.Rate = value);
        ActionRow("RemoveProcessWindow", T("RemoveProcessWindowHint"), () =>
            Edit(list => list.Where((_, i) => i != index).ToArray()));

        string Label(ConstructionProcessWindow item, int i) => string.Format(T("ProcessWindowLabel"), i + 1, item.At, item.Seconds, item.Rate);

        void NumberWindow(string key, float value, Action<ConstructionProcessWindow, float> set) =>
            FloatInput(Row(key).UseInlineControls(104f), key, value, next => Edit(list =>
            {
                set(list[index], next);
                return list;
            }, touchedIndex: index), true, 100f, draftChange: false);

        void Edit(Func<ConstructionProcessWindow[], ConstructionProcessWindow[]> edit, ConstructionProcessWindow touched = null, int touchedIndex = -1) =>
            ApplyConstructionEdit(() =>
            {
                var candidate = ConstructionEditing.Copy(session.Draft);
                int step = Array.FindIndex(session.Draft.Steps, item => item.Operation == operation);
                var target = step >= 0 ? candidate.Steps[step].Operation :
                    ConstructionContactEdits.Parts(candidate.Root).Single(item => item.Path == NodePath(node)).Node.Operations[Array.IndexOf(node.Operations, operation)];
                var list = Windows(target);
                var focus = touched ?? (touchedIndex >= 0 ? list[touchedIndex] : null);
                list = edit(list).OrderBy(item => item.At).ToArray();
                if (target.Kind == ConstructionOperationKind.RetimeErosion) target.TimeWindows = list;
                else
                {
                    var erosion = target.Kind == ConstructionOperationKind.Leave ? target.Residue.Erosion : target.Erosion ??= new ConstructionErosion();
                    erosion.TimeWindows = list.Length == 0 ? null : list;
                }
                access.Validate(candidate);
                if (focus != null) timeWindowChoice = Array.IndexOf(list, focus);
                return candidate;
            });
    }

    private static ConstructionProcessWindow[] Windows(ConstructionOperation operation) => (operation.Kind switch
    {
        ConstructionOperationKind.RetimeErosion => operation.TimeWindows,
        ConstructionOperationKind.Leave => operation.Residue.Erosion.TimeWindows,
        _ => operation.Erosion?.TimeWindows
    }) ?? Array.Empty<ConstructionProcessWindow>();

    private static ConstructionProcessWindow NewWindow(ConstructionProcessWindow[] windows, float limit)
    {
        float cursor = 0f;
        foreach (var window in windows)
        {
            if (window.At - cursor >= .05f) return At(cursor, window.At);
            cursor = Math.Max(cursor, window.At + window.Seconds);
        }
        return limit > cursor ? At(cursor, limit) : null;

        static ConstructionProcessWindow At(float start, float end) => new()
            { At = start, Seconds = Math.Min(.5f, end - start), Rate = 0f };
    }
}
