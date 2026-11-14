using System;
using System.IO;
using System.Linq;
using Cultiway.Core.Skills;
using UnityEngine;

namespace Cultiway.UI;

/// <summary>从少量参数展开普通路径，展开后继续使用原控制点表单、撤销和正式保存。</summary>
public sealed partial class WindowWanfaConstructionEditor
{
    /// <summary>只描述本次曲线命令，不写入法术资料或运行分派。</summary>
    private enum CurveBuilderKind
    {
        /// <summary>沿一个局部方向往复。</summary>
        Wave,
        /// <summary>沿明确折点往复。</summary>
        Zigzag,
        /// <summary>两个局部方向共同盘旋。</summary>
        Spiral,
        /// <summary>沿实际目标方向抬升再落下。</summary>
        Arc
    }
    /// <summary>本次展开命令的选择。</summary>
    private CurveBuilderKind curveKind;
    /// <summary>本次命令是否展开。</summary>
    private bool curveBuilderOpen;
    /// <summary>命令参数，点击展开前不改变已有路径。</summary>
    private float curvePeriod = .8f, curveAmplitude = .6f, curveSeconds = 2f, curveDecay;

    /// <summary>只为已有飞行提供可复用曲线命令，不改动施放、作用、材料或条件。</summary>
    /// <param name="flight">所选部位的实际飞行。</param>
    private void CurveBuilderRows(ConstructionOperation flight)
    {
        ActionRow("CurveBuilder", T("CurveBuilderHint"), () => { curveBuilderOpen = !curveBuilderOpen; Refresh(); });
        if (!curveBuilderOpen) return;
        Choice("CurveKind", T("CurveKind." + curveKind), () => menu.Show(T("CurveKind"),
            Enum.GetValues(typeof(CurveBuilderKind)).Cast<CurveBuilderKind>().Select(kind => new UiOptionMenuOption
            {
                Label = T("CurveKind." + kind), Selected = curveKind == kind,
                Select = () => { curveKind = kind; Refresh(); }
            }).ToArray()));
        Parameter("CurveAmplitude", curveAmplitude, value => curveAmplitude = value);
        if (curveKind != CurveBuilderKind.Arc) Parameter("CurvePeriod", curvePeriod, value => curvePeriod = value);
        if (curveKind == CurveBuilderKind.Spiral)
            Parameter("CurveDecay", curveDecay, value => { curveDecay = value; Refresh(); });
        if (curveKind == CurveBuilderKind.Arc || curveKind == CurveBuilderKind.Spiral && curveDecay > 0f)
            Parameter("CurveSeconds", curveSeconds, value => curveSeconds = value);
        ActionRow("BuildCurve", T("BuildCurveHint"), () => BuildCurve(flight));
        if (flight.Path != null)
            ActionRow("RemoveCurve", T("RemoveCurveHint"), () => Change(() => flight.Path = null));

        /// <summary>命令参数只进入本次输入，应用时才改变法术。</summary>
        /// <param name="key">显示名称。</param><param name="value">当前输入。</param><param name="set">保存命令输入。</param>
        void Parameter(string key, float value, Action<float> set)
            => FloatInput(Row(key).UseInlineControls(104f), key, value, set, true, 100f, draftChange: false);
    }

    /// <summary>先完整展开并核对参数，成功后才替换草稿，失败不留下半条路径或撤销记录。</summary>
    /// <param name="flight">现有飞行。</param>
    private void BuildCurve(ConstructionOperation flight)
    {
        ConstructionPath path;
        try
        {
            path = curveKind switch
            {
                CurveBuilderKind.Wave => ConstructionPathCurves.Wave(curvePeriod, Vector3.right * curveAmplitude),
                CurveBuilderKind.Zigzag => ConstructionPathCurves.Zigzag(curvePeriod, Vector3.right * curveAmplitude),
                CurveBuilderKind.Spiral => ConstructionPathCurves.Spiral(curveSeconds, curvePeriod,
                    Vector3.right * curveAmplitude, Vector3.up * curveAmplitude, curveDecay),
                _ => ConstructionPathCurves.Arc(curveSeconds, curveAmplitude)
            };
            _ = new CompiledConstructionPath(path, "新曲线");
        }
        catch (InvalidDataException error) { state.text = error.Message; return; }
        Change(() =>
        {
            flight.Path = path;
            if (curveKind == CurveBuilderKind.Arc)
            {
                flight.Vector = new float[3]; flight.Acceleration = null; flight.InheritVelocity = false;
            }
        });
    }
}
