using System;
using System.Linq;
using Cultiway.Core.Skills;

namespace Cultiway.UI;

/// <summary>在同一运动页分别编辑各条叠加曲线，共用实际参照和进度。</summary>
public sealed partial class WindowWanfaConstructionEditor
{
    /// <summary>零表示主曲线，其余对应附加曲线；重新打开窗口回到主曲线。</summary>
    private int curveLayer;

    /// <summary>选择、增删有限附加曲线，返回当前编辑的叶曲线，空值表示主曲线。</summary>
    /// <param name="path">本次完整运动路径。</param>
    private ConstructionPathTerm CurveLayerRows(ConstructionPath path)
    {
        ConstructionPathTerm[] additions = path.Additions ?? Array.Empty<ConstructionPathTerm>();
        curveLayer = Math.Min(curveLayer, additions.Length);
        if (additions.Length > 0)
        {
            string Label(int index) => index == 0 ? T("CurveLayer.Base") : string.Format(T("CurveLayer.Addition"), index);
            Choice("CurveLayer", Label(curveLayer), () => menu.Show(T("CurveLayer"),
                Enumerable.Range(0, additions.Length + 1).Select(index => new UiOptionMenuOption
                {
                    Label = Label(index), Selected = curveLayer == index,
                    Select = () => { curveLayer = index; Refresh(); }
                }).ToArray()));
            Row("CurveLayerRules", T("CurveLayerHint"));
        }
        int segments = path.Keys.Length - 1 + additions.Sum(item => item.Keys.Length - 1);
        if (additions.Length < 7 && segments < 64)
            ActionRow("AddCurveLayer", T("CurveLayerHint"), () => Change(() =>
            {
                path.Additions = additions.Concat(new[] { new ConstructionPathTerm
                {
                    Keys = new[] { new ConstructionPathKey(), new ConstructionPathKey { At = .5f } }
                } }).ToArray();
                curveLayer = path.Additions.Length;
            }));
        if (curveLayer == 0) return null;
        int selected = curveLayer - 1;
        ActionRow("RemoveCurveLayer", T("CurveLayerHint"), () => Change(() =>
        {
            var remaining = additions.Where((_, index) => index != selected).ToArray();
            path.Additions = remaining.Length == 0 ? null : remaining; curveLayer = 0;
        }));
        return additions[selected];
    }
}
