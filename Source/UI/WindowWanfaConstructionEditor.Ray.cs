using System.Linq;
using Cultiway.Core.Skills;
using Cultiway.Core.Visuals.Forms;

namespace Cultiway.UI;

/// <summary>射线继续现有运动页、条件页、撤销及正式保存。</summary>
public sealed partial class WindowWanfaConstructionEditor
{
    /// <summary>只为实际伸展模型提供入口；已有射线显示同一份明确参数。</summary>
    /// <param name="node">当前草稿部位。</param>
    private bool RayRows(ConstructionNode node)
    {
        ConstructionOperation operation = node.Operations.FirstOrDefault(value => value.Ray != null);
        if (operation == null)
        {
            if (FormResources.Load(node.Form).Span != null)
                ActionRow("AddRay", T("AddRayHint"), () => Change(() => node.Operations = node.Operations.Concat(new[]
                {
                    new ConstructionOperation { Kind = ConstructionOperationKind.Ray, Ray = new ConstructionRay() }
                }).ToArray()));
            return false;
        }
        Row("RayRules", T("RayRulesHint"));
        Number("RayAt", operation.At, value => operation.At = value);
        RayParameters(operation.Ray);
        ActionRow("RemoveRay", "", () => Change(() => node.Operations = node.Operations.Where(value => value != operation).ToArray()));
        return true;
    }

    /// <summary>定时与条件射线共同编辑实际长度、目标更新和转向。</summary>
    /// <param name="ray">草稿独占的同一份参数。</param>
    private void RayParameters(ConstructionRay ray)
    {
        Number("RayLength", ray.Length, value => ray.Length = value);
        Toggle("RayFollowTarget", ray.FollowTarget, value => ray.FollowTarget = value);
        Number("RayTurnRate", ray.TurnRate, value => ray.TurnRate = value);
    }
}
