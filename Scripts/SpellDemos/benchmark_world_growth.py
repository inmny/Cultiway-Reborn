#!/usr/bin/env python3
"""复用正式世界和创法用例，测量真实保存及恢复。"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import tempfile

import baseline_scenarios as baseline
from common import PROJECT, version_stamp, write_json

SCRIPTS = Path(__file__).resolve().parent
CASES = ("creation", "city", "conditions", "facilities")


def source_for(case, request_path):
    request = f'var growthRequest = JObject.Parse(File.ReadAllText({json.dumps(str(request_path))}));'
    helpers = (SCRIPTS / "world_growth_save.script").read_text()
    if case == "creation":
        source = (SCRIPTS / "benchmark_creation.script").read_text()
        source = source.replace("using Newtonsoft.Json;", "using Newtonsoft.Json;\nusing Newtonsoft.Json.Linq;")
        source = source.replace("Config.paused=true;Game.Resume();World.world.save_manager.loadWorld(", request + "\n" + helpers
                                + "\nConfig.paused=true;Game.Resume();World.world.save_manager.loadWorld(", 1)
        source = source.replace('Path.Combine(root,"artifacts/skill_rebuild/similarity")',
                                'Path.GetDirectoryName(' + json.dumps(str(request_path)) + ')', 1)
        source = source.replace('Path.Combine(root,"Scripts/WorldSaveTests/Fixtures/world-format-1")',
                                '(string)growthRequest["worldPath"]', 1)
        source = source.replace(
            "for(int i=0;i<512;i++){var draft=ConstructedAbilityService.Generate(people[0],i,input);ConstructionSimilarity.Describe(draft);",
            """var rejectedDrafts = new Dictionary<int, string>();
for(int i=0;i<512;i++){
    try
    {
        var draft = ConstructedAbilityService.Generate(people[0], i, input);
        ConstructionSimilarity.Describe(draft);
    }
    catch (InvalidDataException error)
    {
        rejectedDrafts.Add(i, error.Message);
    }""", 1)
        source = source.replace("double worldStart=World.world.getCurWorldTime();time.Restart();",
                                """var actorIds = people.Select(actor => actor.Base.id).ToArray();
JObject Knowledge()
{
    var peopleInWorld = actorIds.Select(id => World.world.units.get(id)).ToArray();
    return new JObject
    {
        ["declarations"] = JArray.FromObject(access.List().Select(row => row.Id).OrderBy(id => id)),
        ["people"] = JArray.FromObject(peopleInWorld.Where(actor => actor != null && actor.isAlive()).Select(actor => new
        {
            id = actor.id, known = access.List(actor.GetExtend()).Select(row => row.Id).OrderBy(id => id).ToArray()
        }))
    };
}
var beforeGrowth = Knowledge();
var earlySave = SaveWorld("before-growth");
double worldStart=World.world.getCurWorldTime();time.Restart();""", 1)
        return source.replace("return result;", """var afterGrowth = Knowledge();
var lateSave = SaveWorld("after-growth");
var earlyRead = await ReadWorld(earlySave.Path);
bool earlyRestored = JToken.DeepEquals(beforeGrowth, Knowledge());
var lateRead = await ReadWorld(lateSave.Path);
bool lateRestored = JToken.DeepEquals(afterGrowth, Knowledge());
return PublishGrowth(earlyRestored && lateRestored, new
{
    beforeGrowth, afterGrowth, earlySave, lateSave, earlyRead, lateRead, earlyRestored, lateRestored,
    rejectedDrafts, result
});""", 1)
    source = (SCRIPTS / "benchmark_world_growth.script").read_text()
    return source.replace("// WORLD_GROWTH_SAVE", request + "\n" + helpers).replace(
        "// WORLD_GROWTH_FRAMES", (SCRIPTS / "frame_measurement.script").read_text())


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--case", required=True, choices=CASES)
    parser.add_argument("--world", type=Path, help="已有通过功能验收的正式存档目录，创法场景可使用仓库夹具")
    parser.add_argument("--facts", type=Path, help="正式档的已有验收资料")
    args = parser.parse_args()
    if not os.environ.get("CULTIWAY_DEV_ACTIVE"):
        parser.error("请通过独立游戏的 dev.py exec 运行")
    if args.world is None and args.case != "creation":
        parser.error("请提供本场景已有的正式存档目录")
    if args.facts is None and args.case in ("city", "facilities"):
        parser.error("城市和设施场景需要对应档案的验收资料")
    world = (args.world or PROJECT / "Scripts/WorldSaveTests/Fixtures/world-format-1").resolve()
    file = world / "map.wbox"
    digest = hashlib.sha256(file.read_bytes()).hexdigest()
    destination = Path(tempfile.mkdtemp(prefix=f"world-growth-{args.case}-", dir=os.environ["CULTIWAY_DEV_OUTPUT"]))
    connection = baseline.connect(False)
    version, _ = version_stamp()
    request = destination / "request.json"
    write_json(request, dict(scenario=args.case, worldPath=str(world), worldSha256=digest, output=str(destination),
                             source=version, connection=connection,
                             facts=json.loads(args.facts.read_text()) if args.facts else None,
                             frameLimit=60, warmupSeconds=0, sampleSeconds=45, repeat=0))
    script = destination / "measurement.script"
    script.write_text(source_for(args.case, request))
    try:
        result = baseline.call("run", str(script), "205000")
    except RuntimeError as error:
        write_json(destination / "failure.json", dict(error=str(error)))
        raise RuntimeError(f"世界运行测量失败，原始输入与错误在 {destination}") from None
    if not result["passed"] or hashlib.sha256(file.read_bytes()).hexdigest() != digest:
        raise RuntimeError(f"世界负载或原档校验失败，见 {destination}")
    print(destination, flush=True)


if __name__ == "__main__":
    main()
