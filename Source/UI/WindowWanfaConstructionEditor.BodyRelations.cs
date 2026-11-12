using System;
using System.Linq;
using Cultiway.Core.Skills;
using Cultiway.UI.Prefab;

namespace Cultiway.UI;

/// <summary>编辑一招内的双方关系，继续使用同一草稿、编译、撤销和保存。</summary>
public sealed partial class WindowWanfaConstructionEditor
{
    /// <summary>共用身体和装备名称；其他实际模型部件仍可明确填写。</summary>
    private static readonly string[] connectionParts =
    {
        "surface", "torso", "head", "hand.left", "hand.right", "foot.left", "foot.right",
        "arm.left.upper", "arm.left.lower", "arm.right.upper", "arm.right.lower",
        "leg.left.upper", "leg.left.lower", "leg.right.upper", "leg.right.lower",
        "armor.helmet", "armor.chest", "armor.boots", "item", "tail", "wing.left", "wing.right"
    };

    /// <summary>编辑抓持、抵住及拨压，普通命中和握持沿用原声明。</summary>
    private void BodyRelations()
    {
        BodyConstructionDefinition construction = session.Draft.Body.Construction;
        if (construction != null)
            foreach (BodyRelationDefinition relation in construction.Relations.Where(r =>
                         r.Kind is BodyRelationKind.Grab or BodyRelationKind.Brace or BodyRelationKind.Deflect))
            {
                WanfaEditorRow row = rows.GetNext();
                row.Setup(T("BodyConnection"), T("ConnectionKind." + relation.Kind), T("RemoveConnection"), true,
                    () => Change(() =>
                    {
                        construction.Relations = construction.Relations.Where(r => r != relation).ToArray();
                        construction.Parts = construction.Parts.Where(p => p.Id != relation.To ||
                                                                           construction.Relations.Any(r =>
                                                                               r.From == p.Id || r.To == p.Id))
                            .ToArray();
                    }),
                    UiIcons.Delete);
                Choice("ConnectionKind", T("ConnectionKind." + relation.Kind), () => menu.Show(T("ConnectionKind"),
                    new[] { BodyRelationKind.Grab, BodyRelationKind.Brace, BodyRelationKind.Deflect }.Select(kind => new UiOptionMenuOption
                    {
                        Label = T("ConnectionKind." + kind), Selected = relation.Kind == kind,
                        Select = () => Change(() =>
                        {
                            relation.Kind = kind;
                            if (kind != BodyRelationKind.Grab) relation.MoveTarget = relation.RotateTarget = false;
                            if (kind == BodyRelationKind.Deflect)
                                construction.Parts.Single(p => p.Id == relation.To).Part = "item";
                        })
                    }).ToArray()));
                bool held = construction.Parts.Single(p => p.Id == relation.From).Subject == BodyPartSubject.Held;
                Choice("ConnectionSource",
                    held ? T("ConnectionPart.item") : T(previewPlan?.Body?.Part.Label ?? "Effector"), () =>
                        menu.Show(T("ConnectionSource"),
                            construction.Parts.Where(p => session.Draft.Body.WeaponForm != null
                                    ? p.Subject == BodyPartSubject.Held
                                    : p.Subject == BodyPartSubject.Source && p.Id == session.Draft.Body.Effector)
                                .Select(p => new UiOptionMenuOption
                                {
                                    Label = p.Subject == BodyPartSubject.Held
                                        ? T("ConnectionPart.item")
                                        : T(previewPlan?.Body?.Part.Label ?? "Effector"),
                                    Selected = relation.From == p.Id,
                                    Select = () => Change(() =>
                                    {
                                        relation.From = p.Id;
                                        relation.FromAnchor = null;
                                    })
                                }).ToArray()));
                BodyPartInput target = construction.Parts.Single(p => p.Id == relation.To);
                Choice("ConnectionTarget", ConnectionPartName(target.Part), () => menu.Show(T("ConnectionTarget"),
                    connectionParts.Where(part => relation.Kind != BodyRelationKind.Deflect || part == "item").Select(part => new UiOptionMenuOption
                    {
                        Label = ConnectionPartName(part), Selected = target.Part == part,
                        Select = () => Change(() => target.Part = part)
                    }).ToArray(), true));
                ConnectionText("ConnectionCustomPart", target.Part, value => target.Part = value);
                ConnectionText("ConnectionContact", relation.FromAnchor ?? "", value =>
                    relation.FromAnchor = string.IsNullOrWhiteSpace(value) ? null : value);
                ConnectionMarker("ConnectionStart", relation.FromMarker, value => relation.FromMarker = value);
                ConnectionMarker("ConnectionUntil", relation.UntilMarker, value => relation.UntilMarker = value);
                Number("ConnectionStrength", relation.ControlStrength, value => relation.ControlStrength = value);
                if (relation.Kind == BodyRelationKind.Grab)
                    Toggle("ConnectionMove", relation.MoveTarget, value =>
                    {
                        relation.MoveTarget = value;
                        if (!value) relation.RotateTarget = false;
                    });
                if (relation.MoveTarget)
                {
                    Toggle("ConnectionRotate", relation.RotateTarget, value => relation.RotateTarget = value);
                    Number("ConnectionCollision", relation.CollisionDamageScale, value => relation.CollisionDamageScale = value);
                }
            }

        if ((construction?.Relations.Length ?? 0) < 32 && (construction?.Parts.Length ?? 0) < 32)
            ActionRow("AddConnection", T("ConnectionHint"), () => ApplyConstructionEdit(() =>
            {
                AbilityDefinition next = BodyConstructionEdits.ExplicitRelations(session.Draft);
                BodyConstructionDefinition parts = next.Body.Construction;
                string id = "connection";
                for (int i = 1; parts.Parts.Any(p => p.Id == id) || parts.Relations.Any(r => r.Id == id); i++)
                    id = "connection" + i;
                parts.Parts = parts.Parts.Concat(new[]
                {
                    new BodyPartInput { Id = id, Subject = BodyPartSubject.Target, Part = "surface" }
                }).ToArray();
                parts.Relations = parts.Relations.Concat(new[]
                {
                    new BodyRelationDefinition
                    {
                        Id = id, Kind = BodyRelationKind.Grab,
                        From = parts.Parts.FirstOrDefault(p => p.Subject == BodyPartSubject.Held)?.Id ??
                               next.Body.Effector,
                        To = id
                    }
                }).ToArray();
                return next;
            }));
    }

