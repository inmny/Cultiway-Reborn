// 仅在独立测试世界使用：经过原版完整保存、清理世界和读档流程比较妖兽成长记录。
using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using Cultiway.Content.Components;
using Cultiway.Content.YaoBeasts;
using Cultiway.Utils.Extension;

Config.paused = true;
Game.Resume();
var actors = World.world.units.getSimpleList().Where(a => a.TryGetExtend(out var e) && e.HasCultisys<Yao>()).ToArray();
var expected = actors.Select(a => new { id = a.data.id, json = JObject.Parse(YaoPersistence.Capture(a.GetExtend())), health = a.data.health }).ToArray();
string path = Path.Combine(Cultiway.ModClass.I.GetDeclaration().FolderPath, "artifacts/creature_appearance/save_roundtrip");
SaveManager.saveWorldToDirectory(path);
World.world.save_manager.loadWorld(path);
await Game.WaitUntil(() => !SmoothLoader.isLoading(), 1800);
Config.paused = true;
foreach (var record in expected)
{
    Actor restored = World.world.units.get(record.id);
    if (restored == null) throw new Exception("单位未恢复：" + record.id);
    var actual = JObject.Parse(YaoPersistence.Capture(restored.GetExtend()));
    foreach (string section in new[] { "Body", "Genome", "Digestion", "Core", "Tribulation", "Nirvana" })
        if (!JToken.DeepEquals(record.json[section], actual[section])) throw new Exception("成长记录未恢复：" + record.id + "/" + section);
    if ((int)record.json["Yao"]["level"] != (int)actual["Yao"]["level"] || restored.data.health != record.health)
        throw new Exception("境界或生命未恢复：" + record.id + ", " + record.health + " -> " + restored.data.health);
}
return new { passed = true, restoredYao = expected.Length, path };
