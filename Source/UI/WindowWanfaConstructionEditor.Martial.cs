using System;
using System.Linq;
using Cultiway.Core.Skills;
using Cultiway.Core.Visuals.Forms;
using Cultiway.Utils.Extension;

namespace Cultiway.UI;

public sealed partial class WindowWanfaConstructionEditor
{
    /// <summary>编辑已经提供的身体、握持和普通接触，不添加虚构的装备或接触点。</summary>
    private void MartialBindingRows(CompiledBodyAction action)
    {
        if (action == null) return;
        string opposite = BodyConstructionEdits.OppositeSide(action.Effector);
        if (opposite != null && action.WeaponForm == null)
            ActionRow("MirrorBodySide", T(new BodyPartSelection(opposite).Label),
                () => ApplyConstructionEdit(() => BodyConstructionEdits.MirrorSide(session.Draft, action)));

        if (action.WeaponForm != null)
        {
            Row("MirrorBodySide", T("MartialWeaponSlot"));
            MartialChoice("MartialGrip", action.Grip,
                FormResources.Load(action.WeaponForm).Description.Grips.Select(grip => grip.Id).ToArray(), GripLabel,
                value => ApplyConstructionEdit(() => BodyConstructionEdits.ChangeGrip(session.Draft, value)));
        }

        if (!action.Construction.HasTouchRelations) return;
        if (action.Construction.HasControlRelations)
        {
            Row("MartialTouch", T("MartialTouchShared"));
            return;
        }

        string[] shapes = action.Part.Contact == BodyContactPart.Hand
            ? FormResources.Load(action.WeaponForm ?? "hand").Description.Contacts.SelectMany(contact => ShapeNames(contact.Id))
                .Distinct(StringComparer.Ordinal).ToArray()
            : new[] { action.Part.Shape };
        string[] builtIn = { "start", "active", "release", "recover", "end" };
        string[] markers = builtIn.Concat(session.Draft.Body.Keys.Select(key => key.Id).Where(id => id != null)).ToArray();
        var touches = session.Draft.Body.Construction?.Relations.Where(relation => relation.Kind == BodyRelationKind.Touch).ToArray() ??
                      new[] { new BodyRelationDefinition { Id = "touch" } };
        foreach (BodyRelationDefinition touch in touches)
        {
            if (touches.Length > 1) Row("MartialTouch", touch.Id);
            MartialChoice("MartialTouchShape", touch.FromAnchor, new[] { (string)null }.Concat(shapes).ToArray(),
                ShapeLabel, value => Edit(relation => relation.FromAnchor = value));
            MartialChoice("MartialTouchStart", touch.FromMarker, markers, MarkerLabel,
                value => Edit(relation => relation.FromMarker = value));
            MartialChoice("MartialTouchEnd", touch.UntilMarker, markers, MarkerLabel,
                value => Edit(relation => relation.UntilMarker = value));

            void Edit(Action<BodyRelationDefinition> edit) =>
                ApplyConstructionEdit(() => BodyConstructionEdits.ChangeTouch(session.Draft, touch.Id, edit));
        }

        string MarkerLabel(string marker) => builtIn.Contains(marker) ? T("ConnectionMarker." + marker) : marker;
        string GripLabel(string grip) => grip switch
        {
            "grip" => T("MartialGripStandard"), "choke" => T("MartialGripChoke"), _ => grip
        };
    }

    /// <summary>常见表面显示本地名称；自定义资源保留自身标识。</summary>
    private static string ShapeLabel(string id)
    {
        if (id == null) return T("MartialTouchAll");
        int split = id.IndexOf('/');
        string group = split < 0 ? id : id.Substring(0, split);
        return LMTools.GetOrFallback("Cultiway.Construction.MartialShape." + group, group) +
               (split < 0 ? "" : " / " + id.Substring(split + 1));
    }

    /// <summary>同时提供整个实际刃面和其可单独使用的子形状。</summary>
    private static System.Collections.Generic.IEnumerable<string> ShapeNames(string id)
    {
        for (int at = id.IndexOf('/'); at >= 0; at = id.IndexOf('/', at + 1)) yield return id.Substring(0, at);
        yield return id;
    }

    /// <summary>选项只写入候选资料，当前选择不重复记入撤销历史。</summary>
    private void MartialChoice(string key, string current, string[] values, Func<string, string> label, Action<string> select) =>
        Choice(key, label(current), () => menu.Show(T(key), values.Select(value => new UiOptionMenuOption
        {
            Label = label(value), Selected = current == value,
            Select = () => { if (current != value) select(value); }
        }).ToArray(), values.Length > 8));
}
