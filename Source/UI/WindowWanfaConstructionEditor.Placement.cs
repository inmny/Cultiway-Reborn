using System;
using System.IO;
using System.Linq;
using Cultiway.Core.Skills;
using Cultiway.UI.Prefab;

namespace Cultiway.UI;

/// <summary>编辑真实连接及局部运动，沿用普通草稿、撤销和正式保存。</summary>
public sealed partial class WindowWanfaConstructionEditor
{
    /// <summary>显示整次法术的连接和所选部位的位置、旋转速度。</summary>
    private void PlacementPage()
    {
        if (parts.Count == 0) { Row("BodyOnly", T("BodyHint")); return; }
        ConstructionBinding binding = session.Draft.Binding;
        AbilityAnchor anchor = binding?.Anchor ?? AbilityAnchor.Target;
        Choice("Anchor", T("Anchor." + anchor), () => menu.Show(T("Anchor"),
            new[] { AbilityAnchor.Source, AbilityAnchor.Target, AbilityAnchor.Hand }
                .Where(value => value != AbilityAnchor.Hand || session.Draft.Body != null)
                .Select(value => new UiOptionMenuOption
                {
                    Label = T("Anchor." + value), Selected = anchor == value,
                    Select = () => Change(() =>
                    {
                        session.Draft.Binding ??= new ConstructionBinding();
                        session.Draft.Binding.Anchor = value;
                    })
                }).ToArray()));
        Toggle("Follow", binding?.Follow ?? false, value =>
        {
            session.Draft.Binding ??= new ConstructionBinding { Anchor = anchor };
            session.Draft.Binding.Follow = value;
        });
        Toggle("FaceTarget", binding?.FaceTarget ?? false, value =>
        {
            session.Draft.Binding ??= new ConstructionBinding { Anchor = anchor };
            session.Draft.Binding.FaceTarget = value;
        });
        Toggle("ScaleByBody", binding?.ScaleByBody ?? false, value =>
        {
            session.Draft.Binding ??= new ConstructionBinding { Anchor = anchor };
            session.Draft.Binding.ScaleByBody = value;
        });
        Number("BindingHeight", binding?.Height ?? 0f, value =>
        {
            session.Draft.Binding ??= new ConstructionBinding { Anchor = anchor };
            session.Draft.Binding.Height = value;
        });
        Row("PlacementRules", T("PlacementRulesHint"));
        ActionRow("MakeFollow", T("MakeFollowHint"), () => PlacementEdit(() => ConstructionPlacementEdits.Follow(session.Draft, anchor)));
        ActionRow("MakeOrbit", T("MakeOrbitHint"), () => PlacementEdit(() => ConstructionPlacementEdits.Orbit(session.Draft, anchor, 3f, 4f)));
        ConstructionNode node = parts[part];
        for (int i = 0; i < 3; i++)
        {
            int axis = i;
            Number(new[] { "OffsetX", "OffsetY", "OffsetZ" }[i], node.Offset[i], value => node.Offset[axis] = value);
        }
        ConstructionOperation spin = node.Operations.FirstOrDefault(o => o.Kind == ConstructionOperationKind.Spin);
        for (int i = 0; i < 3; i++)
        {
            int axis = i;
            Number(new[] { "SpinX", "SpinY", "SpinZ" }[i], spin?.Vector[i] ?? 0f, value =>
            {
                if (spin == null)
                {
                    spin = new ConstructionOperation { Kind = ConstructionOperationKind.Spin };
                    node.Operations = node.Operations.Concat(new[] { spin }).ToArray();
                }
                spin.Vector[axis] = value;
            });
        }
    }

    /// <summary>转换成功才加入撤销记录，编译拒绝保留原草稿并给出原因。</summary>
    /// <param name="edit">基于当前资料建立完整候选的改造。</param>
    private void PlacementEdit(Func<AbilityDefinition> edit)
    {
        try
        {
            AbilityDefinition candidate = edit();
            Change(() => session.Draft = candidate);
        }
        catch (InvalidDataException error) { state.text = string.Format(T("Invalid"), error.Message); }
    }
}
