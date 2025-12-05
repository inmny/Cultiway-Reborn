using ai;
using Cultiway.Core.Pathfinding;
using HarmonyLib;

namespace Cultiway.Patch
{
    internal static class PatchAboutPathfinding
    {
        /**寻路调整方案
        * （1）完全替换原版寻路，调用goTo后会提交一个任务用以多线程寻路，goTo这边的结果始终为成功
        * （2）多线程寻路不要求最优，但省时、流式输出。
        * （3）当调用goTo之后会将生物设置为等待寻路结果的情况，啥事都不干（如果有突发事件则直接打断寻路任务）
        */
        [HarmonyPrefix, HarmonyPatch(typeof(Actor), nameof(Actor.goTo))]
        private static bool goTo_prefix(ref ExecuteEvent __result, Actor __instance, WorldTile pTile,
            bool pPathOnWater = false, bool pWalkOnBlocks = false, bool pWalkOnLava = false,
            int pLimitPathfindingRegions = 0)
        {
            AbortPath(__instance);
            if (pTile == null)
            {
                __result = ExecuteEvent.False;
                return false;
            }

            __instance.setTileTarget(pTile);
            __instance.next_step_position = __instance.current_tile?.posV3 ?? __instance.next_step_position;

            PathFinder.Instance.RequestPath(__instance, pTile, pPathOnWater, pWalkOnBlocks, pWalkOnLava,
                pLimitPathfindingRegions);

            __result = ExecuteEvent.True;
            return false;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(Actor), nameof(Actor.updatePathMovement))]
        private static bool updatePathMovement(Actor __instance)
        {
            if (!PathFinder.Instance.TryPeekStep(__instance, out var step, out var finished))
            {
                __instance.makeWait(0.3f);
                return false;
            }

            var result = HandleStep(__instance, step);
            switch (result)
            {
                case PathProcessResult.Consumed:
                    PathFinder.Instance.ConsumeStep(__instance);
                    break;
                case PathProcessResult.Abort:
                    AbortPath(__instance);
                    __instance.ai.clearBeh();
                    __instance.ai.setTaskBehFinished();
                    break;
                case PathProcessResult.Deferred:
                    __instance.makeWait(0.3f);
                    break;
            }

            return false;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(Actor), nameof(Actor.isUsingPath))]
        private static void isUsingPath_postfix(Actor __instance, ref bool __result)
        {
            __result = __result || PathFinder.Instance.IsActorPathing(__instance);
        }
        [HarmonyPostfix, HarmonyPatch(typeof(MapBox), nameof(MapBox.clearWorld))]
        private static void clearWorld_prefix()
        {
            PathFinder.Instance.Clear();
        }

        private static PathProcessResult HandleStep(Actor actor, PathStep step)
        {
            return step.Method switch
            {
                MovementMethod.Walk => HandleWalk(actor, step.Tile),
                MovementMethod.Swim => HandleSwim(actor, step.Tile),
                MovementMethod.Sail => HandleSail(actor, step.Tile),
                _ => PathProcessResult.Deferred
            };
        }

        private static PathProcessResult HandleWalk(Actor actor, WorldTile tile)
        {
            return TryMove(actor, tile, allowBlocks: false, allowLava: false, allowOcean: false);
        }

        private static PathProcessResult HandleSwim(Actor actor, WorldTile tile)
        {
            return TryMove(actor, tile, allowBlocks: false, allowLava: true, allowOcean: true);
        }

        private static PathProcessResult HandleSail(Actor actor, WorldTile tile)
        {
            // 留空供后续实现乘船逻辑
            return PathProcessResult.Deferred;
        }

        private static PathProcessResult TryMove(Actor actor, WorldTile tile, bool allowBlocks, bool allowLava,
            bool allowOcean)
        {
            if (tile == null)
            {
                return PathProcessResult.Abort;
            }

            var tileType = tile.Type;
            if (!allowBlocks && tileType.block && !actor.ignoresBlocks())
            {
                return PathProcessResult.Abort;
            }

            if (!allowLava && actor.asset.die_in_lava && tileType.lava)
            {
                return PathProcessResult.Abort;
            }

            if (!allowOcean && tileType.ocean && actor.isDamagedByOcean())
            {
                return PathProcessResult.Abort;
            }

            if (tileType.damaged_when_walked)
            {
                actor.current_tile?.tryToBreak();
            }

            if (tile.isOnFire() && !actor.isImmuneToFire() && !(actor.current_tile?.isOnFire() ?? false))
            {
                actor.cancelAllBeh();
                actor.makeWait(0.3f);
                return PathProcessResult.Consumed;
            }

            actor.moveTo(tile);
            return PathProcessResult.Consumed;
        }

        private static void AbortPath(Actor actor)
        {
            PathFinder.Instance.Cancel(actor);
            actor.stopMovement();
        }

        private enum PathProcessResult
        {
            Consumed,
            Deferred,
            Abort
        }
    }
}
