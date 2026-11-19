using System;
using System.Linq;
using UnityEngine;

namespace Cultiway.Content.Railways;

/// <summary>真实车头经过轨道时将地面单位向两侧击飞，乘车关系及飞行单位不受影响。</summary>
internal static class RailwayMotionEffects
{
    /// <summary>扫过本次实际经过的每格，较大的模拟步长也不会漏掉中途单位。</summary>
    /// <param name="body">实际列车。</param>
    /// <param name="from">原轨道进度。</param>
    /// <param name="to">本次实际到达进度。</param>
    internal static void PushTrackUnits(ActorComponents.Train body, float from, float to)
    {
        var track = body.Track;
        int first = Math.Max(0, (int)Math.Floor(Math.Min(from, to)));
        int last = Math.Min(track.Count - 1, (int)Math.Floor(Math.Max(from, to)));
        for (int i = first; i <= last; i++)
        {
            var tile = track[i];
            if (tile.building != null || tile.top_type != TopTileTypes.TrainTrack) continue;
            var forward = (Vector2)(track[Math.Min(track.Count - 1, i + 1)].posV3 - track[Math.Max(0, i - 1)].posV3);
            if (to < from) forward = -forward;
            var side = new Vector2(-forward.y, forward.x).normalized;
            foreach (var unit in tile._units.ToArray())
            {
                if (!unit.isAlive() || unit.asset == Actors.Train || unit.is_inside_boat || unit.isFlying() || unit.isInAir()) continue;
                float offset = Vector2.Dot(unit.current_position - (Vector2)tile.posV3, side);
                float sign = Math.Abs(offset) < .05f ? (unit.id % 2 == 0 ? 1 : -1) : Mathf.Sign(offset);
                unit.addForce(side.x * sign * 6, side.y * sign * 6, 2.5f, pCheckLandCancelAllActions: true);
            }
        }
    }
}
