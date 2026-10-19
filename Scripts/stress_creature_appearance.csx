// 仅在独立测试世界使用：检查 1024 只合法妖兽同时显示时的缓存复用和绘制耗时。
using System;
using System.Diagnostics;
using System.Linq;
using Cultiway.Content.Components;
using Cultiway.Content.CreatureCompositions.Visuals;
using Cultiway.Content.YaoBeasts;
using Cultiway.Utils.Extension;
using UnityEngine;

Config.paused = true;
Game.Resume();
WorldTile center = World.world.tiles_list.Where(t => !t.Type.liquid)
    .OrderBy(t => Math.Abs(t.pos.x - MapBox.width / 2) + Math.Abs(t.pos.y - MapBox.height / 2)).First();
var organs = new[]
{
    new YaoOrganRecord { SlotId = YaoContent.Slots.Surface, OrganId = "yao.scale.basic", Rank = 1 },
    new YaoOrganRecord { SlotId = YaoContent.Slots.Head, OrganId = "yao.horn.evilspotter", Rank = 2 },
    new YaoOrganRecord { SlotId = YaoContent.Slots.Limbs, OrganId = "yao.wing.fierce", Rank = 2 }
};
Kingdom kingdom = null;
var created = new Actor[1024];
for (int i = 0; i < created.Length; i++)
{
    // 密集摆放以保证镜头内实际可见，暂停模拟以排除捕食与战斗的干扰。
    Actor owner = World.world.units.createNewUnit("wolf",
        World.world.GetTile(center.pos.x + i % 16 - 8, center.pos.y + i / 16 % 16 - 8), pAdultAge: true);
    kingdom ??= owner.kingdom;
    owner.setKingdom(kingdom);
    var actor = owner.GetExtend();
    // 原版新生动物可能随机进入其他修炼体系；压力样本统一准备为尚未修炼的凡兽。
    if (actor.HasCultisys<Xian>()) actor.E.RemoveComponent<Xian>();
    if (actor.HasCultisys<Knight>()) actor.E.RemoveComponent<Knight>();
    if (actor.HasCultisys<Magic>()) actor.E.RemoveComponent<Magic>();
    if (!YaoAwakeningService.TryAwakenByRain(actor))
        throw new Exception("压力样板启灵失败：" + i + ", rekt=" + owner.isRekt() + ", components=" + actor.E);
    if (!YaoFormPlanService.TrySetOrgans(actor, YaoFormIds.TrueForm, organs))
        throw new Exception("压力样板器官应用失败：" + i);
    created[i] = owner;
    // 分帧准备样板，避免一次脚本长时间占用游戏主线程。
    if (i % 4 == 3) await Game.NextFrame();
}
MoveCamera.instance.focusOn(new Vector3(center.pos.x, center.pos.y));
World.world.setZoomOrthographic(20f);
for (int i = 0; i < 90; i++) await Game.NextFrame();
var times = new double[7];
for (int i = 0; i < times.Length; i++)
{
    var timer = Stopwatch.StartNew();
    World.world.units.calculateVisibleActors();
    timer.Stop();
    times[i] = timer.Elapsed.TotalMilliseconds;
    await Game.NextFrame();
}
var manager = World.world.units;
var samples = new System.Collections.Generic.HashSet<Actor>(created);
int composed = 0, composedSamples = 0;
for (int i = 0; i < manager.visible_units.count; i++)
    if (manager.render_data.has_normal_render[i] &&
        manager.render_data.main_sprite_colored[i]?.name.StartsWith("cultiway_") == true)
    {
        composed++;
        if (samples.Contains(manager.visible_units.array[i])) composedSamples++;
    }
if (composedSamples != created.Length) throw new Exception("镜头中未验证到全部 1024 只妖兽：" + composedSamples);
if (CreatureAppearanceService.CachedFrameCount > CreatureAppearanceService.MaximumCachedFrames)
    throw new Exception("合成图片超过缓存上限");
Array.Sort(times);
return new { passed = true, created = created.Length, visible = manager.visible_units.count, composed, composedSamples,
    cachedFrames = CreatureAppearanceService.CachedFrameCount,
    wholeVisiblePreparationMedianMs = times[3], wholeVisiblePreparationMaxMs = times[6],
    scope = "暂停模拟，原版可见单位准备与身体合成总耗时；不代表战斗或长期生态负载" };
