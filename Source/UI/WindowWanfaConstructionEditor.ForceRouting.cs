using System;
using System.Linq;
using Cultiway.Core.Skills;

namespace Cultiway.UI;

/// <summary>传力和身体承力共用普通分支表单，不增加连招或节点编辑。</summary>
public sealed partial class WindowWanfaConstructionEditor
{
    /// <summary>显示实际分配顺序、份额及有限传播边界。</summary>
    /// <param name="routing">当前独立草稿的出口。</param>
    private void ForceRoutingRows(ConstructionForceRouting routing)
    {
        Row("ForceRoutingRules", T("ForceRoutingHint"));
        Number("ForceMaximumDepth", routing.MaximumDepth, value => routing.MaximumDepth = (int)value);
        Number("ForceMaximumActors", routing.MaximumActors, value => routing.MaximumActors = (int)value);
        for (int i = 0; i < routing.Routes.Length; i++)
        {
            int index = i;
            ConstructionForceRoute route = routing.Routes[i];
            Row("ForceRoute", string.Format(T("ForceRouteIndex"), i + 1));
            Choice("ForceDestination", T("ForceDestination." + route.Destination), () =>
                menu.Show(T("ForceDestination"), Enum.GetValues(typeof(ConstructionForceDestination))
                    .Cast<ConstructionForceDestination>().Select(destination => new UiOptionMenuOption
                    {
                        Label = T("ForceDestination." + destination), Selected = route.Destination == destination,
                        Select = () => Change(() =>
                        {
                            route.Destination = destination;
                            if (destination == ConstructionForceDestination.Support) route.Direction = null;
                        })
                    }).ToArray()));
            Number("ForceRouteShare", route.Share, value => route.Share = value);
            if (route.Destination == ConstructionForceDestination.Contacts)
                Number("ForceRouteAngle", route.Angle, value => route.Angle = value);
            if (route.Destination != ConstructionForceDestination.Support)
            {
                Toggle("ForceRouteDirection", route.Direction != null,
                    value => route.Direction = value ? new[] { 1f, 0f, 0f } : null);
                if (route.Direction != null)
                    for (int axis = 0; axis < 3; axis++)
                    {
                        int coordinate = axis;
                        Number(new[] { "ForceRouteForward", "ForceRouteSide", "ForceRouteUp" }[axis],
                            route.Direction[axis], value => route.Direction[coordinate] = value);
                    }
            }

            if (routing.Routes.Length > 1)
                ActionRow("RemoveForceRoute", "",
                    () => Change(() => routing.Routes = routing.Routes.Where((_, at) => at != index).ToArray()));
        }

        if (routing.Routes.Length < 8)
            ActionRow("AddForceRoute", T("AddForceRouteHint"), () => Change(() =>
            {
                ConstructionForceRoute last = routing.Routes[routing.Routes.Length - 1];
                last.Share *= .5f;
                routing.Routes = routing.Routes.Concat(new[]
                {
                    new ConstructionForceRoute
                        { Destination = ConstructionForceDestination.Contacts, Share = last.Share }
                }).ToArray();
            }));
    }

    /// <summary>有效站姿可以按有限额度处理来力，关闭后不保留隐含免疫。</summary>
    /// <param name="body">实际身体草稿。</param>
    private void BodyForceResponseRows(BodyActionDefinition body)
    {
        Toggle("BodyForceResponse", body.ForceResponse != null, value => body.ForceResponse = value
            ? new BodyForceResponse { Routing = NewForceRouting(ConstructionForceDestination.Support, 1f) }
            : null);
        if (body.ForceResponse == null) return;
        Row("BodyForceResponseRules", T("BodyForceResponseHint"));
        Number("ForceResponseCapacity", body.ForceResponse.Capacity, value => body.ForceResponse.Capacity = value);
        ForceRoutingRows(body.ForceResponse.Routing);
    }

    /// <summary>编辑新增参数时提供一条有限分支，不直接修改正式能力。</summary>
    /// <param name="destination">初始出口。</param><param name="share">初始份额。</param>
    private static ConstructionForceRouting NewForceRouting(ConstructionForceDestination destination, float share) =>
        new()
        {
            Routes = new[] { new ConstructionForceRoute { Destination = destination, Share = share } }
        };
}