    /// <summary>共用名称翻译，实际模型部件保留原名称，避免改写选择语义。</summary>
    /// <param name="part">明确的目标部位。</param>
    private static string ConnectionPartName(string part) => connectionParts.Contains(part)
        ? T("ConnectionPart." + part)
        : part;

    /// <summary>接触面与原模型部件为可拓展名称；编辑时仍由正式编译和施放核对。</summary>
    /// <param name="key">字段名称。</param><param name="value">当前名称。</param>
    /// <param name="set">写入独立草稿。</param>
    private void ConnectionText(string key, string value, Action<string> set)
    {
        var field = UiElements.CreateInput(Row(key).UseInlineControls(260f), key, value, T(key), 260f, 22f);
        field.onEndEdit.AddListener(text =>
        {
            if (!refreshing) Change(() => set(text.Trim()));
        });
    }

    /// <summary>阶段来自同一动作已有标记，不另建一套计时。</summary>
    /// <param name="key">字段名称。</param><param name="value">当前标记。</param>
    /// <param name="set">写入关系。</param>
    private void ConnectionMarker(string key, string value, Action<string> set)
    {
        string[] builtIn = { "start", "active", "recover", "release", "end" };
        Choice(key, builtIn.Contains(value) ? T("ConnectionMarker." + value) : value, () => menu.Show(T(key),
            builtIn.Concat(session.Draft.Body.Keys.Where(k => k.Id != null).Select(k => k.Id))
                .Select(marker => new UiOptionMenuOption
                {
                    Label = builtIn.Contains(marker) ? T("ConnectionMarker." + marker) : marker,
                    Selected = value == marker, Select = () => Change(() => set(marker))
                }).ToArray(), true));
    }
}
