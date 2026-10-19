// 在测试世界中摆放样板，并导出实际游戏合成图片。
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Cultiway.Content.Components;
using Cultiway.Content.YaoBeasts;
using Cultiway.Utils.Extension;
using UnityEngine;

Config.paused = true;
Game.Resume();
var scenarios = new[]
{
    ("wolf", "wolf_horn_wing", new[] { "yao.scale.basic", "yao.horn.evilspotter", "yao.wing.fierce" }, new[] { 1, 2, 2 }),
    ("fox", "fox_one_tail", new[] { "yao.fur.moonlight", "yao.spirit.sense", "yao.crown.tails" }, new[] { 2, 1, 1 }),
    ("fox", "fox_three_tails", new[] { "yao.scale.basic", "yao.spirit.sense", "yao.crown.tails" }, new[] { 1, 1, 3 }),
    ("fox", "fox_six_tails", new[] { "yao.scale.basic", "yao.spirit.sense", "yao.crown.tails" }, new[] { 1, 1, 6 }),
    ("snake", "snake_thunder", new[] { "yao.scale.basic", "yao.horn.thunder" }, new[] { 1, 2 }),
    ("turtle", "turtle_shell", new[] { "yao.shell.black", "yao.spirit.sense" }, new[] { 2, 1 })
};
WorldTile tile = World.world.tiles_list.Where(t => !t.Type.liquid)
    .OrderBy(t => Math.Abs(t.pos.x - MapBox.width / 2) + Math.Abs(t.pos.y - MapBox.height / 2)).First();
string directory = Path.Combine(Cultiway.ModClass.I.GetDeclaration().FolderPath, "artifacts", "creature_appearance", "runtime");
Directory.CreateDirectory(directory);
var results = new System.Collections.Generic.List<object>();
Kingdom sampleKingdom = null;
for (int n = 0; n < scenarios.Length; n++)
{
    var sample = scenarios[n];
    Actor owner = World.world.units.createNewUnit(sample.Item1,
        World.world.GetTile(tile.pos.x + n % 3 * 6, tile.pos.y + n / 3 * 6), pAdultAge: true);
    var actor = owner.GetExtend();
    // 固定测试前提：样板尚未进入其他修炼体系。
    if (actor.HasCultisys<Xian>()) actor.E.RemoveComponent<Xian>();
    if (actor.HasCultisys<Knight>()) actor.E.RemoveComponent<Knight>();
    if (actor.HasCultisys<Magic>()) actor.E.RemoveComponent<Magic>();
    sampleKingdom ??= owner.kingdom;
    owner.setKingdom(sampleKingdom);
    owner.setName(sample.Item2, pTrack: false);
    if (!YaoAwakeningService.TryAwakenByRain(actor)) throw new Exception("样板启灵失败：" + sample.Item2);
    var organs = sample.Item3.Select((id, i) => new YaoOrganRecord
    {
        OrganId = id, Rank = sample.Item4[i], Origin = YaoOrganOrigin.GodEdited,
        SlotId = id.StartsWith("yao.wing.") ? YaoContent.Slots.Limbs :
            id.StartsWith("yao.crown.") ? YaoContent.Slots.Tail :
            id.StartsWith("yao.horn.") || id == "yao.spirit.sense" ? YaoContent.Slots.Head : YaoContent.Slots.Surface
    }).ToArray();
    if (!YaoFormPlanService.TrySetOrgans(actor, YaoFormIds.TrueForm, organs)) throw new Exception("样板身体不合法：" + sample.Item2);
    Sprite sprite = (Sprite)typeof(Actor).GetMethod("checkSpriteToRender", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance).Invoke(owner, null);
    RenderTexture previous = RenderTexture.active;
    var temporary = RenderTexture.GetTemporary(sprite.texture.width, sprite.texture.height, 0);
    var readable = new Texture2D(sprite.texture.width, sprite.texture.height, TextureFormat.RGBA32, false);
    Graphics.Blit(sprite.texture, temporary);
    RenderTexture.active = temporary;
    readable.ReadPixels(new Rect(0, 0, readable.width, readable.height), 0, 0);
    readable.Apply();
    File.WriteAllBytes(Path.Combine(directory, sample.Item2 + ".png"), readable.EncodeToPNG());
    RenderTexture.active = previous;
    RenderTexture.ReleaseTemporary(temporary);
    UnityEngine.Object.Destroy(readable);
    results.Add(new { name = sample.Item2, actor = owner.data.id, sprite = sprite.name });
}
MoveCamera.instance.focusOn(new Vector3(tile.pos.x + 6, tile.pos.y + 3));
World.world.setZoomOrthographic(15f);
ScrollWindow.hideAllEvent(false);
foreach (var console in UnityEngine.Object.FindObjectsOfType<WorldBoxConsole.Console>()) console.Hide();
for (int i = 0; i < 90; i++) await Game.NextFrame();
return new { samples = results, directory, screenshot = Screen.Capture("cultiway-appearance-world.png") };
