using ai.behaviours;
using Cultiway.Utils;
using UnityEngine;

namespace Cultiway.Content.Behaviours;

public class BehHoldSimpleCeremony : BehaviourActionActor
{
    private float _radius;
    private int _expected_times;
    public BehHoldSimpleCeremony(float radius = 3, int expected_times = 2)
    {
        _radius = radius;
        _expected_times = expected_times;
    }
    public override BehResult execute(Actor pObject)
    {
        if (pObject.beh_tile_target == null)
        {
            pObject.beh_tile_target = pObject.current_tile;
        }
        var next_dir = CalculateCircularMoveDir(pObject.current_position, pObject.beh_tile_target.pos);
        var next_tile_pos = MathUtils.NextGrid(pObject.current_position, next_dir);
        
        var tile = World.world.GetTile(next_tile_pos.x, next_tile_pos.y);
        if (tile == null)
        {
            return BehResult.Continue;
        }
        pObject.moveTo(tile);
        if (Randy.randomChance(1f / (2 * Mathf.PI * _radius * _expected_times)))
        {
            return BehResult.Continue;
        }
        return BehResult.RepeatStep;
    }
    private Vector2 CalculateCircularMoveDir(Vector2 actorPos, Vector2 targetPos)
    {
        Vector2 radial_vector = actorPos - targetPos;
        float current_distance = radial_vector.magnitude;

        if (current_distance < 0.1f)
        {
            return new Vector2(1, 0);
        }

        Vector2 radial_dir = radial_vector.normalized;

        Vector2 tangent_dir = new Vector2(
            -radial_dir.y,
            radial_dir.x
        );

        float distance_diff = Mathf.Abs(current_distance - _radius);
        float adjust_factor = Mathf.Clamp01(distance_diff / _radius);

        Vector2 radial_adjust_dir = current_distance > _radius 
            ? -radial_dir 
            : radial_dir; 

        Vector2 final_dir = Vector2.Lerp(
            tangent_dir,  
            radial_adjust_dir, 
            adjust_factor     
        ).normalized;

        return final_dir;
    }
}