// 在独立测试世界运行。检查器官能力、外观选择、移除与妖兽存档恢复。
using System;
using System.Linq;
using System.Reflection;
using Cultiway;
using Cultiway.Content;
using Cultiway.Content.Components;
using Cultiway.Content.CreatureCompositions.Components;
using Cultiway.Content.CreatureCompositions.Services;
using Cultiway.Content.CreatureCompositions.Visuals;
using Cultiway.Content.YaoBeasts;
using Cultiway.Core.Persistence;
using Newtonsoft.Json.Linq;
using Cultiway.Utils.Extension;
using UnityEngine;

if (SmoothLoader.isLoading() || World.world.tiles_list.Length == 0)
    throw new InvalidOperationException("请先打开独立测试世界并等待加载结束");
WorldTile tile = World.world.tiles_list.Where(t => !t.Type.liquid)
    .OrderBy(t => Math.Abs(t.pos.x - MapBox.width / 2) + Math.Abs(t.pos.y - MapBox.height / 2)).First();
MoveCamera.instance.focusOn(new Vector3(tile.pos.x, tile.pos.y));
World.world.setZoomOrthographic(15f);
await Game.NextFrame();
Config.paused = true;
Game.Resume();
var wolf = World.world.units.createNewUnit("wolf", tile, pAdultAge: true);
var actor = wolf.GetExtend();
// 固定测试前提：样板尚未进入其他修炼体系。
if (actor.HasCultisys<Xian>()) actor.E.RemoveComponent<Xian>();
if (actor.HasCultisys<Knight>()) actor.E.RemoveComponent<Knight>();
if (actor.HasCultisys<Magic>()) actor.E.RemoveComponent<Magic>();
if (!YaoAwakeningService.TryAwakenByRain(actor)) throw new Exception("测试狼启灵失败");
long subspecies = wolf.subspecies.data.id;
var organs = new[]
{
    new YaoOrganRecord { SlotId = YaoContent.Slots.Surface, OrganId = "yao.scale.basic", Rank = 1, Origin = YaoOrganOrigin.Innate },
    new YaoOrganRecord { SlotId = YaoContent.Slots.Head, OrganId = "yao.horn.evilspotter", Rank = 2, Origin = YaoOrganOrigin.Digested },
    new YaoOrganRecord { SlotId = YaoContent.Slots.Limbs, OrganId = "yao.wing.fierce", Rank = 2, Origin = YaoOrganOrigin.GodEdited }
};
if (!YaoFormPlanService.TrySetOrgans(actor, YaoFormIds.TrueForm, organs)) throw new Exception("合法身体表达失败");
var phenotype = actor.E.GetComponent<CreaturePhenotype>();
if (!CreaturePhenotypeCompiler.TryGetCompiled(phenotype.CompiledIndex, phenotype.Signature, out var compiled)) throw new Exception("身体整理结果丢失");
if (compiled.OrderedOrgans.Count != 3 || compiled.Stats.Count == 0) throw new Exception("外观删掉了实际器官或属性");
string[] visible = CreatureAppearanceService.GetVisibleLayerIds(compiled);
if (visible.Length != 3 || !visible.Contains("yao.layer.horn_evilspotter") || !visible.Contains("yao.layer.wing_fierce"))
    throw new Exception("角、翼和体表的组合不正确");
Sprite sprite = (Sprite)typeof(Actor).GetMethod("checkSpriteToRender", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(wolf, null);
if (!sprite.name.StartsWith("cultiway_")) throw new Exception("主体帧合成未接入实际绘制");
if (wolf.subspecies.data.id != subspecies) throw new Exception("外观改变了亚种");

// 去掉角，保留合法头部，确认旧外观撤销且其余器官仍存在。
var withoutHorn = (YaoOrganRecord[])organs.Clone();
withoutHorn[1] = new YaoOrganRecord { SlotId = YaoContent.Slots.Head, OrganId = "yao.fang.basic", Rank = 1, Origin = YaoOrganOrigin.Innate };
if (!YaoFormPlanService.TrySetOrgans(actor, YaoFormIds.TrueForm, withoutHorn)) throw new Exception("移除角失败");
phenotype = actor.E.GetComponent<CreaturePhenotype>();
CreaturePhenotypeCompiler.TryGetCompiled(phenotype.CompiledIndex, phenotype.Signature, out compiled);
if (CreatureAppearanceService.GetVisibleLayerIds(compiled).Contains("yao.layer.horn_evilspotter")) throw new Exception("旧角外观残留");

YaoFormPlanService.TrySetOrgans(actor, YaoFormIds.TrueForm, organs);
Yao yao = actor.GetCultisys<Yao>();
yao.yao_power = 31;
actor.E.GetComponent<Yao>() = yao;
actor.E.AddComponent(new YaoCore { Quality = 63, Stability = 72, Cracks = 1 });
wolf.prepareForSave();
wolf.data.get(WorldSaveCoordinator.ActorDataKey, out string json, "");
if (string.IsNullOrEmpty(json) || json.Contains("CompiledIndex") || json.Contains("Asset")) throw new Exception("单位保存接点或数据边界错误");
var restored = World.world.units.createNewUnit("wolf", tile, pAdultAge: true).GetExtend();
YaoPersistence.Restore(restored, JObject.Parse(json)["sections"]["yao"]["data"].ToString());
if (restored.GetCultisys<Yao>().yao_power != 31 || restored.E.GetComponent<YaoCore>().Quality != 63 ||
    restored.E.GetComponent<YaoBody>().Forms[0].Organs[1].Origin != YaoOrganOrigin.Digested)
    throw new Exception("妖兽成长结果读取不一致");

return new { passed = true, originalId = wolf.data.id, restoredId = restored.Base.data.id,
    sprite = sprite.name, visible, savedCharacters = json.Length, cachedFrames = CreatureAppearanceService.CachedFrameCount };
