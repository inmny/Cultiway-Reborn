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
                         r.Kind is BodyRelationKind.Grab or BodyRelationKind.Brace or BodyRelationKind.Deflect or BodyRelationKind.Disarm))
            {
                string id = relation.Id;
                WanfaEditorRow row = rows.GetNext();
                row.Setup(T("BodyConnection"), T("ConnectionKind." + relation.Kind), T("RemoveConnection"), true,
                    () => EditBodyMotion(next =>
                    {
                        var parts = next.Construction;
                        parts.Relations = parts.Relations.Where(r => r.Id != id).ToArray();
                        if (!parts.Relations.Any(r => r.From == relation.To || r.To == relation.To))
                            parts.Parts = parts.Parts.Where(p => p.Id != relation.To).ToArray();
                    }), UiIcons.Delete);
                Choice("ConnectionKind", T("ConnectionKind." + relation.Kind), () => menu.Show(T("ConnectionKind"),
                    new[] { BodyRelationKind.Grab, BodyRelationKind.Brace, BodyRelationKind.Deflect, BodyRelationKind.Disarm }.Select(kind => new UiOptionMenuOption
                    {
                        Label = T("ConnectionKind." + kind), Selected = relation.Kind == kind,
                        Select = () => Edit((parts, rule) =>
                        {
                            rule.Kind = kind;
                            if (kind != BodyRelationKind.Grab) rule.MoveTarget = rule.RotateTarget = false;
                            if (kind is BodyRelationKind.Deflect or BodyRelationKind.Disarm)
                                BodyConstructionEdits.SetConnectionTarget(parts, rule, "item");
                        })
                    }).ToArray()));
                bool held = construction.Parts.Single(p => p.Id == relation.From).Subject == BodyPartSubject.Held;
                Row("ConnectionSource", held ? T("ConnectionPart.item") : T(previewPlan?.Body?.Part.Label ?? "Effector"));
                BodyPartInput target = construction.Parts.Single(p => p.Id == relation.To);
                MartialChoice("ConnectionTarget", target.Part,
                    connectionParts.Where(part => relation.Kind is not (BodyRelationKind.Deflect or BodyRelationKind.Disarm) || part == "item").ToArray(),
                    ConnectionPartName, SetTarget);
                ConnectionText("ConnectionCustomPart", target.Part, SetTarget, draftChange: false);
                if (previewPlan?.Body is { } action)
                    MartialChoice("ConnectionSurface", relation.FromAnchor, new[] { (string)null }.Concat(MartialContactShapes(action)).ToArray(),
                        ShapeLabel, value => Edit((_, rule) => rule.FromAnchor = value));
                ConnectionText("ConnectionContact", relation.FromAnchor ?? "", value =>
                    Edit((_, rule) => rule.FromAnchor = string.IsNullOrWhiteSpace(value) ? null : value), draftChange: false);
                string[] markers = BodyMarkerChoices();
                MartialChoice("ConnectionStart", relation.FromMarker, markers, BodyMarkerName, value => Edit((_, rule) => rule.FromMarker = value));
                MartialChoice(relation.Kind == BodyRelationKind.Disarm ? "DisarmUntil" : "ConnectionUntil", relation.UntilMarker,
                    markers, BodyMarkerName, value => Edit((_, rule) => rule.UntilMarker = value));
                RelationNumber(relation.Kind == BodyRelationKind.Disarm ? "DisarmStrength" : "ConnectionStrength", relation.ControlStrength,
                    (rule, value) => rule.ControlStrength = value);
                if (relation.Kind == BodyRelationKind.Grab)
                    BodyMotionToggle("ConnectionMove", relation.MoveTarget, value => Edit((_, rule) =>
                    {
                        rule.MoveTarget = value;
                        if (!value) rule.RotateTarget = false;
                    }));
                if (relation.MoveTarget)
                {
                    BodyMotionToggle("ConnectionRotate", relation.RotateTarget, value => Edit((_, rule) => rule.RotateTarget = value));
                    RelationNumber("ConnectionCollision", relation.CollisionDamageScale, (rule, value) => rule.CollisionDamageScale = value);
                }

                void Edit(Action<BodyConstructionDefinition, BodyRelationDefinition> edit) => EditBodyMotion(next =>
                    edit(next.Construction, next.Construction.Relations.Single(r => r.Id == id)));

                void SetTarget(string value) => Edit((parts, rule) => BodyConstructionEdits.SetConnectionTarget(parts, rule, value));

                void RelationNumber(string key, float value, Action<BodyRelationDefinition, float> set) =>
                    BodyMotionNumber(key, value, (next, number) => set(next.Construction.Relations.Single(r => r.Id == id), number));
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
                access.Validate(next);
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
    /// <param name="draftChange">是否由此控件提交草稿；完整候选命令自行建立撤销记录。</param>
    private void ConnectionText(string key, string value, Action<string> set, bool draftChange = true)
    {
        var field = UiElements.CreateInput(Row(key).UseInlineControls(260f), key, value, T(key), 260f, 22f);
        field.onEndEdit.AddListener(text =>
        {
            if (refreshing) return;
            if (draftChange) Change(() => set(text.Trim()));
            else set(text.Trim());
        });
    }

}
