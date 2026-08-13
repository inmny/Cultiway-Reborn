using Cultiway.Core.Components;
using Cultiway.Core.SubWorlds.Runtime;
using UnityEngine;

namespace Cultiway.UI.SubWorlds;

/// <summary>使用原版主相机聚焦语义导航主世界、小世界和测试 Pawn。</summary>
internal sealed class SubWorldCameraNavigator
{
    internal long? FocusedInstanceId { get; private set; }

    internal void FocusMainWorld()
    {
        FocusedInstanceId = null;
        MoveCamera.instance.focusOn(new Vector3(MapBox.width * 0.5f, MapBox.height * 0.5f));
    }

    internal void Focus(SubWorldRuntime runtime, SubWorldSpatialSlot slot)
    {
        FocusedInstanceId = runtime.InstanceId;
        MoveCamera.instance.focusOn(slot.WorldBounds.center);
    }

    internal void FocusPawn(SubWorldRuntime runtime, SubWorldSpatialSlot slot)
    {
        FocusedInstanceId = runtime.InstanceId;
        Vector2 localPosition = runtime.PawnEntity.GetComponent<Position>().v2;
        MoveCamera.instance.focusOn(slot.ToWorld(localPosition));
    }

    internal void Select(long instanceId)
    {
        FocusedInstanceId = instanceId;
    }

    internal void Reset()
    {
        FocusedInstanceId = null;
    }
}
