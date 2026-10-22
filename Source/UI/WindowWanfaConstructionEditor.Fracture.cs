using System;
using System.Linq;
using Cultiway.Core.Skills;
using Cultiway.Core.Visuals.Forms;
using Cultiway.Utils.Extension;

namespace Cultiway.UI;

/// <summary>真实分件碎解沿用普通表单、撤销和同一编译入口。</summary>
public sealed partial class WindowWanfaConstructionEditor
{
    /// <summary>为实际多部件形体开放初始定时碎解。</summary>
    /// <param name="node">当前草稿主体。</param>
    private void InitialFractureRows(ConstructionNode node)
    {
        if (FormResources.Load(node.Form).Meshes.Length < 2) return;
        ConstructionOperation operation = node.Operations.FirstOrDefault(o => o.Kind == ConstructionOperationKind.Fracture);
        if (operation?.When != null) return;
        Toggle("FractureEnabled", operation != null, value => node.Operations = value ? node.Operations.Concat(new[]
        { new ConstructionOperation { Kind = ConstructionOperationKind.Fracture, At = Math.Min(.5f, session.Draft.Lifetime * .25f), Fracture = NewFracture(node, session.Draft.Lifetime) } }).ToArray() :
            node.Operations.Where(o => o != operation).ToArray());
        if (operation == null) return;
        Number("FractureAt", operation.At, value => operation.At = value);
        FractureParameters(node, operation.Fracture);
    }

    /// <summary>显示真实分件及作用资格，装饰边界不会混成额外伤害区。</summary>
    /// <param name="node">提供原用途及分件的实际主体。</param>
    /// <param name="fracture">同一可保存参数。</param>
    private void FractureParameters(ConstructionNode node, ConstructionFracture fracture)
    {
        Number("FractureSeconds", fracture.Seconds, value => fracture.Seconds = value);
        Number("FractureSpeed", fracture.Speed, value => fracture.Speed = value);
        Number("FractureSpin", fracture.Spin, value => fracture.Spin = value);
        Number("FractureGravity", fracture.Gravity, value => fracture.Gravity = value);
        Toggle("FractureChildren", fracture.ReleaseChildren, value => fracture.ReleaseChildren = value);
        var description = FormResources.Load(node.Form).Description;
        for (int part = 0; part < description.Parts.Length; part++)
        {
            int index = part; string id = description.Parts[part].Id;
            bool available = node.ContactRole != ConstructionContactRole.Decoration && description.Contacts.Any(contact => contact.Part == index);
            var row = rows.GetNext(); row.Setup(string.Format(T("FracturePart"), FracturePartName(id, part)),
                available ? T("FracturePartHint") : T("FractureNoContact"), "", false, null);
            var toggle = UiElements.CreateIconToggle(row.UseInlineControls(32f), "FractureActive" + part, UiIcons.Options, fracture.ActiveParts.Contains(id), 28f, 22f);
            toggle.interactable = available;
            toggle.onValueChanged.AddListener(value => Change(() => fracture.ActiveParts = value ? fracture.ActiveParts.Concat(new[] { id }).Distinct().ToArray() : fracture.ActiveParts.Where(name => name != id).ToArray()));
            UiTooltip.Set(toggle, "Cultiway.Construction.FracturePartHint", "Cultiway.Construction.FracturePartHint");
        }
    }

    /// <summary>显示常见部件的普通名称，其余按资源实际顺序显示。</summary>
    /// <param name="id">资源中的稳定部件编号。</param>
    /// <param name="index">实际资源顺序。</param>
    private static string FracturePartName(string id, int index) => LMTools.GetOrFallback("Cultiway.Construction.Piece." + id, string.Format(T("FracturePartNumber"), index + 1));

    /// <summary>初始和事件入口共用默认值，继续作用仅选资源已有作用形状。</summary>
    /// <param name="node">实际主体。</param>
    /// <param name="lifetime">整次存在上限。</param>
    private static ConstructionFracture NewFracture(ConstructionNode node, float lifetime)
    {
        var description = FormResources.Load(node.Form).Description;
        return new ConstructionFracture { Seconds = Math.Min(2f, lifetime), ActiveParts = node.ContactRole == ConstructionContactRole.Decoration ? Array.Empty<string>() :
            description.Parts.Where((_, index) => description.Contacts.Any(contact => contact.Part == index)).Select(part => part.Id).ToArray() };
    }
}
