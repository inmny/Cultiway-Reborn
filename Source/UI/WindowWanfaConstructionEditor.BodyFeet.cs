using System;
using System.Linq;
using Cultiway.Core.Skills;
using Cultiway.Utils.Extension;
using Cultiway.UI.Prefab;
using UnityEngine;

namespace Cultiway.UI;

/// <summary>实际部位和各自命名足的制作表单，继续使用共同草稿和撤销。</summary>
public sealed partial class WindowWanfaConstructionEditor
{
    /// <summary>当前编辑的真实足名，切换显示不改变动作资料。</summary>
    private string bodyFoot;

    /// <summary>明确区分原骨段与器官；未知实际部件保留稳定名称。</summary>
    /// <param name="part">完整实际部位。</param>
    private static string ActualPartName(string part)
    {
        if (part?.StartsWith("part:", StringComparison.Ordinal) == true)
            return LMTools.GetOrFallback("Cultiway.Construction.ActualPart." + part.Substring(5), T("Joint") + " / " + part.Substring(5));
        if (part?.StartsWith("organ:", StringComparison.Ordinal) != true || part.Length <= 6) return part ?? T("UnsupportedEffector");
        var selection = new BodyPartSelection(part);
        return LMTools.GetOrFallback("Cultiway.CreatureOrgan." + selection.OrganId, T("NaturalOrgan")) +
            (selection.OrganPart == null ? "" : " / " + selection.OrganPart);
    }

    /// <summary>从当前学习者已有的真实部位中改用来源，不凭输入名称造身体。</summary>
    /// <param name="effector">当前编译后的真实部位。</param>
    private void ActualPartRows(string effector)
    {
        string[] choices = access.PartChoices(HasLearner ? learner.GetExtend() : null).Effectors;
        if (choices.Length == 0) { Row("ActualPart", T(HasLearner ? "EmptyActualParts" : "PreviewNeedsLearner")); return; }
        Choice("ActualPart", ActualPartName(effector), () => menu.Show(T("ActualPart"), choices.Select(part => new UiOptionMenuOption
        {
            Label = ActualPartName(part), SearchText = ActualPartName(part) + " " + part, Selected = part == effector,
            Select = () => ApplyConstructionEdit(() => BodyConstructionEdits.RetargetPart(session.Draft, part))
        }).ToArray(), true));
    }

    /// <summary>整段过程使用同一实际足集合，新增足各自从自然落点开始。</summary>
    /// <param name="body">当前独立草稿。</param>
    private void NamedFootRows(BodyActionDefinition body)
    {
        string[] actual = access.PartChoices(HasLearner ? learner.GetExtend() : null).Feet;
        if (actual.Length > 0)
            ActionRow("UseActualFeet", T("UseActualFeetHint"), () => ApplyConstructionEdit(() => BodyConstructionEdits.NamedFeet(session.Draft, actual)));
        else Row("UseActualFeet", T(HasLearner ? "EmptyActualFeet" : "PreviewNeedsLearner"));
        string[] names = body.Keys.SelectMany(key => key.Support?.Parts?.Keys ?? Enumerable.Empty<string>()).Distinct(StringComparer.Ordinal).OrderBy(part => part, StringComparer.Ordinal).ToArray();
        if (names.Length == 0) { bodyFoot = null; return; }
        if (!names.Contains(bodyFoot)) bodyFoot = names[0];
        Choice("NamedFoot", ActualPartName(bodyFoot), () => menu.Show(T("NamedFoot"), names.Select(part => new UiOptionMenuOption
        {
            Label = ActualPartName(part), Selected = part == bodyFoot, Select = () => { bodyFoot = part; Refresh(); }
        }).ToArray(), true));
    }

    /// <summary>身体平移与足部过程共用世界秒数，路径仍为原来的有限曲线。</summary>
    /// <param name="body">当前身体草稿。</param>
    private void BodyFootworkRows(BodyActionDefinition body)
    {
        if (!body.Keys.All(key => key.Support?.Parts != null))
        {
            Row("BodyFootwork", T("UseActualFeetHint"));
            return;
        }
        Toggle("BodyFootworkEnabled", body.Construction?.Footwork != null, value =>
        {
            AbilityDefinition next = BodyConstructionEdits.ExplicitRelations(session.Draft);
            next.Body.Construction.Footwork = value ? new ConstructionPath
            {
                Forward = null, Keys = new[] { new ConstructionPathKey(), new ConstructionPathKey { At = body.Keys.Last().At } }
            } : null;
            session.Draft = next;
        });
        ConstructionPath path = body.Construction?.Footwork;
        if (path == null) return;
        Row("BodyFootwork", T("BodyFootworkHint"));
        Choice("Handles", T(new[] { "Point", "InHandle", "OutHandle" }[handle]), () => menu.Show(T("Handles"),
            Enumerable.Range(0, 3).Select(index => new UiOptionMenuOption
            { Label = T(new[] { "Point", "InHandle", "OutHandle" }[index]), Selected = handle == index, Select = () => { handle = index; Refresh(); } }).ToArray()));
        for (int i = 0; i < path.Keys.Length; i++)
        {
            int index = i;
            ConstructionPathKey key = path.Keys[i];
            WanfaEditorRow row = rows.GetNext();
            row.Setup(string.Format(T("Key"), i + 1), "", T("RemovePoint"), true,
                i > 0 && i < path.Keys.Length - 1 ? () => Change(() => path.Keys = path.Keys.Where((_, at) => at != index).ToArray()) : null, UiIcons.Delete);
            Transform controls = row.UseInlineControls(260f);
            FloatInput(controls, "At", key.At, value => key.At = value, i > 0, 48f);
            float[] vector = handle == 0 ? key.Point : handle == 1 ? key.In : key.Out;
            for (int axis = 0; axis < 2; axis++)
            {
                int coordinate = axis;
                FloatInput(controls, new[] { "FootworkForward", "FootworkSide" }[axis], vector[axis], value => vector[coordinate] = value, handle != 0 || i != 0, 56f);
            }
        }
        if (path.Keys.Length < 65)
            ActionRow("InsertFootworkPoint", T("InsertFootworkHint"), () => ApplyConstructionEdit(() => BodyConstructionEdits.InsertFootworkPoint(session.Draft)));
    }
}
