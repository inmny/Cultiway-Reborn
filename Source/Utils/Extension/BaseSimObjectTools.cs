using UnityEngine;

namespace Cultiway.Utils.Extension;

public static class BaseSimObjectTools
{
    public static Vector3 GetSimPos(this BaseSimObject obj)
    {
        var pos = obj.current_position;
        return new(pos.x, pos.y, obj.getHeight());
    }
}