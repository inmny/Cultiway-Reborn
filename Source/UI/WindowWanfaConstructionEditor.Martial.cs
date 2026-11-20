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

        if (action.Part.Contact == BodyContactPart.Hand && action.WeaponForm == null && !action.Construction.HasControlRelations)
            MartialChoice("MartialHandShape", action.Part.IsFist ? "closed" : "open", new[] { "open", "closed" },
                value => T(value == "closed" ? "MartialClosedHand" : "MartialOpenHand"),
                value => ApplyConstructionEdit(() => BodyConstructionEdits.ChangeHandShape(session.Draft, value == "closed")));

        if (action.WeaponForm != null)
        {
            Row("MirrorBodySide", T("MartialWeaponSlot"));
            string[] forms = BodyConstructionEdits.WeaponFormChoices(session.Draft, action);
            if (forms.Length > 1)
                MartialChoice("MartialWeaponForm", action.WeaponForm, forms, FormLabel, value => ApplyConstructionEdit(() =>
                {
                    AbilityDefinition candidate = BodyConstructionEdits.ChangeWeaponForm(session.Draft, action, value);
                    access.Validate(candidate);
                    return candidate;
                }));
            MartialChoice("MartialGrip", action.Grip,
                FormResources.Load(action.WeaponForm).Description.Grips.Select(grip => grip.Id).ToArray(), GripLabel,
                value => ApplyConstructionEdit(() => BodyConstructionEdits.ChangeGrip(session.Draft, value)));
        }

        if (!action.Construction.HasTouchRelations) return;
        string[] shapes = MartialContactShapes(action);
        string[] markers = BodyMarkerChoices();
        var touches = session.Draft.Body.Construction?.Relations.Where(relation => relation.Kind == BodyRelationKind.Touch).ToArray() ??
                      new[] { new BodyRelationDefinition { Id = "touch" } };
        foreach (BodyRelationDefinition touch in touches)
        {
            if (touches.Length > 1) Row("MartialTouch", touch.Id);
            MartialChoice("MartialTouchShape", touch.FromAnchor, new[] { (string)null }.Concat(shapes).ToArray(),
                ShapeLabel, value => Edit(relation => relation.FromAnchor = value));
            MartialChoice("MartialTouchStart", touch.FromMarker, markers, BodyMarkerName,
                value => Edit(relation => relation.FromMarker = value));
            MartialChoice("MartialTouchEnd", touch.UntilMarker, markers, BodyMarkerName,
                value => Edit(relation => relation.UntilMarker = value));

            void Edit(Action<BodyRelationDefinition> edit) =>
                ApplyConstructionEdit(() => BodyConstructionEdits.ChangeTouch(session.Draft, touch.Id, edit));
        }

        string GripLabel(string grip) => grip switch
        {
            "grip" => T("MartialGripStandard"), "choke" => T("MartialGripChoke"), _ => grip
        };
    }

    /// <summary>普通接触和控制关系选择同一资源提供的真实表面。</summary>
    /// <param name="action">当前能编译的动作。</param>
    private static string[] MartialContactShapes(CompiledBodyAction action) => action.Part.Contact == BodyContactPart.Hand
        ? (action.Fist?.Knuckles.Select(contact => contact.Id) ??
           FormResources.Load(action.WeaponForm ?? "hand").Description.Contacts.Select(contact => contact.Id)).SelectMany(ShapeNames)
            .Distinct(StringComparer.Ordinal).ToArray()
        : new[] { action.Part.Shape };

    /// <summary>常见表面显示本地名称；自定义资源保留自身标识。</summary>
    private static string ShapeLabel(string id)
    {
        if (id == null) return T("MartialTouchAll");
        const string prefix = "Cultiway.Construction.MartialShape.";
        string exact = LMTools.GetOrFallback(prefix + id, null);
        if (exact != null) return exact;
        int split = id.IndexOf('/');
        string group = split < 0 ? id : id.Substring(0, split);
        return LMTools.GetOrFallback(prefix + group, group) +
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
